#!/usr/bin/env pwsh
# EXPERIMENTAL: Build TorgLink for Android ARM32 with Mono runtime on .NET 10.0
# WARNING: This is unsupported and may not work!

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Red
Write-Host "EXPERIMENTAL BUILD" -ForegroundColor Red
Write-Host "ARM32 + Mono on .NET 10.0" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Red
Write-Host ""
Write-Host "WARNING: This is an experimental build!" -ForegroundColor Yellow
Write-Host "ARM32 is not officially supported in .NET 10.0" -ForegroundColor Yellow
Write-Host "This may fail or produce unstable results." -ForegroundColor Yellow
Write-Host ""

$ProjectPath = "src/TorgLink.Maui/TorgLink.Maui.csproj"
$OutputDir = "artifacts/android-arm32-net10-experimental"
$Framework = "net10.0-android"

# Проверка наличия Mono runtime пакетов
Write-Host "Checking for Mono runtime packages..." -ForegroundColor Cyan

# Очистка
Write-Host "Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}

# Попытка 1: Прямая сборка с указанием Mono и ARM32
Write-Host ""
Write-Host "Attempt 1: Direct build with Mono runtime..." -ForegroundColor Cyan
Write-Host ""

try {
    dotnet publish $ProjectPath `
        -f $Framework `
        -c $Configuration `
        -o $OutputDir `
        -p:ShortP2PBuildAndroid=true `
        -p:RuntimeIdentifier=android-arm `
        -p:AndroidSupportedAbis=armeabi-v7a `
        -p:UseMonoRuntime=true `
        -p:AndroidPackageFormat=apk `
        -p:RunAOTCompilation=false `
        -p:PublishTrimmed=false `
        -p:AndroidEnableMarshalMethods=false `
        -p:EnableLLVM=false `
        -p:_IsAndroidArm32Build=true `
        -v:detailed
    
    Write-Host ""
    Write-Host "Build succeeded!" -ForegroundColor Green
    
    # Проверка результата
    $ApkPath = Get-ChildItem -Path $OutputDir -Filter "*.apk" -Recurse | Select-Object -First 1
    if ($ApkPath) {
        $ApkSize = [math]::Round($ApkPath.Length / 1MB, 2)
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "APK Created!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "APK file: $($ApkPath.Name)" -ForegroundColor Cyan
        Write-Host "APK size: $ApkSize MB" -ForegroundColor Cyan
        Write-Host "Location: $OutputDir" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "IMPORTANT: Test thoroughly before deployment!" -ForegroundColor Yellow
    } else {
        Write-Host "APK file not found in output directory" -ForegroundColor Red
    }
}
catch {
    Write-Host ""
    Write-Host "Attempt 1 failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    
    # Попытка 2: С явным указанием Mono пакетов
    Write-Host "Attempt 2: Adding explicit Mono runtime packages..." -ForegroundColor Cyan
    Write-Host ""
    
    # Создаем временный файл с дополнительными пакетами
    $TempPropsFile = "obj/ExperimentalArm32.props"
    $TempPropsContent = @"
<Project>
  <ItemGroup>
    <!-- Попытка добавить Mono runtime для ARM32 -->
    <PackageReference Include="Microsoft.NETCore.App.Runtime.Mono.android-arm" Version="10.0.0" Condition="'`$(RuntimeIdentifier)' == 'android-arm'" />
    <PackageReference Include="Microsoft.Android.Runtime.android-arm" Version="10.0.0" Condition="'`$(RuntimeIdentifier)' == 'android-arm'" />
  </ItemGroup>
  
  <PropertyGroup Condition="'`$(RuntimeIdentifier)' == 'android-arm'">
    <UseMonoRuntime>true</UseMonoRuntime>
    <ForceMonoRuntime>true</ForceMonoRuntime>
    <_UseMonoRuntime>true</_UseMonoRuntime>
  </PropertyGroup>
</Project>
"@
    
    New-Item -Path (Split-Path $TempPropsFile -Parent) -ItemType Directory -Force | Out-Null
    Set-Content -Path $TempPropsFile -Value $TempPropsContent
    
    try {
        dotnet publish $ProjectPath `
            -f $Framework `
            -c $Configuration `
            -o $OutputDir `
            -p:ShortP2PBuildAndroid=true `
            -p:RuntimeIdentifier=android-arm `
            -p:AndroidSupportedAbis=armeabi-v7a `
            -p:CustomBeforeMicrosoftCommonTargets=$TempPropsFile `
            -p:AndroidPackageFormat=apk `
            -p:RunAOTCompilation=false `
            -p:PublishTrimmed=false `
            -v:detailed
        
        Write-Host ""
        Write-Host "Build succeeded with explicit packages!" -ForegroundColor Green
    }
    catch {
        Write-Host ""
        Write-Host "Attempt 2 also failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Red
        Write-Host "EXPERIMENTAL BUILD FAILED" -ForegroundColor Red
        Write-Host "========================================" -ForegroundColor Red
        Write-Host ""
        Write-Host "Possible reasons:" -ForegroundColor Yellow
        Write-Host "1. .NET 10.0 does not include ARM32 runtime packages" -ForegroundColor Yellow
        Write-Host "2. Mono runtime for ARM32 is not available in .NET 10.0" -ForegroundColor Yellow
        Write-Host "3. Android workload doesn't support ARM32 in .NET 10.0" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "RECOMMENDATION:" -ForegroundColor Cyan
        Write-Host "Use .NET 8.0 LTS which has official ARM32 support" -ForegroundColor Cyan
        Write-Host "Run: ./scripts/build_android_arm32_mono.ps1" -ForegroundColor Cyan
        Write-Host ""
        
        exit 1
    }
}

Write-Host ""
Write-Host "Note: If build succeeded, verify the APK contains:" -ForegroundColor Yellow
Write-Host "  - lib/armeabi-v7a/libmonosgen-2.0.so" -ForegroundColor Yellow
Write-Host "  - lib/armeabi-v7a/libmonodroid.so" -ForegroundColor Yellow
Write-Host ""
Write-Host "To inspect APK contents:" -ForegroundColor Cyan
Write-Host "  unzip -l `$ApkPath | grep 'lib/armeabi-v7a'" -ForegroundColor Cyan
