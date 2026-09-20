#Requires -Version 5.1
# Build TorgLink then pack an NSIS installer for Windows 8 x32.
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
& "$PSScriptRoot\_nsis.ps1" -WinVer '8' -Arch 'x32' -Configuration $Configuration
exit $LASTEXITCODE
