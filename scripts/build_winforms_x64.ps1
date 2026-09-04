#Requires -Version 5.1
# Publish Iskra.WinForms for Windows 7+ x64 (.NET Framework 4.8).
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
& "$PSScriptRoot\_publish_winforms.ps1" -Arch 'x64' -Configuration $Configuration
exit $LASTEXITCODE
