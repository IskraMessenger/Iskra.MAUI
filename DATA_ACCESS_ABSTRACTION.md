# Абстракция слоя доступа к данным (Data Access Layer Abstraction)

## Обзор

Реализована абстракция для слоя доступа к данным, которая позволяет легко переключаться между различными бэкэндами баз данных (SQLite, LiteDB и потенциально другими) без необходимости изменения кода бизнес-логики.

## Архитектура

### Основные компоненты

1. **IDataAccessProvider** - основной интерфейс, определяющий контракт для работы с базой данных
2. **IDataConnection** - интерфейс для работы с отдельным подключением к БД
3. **DatabaseProviderType** - enum с доступными типами провайдеров
4. **DatabaseProviderSettings** - класс для сохранения и загрузки выбранного провайдера

### Реализации

1. **SqliteDataAccessProvider** - обёртка над существующей `AppDatabase` для работы с SQLite
2. **LiteDbAsyncDataAccessProvider** - реализация для LiteDB (экспериментальная)

## Как работает абстракция

```
┌─────────────────────────────────────────────────────┐
│              Бизнес-логика (Repositories)          │
│        (ChatRepository, PeerBlacklist и т.д.)       │
└──────────────────┬──────────────────────────────────┘
                   │
                   │ Использует IDataAccessProvider
                   ▼
┌─────────────────────────────────────────────────────┐
│           IDataAccessProvider (интерфейс)           │
│   - InitializeAsync()                               │
│   - GetWriteConnectionAsync()                        │
│   - GetReadConnectionAsync()                         │
│   - WriteAsync<T>()                                 │
│   - ReadAsync<T>()                                  │
│   - EnsureSchemaAsync()                             │
│   - CloseAsync()                                    │
└──────────────────┬──────────────────────────────────┘
                   │
        ┌──────────┴──────────┐
        ▼                     ▼
┌─────────────────────┐  ┌──────────────────────────┐
│ SqliteData Access   │  │  LiteDbAsyncData Access  │
│    Provider         │  │      Provider            │
├─────────────────────┤  ├──────────────────────────┤
│ • Обёртывает App    │  │ • Использует LiteDB      │
│   Database          │  │ • Асинхронные операции   │
│ • Совместима с      │  │ • Альтернативная БД      │
│   существующим      │  │                          │
│   кодом             │  │                          │
└──────────┬──────────┘  └────────────┬─────────────┘
           │                          │
           ▼                          ▼
      ┌─────────────┐          ┌──────────────┐
      │   SQLite    │          │   LiteDB     │
      │  Database   │          │  Database    │
      └─────────────┘          └──────────────┘
```

## Использование

### В слое бизнес-логики (Repositories)

```csharp
// Вместо:
public sealed class MyRepository(AppDatabase db)
{
    public async Task<MyEntity?> FindAsync(int id)
    {
        var conn = await db.GetConnectionAsync();
        return await conn.FindAsync<MyEntity>(id);
    }
}

// Делаем:
public sealed class MyRepository(IDataAccessProvider dataAccessProvider)
{
    private readonly IDataAccessProvider _db = dataAccessProvider;

    public async Task<MyEntity?> FindAsync(int id)
    {
        return await _db.ReadAsync(async conn =>
        {
            return await conn.FindAsync<MyEntity>(id);
        });
    }
}
```

### Регистрация в DI контейнере (MauiProgram.cs)

```csharp
// Загрузка настроек провайдера
builder.Services.AddSingleton(sp =>
{
    var dbSettings = new DatabaseProviderSettings(FileSystem.AppDataDirectory);
    return dbSettings;
});

// Регистрация выбранного провайдера
builder.Services.AddSingleton(sp =>
{
    var dbSettings = sp.GetRequiredService<DatabaseProviderSettings>();
    var dbPath = Path.Combine(FileSystem.AppDataDirectory, "shortp2p.db");

    IDataAccessProvider provider = dbSettings.CurrentProvider switch
    {
        DatabaseProviderType.Sqlite =>
            new SqliteDataAccessProvider(new AppDatabase(dbPath)),
        DatabaseProviderType.LiteDbAsync =>
            new LiteDbAsyncDataAccessProvider(dbPath),
        _ => throw new InvalidOperationException($"Unknown provider: {dbSettings.CurrentProvider}")
    };

    return provider;
});

// Регистрация репозиториев - они автоматически получат IDataAccessProvider
builder.Services.AddSingleton<IUserAuthRepository, SqliteUserAuthRepository>();
builder.Services.AddSingleton<ChatRepository>();
builder.Services.AddSingleton<PeerBlacklist>();
// и т.д.
```

## Переключение провайдеров БД

### Сохранение выбора

Выбор провайдера сохраняется в файле `db-provider.json` в папке приложения:

```json
{
  "providerType": "Sqlite",
  "timestamp": "2026-09-10T12:34:56.789Z"
}
```

### Смена провайдера

1. Откройте Настройки → раздел "База данных"
2. Выберите новый провайдер из выпадающего списка
3. Подтвердите смену (потребуется перезагрузка приложения)
4. Приложение перезагружается и использует новый провайдер

**Важно:** При смене провайдера БД данные ВНЕ переносятся. Это означает:
- Старые данные остаются в формате старого провайдера
- Новый провайдер создаёт пустую БД
- Рекомендуется экспортировать важные данные перед сменой

## Методы IDataAccessProvider

### InitializeAsync()
```csharp
// Инициализирует провайдер (создание БД, миграции и т.д.)
await provider.InitializeAsync();
```

### ReadAsync<T>()
```csharp
// Выполнить операцию чтения
var result = await provider.ReadAsync<List<ChatEntity>>(async conn =>
{
    return await conn.Table<ChatEntity>()
        .Where(c => c.UserId == userId)
        .ToListAsync();
});
```

### WriteAsync()
```csharp
// Выполнить операцию записи с гарантией исключительного доступа
await provider.WriteAsync(async conn =>
{
    var entity = new ChatEntity { /* ... */ };
    await conn.InsertAsync(entity);
});
```

### WriteAsync<T>()
```csharp
// Выполнить операцию записи и вернуть результат
var id = await provider.WriteAsync<int>(async conn =>
{
    var entity = new ChatEntity { /* ... */ };
    await conn.InsertAsync(entity);
    return entity.Id;
});
```

### GetWriteConnectionAsync()
```csharp
// Получить подключение для write-операций (не рекомендуется - используйте WriteAsync)
var conn = await provider.GetWriteConnectionAsync();
await conn.InsertAsync(entity);
```

### GetReadConnectionAsync()
```csharp
// Получить подключение для read-операций (не рекомендуется - используйте ReadAsync)
var conn = await provider.GetReadConnectionAsync();
var results = await conn.Table<Entity>().ToListAsync();
```

## Методы IDataConnection

```csharp
// CRUD операции
await conn.InsertAsync(entity);
await conn.UpdateAsync(entity);
await conn.DeleteAsync(entity);
await conn.FindAsync<T>(primaryKey);

// Запросы
var table = conn.Table<Entity>();  // IQueryable<T>
var results = await table.Where(e => e.Id > 5).ToListAsync();

// DDL операции
await conn.CreateTableAsync<Entity>();
var columns = await conn.GetTableInfoAsync<Entity>();

// Сырые SQL команды (зависит от провайдера)
await conn.ExecuteAsync("PRAGMA journal_mode = WAL");
var value = await conn.ExecuteScalarAsync<string>("SELECT version()");
```

## Миграция существующего кода

### Шаг 1: Обновить импорты
```csharp
using ShortP2P.Client.Data.Abstractions;  // Добавить эту строку
```

### Шаг 2: Изменить конструктор
```csharp
// Было:
public MyRepository(AppDatabase db) { }

// Стало:
public MyRepository(IDataAccessProvider dataAccessProvider) { }
```

### Шаг 3: Обновить код
```csharp
// Было:
var conn = await _db.GetConnectionAsync();
var result = await conn.FindAsync<Entity>(id);

// Стало:
var result = await _db.ReadAsync(async conn =>
{
    return await conn.FindAsync<Entity>(id);
});
```

## Тестирование

При тестировании можно создать mock реализацию IDataAccessProvider:

```csharp
public class MockDataAccessProvider : IDataAccessProvider
{
    private Dictionary<Type, List<object>> _data = new();

    public async Task<T> ReadAsync<T>(Func<IDataConnection, Task<T>> work)
    {
        var conn = new MockDataConnection(_data);
        return await work(conn);
    }

    // Остальные методы...
}
```

## Производительность

### SQLite (рекомендуется для производства)
- **Преимущества:**
  - Проверенный и стабильный
  - Оптимизирован для мобильных устройств
  - Встроенная поддержка сложных запросов
  - WAL режим для параллельных операций

- **Характеристики:**
  - Пул read-соединений (по умолчанию 4)
  - Single write-соединение
  - Семафор для сериализации write операций

### LiteDB (экспериментально)
- **Преимущества:**
  - Встраиваемая документо-ориентированная БД
  - Простота использования
  - Нет зависимостей от системных библиотек

- **Ограничения:**
  - Документо-ориентированный подход может не совпадать с реляционной схемой
  - Менее оптимизирован для сложных запросов
  - Требует синхронизации при асинхронном использовании

## Будущие расширения

1. **Добавить новые провайдеры:**
   - PostgreSQL (для серверной части)
   - MongoDB (документо-ориентированный)
   - Realm (альтернатива для мобильных)

2. **Миграция данных:**
   - Инструмент для переноса данных между провайдерами
   - Автоматическая миграция при смене провайдера

3. **Кэширование:**
   - Уровень кэширования выше провайдера
   - Оптимизация повторяющихся запросов

4. **Мониторинг производительности:**
   - Логирование времени выполнения запросов
   - Сбор метрик использования БД

## Ссылки

- [IDataAccessProvider.cs](ShortP2P/src/ShortP2P.Client/Data/Abstractions/IDataAccessProvider.cs) - основной интерфейс
- [DatabaseProviderSettings.cs](ShortP2P/src/ShortP2P.Client/Data/Abstractions/DatabaseProviderSettings.cs) - управление настройками
- [SqliteDataAccessProvider.cs](ShortP2P/src/ShortP2P.Client/Data/Abstractions/SqliteDataAccessProvider.cs) - реализация для SQLite
- [LiteDbAsyncDataAccessProvider.cs](ShortP2P/src/ShortP2P.Client/Data/Abstractions/LiteDbAsyncDataAccessProvider.cs) - реализация для LiteDB
