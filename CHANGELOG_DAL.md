# Changelog: Data Access Layer Abstraction

## Описание изменений

Реализована полная абстракция слоя доступа к данным с поддержкой множественных бэкэндов (SQLite, LiteDB).

## Созданные файлы

### Интерфейсы и конфигурация
- `ShortP2P/src/ShortP2P.Client/Data/Abstractions/IDataAccessProvider.cs` - основной интерфейс для работы с БД
- `ShortP2P/src/ShortP2P.Client/Data/Abstractions/DatabaseProviderSettings.cs` - управление выбором провайдера

### Реализации провайдеров
- `ShortP2P/src/ShortP2P.Client/Data/Abstractions/SqliteDataAccessProvider.cs` - адаптер для SQLite
- `ShortP2P/src/ShortP2P.Client/Data/Abstractions/LiteDbAsyncDataAccessProvider.cs` - реализация для LiteDB

## Изменённые файлы

### Файлы проекта
- `src/TorgLink.Maui/TorgLink.Maui.csproj` - добавлен пакет LiteDB v5.0.16

### Программа инициализации
- `src/TorgLink.Maui/MauiProgram.cs`
  - Добавлен импорт `ShortP2P.Client.Data.Abstractions`
  - Добавлена регистрация `DatabaseProviderSettings`
  - Добавлена регистрация `IDataAccessProvider` с выбором провайдера на основе настроек

### Репозитории (переведены на IDataAccessProvider)
- `ShortP2P/src/ShortP2P.Client/Data/SqliteUserAuthRepository.cs` - используется IDataAccessProvider
- `ShortP2P/src/ShortP2P.Client/Services/ChatRepository.cs` - используется IDataAccessProvider
- `ShortP2P/src/ShortP2P.Client/Services/SqliteBleDiscoveredPeerStore.cs` - переведена на IDataAccessProvider
- `ShortP2P/src/ShortP2P.Client/Services/MessengerServers/SqliteMessengerServerRepository.cs` - используется IDataAccessProvider
- `ShortP2P/src/ShortP2P.Client/Services/PeerBlacklist.cs` - используется IDataAccessProvider
- `ShortP2P/src/ShortP2P.Client/Services/BluetoothPresencePingTargetsProvider.cs` - используется IDataAccessProvider

### UI Компоненты
- `src/TorgLink.Maui/SettingsPage.xaml` - добавлен раздел "База данных" с Picker для выбора провайдера
- `src/TorgLink.Maui/SettingsPage.xaml.cs` - добавлена логика для управления выбором провайдера

### Локализация
- `src/TorgLink.Maui/Localization/StringCatalog.Ru.cs` - добавлены строки для РУ локализации:
  - `settings.database` - название раздела
  - `settings.database_hint` - подсказка о перезагрузке
  - `settings.database_change_title` - заголовок диалога
  - `settings.database_change_message` - сообщение диалога
  
- `src/TorgLink.Maui/Localization/StringCatalog.En.cs` - добавлены строки для EN локализации

## Ключевые особенности

### 1. Абстракция
- Полная изоляция бизнес-логики от конкретной реализации БД
- Простое переключение между провайдерами без изменения кода репозиториев

### 2. Множественные бэкэнды
- **SQLite** - основной, проверенный провайдер
- **LiteDB** - альтернативный документо-ориентированный вариант

### 3. Сохранение выбора
- Выбор провайдера сохраняется в `db-provider.json`
- Загружается автоматически при запуске приложения

### 4. Требование перезагрузки
- При смене провайдера приложение требует перезагрузки
- Гарантирует корректную работу нового провайдера

### 5. User Experience
- Интуитивный интерфейс в Настройках
- Понятные диалоги с предупреждениями
- Локализация на РУ и EN

## Миграция кода

Все репозитории успешно переведены на новую абстракцию:

```csharp
// Старый код:
public sealed class MyRepository(AppDatabase db)
{
    var conn = await db.GetConnectionAsync();
    // ...
}

// Новый код:
public sealed class MyRepository(IDataAccessProvider dataAccessProvider)
{
    await _db.ReadAsync(async conn =>
    {
        // ...
    });
}
```

## Протестированные компоненты

- ✅ SqliteUserAuthRepository
- ✅ ChatRepository
- ✅ SqliteBleDiscoveredPeerStore
- ✅ SqliteMessengerServerRepository
- ✅ PeerBlacklist
- ✅ BluetoothPresencePingTargetsProvider

## Known Issues & Limitations

1. **LiteDB реализация** - экспериментальна, требует тестирования:
   - Некоторые операции синхронизированы через Task.Run
   - Документо-ориентированный подход может не совпадать со схемой

2. **Миграция данных** - при смене провайдера данные ВНЕ переносятся:
   - Рекомендуется экспортировать важные данные перед сменой
   - Каждый провайдер работает со своей БД

3. **WinForms приложение** - требует отдельной модификации:
   - `TorgLink.WinForms/Program.cs` ещё использует AppDatabase напрямую
   - Нужно будет обновить аналогично MAUI приложению

## Следующие шаги

1. Протестировать LiteDB реализацию в реальных условиях
2. Добавить миграцию данных между провайдерами
3. Оптимизировать производительность LiteDB
4. Обновить WinForms приложение на новую абстракцию
5. Добавить поддержку других провайдеров (PostgreSQL, Realm и т.д.)

## Документация

- Подробное описание: `DATA_ACCESS_ABSTRACTION.md`
- Примеры кода: в классах реализации
- API документация: в интерфейсах (комментарии XML)
