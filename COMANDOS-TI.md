# Comandos úteis de PowerShell para TI

Referência rápida para suporte e campo. Os itens marcados com **(admin)** precisam de um PowerShell aberto como administrador.
Este arquivo também alimenta o painel **F10** do HackerTyper. Para acrescentar um comando, siga o formato: `## Categoria`, depois `**Descrição**`, uma linha opcional `> observação` e o bloco de código.

## Instalação e OOBE

**Instalar o Windows 11 sem internet / sem conta Microsoft (OOBE)**
> Na tela de rede do OOBE aperte Shift+F10 para abrir o prompt. A máquina reinicia e aparece a opção "Não tenho internet".
```powershell
OOBE\BYPASSNRO
```

**Alternativa ao BYPASSNRO (builds mais novas em que ele foi removido)**
> Também no Shift+F10 do OOBE. Abre direto a criação de conta local.
```powershell
start ms-cxh:localonly
```

**BYPASSNRO via registro (se o script não existir)**
> Shift+F10 no OOBE. Grava a chave e reinicia na hora.
```powershell
reg add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE /v BypassNRO /t REG_DWORD /d 1 /f; shutdown /r /t 0
```

**Derrubar a rede durante o OOBE**
```powershell
ipconfig /release
```

**Versão, edição e build do Windows**
```powershell
Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion, OsBuildNumber, OsArchitecture
```

**Chave do Windows gravada na BIOS (OEM)**
```powershell
(Get-CimInstance SoftwareLicensingService).OA3xOriginalProductKey
```

**Status de ativação do Windows**
```powershell
slmgr /dlv
```

## Hardware

**Processador (modelo, núcleos, threads, clock)**
```powershell
Get-CimInstance Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed
```

**Número de série (Service Tag) do notebook/desktop**
```powershell
(Get-CimInstance Win32_BIOS).SerialNumber
```

**Fabricante, modelo e serial do equipamento**
> Em Lenovo o nome comercial aparece em Version.
```powershell
Get-CimInstance Win32_ComputerSystemProduct | Select-Object Vendor, Name, Version, IdentifyingNumber
```

**Versão e data da BIOS**
```powershell
Get-CimInstance Win32_BIOS | Select-Object Manufacturer, SMBIOSBIOSVersion, ReleaseDate
```

**Placa-mãe**
```powershell
Get-CimInstance Win32_BaseBoard | Select-Object Manufacturer, Product, SerialNumber, Version
```

**Memória RAM total (GB)**
```powershell
[math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 1)
```

**Pentes de memória instalados (slot, fabricante, tamanho, velocidade)**
```powershell
Get-CimInstance Win32_PhysicalMemory | Select-Object BankLabel, DeviceLocator, Manufacturer, PartNumber, @{n='GB';e={$_.Capacity/1GB}}, Speed
```

**Quantidade de slots de memória**
```powershell
(Get-CimInstance Win32_PhysicalMemoryArray).MemoryDevices
```

**Placa de vídeo e versão do driver**
```powershell
Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, VideoModeDescription
```

**Discos físicos (SSD/HD, barramento, saúde, tamanho)**
```powershell
Get-PhysicalDisk | Select-Object FriendlyName, MediaType, BusType, HealthStatus, @{n='GB';e={[math]::Round($_.Size/1GB)}}
```

**Relatório de saúde da bateria (salva na Área de Trabalho)**
```powershell
powercfg /batteryreport /output "$env:USERPROFILE\Desktop\bateria.html"
```

**Carga atual da bateria**
```powershell
Get-CimInstance Win32_Battery | Select-Object Name, EstimatedChargeRemaining, BatteryStatus
```

**TPM presente e pronto (admin)**
```powershell
Get-Tpm
```

**Secure Boot ligado? (admin)**
```powershell
Confirm-SecureBootUEFI
```

**Modelo dos monitores conectados**
```powershell
Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorID | ForEach-Object { -join [char[]]($_.UserFriendlyName -ne 0) }
```

**Dispositivos com problema / sem driver**
```powershell
Get-PnpDevice -PresentOnly | Where-Object Status -ne 'OK' | Select-Object Class, FriendlyName, Status
```

**Inventário rápido em CSV na Área de Trabalho**
```powershell
Get-CimInstance Win32_ComputerSystem | Select-Object Name, Manufacturer, Model, @{n='Serial';e={(Get-CimInstance Win32_BIOS).SerialNumber}}, @{n='CPU';e={(Get-CimInstance Win32_Processor).Name}}, @{n='RAM_GB';e={[math]::Round($_.TotalPhysicalMemory/1GB)}} | Export-Csv "$env:USERPROFILE\Desktop\inventario.csv" -NoTypeInformation -Encoding UTF8
```

## Sistema

**Nome do computador e usuário logado**
```powershell
Get-CimInstance Win32_ComputerSystem | Select-Object Name, Domain, UserName
```

**Há quanto tempo a máquina está ligada**
```powershell
(Get-Date) - (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
```

**Data de instalação do Windows**
```powershell
(Get-CimInstance Win32_OperatingSystem).InstallDate
```

**Renomear o computador (admin)**
```powershell
Rename-Computer -NewName "PC-NOVO" -Restart
```

**Colocar a máquina no domínio (admin)**
```powershell
Add-Computer -DomainName empresa.local -Credential empresa\admin -Restart
```

**Reparar relação de confiança com o domínio (admin)**
```powershell
Test-ComputerSecureChannel -Repair -Credential empresa\admin
```

**Programas instalados**
```powershell
Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*, HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* | Where-Object DisplayName | Select-Object DisplayName, DisplayVersion, Publisher | Sort-Object DisplayName
```

**Últimas atualizações (KBs) instaladas**
```powershell
Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 15 HotFixID, Description, InstalledOn
```

**Permitir rodar scripts .ps1 do usuário atual**
```powershell
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**Atalhos de console (abrir direto)**
> devmgmt.msc = Dispositivos, diskmgmt.msc = Discos, services.msc = Serviços, eventvwr.msc = Eventos, ncpa.cpl = Rede, appwiz.cpl = Programas.
```powershell
devmgmt.msc
```

## Reparo do Windows

**Verificar e reparar arquivos do sistema (admin)**
```powershell
sfc /scannow
```

**Reparar a imagem do Windows com DISM (admin)**
> Rode antes do sfc quando o sfc não conseguir corrigir.
```powershell
DISM /Online /Cleanup-Image /RestoreHealth
```

**Verificar o disco no próximo boot (admin)**
```powershell
chkdsk C: /f /r
```

**Resetar o Windows Update (admin)**
```powershell
Stop-Service wuauserv, bits, cryptsvc -Force
Rename-Item C:\Windows\SoftwareDistribution SoftwareDistribution.old
Start-Service wuauserv, bits, cryptsvc
```

**Reiniciar o Explorer (barra de tarefas travada)**
```powershell
Stop-Process -Name explorer -Force
```

**Limpar arquivos temporários do usuário**
```powershell
Remove-Item "$env:TEMP\*" -Recurse -Force -ErrorAction SilentlyContinue
```

**Recriar o boot do Windows (no prompt do WinRE)**
> Use na tela de recuperação quando o Windows não inicia. Ajuste a letra da unidade, que no WinRE pode não ser C:.
```powershell
bcdboot C:\Windows
```

## Rede

**Configuração de IP de todas as placas**
```powershell
Get-NetIPConfiguration
```

**Placas de rede, MAC e velocidade**
```powershell
Get-NetAdapter | Select-Object Name, InterfaceDescription, MacAddress, Status, LinkSpeed
```

**Limpar cache de DNS**
```powershell
Clear-DnsClientCache
```

**Resetar a pilha de rede (admin, reinicie depois)**
```powershell
netsh winsock reset; netsh int ip reset
```

**Testar se uma porta responde**
```powershell
Test-NetConnection google.com -Port 443
```

**Ping contínuo**
```powershell
ping -t 8.8.8.8
```

**Redes Wi-Fi salvas**
```powershell
netsh wlan show profiles
```

**Senha de uma rede Wi-Fi salva**
```powershell
netsh wlan show profile name="NOME_DA_REDE" key=clear
```

**IP público**
```powershell
Invoke-RestMethod https://api.ipify.org
```

**Portas abertas escutando e processo dono**
```powershell
Get-NetTCPConnection -State Listen | Select-Object LocalAddress, LocalPort, OwningProcess, @{n='Processo';e={(Get-Process -Id $_.OwningProcess).Name}} | Sort-Object LocalPort
```

**Definir IP fixo (admin)**
```powershell
New-NetIPAddress -InterfaceAlias "Ethernet" -IPAddress 192.168.0.50 -PrefixLength 24 -DefaultGateway 192.168.0.1
Set-DnsClientServerAddress -InterfaceAlias "Ethernet" -ServerAddresses 8.8.8.8, 1.1.1.1
```

**Voltar para DHCP (admin)**
```powershell
Set-NetIPInterface -InterfaceAlias "Ethernet" -Dhcp Enabled; Set-DnsClientServerAddress -InterfaceAlias "Ethernet" -ResetServerAddresses
```

**Mapear unidade de rede**
```powershell
New-PSDrive -Name Z -PSProvider FileSystem -Root \\servidor\pasta -Persist
```

**Status do firewall por perfil**
```powershell
Get-NetFirewallProfile | Select-Object Name, Enabled
```

## Discos e armazenamento

**Espaço livre em cada unidade**
```powershell
Get-Volume | Where-Object DriveLetter | Select-Object DriveLetter, FileSystemLabel, @{n='LivreGB';e={[math]::Round($_.SizeRemaining/1GB,1)}}, @{n='TotalGB';e={[math]::Round($_.Size/1GB,1)}}
```

**Discos e estilo de partição (GPT/MBR)**
```powershell
Get-Disk | Select-Object Number, FriendlyName, PartitionStyle, @{n='GB';e={[math]::Round($_.Size/1GB)}}
```

**Status do BitLocker (admin)**
```powershell
Get-BitLockerVolume | Select-Object MountPoint, VolumeStatus, ProtectionStatus, EncryptionPercentage
```

**Chave de recuperação do BitLocker (admin)**
```powershell
(Get-BitLockerVolume -MountPoint C:).KeyProtector | Where-Object KeyProtectorType -eq 'RecoveryPassword' | Select-Object KeyProtectorId, RecoveryPassword
```

**20 maiores arquivos do disco C:**
```powershell
Get-ChildItem C:\ -Recurse -File -ErrorAction SilentlyContinue | Sort-Object Length -Descending | Select-Object -First 20 FullName, @{n='MB';e={[math]::Round($_.Length/1MB)}}
```

**Otimizar SSD (TRIM) (admin)**
```powershell
Optimize-Volume -DriveLetter C -ReTrim -Verbose
```

## Usuários e segurança

**Usuários locais**
```powershell
Get-LocalUser | Select-Object Name, Enabled, LastLogon
```

**Criar usuário local e torná-lo administrador (admin)**
> O SID S-1-5-32-544 é o grupo Administradores em qualquer idioma do Windows.
```powershell
New-LocalUser -Name "tecnico" -Password (Read-Host -AsSecureString "Senha")
Add-LocalGroupMember -SID S-1-5-32-544 -Member "tecnico"
```

**Trocar a senha de um usuário local (admin)**
```powershell
Set-LocalUser -Name "usuario" -Password (Read-Host -AsSecureString "Nova senha")
```

**Ativar o Administrador interno oculto (admin)**
```powershell
Get-LocalUser | Where-Object { $_.SID -like '*-500' } | Enable-LocalUser
```

**O PowerShell atual está como administrador?**
```powershell
([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
```

**Sessões de usuários logados**
```powershell
query user
```

**Status do Windows Defender**
```powershell
Get-MpComputerStatus | Select-Object AMServiceEnabled, RealTimeProtectionEnabled, AntivirusSignatureLastUpdated
```

**Atualizar o Defender e fazer verificação rápida (admin)**
```powershell
Update-MpSignature; Start-MpScan -ScanType QuickScan
```

**Aplicar GPOs e ver as aplicadas**
```powershell
gpupdate /force; gpresult /r
```

## Processos, serviços e impressão

**10 processos que mais usam CPU**
```powershell
Get-Process | Sort-Object CPU -Descending | Select-Object -First 10 Name, Id, CPU, @{n='MemMB';e={[math]::Round($_.WS/1MB)}}
```

**Encerrar um processo travado**
```powershell
Stop-Process -Name "chrome" -Force
```

**Serviços automáticos que estão parados**
```powershell
Get-CimInstance Win32_Service -Filter "StartMode='Auto' AND State<>'Running'" | Select-Object Name, DisplayName, State
```

**Programas que iniciam com o Windows**
```powershell
Get-CimInstance Win32_StartupCommand | Select-Object Name, Command, Location
```

**Impressoras instaladas**
```powershell
Get-Printer | Select-Object Name, DriverName, PortName, Shared
```

**Destravar a fila de impressão (admin)**
```powershell
Stop-Service Spooler -Force; Remove-Item C:\Windows\System32\spool\PRINTERS\* -Force; Start-Service Spooler
```

## Energia e reinicialização

**Reiniciar agora**
```powershell
Restart-Computer -Force
```

**Desligar agora**
```powershell
Stop-Computer -Force
```

**Reiniciar direto na BIOS/UEFI (admin)**
```powershell
shutdown /r /fw /t 0
```

**Reiniciar nas opções avançadas de inicialização (WinRE)**
```powershell
shutdown /r /o /t 0
```

**Agendar desligamento em 1 hora (cancelar: shutdown /a)**
```powershell
shutdown /s /t 3600
```

**Iniciar em modo de segurança no próximo boot (admin)**
> Para voltar ao normal: bcdedit /deletevalue {current} safeboot
```powershell
bcdedit /set {current} safeboot minimal
```

**Desativar a hibernação e liberar espaço (admin)**
```powershell
powercfg /h off
```

**Por que a máquina desligou/reiniciou (últimos eventos)**
```powershell
Get-WinEvent -FilterHashtable @{LogName='System'; Id=41,1074,6008} -MaxEvents 10 | Select-Object TimeCreated, Id, Message
```

## Atualizações e software

**Atualizar todos os programas com o winget**
```powershell
winget upgrade --all --accept-package-agreements --accept-source-agreements
```

**Procurar e instalar um programa pelo winget**
```powershell
winget search chrome; winget install --id Google.Chrome -e
```

**Exportar a lista de programas para reinstalar em outra máquina**
> Na máquina nova: winget import -i apps.json
```powershell
winget export -o "$env:USERPROFILE\Desktop\apps.json"
```

**Abrir o Windows Update**
```powershell
start ms-settings:windowsupdate
```

## Logs e diagnóstico

**Erros do sistema nas últimas 24 horas**
```powershell
Get-WinEvent -FilterHashtable @{LogName='System'; Level=2; StartTime=(Get-Date).AddDays(-1)} | Select-Object TimeCreated, ProviderName, Message
```

**Telas azuis (BSOD) registradas**
```powershell
Get-WinEvent -FilterHashtable @{LogName='System'; Id=1001} -MaxEvents 10 | Select-Object TimeCreated, Message
```

**Relatório completo do sistema em texto**
```powershell
systeminfo
```

## Acesso remoto

**Habilitar a Área de Trabalho Remota (admin)**
> O grupo do firewall é identificado pelo código interno, que funciona em qualquer idioma.
```powershell
Set-ItemProperty 'HKLM:\System\CurrentControlSet\Control\Terminal Server' -Name fDenyTSConnections -Value 0; Enable-NetFirewallRule -Group "@FirewallAPI.dll,-28752"
```

**Habilitar o PowerShell remoto (admin)**
```powershell
Enable-PSRemoting -Force
```

**Abrir sessão remota em outro PC**
```powershell
Enter-PSSession -ComputerName PC01
```

**Rodar um comando em vários PCs de uma vez**
```powershell
Invoke-Command -ComputerName PC01, PC02 -ScriptBlock { (Get-CimInstance Win32_BIOS).SerialNumber }
```

**Reiniciar um PC remoto**
```powershell
Restart-Computer -ComputerName PC01 -Force
```
