#Requires -Version 5.1
param(
    [Parameter(Mandatory)]
    [ValidateSet('7', '8', '10', '11')]
    [string]$WinVer,

    [Parameter(Mandatory)]
    [ValidateSet('x32', 'x64')]
    [string]$Arch,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

& "$PSScriptRoot\_publish.ps1" -WinVer $WinVer -Arch $Arch -Configuration $Configuration

$makensis = Find-MakeNsis
if (-not $makensis) {
    throw @"
makensis.exe не найден. Установите NSIS 3: https://nsis.sourceforge.io/Download
или задайте NSIS_HOME (папка с makensis.exe).
"@
}

$ver = Get-IskraVersion
$sourceDir = (Get-PublishDir $WinVer $Arch).TrimEnd('\')
$installerDir = Get-InstallerDir
New-Item -ItemType Directory -Force -Path $installerDir | Out-Null

$outFile = Join-Path $installerDir "Iskra-$($ver.Display)-win$WinVer-$Arch-setup.exe"
$nsi = Join-Path $PSScriptRoot 'installer.nsi'
$sourceFwd = ($sourceDir -replace '\\', '/')
$outFwd = ($outFile -replace '\\', '/')

$nsisArgs = @(
    "/DAPP_NAME=Iskra",
    "/DAPP_VERSION=$($ver.Display)",
    "/DAPP_PRODUCT_VERSION=$($ver.Product)",
    "/DWINVER=$WinVer",
    "/DARCH=$Arch",
    "/DEXE_NAME=Iskra.Maui.exe",
    "/DSOURCE_DIR=$sourceFwd",
    "/DOUT_FILE=$outFwd",
    $nsi
)

Write-Host "NSIS  Windows $WinVer $Arch  -> $outFile"
& $makensis @nsisArgs
if ($LASTEXITCODE -ne 0) {
    throw "makensis завершился с кодом $LASTEXITCODE"
}

Write-Host "OK  $outFile"
