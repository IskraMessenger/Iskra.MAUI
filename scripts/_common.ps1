#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    Split-Path -Parent $PSScriptRoot
}

function Get-IskraVersion {
    $csproj = Join-Path (Get-RepoRoot) 'src\Iskra.Maui\Iskra.Maui.csproj'
    $text = Get-Content -LiteralPath $csproj -Raw
    if ($text -notmatch '<ApplicationDisplayVersion>([^<]+)</ApplicationDisplayVersion>') {
        throw "ApplicationDisplayVersion not found in $csproj"
    }
    $display = $Matches[1].Trim()
    $rev = '0'
    if ($text -match '<ApplicationVersion>([^<]+)</ApplicationVersion>') {
        $rev = $Matches[1].Trim()
    }
    $parts = @($display.Split('.'))
    while ($parts.Count -lt 3) {
        $parts += '0'
    }
    $product = '{0}.{1}.{2}.{3}' -f $parts[0], $parts[1], $parts[2], $rev
    [pscustomobject]@{
        Display = $display
        Product = $product
        Revision = $rev
    }
}

function Get-PublishRid([string]$Arch) {
    if ($Arch -eq 'x32') { 'win-x86' } else { 'win-x64' }
}

function Get-MsbuildPlatform([string]$Arch) {
    if ($Arch -eq 'x32') { 'x86' } else { 'x64' }
}

function Get-PublishDir([string]$WinVer, [string]$Arch) {
    Join-Path (Get-RepoRoot) "dist\win$WinVer-$Arch"
}

function Get-InstallerDir {
    Join-Path (Get-RepoRoot) 'dist\installers'
}

function Write-LegacyWindowsWarning([string]$WinVer) {
    if ($WinVer -notin @('7', '8')) {
        return
    }
    Write-Warning @"
Iskra.Maui — WinUI 3 / Windows App SDK / net10.0-windows10.0.19041.0.
Официально нужен Windows 10 1809+ (сборка 17763). Папка dist\win$WinVer-* всё равно создаётся,
но на Windows $WinVer приложение, скорее всего, не запустится.
"@
}

function Find-MakeNsis {
    $fromPath = Get-Command makensis -ErrorAction SilentlyContinue
    $candidates = @(
        $(if ($fromPath) { $fromPath.Source }),
        (Join-Path ${env:ProgramFiles} 'NSIS\makensis.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe')
    )
    if ($env:NSIS_HOME) {
        $candidates += Join-Path $env:NSIS_HOME 'makensis.exe'
    }
    foreach ($path in $candidates) {
        if ($path -and (Test-Path -LiteralPath $path)) {
            return $path
        }
    }
    return $null
}
