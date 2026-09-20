# TorgLink.WinForms (черновик, .NET Framework 4.8)

Черновой десктопный клиент для Windows 7 SP1+: вход, чаты через messenger-сервер, QR **из файла** (без камеры и без BLE).

Собирается против отдельных net48-библиотек в `ShortP2P/src/Fx48`.

Платформы: **x86** (по умолчанию, Win7 32-bit) и **x64**.

LAN scan: UDP presence **17501**, discovery wire **17890**, плюс GetClients с messenger-серверов. BLE нет. Разрешите порты в firewall.

```
dotnet build src/TorgLink.WinForms/TorgLink.WinForms.csproj -p:Platform=x86
dotnet build src/TorgLink.WinForms/TorgLink.WinForms.csproj -p:Platform=x64
dotnet run --project src/TorgLink.WinForms/TorgLink.WinForms.csproj -p:Platform=x86
```

Нужен установленный .NET Framework 4.8 (на Windows 7 — [KB4033369](https://support.microsoft.com/help/4033369) / Developer Pack на машине сборки) и TLS 1.2.
