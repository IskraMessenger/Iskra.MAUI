# Iskra.Maui

Клиент **Iskra** на .NET MAUI. Логика P2P — из субмодуля `ShortP2P` (не изменяется).

- Windows: `net10.0-windows10.0.19041.0` (цель по умолчанию)
- Android: `net10.0-android`, min SDK **21** (Android 5.0)

Запуск на Windows из корня репозитория:

```
dotnet run --project src/Iskra.Maui/Iskra.Maui.csproj
```

В Cursor / VS Code: конфигурация **Iskra (Windows)**. В Rider / Visual Studio: профиль **Windows Machine**, платформа **x64**.

Сборка:

```
dotnet build Iskra.Maui.sln
```

Android: `dotnet build -p:IncludeAndroid=true`
