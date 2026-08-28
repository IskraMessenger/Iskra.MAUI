#Requires -Version 5.1
# Publish Iskra for Windows 10 x64 (self-contained unpackaged MAUI).
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
& "$PSScriptRoot\_publish.ps1" -WinVer '10' -Arch 'x64' -Configuration $Configuration
exit $LASTEXITCODE
