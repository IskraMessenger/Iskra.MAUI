#Requires -Version 5.1
param(
    [Parameter(Mandatory)]
    [ValidateSet('x86', 'x32', 'x64')]
    [string]$Arch,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_common.ps1"

# x32 is an alias for x86; installer file stays Iskra-*-winforms-x86-setup.exe
if ($Arch -eq 'x32') { $Arch = 'x86' }

& "$PSScriptRoot\_publish_winforms.ps1" -Arch $Arch -Configuration $Configuration

$makensis = Find-MakeNsis
if (-not $makensis) {
    throw @"
makensis.exe не найден. Установите NSIS 3: https://nsis.sourceforge.io/Download
или задайте NSIS_HOME (папка с makensis.exe).
"@
}

$ver = Get-IskraVersion
$sourceDir = (Get-WinFormsPublishDir $Arch).TrimEnd('\')
$installerDir = Get-InstallerDir
New-Item -ItemType Directory -Force -Path $installerDir | Out-Null

$outFile = Join-Path $installerDir "Iskra-$($ver.Display)-winforms-$Arch-setup.exe"
$nsi = Join-Path $PSScriptRoot 'installer_winforms.nsi'
$sourceFwd = ($sourceDir -replace '\\', '/')
$outFwd = ($outFile -replace '\\', '/')

$nsisArgs = @(
    "/DAPP_VERSION=$($ver.Display)",
    "/DAPP_PRODUCT_VERSION=$($ver.Product)",
    "/DARCH=$Arch",
    "/DSOURCE_DIR=$sourceFwd",
    "/DOUT_FILE=$outFwd",
    $nsi
)

Write-Host "NSIS  WinForms $Arch  -> $outFile"
& $makensis @nsisArgs
if ($LASTEXITCODE -ne 0) {
    throw "makensis завершился с кодом $LASTEXITCODE"
}

Write-Host "OK  $outFile"
