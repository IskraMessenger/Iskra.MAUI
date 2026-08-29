# Iskra.WinForms (черновик, .NET Framework 4.8)

Черновой десктопный клиент для Windows 7 SP1+: вход, чаты через messenger-сервер, QR **из файла** (без камеры и без BLE).

Собирается против отдельных net48-библиотек в `ShortP2P/src/Fx48`.

```
dotnet build src/Iskra.WinForms/Iskra.WinForms.csproj
dotnet run --project src/Iskra.WinForms/Iskra.WinForms.csproj
```

Нужен установленный .NET Framework 4.8 (на Windows 7 — [KB4033369](https://support.microsoft.com/help/4033369) / Developer Pack на машине сборки) и TLS 1.2.
