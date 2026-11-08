<#
.SYNOPSIS
    Operação MAINFRAME - módulo de reconhecimento e sincronização.
.NOTES
    Classificação: ULTRA SECRETO // NÃO DISTRIBUIR
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)][string]   $Alvo        = '10.13.37.0/24',
    [Parameter(Mandatory = $false)][int]      $Threads     = 64,
    [Parameter(Mandatory = $false)][ValidateSet('stealth','agressivo','ludicrous')] $Modo = 'stealth'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:Chave   = [byte[]](0xDE,0xAD,0xBE,0xEF,0xCA,0xFE,0xBA,0xBE,0x13,0x37,0xC0,0xDE,0xF0,0x0D,0xFA,0xCE)
$script:Sessao  = [guid]::NewGuid().ToString('N').Substring(0, 12)
$script:Inicio  = [Diagnostics.Stopwatch]::StartNew()

function ConvertTo-SubRede {
    param([string] $Cidr)
    $ip, $bits = $Cidr -split '/'
    $base = [BitConverter]::ToUInt32(([Net.IPAddress]::Parse($ip).GetAddressBytes()[3..0]), 0)
    $mask = [uint32]([math]::Pow(2, 32) - [math]::Pow(2, 32 - [int]$bits))
    $rede = $base -band $mask
    $total = [math]::Pow(2, 32 - [int]$bits) - 2
    for ($i = 1; $i -le $total; $i++) {
        $b = [BitConverter]::GetBytes([uint32]($rede + $i))
        [Net.IPAddress]::new([byte[]]($b[3], $b[2], $b[1], $b[0]))
    }
}

function Get-Assinatura {
    param([Parameter(ValueFromPipeline)][byte[]] $Dados)
    process {
        $hmac = [Security.Cryptography.HMACSHA256]::new($script:Chave)
        try   { -join ($hmac.ComputeHash($Dados) | ForEach-Object { $_.ToString('x2') }) }
        finally { $hmac.Dispose() }
    }
}

function Invoke-Varredura {
    param([Net.IPAddress[]] $Hosts, [int[]] $Portas = @(22, 80, 443, 3389, 5985, 8080))

    $pool = [RunspaceFactory]::CreateRunspacePool(1, $Threads)
    $pool.Open()
    $jobs = foreach ($h in $Hosts) {
        $ps = [PowerShell]::Create().AddScript({
            param($h, $Portas)
            foreach ($p in $Portas) {
                $tcp = [Net.Sockets.TcpClient]::new()
                $ok  = $tcp.ConnectAsync($h, $p).Wait(150)
                [pscustomobject]@{ Host = $h; Porta = $p; Aberta = $ok -and $tcp.Connected }
                $tcp.Dispose()
            }
        }).AddArgument($h).AddArgument($Portas)
        $ps.RunspacePool = $pool
        [pscustomobject]@{ Pipe = $ps; Handle = $ps.BeginInvoke() }
    }

    $n = 0
    foreach ($j in $jobs) {
        $j.Pipe.EndInvoke($j.Handle) | Where-Object Aberta
        $j.Pipe.Dispose()
        $n++
        Write-Progress -Activity "Varredura $Alvo" -Status "$n / $($jobs.Count)" -PercentComplete (100 * $n / $jobs.Count)
    }
    $pool.Close()
}

function Protect-Pacote {
    param([string] $Texto)
    $aes = [Security.Cryptography.Aes]::Create()
    $aes.Key = $script:Chave + $script:Chave
    $aes.GenerateIV()
    $enc = $aes.CreateEncryptor()
    $raw = [Text.Encoding]::UTF8.GetBytes($Texto)
    $cif = $enc.TransformFinalBlock($raw, 0, $raw.Length)
    [Convert]::ToBase64String($aes.IV + $cif)
    $aes.Dispose()
}

Write-Host "[*] sessão $script:Sessao iniciada em modo $Modo" -ForegroundColor Green
$hosts  = @(ConvertTo-SubRede -Cidr $Alvo)
Write-Host "[*] $($hosts.Count) hosts no escopo, $Threads threads" -ForegroundColor Green

$achados = Invoke-Varredura -Hosts $hosts
$relatorio = $achados | Group-Object Host | ForEach-Object {
    [pscustomobject]@{
        Host   = $_.Name
        Portas = ($_.Group.Porta | Sort-Object) -join ','
        Hash   = [Text.Encoding]::UTF8.GetBytes($_.Name) | Get-Assinatura
    }
}

$relatorio | Format-Table -AutoSize
$pacote = Protect-Pacote -Texto ($relatorio | ConvertTo-Json -Compress)
Write-Host "[+] pacote cifrado: $($pacote.Length) bytes em $($script:Inicio.Elapsed.TotalSeconds.ToString('0.00')) s" -ForegroundColor Green
Write-Host "[+] ACESSO CONCEDIDO" -ForegroundColor Green
