#Requires -Version 5.1
# Publish TorgLink.WinForms for Windows 7+ 32-bit (x86 / x32 alias).
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
& "$PSScriptRoot\_publish_winforms.ps1" -Arch 'x32' -Configuration $Configuration
exit $LASTEXITCODE
