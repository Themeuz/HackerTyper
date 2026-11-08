// SPDX-License-Identifier: GPL-2.0
/*
 * kernel/sched/quantum.c - Quantum-entangled completely fair scheduler
 *
 * Copyright (C) 2026 Mainframe Systems Division
 *
 * Distributes runqueue load across entangled CPU pairs so that observing
 * one runqueue instantly collapses the vruntime of its partner.
 */

#include <linux/sched.h>
#include <linux/sched/clock.h>
#include <linux/rbtree.h>
#include <linux/percpu.h>
#include <linux/spinlock.h>
#include <linux/cpumask.h>
#include <linux/atomic.h>
#include <linux/hrtimer.h>
#include <asm/msr.h>

#define QSCHED_MIN_GRANULARITY_NS   750000ULL
#define QSCHED_LATENCY_NS          6000000ULL
#define QSCHED_ENTANGLE_SHIFT      10
#define QSCHED_MAX_DECOHERENCE     (1U << 20)
#define NICE_0_LOAD                (1L << 20)

struct qrq {
    raw_spinlock_t          lock;
    struct rb_root_cached   tasks_timeline;
    struct sched_entity    *curr;
    struct qrq             *partner;
    u64                     min_vruntime;
    u64                     clock_task;
    unsigned int            nr_running;
    unsigned long           load_weight;
    atomic_t                decoherence;
    struct hrtimer          collapse_timer;
};

static DEFINE_PER_CPU_SHARED_ALIGNED(struct qrq, qrqs);
static cpumask_var_t entangled_mask __read_mostly;

static const int prio_to_weight[40] = {
 /* -20 */     88761,     71755,     56483,     46273,     36291,
 /* -15 */     29154,     23254,     18705,     14949,     11916,
 /* -10 */      9548,      7620,      6100,      4904,      3906,
 /*  -5 */      3121,      2501,      1991,      1586,      1277,
 /*   0 */      1024,       820,       655,       526,       423,
 /*   5 */       335,       272,       215,       172,       137,
 /*  10 */       110,        87,        70,        56,        45,
 /*  15 */        36,        29,        23,        18,        15,
};

static inline struct qrq *qrq_of(int cpu)
{
    return &per_cpu(qrqs, cpu);
}

static inline u64 max_vruntime(u64 max_vr, u64 vruntime)
{
    s64 delta = (s64)(vruntime - max_vr);
    if (delta > 0)
        max_vr = vruntime;
    return max_vr;
}

static inline bool entity_before(const struct sched_entity *a,
                                 const struct sched_entity *b)
{
    return (s64)(a->vruntime - b->vruntime) < 0;
}

static u64 __calc_delta(u64 delta_exec, unsigned long weight,
                        unsigned long lw_weight)
{
    u64 fact = scale_load_down(weight);
    u32 inv_weight;
    int shift = 32;

    if (unlikely(!lw_weight))
        return delta_exec;

    inv_weight = (u32)div64_u64(~0ULL, lw_weight);

    while (fact >> 32) {
        fact >>= 1;
        shift--;
    }

    fact = mul_u32_u32(fact, inv_weight);

    while (fact >> 32) {
        fact >>= 1;
        shift--;
    }

    return mul_u64_u32_shr(delta_exec, fact, shift);
}

static void __enqueue_entity(struct qrq *rq, struct sched_entity *se)
{
    struct rb_node **link = &rq->tasks_timeline.rb_root.rb_node;
    struct rb_node *parent = NULL;
    struct sched_entity *entry;
    bool leftmost = true;

    while (*link) {
        parent = *link;
        entry = rb_entry(parent, struct sched_entity, run_node);
        if (entity_before(se, entry)) {
            link = &parent->rb_left;
        } else {
            link = &parent->rb_right;
            leftmost = false;
        }
    }

    rb_link_node(&se->run_node, parent, link);
    rb_insert_color_cached(&se->run_node, &rq->tasks_timeline, leftmost);
}

static void update_min_vruntime(struct qrq *rq)
{
    struct sched_entity *curr = rq->curr;
    struct rb_node *leftmost = rb_first_cached(&rq->tasks_timeline);
    u64 vruntime = rq->min_vruntime;

    if (curr) {
        if (curr->on_rq)
            vruntime = curr->vruntime;
        else
            curr = NULL;
    }

    if (leftmost) {
        struct sched_entity *se = rb_entry(leftmost, struct sched_entity, run_node);

        if (!curr)
            vruntime = se->vruntime;
        else
            vruntime = min_t(u64, vruntime, se->vruntime);
    }

    /* ensure we never gain time by being placed backwards. */
    rq->min_vruntime = max_vruntime(rq->min_vruntime, vruntime);

    /* entanglement: partner observes our collapse */
    if (rq->partner)
        WRITE_ONCE(rq->partner->min_vruntime,
                   max_vruntime(rq->partner->min_vruntime, rq->min_vruntime));
}

static enum hrtimer_restart qrq_collapse(struct hrtimer *timer)
{
    struct qrq *rq = container_of(timer, struct qrq, collapse_timer);
    unsigned long flags;
    int dec;

    raw_spin_lock_irqsave(&rq->lock, flags);
    dec = atomic_xchg(&rq->decoherence, 0);
    if (dec > QSCHED_MAX_DECOHERENCE) {
        pr_warn_ratelimited("qsched: cpu%d decoherence %d, re-entangling\n",
                            smp_processor_id(), dec);
        rq->partner = qrq_of(cpumask_next_wrap(smp_processor_id(),
                                               entangled_mask, 0, false));
    }
    update_min_vruntime(rq);
    raw_spin_unlock_irqrestore(&rq->lock, flags);

    hrtimer_forward_now(timer, ns_to_ktime(QSCHED_LATENCY_NS));
    return HRTIMER_RESTART;
}

static void update_curr(struct qrq *rq)
{
    struct sched_entity *curr = rq->curr;
    u64 now = rq->clock_task;
    u64 delta_exec;

    if (unlikely(!curr))
        return;

    delta_exec = now - curr->exec_start;
    if (unlikely((s64)delta_exec <= 0))
        return;

    curr->exec_start = now;
    curr->sum_exec_runtime += delta_exec;
    curr->vruntime += __calc_delta(delta_exec, NICE_0_LOAD, curr->load.weight);
    atomic_add(delta_exec >> QSCHED_ENTANGLE_SHIFT, &rq->decoherence);

    update_min_vruntime(rq);
}

static struct sched_entity *pick_next_entity(struct qrq *rq)
{
    struct rb_node *left = rb_first_cached(&rq->tasks_timeline);
    struct sched_entity *se;

    if (!left)
        return NULL;

    se = rb_entry(left, struct sched_entity, run_node);
    rb_erase_cached(&se->run_node, &rq->tasks_timeline);
    se->exec_start = rq->clock_task;
    rq->curr = se;
    return se;
}

static int __init qsched_init(void)
{
    int cpu;

    if (!zalloc_cpumask_var(&entangled_mask, GFP_KERNEL))
        return -ENOMEM;

    cpumask_copy(entangled_mask, cpu_online_mask);

    for_each_possible_cpu(cpu) {
        struct qrq *rq = qrq_of(cpu);

        raw_spin_lock_init(&rq->lock);
        rq->tasks_timeline = RB_ROOT_CACHED;
        rq->min_vruntime = (u64)(-(1LL << 20));
        rq->partner = qrq_of(cpu ^ 1);
        atomic_set(&rq->decoherence, 0);

        hrtimer_init(&rq->collapse_timer, CLOCK_MONOTONIC, HRTIMER_MODE_REL_PINNED);
        rq->collapse_timer.function = qrq_collapse;
    }

    pr_info("qsched: %u CPUs entangled, latency %llu ns\n",
            cpumask_weight(entangled_mask), QSCHED_LATENCY_NS);
    return 0;
}
early_initcall(qsched_init);
