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

# x32 is an alias for x86 (same as MAUI nsis_win_*_x32.ps1).
if ($Arch -eq 'x32') { $Arch = 'x86' }

$repo = Get-RepoRoot
$proj = Join-Path $repo 'src\TorgLink.WinForms\TorgLink.WinForms.csproj'
$rid = if ($Arch -eq 'x86') { 'win-x86' } else { 'win-x64' }
$outDir = Get-WinFormsPublishDir $Arch
$ver = Get-TorgLinkVersion

Write-Host "TorgLink.WinForms $($ver.Display)  $Arch  RID=$rid  -> $outDir"

if (Test-Path -LiteralPath $outDir) {
    Remove-Item -LiteralPath $outDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Use --property (not -p:Platform=x86): Windows PowerShell splits -p:Platform=x86
# into an extra MSBuild "project" argument and fails with MSB1008.
$dotnetArgs = @(
    'publish', $proj,
    '-c', $Configuration,
    '-r', $rid,
    "--property:Platform=$Arch",
    "--property:PlatformTarget=$Arch",
    '--property:DebugType=none',
    '--property:DebugSymbols=false',
    '--self-contained', 'false',
    '-o', $outDir
)

& dotnet.exe @dotnetArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish завершился с кодом $LASTEXITCODE"
}

$exe = Join-Path $outDir 'TorgLink.WinForms.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Не найден $exe после publish"
}

Write-Host "OK  $exe"
