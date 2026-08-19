param(
  [string]$InstallDir = "$env:LOCALAPPDATA\UniFlowIT\Agent"
)

$ErrorActionPreference = "Stop"
$source = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $source "publish"
$exe = Join-Path $publishDir "UniFlowIT.Agent.exe"

if (!(Test-Path $exe)) {
  dotnet publish (Join-Path $source "UniFlowIT.Agent.csproj") -c Release -r win-x64 --self-contained false -o $publishDir
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Force -Recurse (Join-Path $publishDir "*") $InstallDir

$installedExe = Join-Path $InstallDir "UniFlowIT.Agent.exe"
$uninstallScript = Join-Path $InstallDir "uninstall-agent.ps1"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\UniFlowIT Agent"

@"
`$ErrorActionPreference = "SilentlyContinue"
`$installDir = "$InstallDir"

Get-Process | Where-Object { `$_.ProcessName -eq "UniFlowIT.Agent" } | Stop-Process -Force
Get-Process | Where-Object { `$_.ProcessName -eq "rustdesk" } | Stop-Process -Force
Get-Service | Where-Object { `$_.Name -like "*RustDesk*" -or `$_.DisplayName -like "*RustDesk*" } | Stop-Service -Force
$winget = Get-Command winget.exe -ErrorAction SilentlyContinue
if (`$winget) { & `$winget.Source uninstall --id RustDesk.RustDesk -e --silent --disable-interactivity --accept-source-agreements | Out-Null }
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "UniFlowIT Agent" -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "HKCU:\Software\Classes\uniflowit-agent" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\UniFlowIT Agent" -Recurse -Force -ErrorAction SilentlyContinue
Set-Location `$env:TEMP
Remove-Item -LiteralPath `$installDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "UniFlowIT Agent desinstalado."
"@ | Set-Content -LiteralPath $uninstallScript -Encoding UTF8

New-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "UniFlowIT Agent" -Value "`"$installedExe`""

New-Item -Path "HKCU:\Software\Classes\uniflowit-agent" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\uniflowit-agent" -Name "(default)" -Value "URL:UniFlowIT Agent"
Set-ItemProperty -Path "HKCU:\Software\Classes\uniflowit-agent" -Name "URL Protocol" -Value ""
New-Item -Path "HKCU:\Software\Classes\uniflowit-agent\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\uniflowit-agent\shell\open\command" -Name "(default)" -Value "`"$installedExe`" `"%1`""

New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "UniFlowIT Agent"
Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "1.0.9"
Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "UniFlowIT"
Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value $installedExe
Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$uninstallScript`""
Set-ItemProperty -Path $uninstallKey -Name "QuietUninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$uninstallScript`""
Set-ItemProperty -Path $uninstallKey -Name "NoModify" -Type DWord -Value 1
Set-ItemProperty -Path $uninstallKey -Name "NoRepair" -Type DWord -Value 1

Start-Process -FilePath $installedExe -WindowStyle Hidden
Write-Host "UniFlowIT Agent instalado em $InstallDir"
