#!/usr/bin/env pwsh
# Build universal APK with both ARM32 and ARM64 using Mono

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building Universal Android APK (Mono)" -ForegroundColor Cyan
Write-Host "ARM32 + ARM64" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ProjectPath = "src/Iskra.Maui/Iskra.Maui.csproj"
$OutputDir = "artifacts/android-universal-mono"
$Framework = "net8.0-android"

# Очистка
Write-Host "Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}

# Сборка с обеими архитектурами
Write-Host "Building for ARM32 + ARM64..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    -f $Framework `
    -c $Configuration `
    -o $OutputDir `
    -p:ShortP2PBuildAndroid=true `
    -p:RuntimeIdentifiers="android-arm;android-arm64" `
    -p:AndroidSupportedAbis="armeabi-v7a;arm64-v8a" `
    -p:UseMonoRuntime=true `
    -p:AndroidPackageFormat=apk `
    -p:RunAOTCompilation=false `
    -p:PublishTrimmed=false

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Universal APK created!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "APK location: $OutputDir" -ForegroundColor Green
Write-Host ""

# Информация о сборке
$ApkPath = Get-ChildItem -Path $OutputDir -Filter "*.apk" -Recurse | Select-Object -First 1
if ($ApkPath) {
    $ApkSize = [math]::Round($ApkPath.Length / 1MB, 2)
    Write-Host "APK file: $($ApkPath.Name)" -ForegroundColor Cyan
    Write-Host "APK size: $ApkSize MB" -ForegroundColor Cyan
    Write-Host "Runtime: Mono" -ForegroundColor Cyan
    Write-Host "Architectures: ARM32 (armeabi-v7a) + ARM64 (arm64-v8a)" -ForegroundColor Cyan
}
