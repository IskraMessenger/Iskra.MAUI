#Requires -Version 5.1
param(
    [Parameter(Mandatory)]
    [ValidateSet('7', '8', '10', '11')]
    [string]$WinVer,

    [Parameter(Mandatory)]
    [ValidateSet('x32', 'x64', 'arm64')]
    [string]$Arch,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

Write-LegacyWindowsWarning $WinVer

$repo = Get-RepoRoot
$proj = Join-Path $repo 'src\TorgLink.Maui\TorgLink.Maui.csproj'
$rid = Get-PublishRid $Arch
$platform = Get-MsbuildPlatform $Arch
$outDir = Get-PublishDir $WinVer $Arch
$ver = Get-TorgLinkVersion

Write-Host "TorgLink $($ver.Display)  Windows $WinVer $Arch  RID=$rid  -> $outDir"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$dotnetArgs = @(
    'publish', $proj,
    '-c', $Configuration,
    '-f', 'net10.0-windows10.0.19041.0',
    '-r', $rid,
    '--self-contained', 'true',
    '-p:WindowsPackageType=None',
    '-p:WindowsAppSDKSelfContained=true',
    '-p:WindowsAppSdkDeploymentManagerInitialize=false',
    '-p:PublishTrimmed=false',
    '-p:IncludeAndroid=false',
    "-p:Platform=$platform",
    '-o', $outDir
)

& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish завершился с кодом $LASTEXITCODE"
}

$exe = Join-Path $outDir 'TorgLink.Maui.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Не найден $exe после publish"
}

Write-Host "OK  $exe"
