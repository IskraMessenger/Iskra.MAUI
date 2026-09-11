#!/usr/bin/env pwsh
# Deploy Iskra Messenger ARM32 APK to itel A25

param(
    [string]$ApkPath = "artifacts/android-arm32-net10-experimental/com.iskra.maui-Signed.apk",
    [string]$AdbPath = "C:\platform-tools\adb.exe"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deploying Iskra to itel A25" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Проверка наличия ADB
if (-not (Test-Path $AdbPath)) {
    Write-Host "ERROR: ADB not found at $AdbPath" -ForegroundColor Red
    Write-Host "Please install Android Platform Tools" -ForegroundColor Yellow
    exit 1
}

# Проверка наличия APK
if (-not (Test-Path $ApkPath)) {
    Write-Host "ERROR: APK not found at $ApkPath" -ForegroundColor Red
    Write-Host "Please build the APK first:" -ForegroundColor Yellow
    Write-Host "  ./scripts/build_android_arm32_net10_experimental.ps1" -ForegroundColor Yellow
    exit 1
}

$ApkSize = [math]::Round((Get-Item $ApkPath).Length / 1MB, 2)
Write-Host "APK file: $ApkPath" -ForegroundColor Green
Write-Host "APK size: $ApkSize MB" -ForegroundColor Green
Write-Host ""

# Проверка подключения устройства
Write-Host "Checking device connection..." -ForegroundColor Yellow
$devicesOutput = & $AdbPath devices
Write-Host $devicesOutput
Write-Host ""

# Проверяем наличие строки с "device" (не "devices attached")
$deviceLines = $devicesOutput | Where-Object { $_ -match "\tdevice$" }
if (-not $deviceLines) {
    Write-Host "ERROR: No device connected!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please:" -ForegroundColor Yellow
    Write-Host "1. Connect itel A25 via USB" -ForegroundColor Yellow
    Write-Host "2. Enable USB Debugging in Developer Options" -ForegroundColor Yellow
    Write-Host "3. Accept USB debugging prompt on device" -ForegroundColor Yellow
    exit 1
}

Write-Host "Device connected successfully!" -ForegroundColor Green
Write-Host ""

# Получение информации об устройстве
Write-Host "Getting device information..." -ForegroundColor Yellow
$deviceModel = & $AdbPath shell getprop ro.product.model
$androidVersion = & $AdbPath shell getprop ro.build.version.release
$sdkVersion = & $AdbPath shell getprop ro.build.version.sdk
$abi = & $AdbPath shell getprop ro.product.cpu.abi

Write-Host "Device Model: $deviceModel" -ForegroundColor Cyan
Write-Host "Android Version: $androidVersion (SDK $sdkVersion)" -ForegroundColor Cyan
Write-Host "CPU ABI: $abi" -ForegroundColor Cyan
Write-Host ""

# Проверка архитектуры
if ($abi -notmatch "armeabi-v7a") {
    Write-Host "WARNING: Device ABI is $abi" -ForegroundColor Yellow
    Write-Host "This APK is built for armeabi-v7a (ARM32)" -ForegroundColor Yellow
    Write-Host "It may not work correctly on this device!" -ForegroundColor Yellow
    Write-Host ""
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne "y") {
        exit 0
    }
}

# Удаление старой версии (если есть)
Write-Host "Checking for existing installation..." -ForegroundColor Yellow
$existing = & $AdbPath shell pm list packages | Select-String "com.iskra.maui"
if ($existing) {
    Write-Host "Found existing installation, uninstalling..." -ForegroundColor Yellow
    & $AdbPath uninstall com.iskra.maui
    Write-Host "Uninstalled successfully" -ForegroundColor Green
    Write-Host ""
}

# Установка APK
Write-Host "Installing APK..." -ForegroundColor Yellow
Write-Host "This may take a minute..." -ForegroundColor Gray
Write-Host ""

$installStart = Get-Date
& $AdbPath install -r $ApkPath
$installEnd = Get-Date
$installTime = ($installEnd - $installStart).TotalSeconds

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Installation Successful!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Installation time: $([math]::Round($installTime, 1)) seconds" -ForegroundColor Cyan
    Write-Host ""
    
    # Запуск приложения
    Write-Host "Launching Iskra Messenger..." -ForegroundColor Yellow
    & $AdbPath shell am start -n com.iskra.maui/crc64e1fb321c08285b90.MainActivity
    
    Write-Host ""
    Write-Host "App launched on device!" -ForegroundColor Green
    Write-Host ""
    Write-Host "To view logs:" -ForegroundColor Cyan
    Write-Host "  $AdbPath logcat | Select-String 'Iskra|Mono|FATAL'" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To view app info:" -ForegroundColor Cyan
    Write-Host "  $AdbPath shell dumpsys package com.iskra.maui" -ForegroundColor Gray
    Write-Host ""
    
    # Начать мониторинг логов
    $monitor = Read-Host "Start monitoring logs? (y/n)"
    if ($monitor -eq "y") {
        Write-Host ""
        Write-Host "Monitoring logs (Ctrl+C to stop)..." -ForegroundColor Yellow
        Write-Host "========================================" -ForegroundColor Gray
        & $AdbPath logcat | Select-String "Iskra|Mono|mono|FATAL|AndroidRuntime"
    }
    
} else {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "Installation Failed!" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Yellow
    Write-Host "1. Insufficient storage space" -ForegroundColor Yellow
    Write-Host "2. Incompatible Android version (need 5.0+)" -ForegroundColor Yellow
    Write-Host "3. ARM32 architecture mismatch" -ForegroundColor Yellow
    Write-Host "4. USB debugging not properly enabled" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Check device logs:" -ForegroundColor Cyan
    Write-Host "  $AdbPath logcat -d | Select-String 'PackageManager'" -ForegroundColor Gray
    exit 1
}
