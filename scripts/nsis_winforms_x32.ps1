#Requires -Version 5.1
# Build Iskra.WinForms then pack an NSIS installer for Windows 7+ 32-bit (x86).
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
& "$PSScriptRoot\_nsis_winforms.ps1" -Arch 'x32' -Configuration $Configuration
exit $LASTEXITCODE
