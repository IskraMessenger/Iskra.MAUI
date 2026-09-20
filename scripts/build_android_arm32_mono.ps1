#!/usr/bin/env pwsh
# Build TorgLink Messenger for Android ARM32 with Mono runtime

param(
    [string]$Configuration = "Release",
    [switch]$Sign = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Building TorgLink for Android ARM32 (Mono)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Параметры
$ProjectPath = "src/TorgLink.Maui/TorgLink.Maui.csproj"
$OutputDir = "artifacts/android-arm32-mono"
$Framework = "net8.0-android"
$RuntimeId = "android-arm"

# Очистка предыдущей сборки
Write-Host "Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}

# Restore
Write-Host "Restoring dependencies..." -ForegroundColor Yellow
dotnet restore $ProjectPath `
    -p:ShortP2PBuildAndroid=true `
    -p:TargetFramework=$Framework

# Build
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build $ProjectPath `
    -f $Framework `
    -c $Configuration `
    -p:ShortP2PBuildAndroid=true `
    -p:RuntimeIdentifier=$RuntimeId `
    -p:AndroidSupportedAbis=armeabi-v7a `
    -p:UseMonoRuntime=true

# Publish
Write-Host "Publishing APK..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    -f $Framework `
    -c $Configuration `
    -r $RuntimeId `
    -o $OutputDir `
    -p:ShortP2PBuildAndroid=true `
    -p:AndroidSupportedAbis=armeabi-v7a `
    -p:UseMonoRuntime=true `
    -p:AndroidPackageFormat=apk `
    -p:RunAOTCompilation=false `
    -p:PublishTrimmed=false

# Подписание (если требуется)
if ($Sign) {
    Write-Host "Signing APK..." -ForegroundColor Yellow
    # Добавьте команды подписания здесь
}

# Результат
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Build completed successfully!" -ForegroundColor Green
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
    Write-Host "Architecture: ARM32 (armeabi-v7a)" -ForegroundColor Cyan
}
