[CmdletBinding()]
param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts 'publish'
$installerOutput = Join-Path $artifacts 'installer'
$appProject = Join-Path $root 'src\ChildScreenTime.Windows\ChildScreenTime.Windows.csproj'
$serviceProject = Join-Path $root 'src\ChildScreenTime.Service\ChildScreenTime.Service.csproj'
$servicePublish = Join-Path $artifacts 'service-publish'
$testsProject = Join-Path $root 'tests\ChildScreenTime.Core.Tests\ChildScreenTime.Core.Tests.csproj'
$windowsTestsProject = Join-Path $root 'tests\ChildScreenTime.Windows.Tests\ChildScreenTime.Windows.Tests.csproj'

dotnet restore $appProject --runtime win-x64 --configfile (Join-Path $root 'NuGet.config')
if ($LASTEXITCODE -ne 0) { throw 'Application restore failed.' }
dotnet restore $serviceProject --runtime win-x64 --configfile (Join-Path $root 'NuGet.config')
if ($LASTEXITCODE -ne 0) { throw 'Service restore failed.' }
dotnet restore $testsProject --configfile (Join-Path $root 'NuGet.config')
if ($LASTEXITCODE -ne 0) { throw 'Test restore failed.' }
dotnet restore $windowsTestsProject --configfile (Join-Path $root 'NuGet.config')
if ($LASTEXITCODE -ne 0) { throw 'Windows test restore failed.' }
dotnet build $appProject --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
dotnet build $serviceProject --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Service build failed.' }
dotnet test $testsProject --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
dotnet test $windowsTestsProject --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Windows tests failed.' }
dotnet publish $appProject --configuration Release --runtime win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false --output $publish
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
dotnet publish $serviceProject --configuration Release --runtime win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false --output $servicePublish
if ($LASTEXITCODE -ne 0) { throw 'Service publish failed.' }
Copy-Item -LiteralPath (Join-Path $servicePublish 'ChildScreenTime.Service.exe') -Destination $publish -Force

if (-not $SkipInstaller) {
    $isccCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    $iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $iscc -or -not (Test-Path -LiteralPath $iscc)) {
        throw 'Inno Setup 6 is required to create the installer. Install it or rerun with -SkipInstaller.'
    }

    New-Item -ItemType Directory -Path $installerOutput -Force | Out-Null
    & $iscc (Join-Path $root 'installer\ChildScreenTime.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
}

Write-Host "Verified application output: $publish"
if (-not $SkipInstaller) {
    Write-Host "Installer output: $installerOutput"
}
