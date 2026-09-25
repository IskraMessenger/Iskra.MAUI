#Requires -Version 5.1
# Publish TorgLink.WinForms for Windows 7+ x86 (.NET Framework 4.7.2).
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
& "$PSScriptRoot\_publish_winforms.ps1" -Arch 'x86' -Configuration $Configuration
exit $LASTEXITCODE
