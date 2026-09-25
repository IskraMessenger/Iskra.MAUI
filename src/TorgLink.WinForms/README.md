# TorgLink.WinForms (черновик, .NET Framework 4.7.2)

Черновой десктопный клиент для Windows 7 SP1+: вход, чаты через messenger-сервер, QR **из файла** (без камеры и без BLE).

Собирается против отдельных net472-библиотек в `ShortP2P/src/Fx48`.

Платформы: **x86** (по умолчанию, Win7 32-bit) и **x64**.

LAN scan: UDP presence **17501**, discovery wire **17890**, плюс GetClients с messenger-серверов. BLE нет. Разрешите порты в firewall.

```
dotnet build src/TorgLink.WinForms/TorgLink.WinForms.csproj -p:Platform=x86
dotnet build src/TorgLink.WinForms/TorgLink.WinForms.csproj -p:Platform=x64
dotnet run --project src/TorgLink.WinForms/TorgLink.WinForms.csproj -p:Platform=x86
```

Нужен установленный .NET Framework 4.7.2 (на Windows 7 SP1 — [KB4054530](https://support.microsoft.com/help/4054530); на машине сборки достаточно reference assemblies) и TLS 1.2. 4.5.2 недостаточно: Span, SHA-256 PBKDF2 и текущие NuGet-пакеты требуют минимум 4.7.2.
