# Legacy Support Branch - Implementation Diagram

## Architecture Overview

```mermaid
graph TB
    subgraph "Main Branch - .NET 10.0"
        M1[Modern Devices]
        M2[ARM64 Android]
        M3[x64/ARM64 Windows]
    end
    
    subgraph "Legacy Branch - .NET 8.0 + .NET Framework 4.8"
        L1[Legacy Devices]
        L2[ARM32 Android]
        L3[x86 Windows]
    end
    
    M1 --> M2
    M1 --> M3
    L1 --> L2
    L1 --> L3
    
    style M1 fill:#90EE90
    style L1 fill:#FFB6C1
```

## Branch Strategy

```mermaid
gitGraph
    commit id: "Initial"
    commit id: "NET 10.0 Development"
    branch legacy-support
    checkout legacy-support
    commit id: "Downgrade to NET 8.0"
    commit id: "Add ARM32 Support"
    commit id: "Update Packages"
    checkout main
    commit id: "Continue NET 10.0"
    checkout legacy-support
    commit id: "Backport Security Fix"
    checkout main
    commit id: "New Features"
```

## Platform Support Matrix

```mermaid
graph LR
    subgraph "Windows Support"
        W1[Windows 7+ x86] --> WF[.NET Framework 4.8]
        W2[Windows 10+ x64] --> W8[.NET 8.0]
        W3[Windows 10+ ARM64] --> W8
    end
    
    subgraph "Android Support"
        A1[Android 5.0+ ARM32] --> A8[.NET 8.0 MAUI]
        A2[Android 5.0+ ARM64] --> A8
    end
    
    style WF fill:#87CEEB
    style W8 fill:#87CEEB
    style A8 fill:#98FB98
```

## Build Pipeline Flow

```mermaid
flowchart TD
    Start[Start Build] --> CheckBranch{Branch?}
    
    CheckBranch -->|Main| Net10[.NET 10.0 Build]
    CheckBranch -->|Legacy| Net8[.NET 8.0 Build]
    
    Net10 --> Win10[Windows ARM64/x64]
    Net10 --> And10[Android ARM64]
    
    Net8 --> Win8[Windows x64/ARM64]
    Net8 --> And8[Android ARM32/ARM64]
    Net8 --> WinFx[Windows x86 .NET Fx 4.8]
    
    Win10 --> Test10[Test Modern]
    And10 --> Test10
    
    Win8 --> Test8[Test Legacy]
    And8 --> Test8
    WinFx --> Test8
    
    Test10 --> Deploy10[Deploy Modern]
    Test8 --> Deploy8[Deploy Legacy]
    
    style Net10 fill:#90EE90
    style Net8 fill:#FFB6C1
    style WinFx fill:#87CEEB
```

## Package Dependency Changes

```mermaid
graph TD
    subgraph "NET 10.0 Packages"
        P10A[Microsoft.Maui.Controls 10.0.30]
        P10B[Microsoft.Extensions 10.0.0]
        P10C[WindowsAppSDK 1.7.x]
    end
    
    subgraph "NET 8.0 Packages"
        P8A[Microsoft.Maui.Controls 8.0.100]
        P8B[Microsoft.Extensions 8.0.2]
        P8C[WindowsAppSDK 1.5.x]
    end
    
    P10A -.Downgrade.-> P8A
    P10B -.Downgrade.-> P8B
    P10C -.Downgrade.-> P8C
    
    style P10A fill:#90EE90
    style P10B fill:#90EE90
    style P10C fill:#90EE90
    style P8A fill:#FFB6C1
    style P8B fill:#FFB6C1
    style P8C fill:#FFB6C1
```

## Android ABI Support

```mermaid
graph LR
    subgraph "Main Branch"
        M[Android Build] --> MA64[ARM64 only]
    end
    
    subgraph "Legacy Branch"
        L[Android Build] --> LA32[ARM32 armeabi-v7a]
        L --> LA64[ARM64 arm64-v8a]
        L --> LX64[x86_64]
    end
    
    style MA64 fill:#90EE90
    style LA32 fill:#FFB6C1
    style LA64 fill:#FFB6C1
    style LX64 fill:#FFB6C1
```

## Implementation Phases

```mermaid
gantt
    title Legacy Support Implementation Timeline
    dateFormat YYYY-MM-DD
    section Phase 1
    Create Branch           :p1, 2026-09-11, 1d
    Update Build Props      :p2, after p1, 1d
    section Phase 2
    Update Projects         :p3, after p2, 2d
    Downgrade Packages      :p4, after p3, 2d
    section Phase 3
    Add ARM32 Config        :p5, after p4, 1d
    Update Build Scripts    :p6, after p5, 2d
    section Phase 4
    Testing                 :p7, after p6, 3d
    Documentation           :p8, after p6, 2d
    section Phase 5
    CI/CD Setup             :p9, after p7, 2d
    Final Validation        :p10, after p9, 1d
```

## File Changes Overview

```mermaid
graph TD
    subgraph "Configuration Files"
        C1[src/Directory.Build.props]
        C2[ShortP2P/Directory.Build.props]
    end
    
    subgraph "Project Files"
        P1[Iskra.Maui.csproj]
        P2[ShortP2P.*.csproj x20]
        P3[Iskra.Web.Api.csproj]
    end
    
    subgraph "Build Scripts"
        S1[build_android_arm32.ps1]
        S2[Existing scripts update]
    end
    
    subgraph "Documentation"
        D1[LEGACY_BRANCH.md]
        D2[README.md update]
    end
    
    C1 --> P1
    C2 --> P2
    C1 --> P3
    
    P1 --> S1
    P2 --> S2
    
    S1 --> D1
    S2 --> D2
    
    style C1 fill:#FFD700
    style C2 fill:#FFD700
    style P1 fill:#87CEEB
    style S1 fill:#98FB98
    style D1 fill:#DDA0DD
```

## Testing Strategy Flow

```mermaid
flowchart TD
    Start[Start Testing] --> Unit[Unit Tests]
    
    Unit --> IntWin[Integration: Windows x86]
    Unit --> IntAnd[Integration: Android ARM32]
    
    IntWin --> WinTest{Windows Tests Pass?}
    IntAnd --> AndTest{Android Tests Pass?}
    
    WinTest -->|No| FixWin[Fix Windows Issues]
    WinTest -->|Yes| E2EWin[E2E: Windows]
    
    AndTest -->|No| FixAnd[Fix Android Issues]
    AndTest -->|Yes| E2EAnd[E2E: Android]
    
    FixWin --> IntWin
    FixAnd --> IntAnd
    
    E2EWin --> Final{All Tests Pass?}
    E2EAnd --> Final
    
    Final -->|No| Debug[Debug Issues]
    Final -->|Yes| Deploy[Ready for Deployment]
    
    Debug --> Unit
    
    style Deploy fill:#90EE90
    style FixWin fill:#FFB6C1
    style FixAnd fill:#FFB6C1
```

## Maintenance Workflow

```mermaid
flowchart LR
    Main[Main Branch Development] --> Critical{Critical Fix?}
    
    Critical -->|Yes| Cherry[Cherry-pick to Legacy]
    Critical -->|No| Feature{Important Feature?}
    
    Feature -->|Yes| Evaluate[Evaluate Backport]
    Feature -->|No| Continue[Continue Main Dev]
    
    Cherry --> TestLegacy[Test on Legacy]
    Evaluate --> Backport{Backport?}
    
    Backport -->|Yes| Manual[Manual Port to Legacy]
    Backport -->|No| Continue
    
    Manual --> TestLegacy
    TestLegacy --> Merge[Merge to Legacy Branch]
    
    style Cherry fill:#FF6B6B
    style Manual fill:#FFD93D
    style Merge fill:#6BCB77
```
