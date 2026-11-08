// HackerTyper - utilitário de bandeja acionado por Ctrl + Alt + P.
// Compile com build.ps1 (usa o csc.exe do .NET Framework que já vem no Windows).
// Sintaxe restrita a C# 5 por causa desse compilador.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HackerTyper
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            bool created;
            using (var mutex = new Mutex(true, "HackerTyper_SingleInstance_7A11", out created))
            {
                if (!created)
                {
                    MessageBox.Show("O HackerTyper já está rodando na bandeja do sistema.\nAperte Ctrl + Alt + P.",
                        "HackerTyper", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayContext(args.Length > 0 ? args[0] : null));
            }
        }
    }

    // ------------------------------------------------------------------ hotkey global
    class HotkeyWindow : NativeWindow, IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        const int WM_HOTKEY = 0x0312;
        const int HOTKEY_ID = 0x7A11;
        const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;

        public event EventHandler Pressed;
        public readonly bool Registered;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
            Registered = RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.P);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID && Pressed != null)
                Pressed(this, EventArgs.Empty);
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Registered) UnregisterHotKey(Handle, HOTKEY_ID);
            DestroyHandle();
        }
    }

    // ------------------------------------------------------------------ temas
    class Theme
    {
        public readonly string Name;
        public readonly Color Fg, Dim, Bright, Err, Panel, Header, Rain;

        public Theme(string name, Color fg, Color dim, Color bright, Color err, Color panel, Color header, Color rain)
        {
            Name = name; Fg = fg; Dim = dim; Bright = bright; Err = err; Panel = panel; Header = header; Rain = rain;
        }

        public static readonly Theme[] All =
        {
            new Theme("Matrix (verde)", C(0, 255, 65), C(0, 150, 45), C(170, 255, 190), C(255, 55, 55), C(0, 14, 5), C(0, 38, 12), C(0, 115, 40)),
            new Theme("Âmbar retrô", C(255, 176, 0), C(170, 110, 0), C(255, 222, 150), C(255, 70, 50), C(18, 10, 0), C(46, 28, 0), C(120, 75, 0)),
            new Theme("Azul", C(0, 200, 255), C(0, 120, 170), C(175, 235, 255), C(255, 80, 80), C(0, 8, 18), C(0, 28, 48), C(0, 80, 120)),
            new Theme("Vermelho", C(255, 50, 50), C(165, 30, 30), C(255, 175, 175), C(255, 220, 0), C(18, 0, 0), C(48, 0, 0), C(120, 12, 12)),
        };

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        public static Color Scale(Color c, double f)
        {
            return Color.FromArgb((int)(c.R * f), (int)(c.G * f), (int)(c.B * f));
        }

        public static Color Mix(Color a, Color b, double f)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * f), (int)(a.G + (b.G - a.G) * f), (int)(a.B + (b.B - a.B) * f));
        }
    }

    // ------------------------------------------------------------------ comandos de TI (lidos do COMANDOS-TI.md)
    class ItCommand
    {
        public string Category, Description, Note, Code, SearchKey;
    }

    static class ItCommands
    {
        public static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "COMANDOS-TI.md"); }
        }

        // Formato: "## Categoria", "**Descrição**", "> observação" (opcional), bloco ``` com o comando.
        public static List<ItCommand> Load()
        {
            var list = new List<ItCommand>();
            if (!File.Exists(FilePath)) return list;

            string category = "Geral", desc = null, note = null;
            StringBuilder code = null;
            foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                string line = raw.TrimEnd();
                if (code != null)
                {
                    if (line.TrimStart().StartsWith("```"))
                    {
                        if (desc != null && code.Length > 0)
                        {
                            var c = new ItCommand();
                            c.Category = category;
                            c.Description = desc;
                            c.Note = note;
                            c.Code = code.ToString().TrimEnd('\n');
                            c.SearchKey = Fold(category + " " + desc + " " + note + " " + c.Code);
                            list.Add(c);
                        }
                        code = null;
                        desc = null;
                        note = null;
                    }
                    else code.Append(line).Append('\n');
                    continue;
                }
                if (line.StartsWith("## ")) { category = line.Substring(3).Trim(); desc = null; note = null; }
                else if (line.StartsWith("**") && line.EndsWith("**") && line.Length > 4) { desc = line.Substring(2, line.Length - 4).Trim(); note = null; }
                else if (line.StartsWith(">") && desc != null) note = line.TrimStart('>').Trim();
                else if (line.TrimStart().StartsWith("```")) code = new StringBuilder();
            }
            return list;
        }

        // minúsculas e sem acento, para a busca achar "configuracao" em "Configuração"
        public static string Fold(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString().ToLowerInvariant();
        }
    }

    // ------------------------------------------------------------------ bandeja
    class TrayContext : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        readonly NotifyIcon tray;
        readonly HackerForm form;
        readonly HotkeyWindow hotkey;
        readonly ToolStripMenuItem autoItem, startupItem, themeMenu;

        static string StateFile
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HackerTyper", "ultimo_arquivo.txt");
            }
        }

        public TrayContext(string fileArg)
        {
            form = new HackerForm();
            form.LoadRequested = PickFile;
            form.FileDropped = delegate(string p) { LoadFile(p, true); };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Mostrar / esconder   (Ctrl+Alt+P)", null, delegate { Toggle(); });
            menu.Items.Add("Comandos de TI   (F10)", null, delegate { form.ShowCommands(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Carregar código-fonte ou log...", null, delegate { PickFile(); });
            menu.Items.Add("Usar exemplos embutidos", null, delegate { LoadDefault(); ForgetLastFile(); });
            autoItem = new ToolStripMenuItem("Modo automático", null, delegate { form.AutoMode = !form.AutoMode; });
            menu.Items.Add(autoItem);
            themeMenu = new ToolStripMenuItem("Tema   (F12)");
            for (int i = 0; i < Theme.All.Length; i++)
            {
                int idx = i;
                themeMenu.DropDownItems.Add(new ToolStripMenuItem(Theme.All[i].Name, null, delegate { form.ThemeIndex = idx; }));
            }
            menu.Items.Add(themeMenu);
            startupItem = new ToolStripMenuItem("Iniciar com o Windows", null, delegate { ToggleStartup(); });
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Sair", null, delegate { ExitApp(); });
            menu.Opening += delegate
            {
                autoItem.Checked = form.AutoMode;
                startupItem.Checked = IsStartupEnabled();
                for (int i = 0; i < themeMenu.DropDownItems.Count; i++)
                    ((ToolStripMenuItem)themeMenu.DropDownItems[i]).Checked = i == form.ThemeIndex;
            };

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = "HackerTyper - Ctrl+Alt+P";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Toggle(); };

            hotkey = new HotkeyWindow();
            hotkey.Pressed += delegate { Toggle(); };

            string last = null;
            try { if (File.Exists(StateFile)) last = File.ReadAllText(StateFile).Trim(); } catch { }
            if (fileArg != null && File.Exists(fileArg)) LoadFile(fileArg, false);
            else if (!string.IsNullOrEmpty(last) && File.Exists(last)) LoadFile(last, false);
            else LoadDefault();

            tray.ShowBalloonTip(4000, "HackerTyper pronto",
                hotkey.Registered
                    ? "Ctrl + Alt + P abre o terminal. F10 abre os comandos de TI."
                    : "Não consegui registrar Ctrl+Alt+P (outro programa já usa). Clique no ícone da bandeja.",
                hotkey.Registered ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }

        void Toggle()
        {
            if (form.Visible) form.HideTerminal(); else form.ShowTerminal();
        }

        void PickFile()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Escolha um código-fonte ou log de compilação";
                dlg.Filter = "Código e logs|*.c;*.h;*.cpp;*.hpp;*.cs;*.ps1;*.psm1;*.py;*.js;*.ts;*.rs;*.go;*.java;*.asm;*.s;*.S;*.log;*.txt;*.md;Makefile|Todos os arquivos|*.*";
                DialogResult r = form.Visible ? dlg.ShowDialog(form) : dlg.ShowDialog();
                if (r == DialogResult.OK) LoadFile(dlg.FileName, true);
            }
        }

        void LoadFile(string path, bool remember)
        {
            try
            {
                string text;
                var info = new FileInfo(path);
                if (info.Length > 8 * 1024 * 1024)
                {
                    // arquivos gigantes: só os primeiros 8 MB já dão horas de "digitação"
                    using (var sr = new StreamReader(path, Encoding.UTF8, true))
                    {
                        var buf = new char[8 * 1024 * 1024];
                        int n = sr.ReadBlock(buf, 0, buf.Length);
                        text = new string(buf, 0, n);
                    }
                }
                else text = File.ReadAllText(path);

                text = Normalize(text);
                if (text.Trim().Length == 0) throw new Exception("O arquivo está vazio.");
                form.SetSource(text, Path.GetFileName(path));
                if (remember)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(StateFile));
                    File.WriteAllText(StateFile, path);
                    tray.ShowBalloonTip(2500, "Código carregado", Path.GetFileName(path) + " (" + text.Length.ToString("N0") + " caracteres)", ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                tray.ShowBalloonTip(4000, "Falha ao carregar", ex.Message, ToolTipIcon.Error);
                if (form.SourceLength == 0) LoadDefault();
            }
        }

        void LoadDefault()
        {
            var sb = new StringBuilder();
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exemplos");
            try
            {
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir);
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    foreach (var f in files)
                    {
                        sb.Append("\n/* ==================== ").Append(Path.GetFileName(f)).Append(" ==================== */\n\n");
                        sb.Append(File.ReadAllText(f)).Append("\n\n");
                    }
                }
            }
            catch { }
            if (sb.Length == 0) sb.Append(FallbackSource.Text);
            form.SetSource(Normalize(sb.ToString()), "exemplos");
        }

        void ForgetLastFile()
        {
            try { if (File.Exists(StateFile)) File.Delete(StateFile); } catch { }
        }

        static string Normalize(string s)
        {
            s = s.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                if (c == '\n' || (c >= ' ' && c != '﻿' && c != '\u007F')) sb.Append(c);
            return sb.ToString();
        }

        static bool IsStartupEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                    return k != null && k.GetValue("HackerTyper") != null;
            }
            catch { return false; }
        }

        void ToggleStartup()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k.GetValue("HackerTyper") != null) k.DeleteValue("HackerTyper");
                    else k.SetValue("HackerTyper", "\"" + Application.ExecutablePath + "\"");
                }
            }
            catch (Exception ex) { tray.ShowBalloonTip(3000, "Erro", ex.Message, ToolTipIcon.Error); }
        }

        static Icon MakeIcon()
        {
            using (var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var f = new Font("Consolas", 15f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var b = new SolidBrush(Color.FromArgb(0, 255, 65)))
                using (var p = new Pen(Color.FromArgb(0, 160, 40), 2))
                {
                    g.Clear(Color.Black);
                    g.DrawRectangle(p, 1, 1, 29, 29);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.DrawString(">_", f, b, 3, 7);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        void ExitApp()
        {
            hotkey.Dispose();
            tray.Visible = false;
            tray.Dispose();
            form.AllowClose = true;
            form.Close();
            ExitThread();
        }
    }

    // ------------------------------------------------------------------ a janela "hacker"
    class HackerForm : Form
    {
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        const string RED = "\u0001";     // marcador: linha de erro
        const string BRIGHT = "\u0002";  // marcador: linha em destaque
        const TextFormatFlags TF = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

        static readonly double[] Speeds = { 35, 90, 220, 600, 2000 };  // caracteres por segundo
        static readonly string[] SpeedNames = { "400 WPM", "1.000 WPM", "2.500 WPM", "7.000 WPM", "LUDICROUS" };
        static readonly double[] Opacities = { 0.92, 0.82, 0.70, 0.55, 1.0 };

        static readonly string RainChars = "ｦｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ0123456789:.=*+-<>|Z";

        static readonly string[] ProgressLabels =
        {
            "Descriptografando handshake RSA-4096", "Injetando payload no kernel", "Bypass do firewall (porta 443)",
            "Quebrando hash SHA-256", "Sincronizando com satélite NSA-7", "Baixando a internet",
            "Recalibrando o mainframe", "Desfragmentando o ciberespaço", "Rastreando IP 127.0.0.1",
            "Compilando módulos do kernel", "Escalando privilégios para root", "Desviando sinal via proxy na Islândia",
            "Minerando bitcoins da NASA", "Reticulando splines", "Hackeando a mainframe do Gibson",
            "Carregando rede neural quântica", "Apagando rastros nos logs", "Sobrescrevendo a BIOS",
        };

        static readonly string[] Dirs = { "kernel/sched", "mm", "fs/ext4", "net/ipv4", "drivers/gpu/drm", "arch/x86/kernel",
            "crypto", "security/selinux", "drivers/net/ethernet/intel", "block", "ipc", "lib", "fs/btrfs", "net/core",
            "drivers/usb/core", "sound/core", "kernel/bpf", "virt/kvm" };
        static readonly string[] Files = { "core", "fair", "slab", "inode", "tcp_input", "main", "aes_generic", "hooks",
            "e1000_main", "blk-mq", "msg", "string", "page_alloc", "namei", "signal", "irq", "dma", "entry_64",
            "verifier", "hub", "skbuff", "extent_io", "rt", "vmscan", "pcm_native", "kvm_main" };

        static readonly string[][] Failures =
        {
            new[] { "kernel/quantum/flux.c:1337:42: error: 'struct mainframe' has no member named 'firewall'",
                    "ld: kernel/hack.o: undefined reference to `__hack_the_planet'",
                    "make[2]: *** [scripts/Makefile.build:250: kernel/quantum/flux.o] Error 1",
                    "Segmentation fault (core dumped)" },
            new[] { "FATAL: modpost: GPL-incompatible module cafe.ko uses GPL-only symbol 'coffee_brew'",
                    "make[1]: *** [scripts/Makefile.modpost:145: Module.symvers] Error 1",
                    "make: *** [Makefile:1180: vmlinux] Error 139",
                    "Kernel panic - not syncing: VFS: Unable to mount root fs on unknown-block(0,0)" },
            new[] { "*** stack smashing detected ***: terminated",
                    "cc1: out of memory allocating 18446744073709551615 bytes",
                    "Killed process 4040 (cc1) total-vm:99999999kB",
                    "Falha em 99%. Como sempre." },
        };

        static readonly string[] Help =
        {
            "COMANDOS",
            "",
            "qualquer tecla ... digita código como um gênio (3-5 chars/tecla)",
            "F10 .............. painel de comandos de TI (buscar e copiar)",
            "F12 .............. trocar tema de cores",
            "F2 ............... modo automático liga/desliga",
            "F8 ............... velocidade do modo automático",
            "F5 ............... modo compilação (trava em 99% e explode)",
            "F3 / F4 .......... ACESSO PERMITIDO / ACESSO NEGADO",
            "F6 ............... chuva Matrix liga/desliga",
            "F7 ............... nível de transparência",
            "F9 ............... limpar a tela",
            "F11 .............. tela cheia / janela (arraste para mover)",
            "Ctrl+O ........... carregar arquivo (ou arraste um arquivo aqui)",
            "Esc .............. esconder   |   Ctrl+Alt+P mostra/esconde",
        };

        public Action LoadRequested;
        public Action<string> FileDropped;
        public bool AllowClose;
        public bool AutoMode;

        readonly Random rnd = new Random();
        readonly List<string> lines = new List<string>();
        readonly StringBuilder cur = new StringBuilder();
        readonly System.Windows.Forms.Timer timer;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Font font, titleFont, bigFont;
        readonly int charW, lineH;

        string source = "";
        string sourceName = "";
        int pos;
        int linesSinceProgress;
        string progLabel;
        double progValue = -1;  // -1 = nenhuma barra inline ativa

        int themeIdx;
        int speedLevel;
        double autoAcc;
        double lastT;
        int tick;
        int opacityIdx;
        bool fullScreen = true;
        bool rainOn = true;
        Bitmap rainBmp;
        int[] drops;

        int compileState;  // 0 nada, 1 compilando, 2 travado em 99%, 3 falhou
        double compileP, stallUntil, pauseUntil, nextLog;
        string compileFile = "";
        string[] failLines;

        string flashText;
        bool flashIsError;
        double flashUntil;
        bool helpOn;
        double hintUntil;
        string toastText;
        double toastUntil;
        double fakeCpu = 97, fakeNet = 9.81, nextStats;

        // painel de comandos de TI
        bool panelOn;
        List<ItCommand> allCmds = new List<ItCommand>();
        List<ItCommand> cmds = new List<ItCommand>();
        string query = "";
        int sel, panelTop;
        Rectangle listRect;
        int rowH, visibleRows;

        public int SourceLength { get { return source.Length; } }
        Theme T { get { return Theme.All[themeIdx]; } }

        public int ThemeIndex
        {
            get { return themeIdx; }
            set
            {
                themeIdx = ((value % Theme.All.Length) + Theme.All.Length) % Theme.All.Length;
                if (rainOn) RecreateRain();
                Invalidate();
            }
        }

        public HackerForm()
        {
            font = new Font("Consolas", 12f);
            titleFont = new Font("Consolas", 15f, FontStyle.Bold);
            bigFont = new Font("Consolas", 44f, FontStyle.Bold);
            Size m = TextRenderer.MeasureText("MMMMMMMMMMMMMMMMMMMM", font, Size.Empty, TF);
            charW = Math.Max(1, m.Width / 20);
            lineH = Math.Max(1, m.Height + 2);

            Text = "HackerTyper";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
            Opacity = Opacities[0];
            KeyPreview = true;
            AllowDrop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 25;
            timer.Tick += delegate { OnTick(); };
        }

        // ---------------------------------------------------------- API
        public void SetSource(string text, string name)
        {
            source = text;
            sourceName = name;
            pos = 0;
        }

        public void ShowTerminal()
        {
            Screen scr = Screen.FromPoint(Cursor.Position);
            if (fullScreen) Bounds = scr.Bounds;
            else
            {
                Rectangle wa = scr.WorkingArea;
                int w = wa.Width * 6 / 10, h = wa.Height * 6 / 10;
                Bounds = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);
            }
            if (!Visible) Show();
            TopMost = true;
            Activate();
            SetForegroundWindow(Handle);
            lastT = clock.Elapsed.TotalSeconds;
            hintUntil = lastT + 6;
            timer.Start();
            Invalidate();
        }

        public void HideTerminal()
        {
            timer.Stop();
            Hide();
        }

        public void ShowCommands()
        {
            if (!Visible) ShowTerminal();
            OpenPanel();
        }

        // ---------------------------------------------------------- digitação
        void TypeChunk(int n)
        {
            if (source.Length == 0) return;

            if (progValue >= 0)
            {
                progValue += 4 + rnd.NextDouble() * 9;
                if (progValue >= 100)
                {
                    AddLine(BRIGHT + FormatProgress(progLabel, 100) + "  [OK]");
                    progValue = -1;
                }
                return;
            }

            for (int i = 0; i < n; i++)
            {
                char c = source[pos];
                if (++pos >= source.Length) pos = 0;
                if (c == '\n')
                {
                    AddLine(cur.ToString());
                    cur.Length = 0;
                    linesSinceProgress++;
                    if (linesSinceProgress > 18 && rnd.NextDouble() < 0.06)
                    {
                        progLabel = ProgressLabels[rnd.Next(ProgressLabels.Length)];
                        progValue = 0;
                        linesSinceProgress = 0;
                        return;
                    }
                }
                else cur.Append(c);
            }
        }

        void AddLine(string s)
        {
            lines.Add(s);
            if (lines.Count > 3000) lines.RemoveRange(0, 1000);
        }

        void FlushCurrent()
        {
            if (cur.Length > 0) { AddLine(cur.ToString()); cur.Length = 0; }
            if (progValue >= 0) { AddLine(BRIGHT + FormatProgress(progLabel, progValue)); progValue = -1; }
        }

        static string FormatProgress(string label, double p)
        {
            const int width = 32;
            int filled = (int)Math.Round(Math.Min(100, p) / 100.0 * width);
            return "  >> " + label.PadRight(42) + " [" + new string('█', filled) + new string('░', width - filled) + "] "
                + ((int)Math.Min(100, p)).ToString().PadLeft(3) + "%";
        }

        // ---------------------------------------------------------- modo compilação
        void StartCompile()
        {
            FlushCurrent();
            AddLine("");
            AddLine(BRIGHT + "root@mainframe:~# make -j64 ARCH=x86_64 bzImage modules");
            AddLine("  HOSTCC  scripts/basic/fixdep");
            AddLine("  SYSHDR  arch/x86/include/generated/uapi/asm/unistd_64.h");
            compileState = 1;
            compileP = 0;
            pauseUntil = 0;
            nextLog = 0;
            helpOn = false;
        }

        string RandomObj()
        {
            string d = Dirs[rnd.Next(Dirs.Length)], f = Files[rnd.Next(Files.Length)];
            switch (rnd.Next(10))
            {
                case 0: return "  CC [M]  " + d + "/" + f + ".o";
                case 1: return "  AR      " + d + "/built-in.a";
                case 2: return "  LD [M]  " + d + "/" + f + ".ko";
                case 3: return "  AS      arch/x86/entry/" + f + ".o";
                default: return "  CC      " + d + "/" + f + ".o";
            }
        }

        void UpdateCompile(double t, double dt)
        {
            if (compileState == 1)
            {
                if (t >= pauseUntil && rnd.NextDouble() < 0.012) pauseUntil = t + 0.3 + rnd.NextDouble() * 0.9;
                if (t >= pauseUntil) compileP += (26 * (1 - compileP / 115) + rnd.NextDouble() * 14) * dt;
                if (t >= nextLog && t >= pauseUntil)
                {
                    compileFile = RandomObj();
                    AddLine(compileFile);
                    nextLog = t + 0.015 + rnd.NextDouble() * 0.07;
                }
                if (compileP >= 99)
                {
                    compileP = 99;
                    compileState = 2;
                    stallUntil = t + 3.5 + rnd.NextDouble() * 3;
                    AddLine("  LD      vmlinux.o");
                    AddLine("  MODPOST vmlinux.symvers");
                    AddLine("  LD      vmlinux");
                }
            }
            else if (compileState == 2 && t >= stallUntil)
            {
                compileState = 3;
                failLines = Failures[rnd.Next(Failures.Length)];
                foreach (var l in failLines) AddLine(RED + l);
                AddLine("");
            }
        }

        // ---------------------------------------------------------- painel de comandos de TI
        void OpenPanel()
        {
            // relê o arquivo toda vez, para edições no .md aparecerem na hora
            try { allCmds = ItCommands.Load(); }
            catch (Exception ex) { allCmds = new List<ItCommand>(); Toast("Erro ao ler COMANDOS-TI.md: " + ex.Message); }
            panelOn = true;
            helpOn = false;
            ApplyFilter();
            Invalidate();
        }

        void ApplyFilter()
        {
            string[] terms = ItCommands.Fold(query).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            cmds = new List<ItCommand>();
            foreach (var c in allCmds)
            {
                bool ok = true;
                foreach (var term in terms) if (c.SearchKey.IndexOf(term, StringComparison.Ordinal) < 0) { ok = false; break; }
                if (ok) cmds.Add(c);
            }
            sel = 0;
            panelTop = 0;
        }

        void MoveSel(int delta)
        {
            if (cmds.Count == 0) return;
            sel = Math.Max(0, Math.Min(cmds.Count - 1, sel + delta));
        }

        void CopySelected()
        {
            if (sel < 0 || sel >= cmds.Count) return;
            string code = cmds[sel].Code.Replace("\n", "\r\n");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    Clipboard.SetText(code);
                    Toast("Copiado: " + cmds[sel].Description);
                    return;
                }
                catch (ExternalException) { Thread.Sleep(50); }
            }
            Toast("A área de transferência está ocupada. Tente de novo.");
        }

        bool HandlePanelKey(Keys key, Keys mods)
        {
            if (mods == Keys.Control)
            {
                if (key == Keys.C) { CopySelected(); return true; }
                if (key == Keys.V)
                {
                    try
                    {
                        string t = Clipboard.GetText().Replace("\r", " ").Replace("\n", " ");
                        if (t.Length > 0) { query += t; ApplyFilter(); }
                    }
                    catch { }
                    return true;
                }
                return key == Keys.ControlKey || key == Keys.Back;
            }
            switch (key)
            {
                case Keys.Escape:
                    if (query.Length > 0) { query = ""; ApplyFilter(); }
                    else panelOn = false;
                    return true;
                case Keys.F10: panelOn = false; return true;
                case Keys.F12: ThemeIndex = themeIdx + 1; return true;
                case Keys.Up: MoveSel(-1); return true;
                case Keys.Down: MoveSel(1); return true;
                case Keys.PageUp: MoveSel(-Math.Max(1, visibleRows - 1)); return true;
                case Keys.PageDown: MoveSel(Math.Max(1, visibleRows - 1)); return true;
                case Keys.Home: sel = 0; return true;
                case Keys.End: sel = Math.Max(0, cmds.Count - 1); return true;
                case Keys.Enter: CopySelected(); return true;
                case Keys.Back:
                    if (query.Length > 0) { query = query.Substring(0, query.Length - 1); ApplyFilter(); }
                    return true;
                case Keys.Delete: query = ""; ApplyFilter(); return true;
                case Keys.ShiftKey: case Keys.CapsLock: case Keys.NumLock: case Keys.LWin: case Keys.RWin: case Keys.Apps:
                    return true;
            }
            if (key >= Keys.F1 && key <= Keys.F24) return true;  // demais F-keys não fazem nada aqui
            return false;  // tecla de texto: deixa virar WM_CHAR e cair no OnKeyPress
        }

        void Toast(string text)
        {
            toastText = text;
            toastUntil = clock.Elapsed.TotalSeconds + 2.2;
        }

        // ---------------------------------------------------------- loop
        void OnTick()
        {
            double t = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.1, t - lastT);
            lastT = t;
            tick++;

            if (compileState != 0) UpdateCompile(t, dt);
            else if (AutoMode)
            {
                autoAcc += Speeds[speedLevel] * dt;
                int guard = 0;
                while (autoAcc >= 4 && guard++ < 1000)
                {
                    TypeChunk(rnd.Next(3, 6));
                    autoAcc -= 4;
                }
            }

            if (t >= nextStats)
            {
                fakeCpu = 91 + rnd.NextDouble() * 9;
                fakeNet = 8 + rnd.NextDouble() * 3;
                nextStats = t + 0.8;
            }

            if (rainOn && tick % 2 == 0) StepRain();
            Invalidate();
        }

        // ---------------------------------------------------------- chuva Matrix
        void RecreateRain()
        {
            int w = Math.Max(1, ClientSize.Width), h = Math.Max(1, ClientSize.Height);
            if (rainBmp != null) rainBmp.Dispose();
            rainBmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(rainBmp)) g.Clear(Color.Black);
            drops = new int[w / (charW + 8) + 1];
            for (int i = 0; i < drops.Length; i++) drops[i] = -rnd.Next(0, 80);
        }

        void StepRain()
        {
            if (rainBmp == null || rainBmp.Width != ClientSize.Width || rainBmp.Height != ClientSize.Height) RecreateRain();
            int w = rainBmp.Width, h = rainBmp.Height, colW = charW + 8;
            using (var g = Graphics.FromImage(rainBmp))
            {
                using (var b = new SolidBrush(Color.FromArgb(36, 0, 0, 0))) g.FillRectangle(b, 0, 0, w, h);
                for (int i = 0; i < drops.Length; i++)
                {
                    if (drops[i] < 0) { drops[i]++; continue; }
                    int y = drops[i] * lineH;
                    if (y > h) { drops[i] = -rnd.Next(5, 90); continue; }
                    string c = RainChars[rnd.Next(RainChars.Length)].ToString();
                    TextRenderer.DrawText(g, c, font, new Point(i * colW, y), T.Rain, TF);
                    drops[i]++;
                }
            }
        }

        // ---------------------------------------------------------- desenho
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int w = ClientSize.Width, h = ClientSize.Height;
            double t = clock.Elapsed.TotalSeconds;

            if (rainOn && rainBmp != null) g.DrawImageUnscaled(rainBmp, 0, 0);
            else g.Clear(Color.Black);

            int headerH = DrawHeader(g, w);
            int margin = 16, y = headerH + 8;
            int avail = Math.Max(1, (h - y - 8) / lineH);
            int start = Math.Max(0, lines.Count + 1 - avail);

            for (int i = start; i < lines.Count; i++, y += lineH) DrawLine(g, lines[i], margin, y);

            string curLine = progValue >= 0 ? BRIGHT + FormatProgress(progLabel, progValue) : cur.ToString();
            DrawLine(g, curLine, margin, y);
            if (progValue < 0 && compileState == 0 && !panelOn && ((int)(t * 2.5)) % 2 == 0)
                TextRenderer.DrawText(g, "█", font, new Point(margin + cur.Length * charW, y), T.Fg, TF);

            if (compileState != 0) DrawCompile(g, w, h, t);
            if (flashText != null && t < flashUntil) DrawFlash(g, w, h, t);
            if (panelOn) DrawPanelCommands(g, w, h, t);
            else if (helpOn) DrawHelp(g, w, h);
            else if (t < hintUntil && compileState == 0)
            {
                const string hint = " F1 ajuda  |  F10 comandos de TI  |  F12 tema  |  F2 automático  |  F5 compilar  |  Esc esconder ";
                DrawBottomBox(g, w, h, hint, T.Fg, T.Dim, false);
            }

            if (toastText != null && t < toastUntil) DrawBottomBox(g, w, h, " " + toastText + " ", T.Bright, T.Fg, true);

            if (!fullScreen)
                using (var p = new Pen(T.Dim, 2)) g.DrawRectangle(p, 1, 1, w - 2, h - 2);
        }

        void DrawBottomBox(Graphics g, int w, int h, string text, Color fg, Color border, bool centered)
        {
            Size s = TextRenderer.MeasureText(g, text, font, Size.Empty, TF);
            int x = centered ? (w - s.Width - 16) / 2 : w - s.Width - 30;
            var r = new Rectangle(Math.Max(4, x), h - lineH - 26, Math.Min(w - 8, s.Width + 16), lineH + 10);
            DrawPanel(g, r, T.Panel, border);
            TextRenderer.DrawText(g, text, font, new Rectangle(r.X + 8, r.Y + 5, r.Width - 12, lineH), fg, TF | TextFormatFlags.EndEllipsis);
        }

        int DrawHeader(Graphics g, int w)
        {
            int hh = lineH + 10;
            using (var b = new SolidBrush(T.Header)) g.FillRectangle(b, 0, 0, w, hh);
            using (var p = new Pen(T.Dim)) g.DrawLine(p, 0, hh, w, hh);
            string left = " root@mainframe:~# ./hack --alvo=" + sourceName + "   |   AUTO: " + (AutoMode ? SpeedNames[speedLevel] : "OFF");
            string right = string.Format("CPU {0}%   RAM 31.9/32 GB   UPLINK {1:0.00} Gb/s   {2:HH:mm:ss} ", (int)fakeCpu, fakeNet, DateTime.Now);
            TextRenderer.DrawText(g, left, font, new Point(4, 5), T.Fg, TF);
            Size s = TextRenderer.MeasureText(g, right, font, Size.Empty, TF);
            TextRenderer.DrawText(g, right, font, new Point(w - s.Width - 4, 5), T.Fg, TF);
            return hh;
        }

        void DrawLine(Graphics g, string s, int x, int y)
        {
            if (s.Length == 0) return;
            Color c;
            if (s.StartsWith(RED)) { c = T.Err; s = s.Substring(1); }
            else if (s.StartsWith(BRIGHT)) { c = T.Bright; s = s.Substring(1); }
            else
            {
                string tr = s.TrimStart();
                c = (tr.StartsWith("//") || tr.StartsWith("/*") || tr.StartsWith("*") || tr.StartsWith("#") || tr.StartsWith("<#"))
                    ? T.Dim : T.Fg;
            }
            TextRenderer.DrawText(g, s, font, new Point(x, y), c, TF);
        }

        static void DrawPanel(Graphics g, Rectangle r, Color bg, Color border)
        {
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, r);
            using (var p = new Pen(border, 2)) g.DrawRectangle(p, r);
        }

        void DrawCompile(Graphics g, int w, int h, double t)
        {
            bool failed = compileState == 3;
            bool blink = ((int)(t * 3)) % 2 == 0;
            int bw = Math.Min(980, w - 40);
            int bh = failed ? lineH * (failLines.Length + 6) : lineH * 8;
            var r = new Rectangle((w - bw) / 2, (h - bh) / 2, bw, bh);
            int x = r.X + 24, y = r.Y + lineH;

            if (failed)
            {
                DrawPanel(g, r, Theme.Scale(T.Err, 0.11), blink ? T.Err : Theme.Scale(T.Err, 0.45));
                TextRenderer.DrawText(g, "!! ERRO FATAL - COMPILAÇÃO ABORTADA EM 99% !!", titleFont, new Point(x, y), T.Err, TF);
                y += lineH * 2;
                foreach (var l in failLines) { TextRenderer.DrawText(g, l, font, new Point(x, y), Theme.Mix(T.Err, Color.White, 0.5), TF); y += lineH; }
                y += lineH / 2;
                TextRenderer.DrawText(g, "[pressione qualquer tecla]", font, new Point(x, y), Theme.Scale(T.Err, 0.65), TF);
                return;
            }

            DrawPanel(g, r, T.Panel, T.Fg);
            TextRenderer.DrawText(g, "COMPILANDO hackos-kernel 6.66.6-rt (x86_64)", titleFont, new Point(x, y), T.Bright, TF);
            y += lineH * 2;

            int pctW = charW * 6;
            var bar = new Rectangle(x, y, bw - 48 - pctW, lineH + 4);
            using (var p = new Pen(T.Fg, 1)) g.DrawRectangle(p, bar);
            int fillW = (int)((bar.Width - 4) * compileP / 100.0);
            using (var b = new SolidBrush(Theme.Scale(T.Fg, 0.8))) g.FillRectangle(b, bar.X + 2, bar.Y + 2, fillW, bar.Height - 3);
            // brilho correndo pela barra
            int glow = (int)((t * 300) % Math.Max(1, fillW));
            using (var b = new SolidBrush(Color.FromArgb(120, T.Bright))) g.FillRectangle(b, bar.X + 2 + glow, bar.Y + 2, Math.Min(40, fillW - glow), bar.Height - 3);
            TextRenderer.DrawText(g, ((int)compileP).ToString().PadLeft(3) + "%", titleFont, new Point(bar.Right + 10, bar.Y), T.Fg, TF);
            y += lineH * 2 + 4;

            string status = compileState == 2
                ? "  LD      vmlinux" + new string('.', 1 + ((int)(t * 2)) % 3) + "   (quase lá...)"
                : compileFile;
            TextRenderer.DrawText(g, status, font, new Point(x, y), T.Fg, TF);
            y += lineH;
            string eta = compileState == 2 ? "Tempo restante: calculando..." : "Tempo restante: " + Math.Max(1, (int)((99 - compileP) / 7)) + " s";
            TextRenderer.DrawText(g, eta, font, new Point(x, y), T.Dim, TF);
        }

        void DrawFlash(Graphics g, int w, int h, double t)
        {
            Color c = flashIsError ? T.Err : T.Fg;
            bool blink = ((int)(t * 4)) % 2 == 0;
            Size s = TextRenderer.MeasureText(g, flashText, bigFont, Size.Empty, TF);
            var r = new Rectangle((w - s.Width) / 2 - 50, (h - s.Height) / 2 - 36, s.Width + 100, s.Height + 72);
            DrawPanel(g, r, flashIsError ? Theme.Scale(T.Err, 0.11) : T.Panel, c);
            if (blink) TextRenderer.DrawText(g, flashText, bigFont, new Point(r.X + 50, r.Y + 36), c, TF);
        }

        void DrawHelp(Graphics g, int w, int h)
        {
            int maxLen = 0;
            foreach (var l in Help) maxLen = Math.Max(maxLen, l.Length);
            int bw = maxLen * charW + 60, bh = Help.Length * lineH + 40;
            var r = new Rectangle((w - bw) / 2, (h - bh) / 2, bw, bh);
            DrawPanel(g, r, T.Panel, T.Fg);
            int y = r.Y + 20;
            for (int i = 0; i < Help.Length; i++, y += lineH)
                TextRenderer.DrawText(g, Help[i], i == 0 ? titleFont : font, new Point(r.X + 30, y), i == 0 ? T.Bright : T.Fg, TF);
        }

        void DrawPanelCommands(Graphics g, int w, int h, double t)
        {
            int m = fullScreen ? Math.Max(20, w / 20) : 12;
            var r = new Rectangle(m, lineH + 10 + m / 2, w - 2 * m, h - (lineH + 10) - m);
            DrawPanel(g, r, T.Panel, T.Fg);
            int x = r.X + 20, y = r.Y + 14, innerW = r.Width - 40;
            int maxChars = Math.Max(10, innerW / charW);

            string title = "COMANDOS DE TI  -  " + cmds.Count + (cmds.Count == allCmds.Count ? "" : " de " + allCmds.Count);
            TextRenderer.DrawText(g, title, titleFont, new Point(x, y), T.Bright, TF);
            y += lineH + 12;

            string cursor = ((int)(t * 2.5)) % 2 == 0 ? "█" : " ";
            string search = "buscar> " + query + cursor;
            TextRenderer.DrawText(g, search, font, new Point(x, y), T.Fg, TF);
            if (query.Length == 0)
                TextRenderer.DrawText(g, "digite para filtrar: serial, processador, oobe, rede, bitlocker...", font,
                    new Point(x + (search.Length + 1) * charW, y), T.Dim, TF);
            y += lineH + 6;
            using (var p = new Pen(T.Dim)) g.DrawLine(p, x, y, x + innerW, y);
            y += 8;

            // área de detalhes + rodapé fixos embaixo
            const int detailLines = 6;
            int footerY = r.Bottom - lineH - 12;
            int detailTop = footerY - detailLines * lineH - 14;

            rowH = lineH * 2 + 6;
            listRect = new Rectangle(x, y, innerW, Math.Max(rowH, detailTop - y - 6));
            visibleRows = Math.Max(1, listRect.Height / rowH);

            if (allCmds.Count == 0)
            {
                TextRenderer.DrawText(g, "COMANDOS-TI.md não encontrado ou vazio em:", font, new Point(x, y), T.Err, TF);
                TextRenderer.DrawText(g, ItCommands.FilePath, font, new Point(x, y + lineH), T.Fg, TF);
            }
            else if (cmds.Count == 0)
                TextRenderer.DrawText(g, "Nada encontrado para \"" + query + "\". Backspace ou Esc para limpar.", font, new Point(x, y), T.Dim, TF);

            if (sel < panelTop) panelTop = sel;
            if (sel >= panelTop + visibleRows) panelTop = sel - visibleRows + 1;
            panelTop = Math.Max(0, Math.Min(panelTop, Math.Max(0, cmds.Count - visibleRows)));

            for (int i = panelTop; i < cmds.Count && i < panelTop + visibleRows; i++)
            {
                var c = cmds[i];
                int ry = listRect.Y + (i - panelTop) * rowH;
                if (i == sel)
                {
                    using (var b = new SolidBrush(Theme.Scale(T.Fg, 0.16))) g.FillRectangle(b, x - 8, ry - 2, innerW + 16, rowH - 2);
                    using (var b = new SolidBrush(T.Fg)) g.FillRectangle(b, x - 8, ry - 2, 4, rowH - 2);
                }
                string cat = "[" + c.Category + "] ";
                TextRenderer.DrawText(g, cat, font, new Point(x + 6, ry), T.Dim, TF);
                TextRenderer.DrawText(g, Clip(c.Description, maxChars - cat.Length - 2), font,
                    new Point(x + 6 + cat.Length * charW, ry), i == sel ? T.Bright : T.Fg, TF);

                string first = c.Code;
                int nl = first.IndexOf('\n');
                if (nl >= 0) first = first.Substring(0, nl) + "  (+" + (c.Code.Split('\n').Length - 1) + " linha/s)";
                TextRenderer.DrawText(g, "  " + Clip(first, maxChars - 4), font, new Point(x + 6, ry + lineH), i == sel ? T.Fg : T.Dim, TF);
            }

            // barra de rolagem
            if (cmds.Count > visibleRows)
            {
                int trackH = visibleRows * rowH;
                int thumbH = Math.Max(20, trackH * visibleRows / cmds.Count);
                int thumbY = listRect.Y + (trackH - thumbH) * panelTop / Math.Max(1, cmds.Count - visibleRows);
                using (var b = new SolidBrush(Theme.Scale(T.Fg, 0.25))) g.FillRectangle(b, r.Right - 10, listRect.Y, 4, trackH);
                using (var b = new SolidBrush(T.Fg)) g.FillRectangle(b, r.Right - 10, thumbY, 4, thumbH);
            }

            // detalhes do selecionado
            using (var p = new Pen(T.Dim)) g.DrawLine(p, x, detailTop, x + innerW, detailTop);
            int dy = detailTop + 8;
            if (sel >= 0 && sel < cmds.Count)
            {
                var c = cmds[sel];
                int used = 0;
                if (!string.IsNullOrEmpty(c.Note))
                {
                    TextRenderer.DrawText(g, Clip("obs: " + c.Note, maxChars), font, new Point(x, dy), T.Bright, TF);
                    dy += lineH; used++;
                }
                foreach (var cl in WrapCode(c.Code, maxChars - 2))
                {
                    if (used >= detailLines - 1) { TextRenderer.DrawText(g, "...", font, new Point(x, dy), T.Dim, TF); break; }
                    TextRenderer.DrawText(g, cl, font, new Point(x, dy), T.Fg, TF);
                    dy += lineH; used++;
                }
            }

            string footer = "↑↓ PgUp PgDn navegar   |   Enter ou duplo clique copia   |   Ctrl+V cola na busca   |   Esc limpa/fecha   |   F12 tema";
            TextRenderer.DrawText(g, Clip(footer, maxChars), font, new Point(x, footerY), T.Dim, TF);
        }

        // quebra o comando em linhas que cabem na largura; a primeira linha de cada comando ganha "PS> "
        static List<string> WrapCode(string code, int width)
        {
            var result = new List<string>();
            width = Math.Max(10, width);
            foreach (string src in code.Split('\n'))
            {
                string s = "PS> " + src;
                while (s.Length > width)
                {
                    result.Add(s.Substring(0, width));
                    s = "    " + s.Substring(width);
                }
                result.Add(s);
            }
            return result;
        }

        static string Clip(string s, int max)
        {
            if (max <= 1) return "";
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        // ---------------------------------------------------------- teclado / mouse
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode, mods = keyData & Keys.Modifiers;
            if ((mods & Keys.Alt) != 0) return base.ProcessCmdKey(ref msg, keyData);  // deixa Alt+F4 etc.

            if (panelOn)
            {
                if (HandlePanelKey(key, mods)) { Invalidate(); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            if (mods == Keys.Control && key == Keys.O)
            {
                if (LoadRequested != null) LoadRequested();
                return true;
            }
            HandleKey(key);
            Invalidate();
            return true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (panelOn && !char.IsControl(e.KeyChar))
            {
                query += e.KeyChar;
                ApplyFilter();
                e.Handled = true;
                Invalidate();
            }
        }

        void HandleKey(Keys key)
        {
            double t = clock.Elapsed.TotalSeconds;
            switch (key)
            {
                case Keys.ShiftKey: case Keys.ControlKey: case Keys.Menu: case Keys.LWin: case Keys.RWin:
                case Keys.CapsLock: case Keys.NumLock: case Keys.Scroll: case Keys.Apps:
                    return;
                case Keys.Escape:
                    if (compileState == 3) compileState = 0;
                    else if (helpOn) helpOn = false;
                    else if (flashText != null && t < flashUntil) flashUntil = 0;
                    else HideTerminal();
                    return;
                case Keys.F1: helpOn = !helpOn; return;
                case Keys.F2: AutoMode = !AutoMode; autoAcc = 0; return;
                case Keys.F3: Flash("ACESSO PERMITIDO", false); return;
                case Keys.F4: Flash("ACESSO NEGADO", true); return;
                case Keys.F5: StartCompile(); return;
                case Keys.F6:
                    rainOn = !rainOn;
                    if (rainOn) RecreateRain();
                    return;
                case Keys.F7: opacityIdx = (opacityIdx + 1) % Opacities.Length; Opacity = Opacities[opacityIdx]; return;
                case Keys.F8: speedLevel = (speedLevel + 1) % Speeds.Length; AutoMode = true; return;
                case Keys.F9: lines.Clear(); cur.Length = 0; progValue = -1; return;
                case Keys.F10: OpenPanel(); return;
                case Keys.F11: fullScreen = !fullScreen; ShowTerminal(); return;
                case Keys.F12:
                    ThemeIndex = themeIdx + 1;
                    Toast("Tema: " + T.Name);
                    return;
            }

            if (compileState == 3) { compileState = 0; return; }
            if (compileState != 0) return;
            helpOn = false;
            TypeChunk(rnd.Next(3, 6));
        }

        void Flash(string text, bool isError)
        {
            flashText = text;
            flashIsError = isError;
            flashUntil = clock.Elapsed.TotalSeconds + 3;
        }

        int RowAt(Point p)
        {
            if (!listRect.Contains(p) || rowH <= 0) return -1;
            int i = panelTop + (p.Y - listRect.Y) / rowH;
            return i < cmds.Count ? i : -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (panelOn)
            {
                int i = RowAt(e.Location);
                if (i >= 0) { sel = i; Invalidate(); }
                return;
            }
            if (!fullScreen && e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);  // WM_NCLBUTTONDOWN / HTCAPTION
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (panelOn && RowAt(e.Location) >= 0) { CopySelected(); Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!panelOn || cmds.Count == 0) return;
            int steps = -e.Delta / 120 * 3;
            panelTop = Math.Max(0, Math.Min(Math.Max(0, cmds.Count - visibleRows), panelTop + steps));
            if (sel < panelTop) sel = panelTop;
            if (sel >= panelTop + visibleRows) sel = panelTop + visibleRows - 1;
            Invalidate();
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0 && FileDropped != null) FileDropped(files[0]);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (rainOn && ClientSize.Width > 0) RecreateRain();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideTerminal();
                return;
            }
            base.OnFormClosing(e);
        }
    }

    static class FallbackSource
    {
        public const string Text =
@"/* exemplos/ não encontrado - usando código embutido */
#include <linux/module.h>
#include <linux/kernel.h>

static int __init mainframe_init(void)
{
    struct task_struct *t;
    unsigned long flags;

    spin_lock_irqsave(&mainframe_lock, flags);
    for_each_process(t) {
        if (t->cred->uid.val == 0)
            atomic_inc(&root_count);
    }
    spin_unlock_irqrestore(&mainframe_lock, flags);
    pr_info(""mainframe: %d processos root encontrados\n"", atomic_read(&root_count));
    return 0;
}
module_init(mainframe_init);
";
    }
}
