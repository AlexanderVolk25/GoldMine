# TPNotifications - Система уведомлений с интеграцией VK API

## Описание
TPNotifications - это плагин для Rust сервера на базе Oxide/uMod, который предоставляет систему уведомлений игроков с интеграцией VK API. Плагин позволяет отправлять уведомления игрокам о важных событиях как в игре, так и через VKontakte.

## Основные возможности

### 1. Типы уведомлений
- **Рейд** - уведомления о рейде дома игрока
- **Убийство** - уведомления о смерти игрока
- **Награда** - уведомления о получении наград
- **Сообщения админов** - важные объявления от администрации
- **Вайп** - уведомления о предстоящем вайпе сервера

### 2. Интеграция с VK API
- Отправка уведомлений в VK личные сообщения
- Отправка уведомлений в VK группу/чат
- Логирование всех отправленных уведомлений
- Настраиваемый VK токен и параметры API

### 3. UI в игре
- Вкладка "Оповещения" в главном меню (интеграция с TPMenuSystem)
- История полученных уведомлений (до 50 записей)
- Настройки включения/отключения типов уведомлений
- Красивый интерфейс с визуальным отображением прочитанных/непрочитанных уведомлений

### 4. Хранение данных
- История уведомлений для каждого игрока
- Персональные настройки уведомлений
- Лог отправленных VK уведомлений

## Установка

1. Скопируйте файл `TPNotifications.cs` в папку `oxide/plugins/`
2. Плагин автоматически создаст файл конфигурации при первой загрузке
3. Настройте VK API токен в файле конфигурации

## Конфигурация

После первой загрузки плагин создаст файл конфигурации `oxide/config/TPNotifications.json`:

```json
{
  "VK API Settings": {
    "VK_Token": "СТАВИМ_СЮДА",
    "Group_ID": "public141783468",
    "Chat_ID": "1",
    "API_Version": "v=5.199"
  },
  "Notification Types": {
    "Raid": true,
    "Kill": true,
    "Reward": true,
    "AdminMessage": true,
    "Wipe": true
  },
  "Settings": {
    "Enable_Notifications": true,
    "Max_History_Items": 50,
    "Notification_Prefix": "[СЕРВЕР]"
  }
}
```

### Настройка VK API

1. **Получение токена VK:**
   - Перейдите на [vk.com/dev](https://vk.com/dev)
   - Создайте Standalone приложение
   - Получите токен доступа с правами на отправку сообщений
   - Вставьте токен в поле `VK_Token`

2. **Group_ID** - ID группы VK для отправки уведомлений
3. **Chat_ID** - ID чата VK для групповых уведомлений (по умолчанию 1)
4. **API_Version** - версия VK API (по умолчанию v=5.199)

### Параметры уведомлений

- **Enable_Notifications** - включить/выключить систему уведомлений
- **Max_History_Items** - максимальное количество уведомлений в истории игрока
- **Notification_Prefix** - префикс для всех уведомлений

## Команды

### Чат команды
- `/notifications` - открыть меню уведомлений
- `/notify` - альтернативная команда для открытия меню

### Консольные команды
- `tpnotifications.open` - открыть UI уведомлений (вызывается из меню)

## API для разработчиков

Плагин предоставляет следующие методы для вызова из других плагинов:

### SendRaidNotification
Отправить уведомление игроку о рейде его дома.

```csharp
TPNotifications?.Call("SendRaidNotification", player, raiderName);
```

**Параметры:**
- `player` (BasePlayer) - игрок, которому отправляется уведомление
- `raiderName` (string) - имя игрока, который рейдит дом

### SendKillNotification
Отправить уведомление игроку об убийстве.

```csharp
TPNotifications?.Call("SendKillNotification", player, killerName);
```

**Параметры:**
- `player` (BasePlayer) - игрок, которому отправляется уведомление
- `killerName` (string) - имя убийцы

### SendRewardNotification
Отправить уведомление игроку о получении награды.

```csharp
TPNotifications?.Call("SendRewardNotification", player, rewardName);
```

**Параметры:**
- `player` (BasePlayer) - игрок, которому отправляется уведомление
- `rewardName` (string) - название награды

### SendAdminMessage
Отправить важное сообщение от администрации всем игрокам.

```csharp
TPNotifications?.Call("SendAdminMessage", message);
```

**Параметры:**
- `message` (string) - текст сообщения

### SendWipeNotification
Отправить уведомление о вайпе всем игрокам.

```csharp
TPNotifications?.Call("SendWipeNotification");
```

### Пример использования в другом плагине

```csharp
using Oxide.Core.Plugins;

[PluginReference] Plugin TPNotifications;

// В вашем коде:
void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
{
    if (entity is BasePlayer && info?.InitiatorPlayer != null)
    {
        var victim = entity as BasePlayer;
        var killer = info.InitiatorPlayer;
        
        // Отправить уведомление о убийстве
        TPNotifications?.Call("SendKillNotification", victim, killer.displayName);
    }
}
```

## Интеграция с TPMenuSystem

Плагин автоматически интегрируется с TPMenuSystem:
- Добавляется вкладка "Оповещения" в главное меню
- При нажатии на кнопку открывается UI с историей уведомлений
- Игроки могут настраивать типы получаемых уведомлений

## Хранение данных

Плагин сохраняет данные в следующих файлах:

- `oxide/data/TPSystem/TPNotifications/PlayerData.json` - данные игроков (история и настройки)
- `oxide/data/TPSystem/TPNotifications/SentLog.json` - лог отправленных VK уведомлений

## Безопасность

- VK токен хранится в конфигурации, а не в коде
- Все отправленные уведомления логируются
- Настройки уведомлений персональны для каждого игрока
- Ограничение истории уведомлений предотвращает переполнение данных

## Требования

- Oxide/uMod
- TPMenuSystem (для интеграции с меню)
- Подключение к интернету для отправки VK уведомлений

## Поддержка

Версия: 1.0.0
Автор: pluginfuel.ru

## Лицензия

Этот плагин является частью сборки GoldMine и распространяется в соответствии с лицензией проекта.
