using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("TPNotifications", "pluginfuel.ru", "1.0.0")]
	[Description("Система уведомлений с интеграцией VK API")]
	class TPNotifications : RustPlugin
	{
		#region Variables
		[PluginReference] Plugin ImageLibrary, TPMenuSystem;
		
		private static TPNotifications _instance;
		private const string Layer = "TPNotifications_UI";
		private Dictionary<ulong, PlayerData> _playerData = new Dictionary<ulong, PlayerData>();
		private List<SentNotification> _sentNotifications = new List<SentNotification>();
		private Dictionary<ulong, PendingVerification> _pendingVerifications = new Dictionary<ulong, PendingVerification>();
		private Dictionary<ulong, string> _tempVkIds = new Dictionary<ulong, string>();
		#endregion

		#region Data Classes
		public class PlayerData
		{
			[JsonProperty("Ник игрока")]
			public string DisplayName { get; set; }
			
			[JsonProperty("VK ID")]
			public string VkId { get; set; } = "";
			
			[JsonProperty("VK Подтвержден")]
			public bool VkConfirmed { get; set; } = false;
			
			[JsonProperty("История уведомлений")]
			public List<NotificationHistory> History { get; set; } = new List<NotificationHistory>();
			
			[JsonProperty("Настройки уведомлений")]
			public NotificationSettings Settings { get; set; } = new NotificationSettings();
		}

		public class NotificationHistory
		{
			[JsonProperty("Тип")]
			public string Type { get; set; }
			
			[JsonProperty("Сообщение")]
			public string Message { get; set; }
			
			[JsonProperty("Время")]
			public string Timestamp { get; set; }
			
			[JsonProperty("Прочитано")]
			public bool IsRead { get; set; }
		}

		public class NotificationSettings
		{
			[JsonProperty("Рейды")]
			public bool Raid { get; set; } = true;
			
			[JsonProperty("Убийства")]
			public bool Kill { get; set; } = true;
			
			[JsonProperty("Награды")]
			public bool Reward { get; set; } = true;
			
			[JsonProperty("Сообщения админов")]
			public bool AdminMessage { get; set; } = true;
			
			[JsonProperty("Вайп")]
			public bool Wipe { get; set; } = true;
		}

		public class PendingVerification
		{
			public string VkId { get; set; }
			public string Code { get; set; }
			public DateTime ExpiresAt { get; set; }
		}

		public class SentNotification
		{
			[JsonProperty("Тип")]
			public string Type { get; set; }
			
			[JsonProperty("Сообщение")]
			public string Message { get; set; }
			
			[JsonProperty("Время")]
			public string Timestamp { get; set; }
			
			[JsonProperty("Статус")]
			public string Status { get; set; }
		}
		#endregion

		#region Configuration
		private Configuration _config;

		public class Configuration
		{
			[JsonProperty("VK API Settings")]
			public VKAPISettings VKSettings { get; set; } = new VKAPISettings();

			[JsonProperty("Notification Types")]
			public NotificationTypesConfig NotificationTypes { get; set; } = new NotificationTypesConfig();

			[JsonProperty("Settings")]
			public GeneralSettings Settings { get; set; } = new GeneralSettings();

			public class VKAPISettings
			{
				[JsonProperty("VK_Token")]
				public string VKToken { get; set; } = "СТАВИМ_СЮДА";

				[JsonProperty("Group_ID")]
				public string GroupID { get; set; } = "public141783468";

				[JsonProperty("Chat_ID")]
				public string ChatID { get; set; } = "1";

				[JsonProperty("API_Version")]
				public string APIVersion { get; set; } = "v=5.199";
			}

			public class NotificationTypesConfig
			{
				[JsonProperty("Raid")]
				public bool Raid { get; set; } = true;

				[JsonProperty("Kill")]
				public bool Kill { get; set; } = true;

				[JsonProperty("Reward")]
				public bool Reward { get; set; } = true;

				[JsonProperty("AdminMessage")]
				public bool AdminMessage { get; set; } = true;

				[JsonProperty("Wipe")]
				public bool Wipe { get; set; } = true;
			}

			public class GeneralSettings
			{
				[JsonProperty("Enable_Notifications")]
				public bool EnableNotifications { get; set; } = true;

				[JsonProperty("Max_History_Items")]
				public int MaxHistoryItems { get; set; } = 50;

				[JsonProperty("Notification_Prefix")]
				public string NotificationPrefix { get; set; } = "[СЕРВЕР]";
			}
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null)
				{
					LoadDefaultConfig();
				}
			}
			catch
			{
				PrintWarning("Ошибка чтения конфигурации, создаем новую");
				LoadDefaultConfig();
			}
			SaveConfig();
		}

		protected override void LoadDefaultConfig()
		{
			_config = new Configuration();
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(_config, true);
		}
		#endregion

		#region Oxide Hooks
		void Init()
		{
			_instance = this;
			LoadData();
		}

		void OnServerInitialized()
		{
			// Регистрация команды для меню
			cmd.AddConsoleCommand("tpnotifications.open", this, nameof(CmdOpenNotifications));
		}

		void Unload()
		{
			SaveData();
			foreach (var player in BasePlayer.activePlayerList)
			{
				DestroyUI(player);
			}
			_instance = null;
		}

		void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			DestroyUI(player);
		}
		#endregion

		#region Data Management
		void LoadData()
		{
			try
			{
				_playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("TPSystem/TPNotifications/PlayerData");
				if (_playerData == null)
					_playerData = new Dictionary<ulong, PlayerData>();
			}
			catch
			{
				_playerData = new Dictionary<ulong, PlayerData>();
			}

			try
			{
				_sentNotifications = Interface.Oxide.DataFileSystem.ReadObject<List<SentNotification>>("TPSystem/TPNotifications/SentLog");
				if (_sentNotifications == null)
					_sentNotifications = new List<SentNotification>();
			}
			catch
			{
				_sentNotifications = new List<SentNotification>();
			}
		}

		void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject("TPSystem/TPNotifications/PlayerData", _playerData);
			Interface.Oxide.DataFileSystem.WriteObject("TPSystem/TPNotifications/SentLog", _sentNotifications);
		}

		PlayerData GetPlayerData(ulong userId)
		{
			if (!_playerData.ContainsKey(userId))
			{
				var player = BasePlayer.FindByID(userId);
				if (player != null)
				{
					_playerData[userId] = new PlayerData
					{
						DisplayName = player.displayName
					};
				}
			}
			return _playerData.ContainsKey(userId) ? _playerData[userId] : null;
		}

		void AddNotificationToHistory(ulong userId, string type, string message)
		{
			var data = GetPlayerData(userId);
			if (data == null) return;

			var notification = new NotificationHistory
			{
				Type = type,
				Message = message,
				Timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
				IsRead = false
			};

			data.History.Insert(0, notification);

			// Ограничение истории
			if (data.History.Count > _config.Settings.MaxHistoryItems)
			{
				data.History = data.History.Take(_config.Settings.MaxHistoryItems).ToList();
			}

			SaveData();
		}

		void LogSentNotification(string type, string message, string status)
		{
			var log = new SentNotification
			{
				Type = type,
				Message = message,
				Timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
				Status = status
			};

			_sentNotifications.Add(log);
			SaveData();
		}
		#endregion

		#region VK API
		private string URLEncode(string input)
		{
			return UnityEngine.Networking.UnityWebRequest.EscapeURL(input);
		}

		private int GetRandomId()
		{
			return UnityEngine.Random.Range(0, 999999999);
		}

		void SendVKNotification(string userIds, string message)
		{
			if (!_config.Settings.EnableNotifications)
			{
				LogSentNotification("VK", message, "Disabled");
				return;
			}

			if (string.IsNullOrEmpty(_config.VKSettings.VKToken) || _config.VKSettings.VKToken == "СТАВИМ_СЮДА")
			{
				PrintWarning("VK Token не настроен!");
				LogSentNotification("VK", message, "No Token");
				return;
			}

			string url = $"https://api.vk.com/method/messages.send?user_ids={userIds}&message={URLEncode(message)}&{_config.VKSettings.APIVersion}&random_id={GetRandomId()}&access_token={_config.VKSettings.VKToken}";
			
			webrequest.Enqueue(url, null, (code, response) =>
			{
				if (code == 200)
				{
					LogSentNotification("VK", message, "Success");
					Puts($"VK уведомление отправлено: {message}");
				}
				else
				{
					LogSentNotification("VK", message, $"Error {code}");
					PrintWarning($"Ошибка отправки VK уведомления: {code}");
				}
			}, this);
		}

		void SendVKNotificationToGroup(string message)
		{
			if (!_config.Settings.EnableNotifications)
			{
				LogSentNotification("VK Group", message, "Disabled");
				return;
			}

			if (string.IsNullOrEmpty(_config.VKSettings.VKToken) || _config.VKSettings.VKToken == "СТАВИМ_СЮДА")
			{
				PrintWarning("VK Token не настроен!");
				LogSentNotification("VK Group", message, "No Token");
				return;
			}

			// Для отправки в группу используется wall.post или другой метод
			// Здесь используем простой пример с messages.send для chat_id
			string url = $"https://api.vk.com/method/messages.send?chat_id={_config.VKSettings.ChatID}&message={URLEncode(message)}&{_config.VKSettings.APIVersion}&random_id={GetRandomId()}&access_token={_config.VKSettings.VKToken}";
			
			webrequest.Enqueue(url, null, (code, response) =>
			{
				if (code == 200)
				{
					LogSentNotification("VK Group", message, "Success");
					Puts($"VK групповое уведомление отправлено: {message}");
				}
				else
				{
					LogSentNotification("VK Group", message, $"Error {code}");
					PrintWarning($"Ошибка отправки VK группового уведомления: {code}");
				}
			}, this);
		}

		string GenerateVerificationCode()
		{
			return UnityEngine.Random.Range(100000, 999999).ToString();
		}

		void StartVkVerification(BasePlayer player, string vkId)
		{
			var data = GetPlayerData(player.userID);
			if (data == null) return;

			// Генерируем код
			string code = GenerateVerificationCode();
			
			// Сохраняем ожидание подтверждения (код действует 10 минут)
			_pendingVerifications[player.userID] = new PendingVerification
			{
				VkId = vkId,
				Code = code,
				ExpiresAt = DateTime.Now.AddMinutes(10)
			};

			// Отправляем код в VK
			string message = $"Ваш код подтверждения для сервера: {code}\nКод действителен 10 минут.";
			SendVKNotification(vkId, message);

			// Уведомляем игрока
			player.ChatMessage($"<color=#4CAF50>[Уведомления]</color> Код подтверждения отправлен в VK. Введите его в окне подтверждения.");
		}

		bool ConfirmVkVerification(BasePlayer player, string code)
		{
			if (!_pendingVerifications.ContainsKey(player.userID))
			{
				player.ChatMessage($"<color=#F44336>[Ошибка]</color> Нет активного запроса на привязку VK.");
				return false;
			}

			var pending = _pendingVerifications[player.userID];

			// Проверяем срок действия
			if (DateTime.Now > pending.ExpiresAt)
			{
				_pendingVerifications.Remove(player.userID);
				player.ChatMessage($"<color=#F44336>[Ошибка]</color> Код подтверждения истек. Попробуйте снова.");
				return false;
			}

			// Проверяем код
			if (pending.Code != code)
			{
				player.ChatMessage($"<color=#F44336>[Ошибка]</color> Неверный код подтверждения.");
				return false;
			}

			// Подтверждаем привязку
			var data = GetPlayerData(player.userID);
			if (data != null)
			{
				data.VkId = pending.VkId;
				data.VkConfirmed = true;
				SaveData();
			}

			_pendingVerifications.Remove(player.userID);
			player.ChatMessage($"<color=#4CAF50>[Успех]</color> VK успешно привязан!");
			return true;
		}

		void UnlinkVk(BasePlayer player)
		{
			var data = GetPlayerData(player.userID);
			if (data != null)
			{
				data.VkId = "";
				data.VkConfirmed = false;
				SaveData();
				player.ChatMessage($"<color=#FFC107>[Уведомления]</color> VK отвязан от вашего аккаунта.");
			}
		}
		#endregion

		#region API Methods
		// API для вызова из других плагинов
		void SendRaidNotification(BasePlayer player, string raiderName)
		{
			if (!_config.NotificationTypes.Raid) return;

			var data = GetPlayerData(player.userID);
			if (data != null && !data.Settings.Raid) return;

			string message = $"{_config.Settings.NotificationPrefix} Твой дом рейдит {raiderName}!";
			
			AddNotificationToHistory(player.userID, "Рейд", message);
			
			// Отправляем в VK если привязан
			if (data != null && data.VkConfirmed && !string.IsNullOrEmpty(data.VkId))
			{
				SendVKNotification(data.VkId, message);
			}
		}

		void SendKillNotification(BasePlayer player, string killerName)
		{
			if (!_config.NotificationTypes.Kill) return;

			var data = GetPlayerData(player.userID);
			if (data != null && !data.Settings.Kill) return;

			string message = $"{_config.Settings.NotificationPrefix} Тебя убил {killerName}!";
			
			AddNotificationToHistory(player.userID, "Убийство", message);
			
			// Отправляем в VK если привязан
			if (data != null && data.VkConfirmed && !string.IsNullOrEmpty(data.VkId))
			{
				SendVKNotification(data.VkId, message);
			}
		}

		void SendRewardNotification(BasePlayer player, string rewardName)
		{
			if (!_config.NotificationTypes.Reward) return;

			var data = GetPlayerData(player.userID);
			if (data != null && !data.Settings.Reward) return;

			string message = $"{_config.Settings.NotificationPrefix} Ты получил награду: {rewardName}!";
			
			AddNotificationToHistory(player.userID, "Награда", message);
			
			// Отправляем в VK если привязан
			if (data != null && data.VkConfirmed && !string.IsNullOrEmpty(data.VkId))
			{
				SendVKNotification(data.VkId, message);
			}
		}

		void SendAdminMessage(string message)
		{
			if (!_config.NotificationTypes.AdminMessage) return;

			string fullMessage = $"{_config.Settings.NotificationPrefix} {message}";
			
			// Отправляем всем игрокам
			foreach (var player in BasePlayer.activePlayerList)
			{
				var data = GetPlayerData(player.userID);
				if (data != null && data.Settings.AdminMessage)
				{
					AddNotificationToHistory(player.userID, "Админ", fullMessage);
				}
			}

			// Отправляем в VK группу
			SendVKNotificationToGroup(fullMessage);
		}

		void SendWipeNotification()
		{
			if (!_config.NotificationTypes.Wipe) return;

			string message = $"{_config.Settings.NotificationPrefix} Внимание! Скоро вайп сервера!";
			
			// Отправляем всем игрокам
			foreach (var player in BasePlayer.activePlayerList)
			{
				var data = GetPlayerData(player.userID);
				if (data != null && data.Settings.Wipe)
				{
					AddNotificationToHistory(player.userID, "Вайп", message);
				}
			}

			// Отправляем в VK группу
			SendVKNotificationToGroup(message);
		}
		#endregion

		#region UI
		void CmdOpenNotifications(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			OpenNotificationsUI(player);
		}

		void OpenNotificationsUI(BasePlayer player)
		{
			DestroyUI(player);

			var data = GetPlayerData(player.userID);
			if (data == null)
			{
				data = new PlayerData { DisplayName = player.displayName };
				_playerData[player.userID] = data;
			}

			CuiElementContainer container = new CuiElementContainer();

			// Главная панель
			container.Add(new CuiPanel
			{
				RectTransform = { AnchorMin = "0.15 0.15", AnchorMax = "0.85 0.85" },
				Image = { Color = "0.1 0.1 0.1 0.95" }
			}, "Overlay", Layer);

			// Заголовок
			container.Add(new CuiLabel
			{
				RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 1" },
				Text = { Text = "ОПОВЕЩЕНИЯ", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			// Кнопка закрытия
			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.92 0.92", AnchorMax = "0.99 0.99" },
				Button = { Command = "tpnotifications.close", Color = "0.8 0.2 0.2 0.8" },
				Text = { Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			// Вкладки
			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.02 0.85", AnchorMax = "0.23 0.90" },
				Button = { Command = "tpnotifications.tab history", Color = "0.3 0.3 0.3 0.8" },
				Text = { Text = "История", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.25 0.85", AnchorMax = "0.46 0.90" },
				Button = { Command = "tpnotifications.tab settings", Color = "0.2 0.2 0.2 0.8" },
				Text = { Text = "Настройки", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.48 0.85", AnchorMax = "0.69 0.90" },
				Button = { Command = "tpnotifications.tab vklink", Color = "0.2 0.2 0.2 0.8" },
				Text = { Text = "Привязка VK", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			// Профиль VK если привязан
			if (data.VkConfirmed && !string.IsNullOrEmpty(data.VkId))
			{
				container.Add(new CuiPanel
				{
					RectTransform = { AnchorMin = "0.02 0.78", AnchorMax = "0.98 0.84" },
					Image = { Color = "0.15 0.6 0.3 0.3" }
				}, Layer, $"{Layer}_vkprofile");

				// Иконка VK
				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.01 0.1", AnchorMax = "0.08 0.9" },
					Text = { Text = "VK", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.3 0.6 0.9 1" }
				}, $"{Layer}_vkprofile");

				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.09 0.1", AnchorMax = "0.5 0.9" },
					Text = { Text = $"VK ID: {data.VkId}", Font = "robotocondensed-regular.ttf", FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
				}, $"{Layer}_vkprofile");

				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.5 0.1", AnchorMax = "0.85 0.9" },
					Text = { Text = "✓ Уведомления в VK включены", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.4 0.8 0.4 1" }
				}, $"{Layer}_vkprofile");

				container.Add(new CuiButton
				{
					RectTransform = { AnchorMin = "0.87 0.15", AnchorMax = "0.99 0.85" },
					Button = { Command = "tpnotifications.tab vklink", Color = "0.3 0.3 0.3 0.8" },
					Text = { Text = "Настроить", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
				}, $"{Layer}_vkprofile");
			}

			// Контент - История уведомлений
			float yPos = data.VkConfirmed && !string.IsNullOrEmpty(data.VkId) ? 0.73f : 0.80f;
			int count = 0;
			foreach (var notification in data.History.Take(10))
			{
				float yMin = yPos - 0.07f;
				float yMax = yPos;

				string color = notification.IsRead ? "0.2 0.2 0.2 0.6" : "0.3 0.4 0.3 0.7";

				container.Add(new CuiPanel
				{
					RectTransform = { AnchorMin = $"0.02 {yMin}", AnchorMax = $"0.98 {yMax}" },
					Image = { Color = color }
				}, Layer, $"{Layer}_notification_{count}");

				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.02 0.5", AnchorMax = "0.2 1" },
					Text = { Text = $"[{notification.Type}]", Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 0.8 0.2 1" }
				}, $"{Layer}_notification_{count}");

				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.98 0.5" },
					Text = { Text = notification.Message, Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.9 0.9 0.9 1" }
				}, $"{Layer}_notification_{count}");

				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.75 0.5", AnchorMax = "0.98 1" },
					Text = { Text = notification.Timestamp, Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleRight, Color = "0.7 0.7 0.7 1" }
				}, $"{Layer}_notification_{count}");

				yPos -= 0.08f;
				count++;

				if (!notification.IsRead)
				{
					notification.IsRead = true;
				}
			}

			if (data.History.Count == 0)
			{
				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.02 0.4", AnchorMax = "0.98 0.5" },
					Text = { Text = "Нет уведомлений", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.6 0.6 0.6 1" }
				}, Layer);
			}

			SaveData();
			CuiHelper.AddUi(player, container);
		}

		void OpenSettingsUI(BasePlayer player)
		{
			DestroyUI(player);

			var data = GetPlayerData(player.userID);
			if (data == null) return;

			CuiElementContainer container = new CuiElementContainer();

			// Главная панель
			container.Add(new CuiPanel
			{
				RectTransform = { AnchorMin = "0.15 0.15", AnchorMax = "0.85 0.85" },
				Image = { Color = "0.1 0.1 0.1 0.95" }
			}, "Overlay", Layer);

			// Заголовок
			container.Add(new CuiLabel
			{
				RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 1" },
				Text = { Text = "НАСТРОЙКИ ОПОВЕЩЕНИЙ", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			// Кнопка закрытия
			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.92 0.92", AnchorMax = "0.99 0.99" },
				Button = { Command = "tpnotifications.close", Color = "0.8 0.2 0.2 0.8" },
				Text = { Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			// Вкладки
			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.02 0.85", AnchorMax = "0.23 0.90" },
				Button = { Command = "tpnotifications.tab history", Color = "0.2 0.2 0.2 0.8" },
				Text = { Text = "История", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.25 0.85", AnchorMax = "0.46 0.90" },
				Button = { Command = "tpnotifications.tab settings", Color = "0.3 0.3 0.3 0.8" },
				Text = { Text = "Настройки", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			// Настройки
			float yPos = 0.75f;
			var settings = new Dictionary<string, bool>
			{
				{ "Рейды", data.Settings.Raid },
				{ "Убийства", data.Settings.Kill },
				{ "Награды", data.Settings.Reward },
				{ "Сообщения админов", data.Settings.AdminMessage },
				{ "Вайп", data.Settings.Wipe }
			};

			foreach (var setting in settings)
			{
				float yMin = yPos - 0.08f;
				float yMax = yPos;

				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = $"0.1 {yMin}", AnchorMax = $"0.6 {yMax}" },
					Text = { Text = setting.Key, Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" }
				}, Layer);

				string buttonText = setting.Value ? "ВКЛ" : "ВЫКЛ";
				string buttonColor = setting.Value ? "0.2 0.6 0.2 0.8" : "0.6 0.2 0.2 0.8";
				string settingKey = setting.Key.ToLower().Replace(" ", "_");

				container.Add(new CuiButton
				{
					RectTransform = { AnchorMin = $"0.7 {yMin + 0.01}", AnchorMax = $"0.9 {yMax - 0.01}" },
					Button = { Command = $"tpnotifications.toggle {settingKey}", Color = buttonColor },
					Text = { Text = buttonText, Font = "robotocondensed-bold.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
				}, Layer);

				yPos -= 0.10f;
			}

			CuiHelper.AddUi(player, container);
		}

		void OpenVkLinkUI(BasePlayer player)
		{
			DestroyUI(player);

			var data = GetPlayerData(player.userID);
			if (data == null) return;

			CuiElementContainer container = new CuiElementContainer();

			// Главная панель
			container.Add(new CuiPanel
			{
				RectTransform = { AnchorMin = "0.15 0.15", AnchorMax = "0.85 0.85" },
				Image = { Color = "0.1 0.1 0.1 0.95" }
			}, "Overlay", Layer);

			// Заголовок
			container.Add(new CuiLabel
			{
				RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 1" },
				Text = { Text = "ПРИВЯЗКА VK", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			// Кнопка закрытия
			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.92 0.92", AnchorMax = "0.99 0.99" },
				Button = { Command = "tpnotifications.close", Color = "0.8 0.2 0.2 0.8" },
				Text = { Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			// Вкладки
			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.02 0.85", AnchorMax = "0.23 0.90" },
				Button = { Command = "tpnotifications.tab history", Color = "0.2 0.2 0.2 0.8" },
				Text = { Text = "История", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.25 0.85", AnchorMax = "0.46 0.90" },
				Button = { Command = "tpnotifications.tab settings", Color = "0.2 0.2 0.2 0.8" },
				Text = { Text = "Настройки", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.48 0.85", AnchorMax = "0.69 0.90" },
				Button = { Command = "tpnotifications.tab vklink", Color = "0.3 0.3 0.3 0.8" },
				Text = { Text = "Привязка VK", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, Layer);

			// Контент
			if (data.VkConfirmed && !string.IsNullOrEmpty(data.VkId))
			{
				// VK уже привязан
				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.1 0.65", AnchorMax = "0.9 0.75" },
					Text = { Text = $"VK ID: {data.VkId}", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.4 0.8 0.4 1" }
				}, Layer);

				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.1 0.58", AnchorMax = "0.9 0.63" },
					Text = { Text = "✓ VK успешно привязан", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.4 0.8 0.4 1" }
				}, Layer);

				container.Add(new CuiButton
				{
					RectTransform = { AnchorMin = "0.35 0.45", AnchorMax = "0.65 0.52" },
					Button = { Command = "tpnotifications.vk.unlink", Color = "0.8 0.3 0.3 0.8" },
					Text = { Text = "Отвязать VK", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
				}, Layer);
			}
			else
			{
				// VK не привязан
				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.1 0.7", AnchorMax = "0.9 0.78" },
					Text = { Text = "Привязка VK аккаунта", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
				}, Layer);

				container.Add(new CuiLabel
				{
					RectTransform = { AnchorMin = "0.1 0.62", AnchorMax = "0.9 0.68" },
					Text = { Text = "Привяжите VK для получения уведомлений в личные сообщения", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" }
				}, Layer);

				// Проверяем, есть ли ожидание подтверждения
				if (_pendingVerifications.ContainsKey(player.userID))
				{
					var pending = _pendingVerifications[player.userID];
					if (DateTime.Now <= pending.ExpiresAt)
					{
						// Показываем форму ввода кода
						container.Add(new CuiLabel
						{
							RectTransform = { AnchorMin = "0.1 0.54", AnchorMax = "0.9 0.60" },
							Text = { Text = $"Код отправлен в VK (ID: {pending.VkId})", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.4 0.8 0.4 1" }
						}, Layer);

						int secondsLeft = (int)(pending.ExpiresAt - DateTime.Now).TotalSeconds;
						container.Add(new CuiLabel
						{
							RectTransform = { AnchorMin = "0.1 0.49", AnchorMax = "0.9 0.53" },
							Text = { Text = $"Осталось времени: {secondsLeft} сек", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 0.8 0.2 1" }
						}, Layer);

						container.Add(new CuiLabel
						{
							RectTransform = { AnchorMin = "0.25 0.42", AnchorMax = "0.75 0.47" },
							Text = { Text = "Введите код: /vkcode <код>", Font = "robotocondensed-regular.ttf", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
						}, Layer);
					}
				}
				else
				{
					// Форма для ввода VK ID
					container.Add(new CuiLabel
					{
						RectTransform = { AnchorMin = "0.1 0.54", AnchorMax = "0.9 0.58" },
						Text = { Text = "Введите команду в чат:", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" }
					}, Layer);

					container.Add(new CuiLabel
					{
						RectTransform = { AnchorMin = "0.25 0.48", AnchorMax = "0.75 0.53" },
						Text = { Text = "/vklink <ваш VK ID>", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.4 0.8 1 1" }
					}, Layer);

					container.Add(new CuiLabel
					{
						RectTransform = { AnchorMin = "0.1 0.40", AnchorMax = "0.9 0.45" },
						Text = { Text = "Где найти VK ID: vk.com/id123456789 (цифры после id)", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.6 0.6 0.6 1" }
					}, Layer);
				}
			}

			CuiHelper.AddUi(player, container);
		}

		void DestroyUI(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, Layer);
		}

		[ConsoleCommand("tpnotifications.close")]
		void CmdClose(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
			DestroyUI(player);
		}

		[ConsoleCommand("tpnotifications.tab")]
		void CmdTab(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			string tab = arg.GetString(0);
			if (tab == "history")
			{
				OpenNotificationsUI(player);
			}
			else if (tab == "settings")
			{
				OpenSettingsUI(player);
			}
			else if (tab == "vklink")
			{
				OpenVkLinkUI(player);
			}
		}

		[ConsoleCommand("tpnotifications.toggle")]
		void CmdToggle(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			var data = GetPlayerData(player.userID);
			if (data == null) return;

			string setting = arg.GetString(0);

			switch (setting)
			{
				case "рейды":
					data.Settings.Raid = !data.Settings.Raid;
					break;
				case "убийства":
					data.Settings.Kill = !data.Settings.Kill;
					break;
				case "награды":
					data.Settings.Reward = !data.Settings.Reward;
					break;
				case "сообщения_админов":
					data.Settings.AdminMessage = !data.Settings.AdminMessage;
					break;
				case "вайп":
					data.Settings.Wipe = !data.Settings.Wipe;
					break;
			}

			SaveData();
			OpenSettingsUI(player);
		}
		#endregion

		#region Chat Commands
		[ChatCommand("notifications")]
		void CmdNotifications(BasePlayer player, string command, string[] args)
		{
			OpenNotificationsUI(player);
		}

		[ChatCommand("notify")]
		void CmdNotify(BasePlayer player, string command, string[] args)
		{
			OpenNotificationsUI(player);
		}

		[ChatCommand("vklink")]
		void CmdVkLink(BasePlayer player, string command, string[] args)
		{
			if (args.Length == 0)
			{
				player.ChatMessage($"<color=#F44336>[Ошибка]</color> Используйте: /vklink <VK ID>");
				player.ChatMessage($"<color=#FFC107>[Подсказка]</color> Найдите ваш VK ID: vk.com/id123456789 (цифры после id)");
				return;
			}

			string vkId = args[0];
			
			// Проверяем, что введены только цифры
			if (!vkId.All(char.IsDigit))
			{
				player.ChatMessage($"<color=#F44336>[Ошибка]</color> VK ID должен содержать только цифры!");
				return;
			}

			StartVkVerification(player, vkId);
		}

		[ChatCommand("vkcode")]
		void CmdVkCode(BasePlayer player, string command, string[] args)
		{
			if (args.Length == 0)
			{
				player.ChatMessage($"<color=#F44336>[Ошибка]</color> Используйте: /vkcode <код>");
				return;
			}

			string code = args[0];
			
			if (ConfirmVkVerification(player, code))
			{
				// Обновляем UI если открыт
				OpenVkLinkUI(player);
			}
		}

		[ConsoleCommand("tpnotifications.vk.unlink")]
		void CmdVkUnlink(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			UnlinkVk(player);
			OpenVkLinkUI(player);
		}

		[ConsoleCommand("tpnotifications.vk.setid")]
		void CmdVkSetId(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			string vkId = arg.GetString(0);
			if (!string.IsNullOrEmpty(vkId))
			{
				_tempVkIds[player.userID] = vkId;
			}
		}

		[ConsoleCommand("tpnotifications.vk.getcode")]
		void CmdVkGetCode(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			if (!_tempVkIds.ContainsKey(player.userID) || string.IsNullOrEmpty(_tempVkIds[player.userID]))
			{
				player.ChatMessage($"<color=#F44336>[Ошибка]</color> Введите VK ID в поле выше!");
				return;
			}

			string vkId = _tempVkIds[player.userID];

			// Проверяем, что введены только цифры
			if (!vkId.All(char.IsDigit))
			{
				player.ChatMessage($"<color=#F44336>[Ошибка]</color> VK ID должен содержать только цифры!");
				return;
			}

			StartVkVerification(player, vkId);
			timer.Once(1f, () => OpenVkLinkUI(player));
		}

		[ConsoleCommand("tpnotifications.vk.confirm")]
		void CmdVkConfirm(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			string code = arg.GetString(0);

			if (string.IsNullOrEmpty(code))
			{
				player.ChatMessage($"<color=#F44336>[Ошибка]</color> Введите код!");
				return;
			}

			if (ConfirmVkVerification(player, code))
			{
				timer.Once(0.5f, () => OpenVkLinkUI(player));
			}
		}
		#endregion
	}
}
