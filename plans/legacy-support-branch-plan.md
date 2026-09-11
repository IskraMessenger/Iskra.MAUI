# Legacy Support Branch Plan

## Overview

This document outlines the plan to create a `legacy-support` branch for the Iskra Messenger project to support older x86 and ARM32 processors that are not compatible with .NET 10.0.

## Current State Analysis

### Current Architecture
- **Main Framework**: .NET 10.0 (net10.0)
- **MAUI Projects**: net10.0-windows10.0.19041.0, net10.0-android
- **Legacy Windows**: .NET Framework 4.8 (net48) for x86/x64 via WinForms
- **Platforms Supported**:
  - Windows: x64, ARM64 (via MAUI), x86/x64 (via WinForms net48)
  - Android: ARM64 only (net10.0-android)

### Identified Issues
1. **.NET 10.0 Requirements**: Requires modern processors with specific instruction sets
2. **Android ARM32 Missing**: No support for older 32-bit ARM Android devices (armeabi-v7a)
3. **Package Compatibility**: Many packages at version 10.x may not support older frameworks

## Target State

### Legacy Branch Goals
- **Branch Name**: `legacy-support`
- **Target Framework**: .NET 8.0 LTS (supported until November 2026)
- **Windows Legacy**: Keep existing .NET Framework 4.8 for x86/x64
- **Android Legacy**: Add ARM32 (armeabi-v7a) support via net8.0-android

### Supported Platforms (Legacy Branch)
- **Windows**:
  - x86 (32-bit) via .NET Framework 4.8 WinForms
  - x64 (64-bit) via .NET Framework 4.8 WinForms or net8.0-windows MAUI
  - ARM64 via net8.0-windows MAUI
- **Android**:
  - ARM32 (armeabi-v7a) via net8.0-android MAUI
  - ARM64 (arm64-v8a) via net8.0-android MAUI

## Implementation Plan

### Phase 1: Branch Creation and Framework Downgrade

#### 1.1 Create Git Branch
```bash
git checkout -b legacy-support
```

#### 1.2 Update Global Build Properties

**File**: [`src/Directory.Build.props`](src/Directory.Build.props:3)
```xml
<NetCoreTargetFramework>net8.0</NetCoreTargetFramework>
<MicrosoftExtensionsVersion>8.0.2</MicrosoftExtensionsVersion>
```

**File**: [`ShortP2P/Directory.Build.props`](ShortP2P/Directory.Build.props:9)
```xml
<NetCoreTargetFramework>net8.0</NetCoreTargetFramework>
<WindowsDesktopTargetFramework>net8.0-windows10.0.17763.0</WindowsDesktopTargetFramework>
<MicrosoftExtensionsVersion>8.0.2</MicrosoftExtensionsVersion>
<MicrosoftEntityFrameworkCoreVersion>8.0.11</MicrosoftEntityFrameworkCoreVersion>
```

### Phase 2: Update Project Files

#### 2.1 Update Iskra.Maui Project

**File**: [`src/Iskra.Maui/Iskra.Maui.csproj`](src/Iskra.Maui/Iskra.Maui.csproj:6)

Changes needed:
- Target frameworks: `net8.0-windows10.0.19041.0` and `net8.0-android`
- Add ARM32 Android support via `RuntimeIdentifiers`
- Downgrade Microsoft.Maui.Controls to 8.x version
- Downgrade Microsoft.WindowsAppSDK to compatible version

```xml
<TargetFrameworks>net8.0-windows10.0.19041.0</TargetFrameworks>
<TargetFrameworks Condition="'$(ShortP2PBuildAndroid)' == 'true'">$(TargetFrameworks);net8.0-android</TargetFrameworks>

<!-- Add ARM32 support for Android -->
<PropertyGroup Condition="$(TargetFramework.Contains('-android'))">
    <RuntimeIdentifiers>android-arm;android-arm64;android-x64</RuntimeIdentifiers>
    <AndroidSupportedAbis>armeabi-v7a;arm64-v8a;x86_64</AndroidSupportedAbis>
</PropertyGroup>
```

#### 2.2 Update ShortP2P Library Projects

All projects using `$(NetCoreTargetFramework)` will automatically use net8.0 after updating Directory.Build.props:
- [`ShortP2P/ShortP2P.Crypto/ShortP2P.Crypto.csproj`](ShortP2P/ShortP2P.Crypto/ShortP2P.Crypto.csproj:4)
- [`ShortP2P/src/ShortP2P.Client/ShortP2P.Client.csproj`](ShortP2P/src/ShortP2P.Client/ShortP2P.Client.csproj:4)
- [`ShortP2P/src/ShortP2P.Auth/ShortP2P.Auth.csproj`](ShortP2P/src/ShortP2P.Auth/ShortP2P.Auth.csproj:4)
- [`ShortP2P/src/ShortP2P.Transport/ShortP2P.Transport.csproj`](ShortP2P/src/ShortP2P.Transport/ShortP2P.Transport.csproj:16)
- [`ShortP2P/src/ShortP2P.Messenger/ShortP2P.Messenger.csproj`](ShortP2P/src/ShortP2P.Messenger/ShortP2P.Messenger.csproj:13)
- [`ShortP2P/src/ShortP2P.Discovery/ShortP2P.Discovery.csproj`](ShortP2P/src/ShortP2P.Discovery/ShortP2P.Discovery.csproj:36)
- [`ShortP2P/src/ShortP2P.TrustSystem/ShortP2P.TrustSystem.csproj`](ShortP2P/src/ShortP2P.TrustSystem/ShortP2P.TrustSystem.csproj:4)

#### 2.3 Update Platform-Specific Projects

**Android Bluetooth**: [`ShortP2P/src/ShortP2P.Transport.Bluetooth.Android/ShortP2P.Transport.Bluetooth.Android.csproj`](ShortP2P/src/ShortP2P.Transport.Bluetooth.Android/ShortP2P.Transport.Bluetooth.Android.csproj:8)
```xml
<TargetFramework>net8.0-android</TargetFramework>
```

**Windows Bluetooth**: [`ShortP2P/src/ShortP2P.Transport.Bluetooth.Windows/ShortP2P.Transport.Bluetooth.Windows.csproj`](ShortP2P/src/ShortP2P.Transport.Bluetooth.Windows/ShortP2P.Transport.Bluetooth.Windows.csproj:5)
```xml
<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
```

**Web API**: [`src/Iskra.Web.Api/Iskra.Web.Api.csproj`](src/Iskra.Web.Api/Iskra.Web.Api.csproj:4)
- Already uses `$(NetCoreTargetFramework)`, will automatically use net8.0

**WinForms**: [`src/Iskra.WinForms/Iskra.WinForms.csproj`](src/Iskra.WinForms/Iskra.WinForms.csproj:4)
- Already targets net48, no changes needed

### Phase 3: NuGet Package Version Updates

#### 3.1 Core MAUI Packages
```xml
<!-- Downgrade from 10.0.30 to latest 8.x -->
<PackageReference Include="Microsoft.Maui.Controls" Version="8.0.100" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="8.0.2" />
```

#### 3.2 Windows App SDK
```xml
<!-- Downgrade from 1.7.x to 1.5.x for net8.0 compatibility -->
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.5.240802000" />
```

#### 3.3 Other Package Updates
Most packages are already compatible:
- NLog 5.3.4 ✓
- SQLitePCLRaw 2.1.13 ✓
- LiteDB 5.0.16 ✓
- Concentus 2.2.2 ✓

Packages needing version check:
- System.IO.Ports: 10.0.0 → 8.0.0
- Polly: 8.6.6 (check compatibility)
- SixLabors.ImageSharp: 3.1.11 → check for net8.0 support

### Phase 4: Build Script Updates

#### 4.1 Add ARM32 Android Build Scripts

Create new scripts for ARM32 Android builds:
- `scripts/build_android_arm32.ps1`
- `scripts/build_android_arm32.sh`

Example PowerShell script:
```powershell
# Build Android ARM32 (armeabi-v7a)
$ErrorActionPreference = 'Stop'

dotnet publish src/Iskra.Maui/Iskra.Maui.csproj `
    -f net8.0-android `
    -c Release `
    -p:AndroidSupportedAbis=armeabi-v7a `
    -p:IncludeAndroid=true `
    -p:AndroidPackageFormat=apk `
    -o artifacts/apk/arm32
```

#### 4.2 Update Existing Scripts

Update references from net10.0 to net8.0 in:
- [`scripts/_publish.ps1`](scripts/_publish.ps1:23)
- [`scripts/_common.ps1`](scripts/_common.ps1:41)
- All build_*.ps1 and build_*.sh scripts

### Phase 5: Android ARM32 Configuration

#### 5.1 Update Iskra.Maui Android Settings

Add to [`src/Iskra.Maui/Iskra.Maui.csproj`](src/Iskra.Maui/Iskra.Maui.csproj:65):

```xml
<PropertyGroup Condition="$(TargetFramework.Contains('-android'))">
    <!-- Support ARM32, ARM64, and x86_64 -->
    <RuntimeIdentifiers>android-arm;android-arm64;android-x64</RuntimeIdentifiers>
    <AndroidSupportedAbis>armeabi-v7a;arm64-v8a;x86_64</AndroidSupportedAbis>
    
    <!-- Minimum SDK 21 (Android 5.0) for ARM32 compatibility -->
    <AndroidMinSdkVersion>21</AndroidMinSdkVersion>
    <SupportedOSPlatformVersion>21.0</SupportedOSPlatformVersion>
    
    <!-- Target SDK 34 (Android 14) for broad compatibility -->
    <TargetPlatformVersion>34.0</TargetPlatformVersion>
    
    <!-- Disable AOT for ARM32 compatibility -->
    <RunAOTCompilation>false</RunAOTCompilation>
    <AndroidEnableProfiledAot>false</AndroidEnableProfiledAot>
    <EnableLLVM>false</EnableLLVM>
    <PublishTrimmed>false</PublishTrimmed>
    <AndroidEnableMarshalMethods>false</AndroidEnableMarshalMethods>
</PropertyGroup>
```

#### 5.2 SQLite Native Libraries for ARM32

Ensure proper SQLite native library inclusion:
```xml
<PackageReference Include="SQLitePCLRaw.lib.e_sqlite3.android" Version="2.1.13" 
                  Condition="$(TargetFramework.Contains('-android'))" />
```

This package includes native libraries for all Android ABIs including armeabi-v7a.

### Phase 6: Documentation

#### 6.1 Create Legacy Branch README

Create `LEGACY_BRANCH.md` in repository root:
```markdown
# Legacy Support Branch

This branch provides support for older processors:
- Windows x86 (32-bit) via .NET Framework 4.8
- Android ARM32 (armeabi-v7a) via .NET 8.0

## Building

### Windows x86
dotnet build src/Iskra.WinForms/Iskra.WinForms.csproj -p:Platform=x86

### Android ARM32
dotnet publish src/Iskra.Maui/Iskra.Maui.csproj -f net8.0-android -p:AndroidSupportedAbis=armeabi-v7a

## Maintenance

This branch is maintained separately from main development.
Critical security fixes and major features may be backported.
```

#### 6.2 Update Main README

Add section about legacy support:
```markdown
## Legacy Device Support

For older devices with x86 or ARM32 processors, use the `legacy-support` branch:
- .NET 8.0 for Android ARM32
- .NET Framework 4.8 for Windows x86

See [LEGACY_BRANCH.md](LEGACY_BRANCH.md) for details.
```

### Phase 7: CI/CD Configuration

#### 7.1 GitHub Actions Workflow

Create `.github/workflows/legacy-build.yml`:
```yaml
name: Legacy Support Build

on:
  push:
    branches: [ legacy-support ]
  pull_request:
    branches: [ legacy-support ]

jobs:
  build-windows-x86:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: recursive
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Build WinForms x86
        run: dotnet build src/Iskra.WinForms/Iskra.WinForms.csproj -p:Platform=x86

  build-android-arm32:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: recursive
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Setup Android SDK
        uses: android-actions/setup-android@v3
      - name: Build Android ARM32
        run: |
          dotnet workload install maui-android
          dotnet publish src/Iskra.Maui/Iskra.Maui.csproj \
            -f net8.0-android \
            -c Release \
            -p:AndroidSupportedAbis=armeabi-v7a \
            -p:IncludeAndroid=true
```

## Compatibility Matrix

| Platform | Architecture | Framework | Min OS Version | Status |
|----------|-------------|-----------|----------------|--------|
| Windows | x86 (32-bit) | .NET Framework 4.8 | Windows 7 SP1 | ✓ Existing |
| Windows | x64 (64-bit) | .NET Framework 4.8 | Windows 7 SP1 | ✓ Existing |
| Windows | x64 (64-bit) | .NET 8.0 | Windows 10 1809+ | ✓ New |
| Windows | ARM64 | .NET 8.0 | Windows 10 1809+ | ✓ New |
| Android | ARM32 (armeabi-v7a) | .NET 8.0 | Android 5.0+ | ✓ New |
| Android | ARM64 (arm64-v8a) | .NET 8.0 | Android 5.0+ | ✓ New |

## Package Version Summary

### Critical Downgrades
- `Microsoft.Maui.Controls`: 10.0.30 → 8.0.100
- `Microsoft.WindowsAppSDK`: 1.7.250909003 → 1.5.240802000
- `Microsoft.Extensions.*`: 10.0.0 → 8.0.2
- `Microsoft.EntityFrameworkCore.*`: 10.0.0 → 8.0.11
- `System.IO.Ports`: 10.0.0 → 8.0.0

### Packages Remaining Same
- NLog: 5.3.4
- SQLitePCLRaw: 2.1.13
- LiteDB: 5.0.16
- Concentus: 2.2.2
- QRCoder: 1.6.0

## Testing Strategy

### Test Scenarios
1. **Windows x86 Build**: Verify WinForms app builds and runs on Windows 7 x86
2. **Android ARM32 Build**: Verify APK builds with armeabi-v7a ABI
3. **Android ARM32 Runtime**: Test on physical ARM32 device or emulator
4. **Feature Parity**: Ensure core features work on legacy platforms
5. **Database Compatibility**: Verify SQLite works on all platforms

### Test Devices
- Windows 7 x86 VM or physical machine
- Android 5.0+ ARM32 device (e.g., older Samsung, Xiaomi devices)
- Android emulator with armeabi-v7a system image

## Maintenance Strategy

### Branch Management
- **Main Branch**: Continue with .NET 10.0 for modern devices
- **Legacy Branch**: Maintain .NET 8.0 for older devices
- **Merge Strategy**: Cherry-pick critical fixes from main to legacy

### Update Policy
1. **Security Fixes**: Always backport to legacy branch
2. **Critical Bugs**: Backport if affects legacy platforms
3. **New Features**: Evaluate case-by-case for backporting
4. **Dependencies**: Keep at .NET 8.0 compatible versions

### Support Timeline
- **.NET 8.0 LTS**: Supported until November 2026
- **.NET Framework 4.8**: Supported as long as Windows 7 ESU exists
- **Legacy Branch**: Maintain until .NET 8.0 EOL (November 2026)

## Migration Path

### For Users
1. Modern devices (ARM64, x64 with modern CPUs): Use main branch builds
2. Older devices (ARM32 Android, x86 Windows): Use legacy-support branch builds
3. Upgrade path: When upgrading device, switch to main branch builds

### For Developers
1. New features: Develop on main branch (.NET 10.0)
2. Legacy support: Backport to legacy-support branch if needed
3. Testing: Test on both branches for critical changes

## Risks and Mitigations

### Risk 1: Package Incompatibility
**Risk**: Some NuGet packages may not support .NET 8.0
**Mitigation**: Test all packages, find alternatives if needed, maintain compatibility matrix

### Risk 2: MAUI 8.0 Limitations
**Risk**: MAUI 8.0 may have fewer features than MAUI 10.0
**Mitigation**: Document feature differences, provide workarounds where possible

### Risk 3: Maintenance Burden
**Risk**: Maintaining two branches increases workload
**Mitigation**: Automate testing, selective backporting, clear support policy

### Risk 4: ARM32 Performance
**Risk**: ARM32 devices may have performance issues
**Mitigation**: Disable AOT, optimize for interpreter, test on real devices

## Success Criteria

- [ ] Legacy branch builds successfully for all target platforms
- [ ] Windows x86 WinForms app runs on Windows 7
- [ ] Android ARM32 APK installs and runs on ARM32 device
- [ ] Core messaging features work on all legacy platforms
- [ ] Database operations work correctly on all platforms
- [ ] Build scripts support all legacy architectures
- [ ] CI/CD pipeline builds and tests legacy branch
- [ ] Documentation is complete and accurate

## Next Steps

1. Create `legacy-support` branch
2. Update Directory.Build.props files
3. Update all project files with new target frameworks
4. Downgrade NuGet packages
5. Add ARM32 Android configuration
6. Update build scripts
7. Test builds on all platforms
8. Create documentation
9. Set up CI/CD pipeline
10. Announce legacy branch availability

## References

- [.NET 8.0 Release Notes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [.NET MAUI 8.0 Documentation](https://learn.microsoft.com/en-us/dotnet/maui/)
- [Android ABIs](https://developer.android.com/ndk/guides/abis)
- [.NET Framework 4.8 Support](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-framework)
