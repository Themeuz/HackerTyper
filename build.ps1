# Compila o HackerTyper.exe usando o csc.exe do .NET Framework (já vem no Windows).
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'csc.exe do .NET Framework 4 nao encontrado.' }

& $csc /nologo /target:winexe /optimize+ /codepage:65001 `
    /out:"$here\HackerTyper.exe" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    "$here\HackerTyper.cs"

if ($LASTEXITCODE -ne 0) { throw "Falha na compilacao (codigo $LASTEXITCODE)." }
Write-Host "OK -> $here\HackerTyper.exe" -ForegroundColor Green
