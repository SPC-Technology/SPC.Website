$ErrorActionPreference = "Stop"

$serviceName = "SPCWebsite"
$publishPath = "C:\SPC\spc-website-2026\publish"
$exePath = Join-Path $publishPath "SPC.Website.exe"

if (-not (Test-Path $exePath)) {
    throw "Executable not found at $exePath. Publish the app for win-x64 first."
}

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Write-Host "Service $serviceName already exists."
    exit 0
}

New-Service `
    -Name $serviceName `
    -BinaryPathName "`"$exePath`"" `
    -DisplayName "SPC Technology Website" `
    -Description "Phoebus ERP marketing website hosted as a Windows Service." `
    -StartupType Automatic

Start-Service -Name $serviceName
Write-Host "Service $serviceName created and started."
