#Requires -Version 5.1
# Build Iskra then pack an NSIS installer for Windows 10 ARM64.
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
& "$PSScriptRoot\_nsis.ps1" -WinVer '10' -Arch 'arm64' -Configuration $Configuration
exit $LASTEXITCODE
