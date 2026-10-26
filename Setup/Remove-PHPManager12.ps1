#Requires -RunAsAdministrator
<#
  Remove o "PHP Manager 1.2 for IIS 7" (instalado pelo Web Platform Installer) em maquinas
  sem o Windows PowerShell 2.0 (Windows 11 24H2+ / Windows Server 2025).

  A desinstalacao do 1.2 executa uma acao customizada que carrega o snap-in
  Web.Management.PHP.PowerShell 1.2 (.NET 2.0), que depende do System.Management.Automation 1.0.
  Sem o PowerShell 2.0 essa acao falha com "Error 1001" e a desinstalacao - e tambem a
  atualizacao para o PHP Manager 3.0 - e desfeita.

  O script desativa apenas essa acao no pacote em cache do 1.2 (com backup) e desinstala o produto.
  Depois rode o PHPManagerForIIS.msi novo normalmente.

  Uso (PowerShell como administrador):
    powershell.exe -ExecutionPolicy Bypass -File Remove-PHPManager12.ps1 [-MsiPath <msi>] [-NoUninstall]
#>
param(
    [string]$MsiPath,
    [switch]$NoUninstall
)

$ErrorActionPreference = 'Stop'
$productCode = '{E851486F-1FE2-44F0-85ED-F969088A68EE}'
$brokenActions = @(
    '_F6B35DCD_218D_4480_9CDB_B106A0B75627.uninstall.SetProperty',
    '_F6B35DCD_218D_4480_9CDB_B106A0B75627.uninstall'
)

if (-not $MsiPath) {
    $props = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\F684158E2EF10F4458DE9F9680A886EE\InstallProperties' -ErrorAction SilentlyContinue
    if (-not $props) {
        Write-Host 'PHP Manager 1.2 nao esta instalado. Nada a fazer.'
        return
    }
    $MsiPath = $props.LocalPackage
}

$backup = "$MsiPath.bak"
if (-not (Test-Path $backup)) {
    Copy-Item $MsiPath $backup
    Write-Host "Backup do pacote em cache: $backup"
}

$installer = New-Object -ComObject WindowsInstaller.Installer
$db = $installer.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $installer, @([string]$MsiPath, [int]1))
foreach ($action in $brokenActions) {
    $sql = "UPDATE ``InstallExecuteSequence`` SET ``Condition`` = '0' WHERE ``Action`` = '$action'"
    $view = $db.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $db, @([string]$sql))
    [void]$view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null)
    [void]$view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null)
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($view)
}
[void]$db.GetType().InvokeMember('Commit', 'InvokeMethod', $null, $db, $null)
# O pacote precisa estar fechado antes do msiexec; se continuar aberto, o Windows Installer
# ignora o cache ("could not be opened as a storage file") e usa o MSI original sem a correcao.
[void][Runtime.InteropServices.Marshal]::ReleaseComObject($db)
[void][Runtime.InteropServices.Marshal]::ReleaseComObject($installer)
$view = $null; $db = $null; $installer = $null
[GC]::Collect()
[GC]::WaitForPendingFinalizers()
try {
    [IO.File]::Open($MsiPath, 'Open', 'Read', 'None').Dispose()
} catch {
    throw "O pacote $MsiPath ainda esta em uso: $($_.Exception.Message)"
}
Write-Host "Acao do snap-in PowerShell 1.2 desativada em $MsiPath"

if ($NoUninstall) { return }

$log = Join-Path $env:TEMP 'PHPManager12-uninstall.log'
Write-Host "Desinstalando PHP Manager 1.2 (log: $log)..."
$p = Start-Process msiexec.exe -ArgumentList "/x $productCode /qb /l*v `"$log`"" -Wait -PassThru
if ($p.ExitCode -in 0, 1605, 3010) {
    Write-Host "Concluido (codigo $($p.ExitCode)). Agora instale o PHPManagerForIIS.msi."
} else {
    Write-Host "msiexec terminou com codigo $($p.ExitCode). Veja o log: $log"
    exit $p.ExitCode
}
