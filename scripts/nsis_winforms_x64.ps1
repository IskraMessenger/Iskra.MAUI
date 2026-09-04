#Requires -Version 5.1
# Build Iskra.WinForms then pack an NSIS installer for Windows 7+ x64.
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
& "$PSScriptRoot\_nsis_winforms.ps1" -Arch 'x64' -Configuration $Configuration
exit $LASTEXITCODE
