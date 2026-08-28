#Requires -Version 5.1
# Build Iskra then pack an NSIS installer for Windows 10 x32.
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
& "$PSScriptRoot\_nsis.ps1" -WinVer '10' -Arch 'x32' -Configuration $Configuration
exit $LASTEXITCODE
