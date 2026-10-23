[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$ExtensionId,

    [string]$InstallDirectory = (Split-Path -Parent $PSScriptRoot),

    [switch]$Unregister
)

$ErrorActionPreference = 'Stop'
$hostName = 'com.childscreentime.browser'
$manifestDirectory = Join-Path $env:LOCALAPPDATA 'ChildScreenTime\native-host'
$manifestPath = Join-Path $manifestDirectory "$hostName.json"
$registryPaths = @(
    "HKCU:\Software\Google\Chrome\NativeMessagingHosts\$hostName",
    "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\$hostName"
)

if ($Unregister) {
    foreach ($registryPath in $registryPaths) {
        Remove-Item -LiteralPath $registryPath -Recurse -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $manifestPath -Force -ErrorAction SilentlyContinue
    Write-Host 'Child Screen Time browser bridge registration removed.'
    exit 0
}

$normalizedIds = @()
foreach ($id in $ExtensionId) {
    $value = $id.Trim().ToLowerInvariant()
    if ($value -notmatch '^[a-p]{32}$') {
        throw "Invalid Chrome/Edge extension ID: $id"
    }
    $normalizedIds += $value
}

$hostExecutable = Join-Path $InstallDirectory 'ChildScreenTime.BrowserHost.exe'
if (-not (Test-Path -LiteralPath $hostExecutable)) {
    throw "Browser host executable not found: $hostExecutable"
}

New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null
$manifest = @{
    name = $hostName
    description = 'Child Screen Time YouTube activity bridge'
    path = $hostExecutable
    type = 'stdio'
    allowed_origins = @($normalizedIds | ForEach-Object { "chrome-extension://$_/" })
} | ConvertTo-Json -Depth 4

[System.IO.File]::WriteAllText(
    $manifestPath,
    $manifest,
    [System.Text.UTF8Encoding]::new($false))

foreach ($registryPath in $registryPaths) {
    New-Item -Path $registryPath -Force | Out-Null
    Set-Item -Path $registryPath -Value $manifestPath
}

Write-Host 'Browser bridge registered for Chrome and Edge.'
Write-Host 'Restart the browser after loading the Child Screen Time extension.'
