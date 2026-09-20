# TorgLink.Maui

Клиент **TorgLink** на .NET MAUI. Логика P2P — из субмодуля `ShortP2P` (не изменяется).

- Windows: `net10.0-windows10.0.19041.0` (цель по умолчанию)
- Android: `net10.0-android`, min SDK **21** (Android 5.0)

Запуск на Windows из корня репозитория:

```
dotnet run --project src/TorgLink.Maui/TorgLink.Maui.csproj
```

В Cursor / VS Code: **TorgLink (Windows)** (Debug) или **TorgLink MAUI (Windows) - release**. В Rider: те же имена в Run Configurations / профиль **Windows Machine**, платформа **x64**.

Сборка:

```
dotnet build TorgLink.Maui.sln
dotnet build src/TorgLink.Maui/TorgLink.Maui.csproj -c Release -f net10.0-windows10.0.19041.0 -p:RuntimeIdentifier=win-x64
```

Android: `dotnet build -p:IncludeAndroid=true`
