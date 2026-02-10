using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.IO;
namespace Oxide.Plugins
{
	[Info("TPCaseSystem", "pluginfuel.ru", "20.0.2")]
	class TPCaseSystem : RustPlugin
	{
		#region Вар
			private static TPCaseSystem inst;
			private Dictionary<ulong, CaseData> dataSettings;
			[PluginReference] Plugin ImageLibrary;
			[PluginReference] Plugin Economics, ServerRewards, IQEconomic, TPEconomic, TPMenuSystem;
			public string Layer = "Case_UI";
			public string LayerHelp1 = "Help1_UI";
			public string LayerInventory = "Inventory_UI";
			private Dictionary<BasePlayer, int> OpenedUIs = new();
			private readonly object LockObject = new();
			private bool isReady = false;
			private EconomyManager economyManager;
		#endregion
		#region Класс
			public class CaseSettings
			{
				[JsonProperty("Номер кейса")] public int Number;
				[JsonProperty("Название кейса")] public string DisplayName;
				[JsonProperty("Информация кейса")] public string Info;
				[JsonProperty("Цена кейса")] public int Price = 0;
				[JsonProperty("Кулдаун")] public double Cooldown;
				[JsonProperty("Изображение кейса")] public string Url;
				[JsonProperty("Список предметов")] public List<ItemList> items;
			}
			public class ItemList
			{
				[JsonProperty("Название предмета или команды")] public string Name;
				[JsonProperty("Короткое название предмета")] public string ShortName;
				[JsonProperty("SkinID предмета")] public ulong SkinID;
				[JsonProperty("Дополнительная команда")] public string Command;
				[JsonProperty("Изображение")] public string Url;
				[JsonProperty("Шанс выпадения")] public int DropChance;
				[JsonProperty("Сколько нужно собрать одинаковых предметов?")] public int Count;
				[JsonProperty("Минимальное количество при выпадени")] public int AmountMin;
				[JsonProperty("Максимальное Количество при выпадени")] public int AmountMax;
				[JsonProperty("Сумма валюты TPEconomic")] public int MoneyAmount = 0;
			}
			public class CaseData
			{
				[JsonProperty("Список предметов игрока")] public Dictionary<string, DataSettings> Inventory = new Dictionary<string, DataSettings>();
				public Dictionary<int, int> Cooldowns = new();
			}
			public class DataSettings
			{
				[JsonProperty("ID предмета")] public int ID;
				[JsonProperty("Название предмета или команды")] public string Name;
				[JsonProperty("Короткое название предмета")] public string ShortName;
				[JsonProperty("SkinID предмета")] public ulong SkinID;
				[JsonProperty("Дополнительная команда")] public string Command;
				[JsonProperty("Изображение")] public string Url;
				[JsonProperty("Собранно одинаковых предметов или команд")] public int Amount;
				public Item GiveItem(BasePlayer player)
				{
					if (!string.IsNullOrEmpty(Command)) inst.Server.Command(Command.Replace("%STEAMID%", player.UserIDString));
					if (!string.IsNullOrEmpty(ShortName))
					{
						Item item = ItemManager.CreateByPartialName(ShortName, Amount);
						return item;
					}
					return null;
				}
				
			}
		#endregion
		
		#region Economy System
			/// <summary>
			/// Типы экономических плагинов
			/// </summary>
			public enum EconomicsType
			{
				Auto = -1,
				Economics = 0,
				ServerRewards = 1,
				IQEconomic = 2,
				Item = 3,
				TPEconomic = 4
			}
			
			/// <summary>
			/// Интерфейс для провайдеров экономических плагинов
			/// </summary>
			public interface IEconomyProvider
			{
				bool IsAvailable();
				decimal GetBalance(BasePlayer player);
				bool WithdrawBalance(BasePlayer player, decimal amount);
				void DepositBalance(BasePlayer player, decimal amount);
				string GetCurrencyName();
				string GetCurrencySymbol();
				EconomicsType GetEconomyType();
			}
			
			/// <summary>
			/// Конфигурация экономической системы
			/// </summary>
			public class EconomyConfiguration
			{
				[JsonProperty("Тип экономики (Auto=-1, Economics=0, ServerRewards=1, IQEconomic=2, Item=3, TPEconomic=4)")]
				public EconomicsType PreferredType { get; set; } = EconomicsType.TPEconomic;
				
				[JsonProperty("Название валюты")]
				public string CurrencyName { get; set; } = "₽";
				
				[JsonProperty("Символ валюты")]
				public string CurrencySymbol { get; set; } = "₽";
				
				[JsonProperty("Короткое название предмета для экономики предметов")]
				public string ItemShortname { get; set; } = "scrap";
				
				[JsonProperty("SkinID предмета для экономики предметов")]
				public ulong ItemSkinId { get; set; } = 0;
			}
			
			/// <summary>
			/// Менеджер экономической системы
			/// </summary>
			public class EconomyManager
			{
				private readonly TPCaseSystem plugin;
				private readonly Dictionary<EconomicsType, IEconomyProvider> providers;
				private IEconomyProvider activeProvider;
				
				public EconomyManager(TPCaseSystem plugin)
				{
					this.plugin = plugin;
					this.providers = new Dictionary<EconomicsType, IEconomyProvider>();
				}
				
				/// <summary>
				/// Регистрирует провайдер экономики
				/// </summary>
				public void RegisterProvider(IEconomyProvider provider)
				{
					providers[provider.GetEconomyType()] = provider;
				}
				
				/// <summary>
				/// Инициализирует экономическую систему
				/// </summary>
				public bool Initialize()
				{
					// Выбираем активный провайдер на основе конфигурации
					SelectActiveProvider();
					
					if (activeProvider == null)
					{
						plugin.PrintError("Не найден ни один доступный экономический плагин!");
						return false;
					}
					
					plugin.Puts($"Используется экономическая система: {activeProvider.GetEconomyType()}");
					return true;
				}
				
				/// <summary>
				/// Выбирает активный провайдер
				/// </summary>
				private void SelectActiveProvider()
				{
					var config = plugin.config.Economy;
					
					// Если указан конкретный тип экономики
					if (config.PreferredType != EconomicsType.Auto)
					{
						if (providers.ContainsKey(config.PreferredType) && providers[config.PreferredType].IsAvailable())
						{
							activeProvider = providers[config.PreferredType];
							return;
						}
						else
						{
							plugin.PrintWarning($"Указанный тип экономики {config.PreferredType} недоступен, переключаемся на автоматический выбор");
						}
					}
					
					// Автоматический выбор по приоритету
					var priorityOrder = new[] { EconomicsType.IQEconomic, EconomicsType.TPEconomic, EconomicsType.Economics, EconomicsType.ServerRewards, EconomicsType.Item };
					
					foreach (var type in priorityOrder)
					{
						if (providers.ContainsKey(type) && providers[type].IsAvailable())
						{
							activeProvider = providers[type];
							if (config.PreferredType != EconomicsType.Auto)
							{
								plugin.PrintWarning($"Переключились на {type} из-за недоступности {config.PreferredType}");
							}
							return;
						}
					}
				}
				
				/// <summary>
				/// Получает баланс игрока
				/// </summary>
				public decimal GetBalance(BasePlayer player)
				{
					return activeProvider?.GetBalance(player) ?? 0;
				}
				
				/// <summary>
				/// Списывает средства с баланса игрока
				/// </summary>
				public bool WithdrawBalance(BasePlayer player, decimal amount)
				{
					return activeProvider?.WithdrawBalance(player, amount) ?? false;
				}
				
				/// <summary>
				/// Начисляет средства на баланс игрока
				/// </summary>
				public void DepositBalance(BasePlayer player, decimal amount)
				{
					activeProvider?.DepositBalance(player, amount);
				}
				
				/// <summary>
				/// Получает название валюты
				/// </summary>
				public string GetCurrencyName()
				{
					var configName = plugin.config.Economy.CurrencyName;
					if (!string.IsNullOrEmpty(configName))
						return configName;
					
					return activeProvider?.GetCurrencyName() ?? "монет";
				}
				
				/// <summary>
				/// Получает символ валюты
				/// </summary>
				public string GetCurrencySymbol()
				{
					var configSymbol = plugin.config.Economy.CurrencySymbol;
					if (!string.IsNullOrEmpty(configSymbol))
						return configSymbol;
					
					return activeProvider?.GetCurrencySymbol() ?? "";
				}
				
				/// <summary>
				/// Проверяет, доступна ли экономическая система
				/// </summary>
				public bool IsAvailable()
				{
					return activeProvider != null && activeProvider.IsAvailable();
				}
			}
		#endregion
		
		#region Конфиг
			public Configuration config;
			public class Configuration
			{
				[JsonProperty("Список кейсов")] public List<CaseSettings> cases;
				[JsonProperty("Настройки экономики")] public EconomyConfiguration Economy = new EconomyConfiguration();
				public static Configuration GetNewCong()
				{
					return new Configuration
					{
						cases = new List<CaseSettings>
						{
							new CaseSettings
							{
								Number = 1,
								DisplayName = "Мешок",
								Info = "Вонючий, потрепанный мешок содержит в себе предметы не самой большой ценности, но среди всего этого барахла, можно найти и стоящие вещички.",
								Price = 100,
								Cooldown = 0,
								Url = "kW6iVdn.png",
								items = new List<ItemList>
								{
									new ItemList
									{
										Name = "gg",
										ShortName = "wood",
										SkinID = 0,
										Command = null,
										Url = null,
										DropChance = 100,
										Count = 0,
										AmountMin = 1000,
										AmountMax = 5000
									}
								}
							},
							new CaseSettings
							{
								Number = 2,
								DisplayName = "Ящик",
								Info = "Информация",
								Price = 200,
								Cooldown = 0,
								Url = "65eSjiO.png",
								items = new List<ItemList>
								{
									new ItemList
									{
										Name = "gg",
										ShortName = "wood",
										SkinID = 0,
										Command = null,
										Url = null,
										DropChance = 100,
										Count = 0,
										AmountMin = 1000,
										AmountMax = 5000
									}
								}
							},
							new CaseSettings
							{
								Number = 3,
								DisplayName = "Сумка",
								Info = "Информация",
								Price = 300,
								Cooldown = 0,
								Url = "nmrGCON.png",
								items = new List<ItemList>
								{
									new ItemList
									{
										Name = "gg",
										ShortName = "wood",
										SkinID = 0,
										Command = null,
										Url = null,
										DropChance = 100,
										Count = 0,
										AmountMin = 1000,
										AmountMax = 5000
									},
									new ItemList
									{
										Name = "Suka",
										ShortName = null,
										SkinID = 0,
										Command = "Suka bleat",
										Url = "qwpPcP3.png",
										DropChance = 50,
										Count = 5,
										AmountMin = 1,
										AmountMax = 1
									},
								}
							},
							new CaseSettings
							{
								Number = 4,
								DisplayName = "Бочка",
								Info = "Информация",
								Price = 400,
								Cooldown = 0,
								Url = "dEeCJst.png",
								items = new List<ItemList>
								{
									new ItemList
									{
										Name = "Камень",
										ShortName = "stones",
										
										SkinID = 0,
										Command = null,
										Url = null,
										DropChance = 100,
										Count = 0,
										AmountMin = 1000,
										AmountMax = 5000
									}
								}
								
							},
						},
						Economy = new EconomyConfiguration()
					};
				}
				
			}
			protected override void LoadConfig()
			{
				base.LoadConfig();
				try
				{
					config = Config.ReadObject<Configuration>();
					if (config?.cases == null) LoadDefaultConfig();
				}
				
				catch
				{
					PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
					LoadDefaultConfig();
				}
				
				NextTick(SaveConfig);
			}
			
			protected override void LoadDefaultConfig() => config = Configuration.GetNewCong();
			protected override void SaveConfig() => Config.WriteObject(config);
		#endregion
		
        #region Хуки
			private void OnLootEntity(BasePlayer player, BaseEntity entity) { }
			private void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player) { }
			private void OnEntityDeath(PatrolHelicopter entity, HitInfo info) { }
			private void OnEntityDeath(BaseEntity entity, HitInfo info) { }
			
			private void SetCooldown(ulong userID, int @case, double endTime)
			{
				if (!dataSettings[userID].Cooldowns.ContainsKey(@case))
                dataSettings[userID].Cooldowns.Add(@case, 0);
				
				dataSettings[userID].Cooldowns[@case] = (int)endTime;
			}
			
			private int GetCooldown(ulong userID, int @case)
			{
				if (!dataSettings[userID].Cooldowns.ContainsKey(@case))
                return -1;
				
				return dataSettings[userID].Cooldowns[@case] - Facepunch.Math.Epoch.Current;
			}
			private void OnServerInitialized()
			{
				inst = this;
				_imageUI = new ImageUI();
				// Запускаем загрузку предопределенных изображений
				_imageUI.DownloadImage();
				
				// Загружаем данные игроков
				if (Interface.Oxide.DataFileSystem.ExistsDatafile("TPCaseSystem/PlayerList"))
				{
					dataSettings = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, CaseData>>("TPCaseSystem/PlayerList");
				}
				else
				{
					dataSettings = new Dictionary<ulong, CaseData>();
				}
				
				// Подключаем всех активных игроков
				BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);
				
				// Логируем активную экономическую систему
				Puts($"Активная экономическая система: {GetActiveEconomyName()}");
				
				isReady = true;
				ServerMgr.Instance.StartCoroutine(UpdateTime());
			}
			
			private IEnumerator UpdateTime()
			{
				while (isReady)
				{
					foreach (var x in OpenedUIs)
					{
						if (x.Value == -1)
                        continue;
						
						UI_UpdateCooldown(x.Key, x.Value);
					}
					
					yield return CoroutineEx.waitForSeconds(1);
				}
			}
			
			private void OnPlayerConnected(BasePlayer player)
			{
				if (!dataSettings.ContainsKey(player.userID))
				{
					dataSettings.Add(player.userID, new CaseData
						{
							Inventory = new(),
							Cooldowns = new()
						});
				}
				SaveData();
			}
			
			private void Unload()
			{
				isReady = false;
				if (_imageUI != null)
				{
					_imageUI.UnloadImages();
					_imageUI = null;
				}
				inst = null;
				foreach (var player in BasePlayer.activePlayerList)
				{
					CuiHelper.DestroyUi(player, Layer);
					CuiHelper.DestroyUi(player, LayerHelp1);
					CuiHelper.DestroyUi(player, LayerInventory);
					CuiHelper.DestroyUi(player, LayerInventory + ".content");
				}
				SaveData();
			}
			
			private void SaveData()
			{
				Interface.Oxide.DataFileSystem.WriteObject("TPCaseSystem/PlayerList", dataSettings);
			}
			
			private DataSettings GetItem(ulong userID, string name)
			{
				if (!dataSettings.ContainsKey(userID))
                dataSettings[userID].Inventory = new Dictionary<string, DataSettings>();
				
				if (!dataSettings[userID].Inventory.ContainsKey(name))
                dataSettings[userID].Inventory[name] = new DataSettings();
				
				return dataSettings[userID].Inventory[name];
			}
			
			private void AddItem(BasePlayer player, ItemList itemList, int Count = 0)
			{
				var data = GetItem(player.userID, itemList.Name);
				var random = Core.Random.Range(1, 10000);
				if (random != null)
				{
					data.ID = random;
				}
				data.Name = itemList.Name;
				data.ShortName = itemList.ShortName;
				data.SkinID = itemList.SkinID;
				data.Command = itemList.Command;
				data.Url = itemList.Url;
				data.Amount += Count;
				if (itemList.MoneyAmount > 0)
				{
					DepositPlayerBalance(player, itemList.MoneyAmount);
					player.ChatMessage($"Вы получили {itemList.MoneyAmount} монет!");
				}
			}
			
			// Временные функции для работы с экономикой до полной реализации EconomyManager
			private decimal GetPlayerBalance(BasePlayer player)
			{
				// Проверяем доступные экономические плагины в порядке приоритета
				// Сначала пробуем IQEconomic
				if (IQEconomic != null)
				{
					try
					{
						var balance = IQEconomic?.Call("API_GET_BALANCE", player.userID);
						if (balance != null) 
						{
							return Convert.ToDecimal(balance);
						}
					}
					catch (Exception ex)
					{
						PrintWarning($"Ошибка IQEconomic: {ex.Message}");
					}
				}
				
				// Пробуем Economics
				if (Economics != null)
				{
					try
					{
						var balance = Economics?.Call("Balance", player.userID);
						if (balance != null) 
						{
							return Convert.ToDecimal(balance);
						}
					}
					catch (Exception ex)
					{
						PrintWarning($"Ошибка Economics: {ex.Message}");
					}
				}
				
				// Пробуем ServerRewards
				if (ServerRewards != null)
				{
					try
					{
						var balance = ServerRewards?.Call("CheckPoints", player.userID);
						if (balance != null) 
						{
							return Convert.ToDecimal(balance);
						}
					}
					catch (Exception ex)
					{
						PrintWarning($"Ошибка ServerRewards: {ex.Message}");
					}
				}
				
				// Пробуем TPEconomic
				if (TPEconomic != null)
				{
					try
					{
						var balance = TPEconomic?.Call("API_GET_BALANCE", player.userID);
						if (balance != null) 
						{
							return Convert.ToDecimal(balance);
						}
					}
					catch (Exception ex)
					{
						PrintWarning($"Ошибка TPEconomic: {ex.Message}");
					}
				}
				
				return 0;
			}
			
			private bool WithdrawPlayerBalance(BasePlayer player, decimal amount)
			{
				// Проверяем доступные экономические плагины в порядке приоритета
				if (IQEconomic != null)
				{
					IQEconomic?.Call("API_REMOVE_BALANCE", player.userID, amount);
					return true;
				}
				
				if (Economics != null)
				{
					var result = Economics?.Call("Withdraw", player.userID, (double)amount);
					return result != null && (bool)result;
				}
				
				if (ServerRewards != null)
				{
					var result = ServerRewards?.Call("TakePoints", player.userID, (int)amount);
					return result != null;
				}
				
				if (TPEconomic != null)
				{
					TPEconomic?.Call("API_PUT_BALANCE_MINUS", player.userID, (float)amount);
					return true;
				}
				
				return false;
			}
			
			private void DepositPlayerBalance(BasePlayer player, decimal amount)
			{
				// Проверяем доступные экономические плагины в порядке приоритета
				if (IQEconomic != null)
				{
					IQEconomic?.Call("API_SET_BALANCE", player.userID, GetPlayerBalance(player) + amount);
					return;
				}
				
				if (Economics != null)
				{
					Economics?.Call("Deposit", player.userID, (double)amount);
					return;
				}
				
				if (ServerRewards != null)
				{
					ServerRewards?.Call("AddPoints", player.userID, (int)amount);
					return;
				}
				
				if (TPEconomic != null)
				{
					TPEconomic?.Call("API_PUT_BALANCE_PLUS", player.userID, (float)amount);
					return;
				}
			}
			
			private string GetActiveEconomyName()
			{
				// Создаем тестового игрока для проверки (используем первого доступного)
				var testPlayer = BasePlayer.activePlayerList.FirstOrDefault();
				if (testPlayer == null) 
				{
					// Если нет игроков, просто проверяем наличие плагинов
					if (IQEconomic != null) return "IQEconomic";
					if (Economics != null) return "Economics";
					if (ServerRewards != null) return "ServerRewards";
					if (TPEconomic != null) return "TPEconomic";
					return "Неизвестно";
				}
				
				// Тестируем каждый плагин
				if (IQEconomic != null)
				{
					try
					{
						var balance = IQEconomic?.Call("API_GET_BALANCE", testPlayer.userID);
						if (balance != null) return "IQEconomic";
					}
					catch { }
				}
				
				if (Economics != null)
				{
					try
					{
						var balance = Economics?.Call("Balance", testPlayer.userID);
						if (balance != null) return "Economics";
					}
					catch { }
				}
				
				if (ServerRewards != null)
				{
					try
					{
						var balance = ServerRewards?.Call("CheckPoints", testPlayer.userID);
						if (balance != null) return "ServerRewards";
					}
					catch { }
				}
				
				if (TPEconomic != null)
				{
					try
					{
						var balance = TPEconomic?.Call("API_GET_BALANCE", testPlayer.userID);
						if (balance != null) return "TPEconomic";
					}
					catch { }
				}
				
				return "Неизвестно";
			}
		#endregion
		
        #region Команды
			
			private void UI_SendNotify(BasePlayer player, string text)
			{
				player.ChatMessage(text);
				var container = new CuiElementContainer();
				container.Add(new CuiButton
					{
						RectTransform = { AnchorMin = "0 0.005", AnchorMax = "0.7 0.25", OffsetMax = "0 0" },
						Button = { Color = "1 1 1 0" },
						Text = { Text = text, FadeIn = 0.2f, Color = "1 1 1 0.3", Align = TextAnchor.UpperLeft, FontSize = 11, Font = "robotocondensed-regular.ttf" }
					}, "Info", "Notify", "Notify");
					CuiHelper.AddUi(player, container);
			}
			private void ChatCase(BasePlayer player)
			{
				CaseUI(player);
				CaseInfoUI(player, 1);
				OpenedUIs.Remove(player);
				OpenedUIs.Add(player, 1);
			}
			
			[ConsoleCommand("case")]
			private void ConsoleCase(ConsoleSystem.Arg args)
			{
				try
				{
					var player = args.Player();
					if (player == null)
					{
						return;
					}
					if (args.Args == null || args.Args.Length == 0)
					{
						return;
					}
					if (dataSettings == null)
					{
						return;
					}
					if (!dataSettings.ContainsKey(player.userID) || dataSettings[player.userID] == null)
					{
						return;
					}
					if (OpenedUIs == null)
					{
						return;
					}
					if (!OpenedUIs.ContainsKey(player))
					{
						OpenedUIs[player] = -1;
					}
					CuiHelper.DestroyUi(player, LayerInventory + ".content");
					OpenedUIs[player] = -1;
					if (player != null && args.HasArgs(1))
					{
						if (args.Args[0] == "open")
						{
							OpenedUIs[player] = int.Parse(args.Args[1]);
							var check = config.cases.FirstOrDefault(z => z.Number == int.Parse(args.Args[1]));
							
							// Сначала проверяем кулдаун
							var cooldown = GetCooldown(player.userID, int.Parse(args.Args[1]));
							if (cooldown > 0)
							{
								var timeSpan = TimeSpan.FromSeconds(cooldown);
								UI_SendNotify(player, $"Кейс на откате! Осталось: {timeSpan.Hours} ч. {timeSpan.Minutes} м. {timeSpan.Seconds} с.");
								return;
							}
							
							if (check.Price > 0)
							{
								// Получаем текущий баланс игрока
								decimal balance = GetPlayerBalance(player);
								
								if (balance < check.Price)
								{
									UI_SendNotify(player, $"Недостаточно средств для открытия кейса! Необходимо: {check.Price}, у вас: {balance:F0}");
									return;
								}
								
								// Списываем деньги с баланса
								if (WithdrawPlayerBalance(player, check.Price))
								{
									player.ChatMessage($"С вашего баланса списано {check.Price} монет за открытие кейса.");
								}
								else
								{
									UI_SendNotify(player, $"Ошибка при списании средств! Попробуйте позже.");
									return;
								}
							}
							
							SetCooldown(player.userID, int.Parse(args.Args[1]), check.Cooldown + Facepunch.Math.Epoch.Current);
							ItemList item = null;
							var attempts = 0;
							while (item == null && attempts < 300)
							{
								var tempItem = check.items.GetRandom();
								attempts++;
								if (Core.Random.Range(0f, 100f) > tempItem.DropChance)
                                continue;
								item = tempItem;
							}
							if (item == null)
                            item = check.items.GetRandom();
							var amount = Core.Random.Range(item.AmountMin, item.AmountMax);
							AddItem(player, item, amount);
							SaveData();
							UI_SendNotify(player, item.Command != null ? $"<color=#8fde5b><size=16>Кейсы:</size></color>\nВы получили услугу: <color=#8fde5b>{item.Name}</color>" : $"<color=#8fde5b><size=16>Кейсы:</size></color>\nВы получили предмет: <color=#8fde5b>{item.Name}</color>\nВ размере: <color=#8fde5b>{amount}шт.</color>");
						}
						
						if (args.Args[0] == "close")
                        OpenedUIs.Remove(player);
						if (args.Args[0] == "ui" || args.Args[0] == "info")
						{
							CaseUI(player);
							CaseInfoUI(player, int.Parse(args.Args[1]));
							OpenedUIs[player] = int.Parse(args.Args[1]);
						}
						
						if (args.Args[0] == "content")
						{
							UI_DrawCaseContent(player, int.Parse(args.Args[1]));
						}
						if (args.Args[0] == "help1")
						{
							HelpUI1(player);
						}
						if (args.Args[0] == "inventory")
						{
							InventoryUI(player);
						}
						if (args.Args[0] == "take")
						{
							var item = dataSettings[player.userID].Inventory.FirstOrDefault(z => z.Value.ID == int.Parse(args.Args[1]));
							if (item.Value.ShortName != null)
							{
								if (player.inventory.containerMain.itemList.Count >= 24)
								{
									player.ChatMessage($"<color=#8fde5b><size=16>Кейсы:</size></color>\nУ вас <color=#8fde5b>недостаточно</color> места в основном инвентаре!");
									return;
								}
							}
							var text = item.Value.Command != null ? $"<color=#8fde5b><size=16>Кейсы:</size></color>\nВы получили услугу: <color=#8fde5b>{item.Value.Name}</color>" : $"<color=#8fde5b><size=16>Кейсы:</size></color>\nВы получили предмет: <color=#8fde5b>{item.Value.Name}</color>\nВ размере: <color=#8fde5b>{item.Value.Amount}шт.</color>";
							SendReply(player, text);
							item.Value.GiveItem(player)?.MoveToContainer(player.inventory.containerMain);
							dataSettings[player.userID].Inventory.Remove(item.Key);
							InventoryUI(player);
						}
					}
				}
				catch (Exception ex)
				{
					PrintError($"[ConsoleCase] Ошибка: {ex.Message}\n{ex.StackTrace}");
				}
			}
		#endregion
		
        #region Интерфейс
			
			#region Основной
				void CaseUI(BasePlayer player)
				{
					CuiElementContainer container = new CuiElementContainer();
					
					container.Add(new CuiElement
						{
							Name = Layer,
							Parent = ".Mains",
							Components =
							{
								new CuiRawImageComponent { Png = _imageUI.GetImage("MAIN_FON") },
								new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
							}
						});
						
						container.Add(new CuiButton
							{
								RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
								Button = { Close = "Menu_UI", Color = "0 0 0 0" },
								Text = { Text = "" }
							}, Layer);
							
							container.Add(new CuiButton
								{
									RectTransform = { AnchorMin = "0.253 0.255", AnchorMax = "0.485 0.67", OffsetMax = "0 0" },
									Button = { Color = "0 0 0 0" },
									Text = { Text = "" }
								}, Layer, "Case");
								
								container.Add(new CuiButton
									{
										RectTransform = { AnchorMin = "0.493 0.255", AnchorMax = "0.745 0.66", OffsetMax = "0 0" },
										Button = { Color = "0 0 0 0" },
										Text = { Text = "" }
									}, Layer, "Inf");
									
									float width = 0.463f, height = 0.46f, startxBox = 0.027f, startyBox = 0.969f - height, xmin = startxBox, ymin = startyBox;
									foreach (var check in config.cases)
									{
										container.Add(new CuiButton
											{
												RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMin = "2 2", OffsetMax = "-2 -2" },
												Button = { Color = "0 0 0 0" },
												Text = { Text = $"" }
											}, "Case", "Options");
											xmin += width + 0.014f;
											if (xmin + width >= 1)
											{
												xmin = startxBox;
												ymin -= height + 0.014f;
											}
											
											container.Add(new CuiElement
												{
													Parent = "Options",
													Components =
													{
														new CuiRawImageComponent { Png = (string) _imageUI.GetImage(check.Url), FadeIn = 0.5f },
														new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "14 14", OffsetMax = "-14 -14" }
													}
												});
												
												container.Add(new CuiButton
													{
														RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
														Button = { Color = "0 0 0 0", Command = $"case info {check.Number}" },
														Text = { Text = "" }
													}, "Options");
													
													var cooldownSeconds = GetCooldown(player.userID, check.Number);
													if (cooldownSeconds > 0)
													{
														var timeSpan = TimeSpan.FromSeconds(cooldownSeconds);
														
														container.Add(new CuiLabel()
															{
																Text = { Text = $"Осталось: {timeSpan.Hours} ч. {timeSpan.Minutes} м. {timeSpan.Seconds} с.", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.LowerCenter, Color = "1 1 1 0.3" },
																RectTransform = { AnchorMin = "0 0.89", AnchorMax = "1 1", OffsetMax = "0 0" }
															}, "Options", "cooldowns");
													}
									}
									
									CuiHelper.AddUi(player, container);
				}
			#endregion
			
			#region Информация о кейсе
				private void CaseInfoUI(BasePlayer player, int Number)
				{
					CuiElementContainer container = new CuiElementContainer();
					var check = config.cases.FirstOrDefault(z => z.Number == Number);
					
					container.Add(new CuiButton
						{
							RectTransform = { AnchorMin = "0.005 0.04", AnchorMax = "0.995 0.96", OffsetMax = "0 0" },
							Button = { Color = "0 0 0 0" },
							Text = { Text = "" }
						}, "Inf", "Info", "Info");
						
						container.Add(new CuiButton
							{
								RectTransform = { AnchorMin = "0.34 0.81", AnchorMax = "0.97 0.91", OffsetMax = "0 0" },
								Button = { Color = "0 0 0 0" },
								Text = { Text = $"{check.DisplayName}\n\n", Color = "1 1 1 0.15", Align = TextAnchor.UpperLeft, FontSize = 14, Font = "robotocondensed-regular.ttf" }
							}, "Info");
							
							container.Add(new CuiButton
								{
									RectTransform = { AnchorMin = "0.34 0.55", AnchorMax = "0.96 0.81", OffsetMax = "0 0" },
									Button = { Color = "0 0 0 0" },
									Text = { Text = $"{check.Info}", Color = "1 1 1 0.15", Align = TextAnchor.UpperLeft, FontSize = 10, Font = "robotocondensed-regular.ttf" }
								}, "Info");
								
								container.Add(new CuiPanel
									{
										RectTransform = { AnchorMin = "0.03 0.56", AnchorMax = "0.33 0.97", OffsetMax = "0 0" },
										Image = { Color = "0 0 0 0" }
									}, "Info", "Image");
									
									container.Add(new CuiElement
										{
											Parent = "Image",
											Components =
											{
												new CuiRawImageComponent { Png = !string.IsNullOrEmpty(check.Url) ? _imageUI.GetImage(check.Url) : null, FadeIn = 0.5f },
												new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 15", OffsetMax = "-5 -15" }
											}
										});
										
										container.Add(new CuiButton
											{
												RectTransform = { AnchorMin = "0 0.408", AnchorMax = "0.484 0.502", OffsetMax = "0 0" },
												Button = { Color = "0 0 0 0", Command = "case inventory" },
												Text = { Text = $"         Склад", Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, FontSize = 11, Font = "robotocondensed-regular.ttf" }
											}, "Info");
											
											container.Add(new CuiButton
												{
													RectTransform = { AnchorMin = "0 0.283", AnchorMax = "0.484 0.38", OffsetMax = "0 0" },
													Button = { Color = "0 0 0 0", Command = "case help1" },
													Text = { Text = $"         Помощь", Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, FontSize = 11, Font = "robotocondensed-regular.ttf" }
												}, "Info");
												container.Add(new CuiButton
													{
														RectTransform = { AnchorMin = "0.51 0.408", AnchorMax = "0.992 0.502", OffsetMax = "0 0" },
														Button = { Color = "0 0 0 0", Command = $"case content {Number}" },
														Text = { Text = $"           Содержимое", Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, FontSize = 11, Font = "robotocondensed-regular.ttf" }
													}, "Info");
													
													container.Add(new CuiButton
														{
															RectTransform = { AnchorMin = "0.51 0.283", AnchorMax = "0.992 0.38", OffsetMax = "0 0" },
															Button = { Color = "0 0 0 0", Command = $"case open {Number}" },
															Text = { Text = $"           Купить", Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, FontSize = 11, Font = "robotocondensed-regular.ttf" }
														}, "Info");
														
														container.Add(new CuiButton
															{
																RectTransform = { AnchorMin = "0.695 0.677", AnchorMax = "0.75 0.705", OffsetMax = "0 0" },
																Button = { Color = "1 1 1 0" },
																Text = { Text = $"Цена: {check.Price}", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf" }
															}, Layer);
															
															CuiHelper.AddUi(player, container);
															
															UI_UpdateCooldown(player, Number);
				}
				
				private void UI_UpdateCooldown(BasePlayer player, int number)
				{
					var container = new CuiElementContainer();
					var cooldownSeconds = GetCooldown(player.userID, number);
					if (cooldownSeconds > 0)
					{
						var timeSpan = TimeSpan.FromSeconds(cooldownSeconds);
						
						container.Add(new CuiLabel()
							{
								Text = { Text = $"Осталось: {timeSpan.Hours} ч. {timeSpan.Minutes} м. {timeSpan.Seconds} с.", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.LowerCenter, Color = "1 1 1 0.3" },
								RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1.1", OffsetMax = "0 0" }
							}, "Image", "cooldown", "cooldown");
					}
					CuiHelper.AddUi(player, container);
				}
			#endregion
			
			#region Помощь
				private void HelpUI1(BasePlayer player)
				{
					CuiElementContainer container = new CuiElementContainer();
					
					container.Add(new CuiElement
						{
							Name = LayerHelp1,
							Parent = Layer,
							Components =
							{
								new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_HELP") },
								new CuiRectTransformComponent { AnchorMin = "0.27 0.33", AnchorMax = "0.69 0.72", OffsetMax = "0 0" },
							}
						});
						
						container.Add(new CuiButton
							{
								RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
								Button = { Color = "1 1 1 0" },
								Text = { Text = $"<color=#8fde5b><size=16>Система кейсов</size></color>\n\nОткрывайте кейсы за игровую валюту и получайте случайные награды!\nДля покупки и открытия кейса выберите его в меню и нажмите <color=#8fde5b><size=16>КУПИТЬ</size></color>.\nВсе предметы попадают на ваш склад — забрать их можно в любое время.\n\nЕсли не хватает <color=#8fde5b><size=16>DropCoin</size></color> можете купить в нашем магазине\nМагазин - <color=#8fde5b><size=16>dropshop.gamestores.app</size></color>", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
							}, LayerHelp1);
							
							container.Add(new CuiLabel
								{
									RectTransform = { AnchorMin = "0.05 0.855", AnchorMax = "0.9 0.925", OffsetMax = "0 0" },
									Text = { Text = "Помощь", Color = "1 1 1 0.3", Align = TextAnchor.MiddleLeft, FontSize = 11, Font = "robotocondensed-regular.ttf" }
								}, LayerHelp1);
								
								container.Add(new CuiButton
									{
										RectTransform = { AnchorMin = "0.928 0.855", AnchorMax = "0.962 0.925" },
										Button = { Close = LayerHelp1, Color = "0 0 0 0" },
										Text = { Text = "" }
									}, LayerHelp1);
									
									CuiHelper.AddUi(player, container);
				}
			#endregion
			
			private void UI_DrawCaseContent(BasePlayer player, int number)
			{
				var @case = config.cases.FirstOrDefault(x => x.Number == number);
				CuiHelper.DestroyUi(player, LayerInventory + ".content");
				var container = new CuiElementContainer();
				
				container.Add(new CuiElement
					{
						Name = LayerInventory + ".content",
						Parent = Layer,
						Components =
						{
							new CuiRawImageComponent { Png = _imageUI.GetImage("CONTENT_INVENTORY") },
							new CuiRectTransformComponent { AnchorMin = "0.24 0.23", AnchorMax = "0.77 0.74", OffsetMax = "0 0" },
						}
					});
					
					container.Add(new CuiButton
						{
							RectTransform = { AnchorMin = "0.03 0.035", AnchorMax = "0.97 0.86", OffsetMax = "0 0" },
							Button = { Color = "0 0 0 0" },
							Text = { Text = "" }
						}, LayerInventory + ".content", "Content");
						container.Add(new CuiLabel()
							{
								Text = { Text = $"Что может выпасть из кейса {@case.DisplayName}", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 11, Color = "1 1 1 0.3" },
								RectTransform = { AnchorMin = "0.05 0.883", AnchorMax = "0.8 0.955", OffsetMax = "0 0" }
							}, LayerInventory + ".content");
							
							container.Add(new CuiButton()
								{
									Button = { Color = "0 0 0 0", Close = LayerInventory + ".content" },
									Text = { Text = "", Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
									RectTransform = { AnchorMin = "0.94 0.883", AnchorMax = "0.972 0.955", OffsetMax = "0 0" }
								}, LayerInventory + ".content");
								
								const int columns = 5;
								const int maxRows = 5;
								const float left = 0.02f;
								const float right = 0.98f;
								const float top = 0.95f;
								const float bottom = 0.05f;
								
								float widthAvail = right - left;
								float heightAvail = top - bottom;
								
								float cellWidth = widthAvail / columns;
								float cellHeight = heightAvail / maxRows;
								
								int maxSlots = columns * maxRows;
								int index = 0;
								
								foreach (var x in @case.items.Take(maxSlots))
								{
									int col = index % columns;
									int row = index / columns; 
									
									float xMin = left + col * cellWidth;
									float xMax = xMin + cellWidth;
									float yMax = top - row * cellHeight;
									float yMin = yMax - cellHeight;
									
									var slotName = $"Content.{index}";
									
									container.Add(new CuiElement
										{
											Name = slotName,
											Parent = "Content",
											Components =
											{
												new CuiImageComponent { Color = "0 0 0 0" },
												new CuiRectTransformComponent
												{
													AnchorMin = $"{xMin} {yMin}",
													AnchorMax = $"{xMax} {yMax}"
												}
											}
										});
										
										string icon = null;
										if (!string.IsNullOrEmpty(x.Url))
										{
											icon = _imageUI.GetImage(x.Url);
										}
										if (icon == null && !string.IsNullOrEmpty(x.ShortName) && ImageLibrary != null)
										{
											// Пробуем с SkinID
											icon = (string)ImageLibrary.Call("GetImage", x.ShortName, x.SkinID);
											
											// Если не нашли с SkinID, пробуем без SkinID
											if (icon == null)
											{
												icon = (string)ImageLibrary.Call("GetImage", x.ShortName, 0UL);
											}
											
											// Если это stones, пробуем stone
											if (icon == null && x.ShortName == "stones")
											{
												icon = (string)ImageLibrary.Call("GetImage", "stone", 0UL);
											}
										}
										
										if (!string.IsNullOrEmpty(icon))
										{
											container.Add(new CuiElement
												{
													Parent = slotName,
													Components =
													{
														new CuiRawImageComponent { Png = icon, FadeIn = 0.5f },
														new CuiRectTransformComponent
														{
															AnchorMin = "0.5 0.5",
															AnchorMax = "0.5 0.5",
															OffsetMin = "-25 -25",
															OffsetMax = "25 25"
														}
													}
												});
										}
										
										container.Add(new CuiButton
											{
												RectTransform =
												{
													AnchorMin = "0.5 0.5",
													AnchorMax = "0.5 0.5",
													OffsetMin = "-25 -25",
													OffsetMax = "25 25"
												},
												Button = { Color = "0 0 0 0" },
												Text = {
													Text = x.Name + (x.AmountMin > 1 ? $" x{x.AmountMin}" : ""),
													Color = "1 1 1 1",
													Align = TextAnchor.LowerCenter,
													FontSize = 8,
													Font = "robotocondensed-regular.ttf"
												}
											}, slotName);
											
											index++;
								}
								
								CuiHelper.AddUi(player, container);
			}
			
			[ConsoleCommand("tfill")]
			private void cmdTfill(ConsoleSystem.Arg arg)
			{
				if (arg.Player() != null && !arg.Player().IsAdmin)
                return;
				
				for (int i = 0; i < 50; i++)
				{
					config.cases[0].items.Add(new()
						{
							Name = Guid.NewGuid().ToString(),
							ShortName = ItemManager.itemList.GetRandom().shortname,
							SkinID = 0,
							Command = null,
							Url = null,
							DropChance = 0,
							Count = 1,
							AmountMin = 1,
							AmountMax = 3
						});
				}
			}
			void InventoryUI(BasePlayer player, int page = 0)
			{
				CuiHelper.DestroyUi(player, LayerInventory);
				CuiElementContainer container = new CuiElementContainer();
				
				container.Add(new CuiElement
					{
						Name = LayerInventory,
						Parent = Layer,
						Components =
						{
							new CuiRawImageComponent { Png = _imageUI.GetImage("CONTENT_INVENTORY") },
							new CuiRectTransformComponent { AnchorMin = "0.24 0.23", AnchorMax = "0.77 0.74", OffsetMax = "0 0" },
						}
					});
					
					container.Add(new CuiButton
						{
							RectTransform = { AnchorMin = "0.03 0.035", AnchorMax = "-0.97 0.86", OffsetMax = "0 0" },
							Button = { Color = "0 0 0 0" },
							Text = { Text = "" }
						}, LayerInventory, "Inventory");
						container.Add(new CuiLabel()
							{
								Text = { Text = $"Инвентарь", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 11, Color = "1 1 1 0.3" },
								RectTransform = { AnchorMin = "0.05 0.883", AnchorMax = "0.8 0.955", OffsetMax = "0 0" }
							}, LayerInventory);
							
							container.Add(new CuiButton()
								{
									Button = { Color = "0 0 0 0", Close = LayerInventory },
									Text = { Text = "", Align = TextAnchor.MiddleCenter, Color = "0 0 0 0" },
									RectTransform = {  AnchorMin = "0.94 0.883", AnchorMax = "0.972 0.955", OffsetMax = "0 0" }
								}, LayerInventory);
								container.Add(new CuiElement()
									{
										Name = "Inventory" + ".place",
										Parent = "Inventory",
										Components =
										{
											new CuiScrollViewComponent
											{
												Vertical = true,
												Horizontal = false,
												MovementType = ScrollRect.MovementType.Unrestricted,
												Elasticity = 0,
												Inertia = false,
												DecelerationRate = 0,
												ScrollSensitivity = 20,
												ContentTransform = new()
												{
													AnchorMin = "0 1",
													AnchorMax = "0 1"
												},
												HorizontalScrollbar = null,
												VerticalScrollbar = null
											},
											new CuiRectTransformComponent()
											{
												AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -285", OffsetMax = "584 0"
											}
										}
									});
									
									float minx = 5;
									float maxx = 115;
									
									float miny = -117;
									float maxy = 0;
									
									int i = 0;
									
									foreach (var check in dataSettings[player.userID].Inventory)
									{
										if (i != 0 && i % 5 == 0)
										{
											miny -= 123;
											maxy -= 123;
											
											minx = 5;
											maxx = 115;
										}
										container.Add(new CuiElement()
											{
												Name = "Inventory" + ".place" + $".{i}",
												Parent = "Inventory" + ".place",
												Components =
												{
													new CuiImageComponent() { Color = "0 0 0 0.3" },
													new CuiRectTransformComponent()
													{
														AnchorMin = "0 1", AnchorMax = "0 1",
														OffsetMin = $"{minx} {miny}", OffsetMax = $"{maxx} {maxy}"
													}
												}
											});
											string invIcon = null;
											if (!string.IsNullOrEmpty(check.Value.Url))
											invIcon = _imageUI.GetImage(check.Value.Url);
											if (invIcon == null && !string.IsNullOrEmpty(check.Value.ShortName) && ImageLibrary != null)
											invIcon = (string)ImageLibrary.Call("GetImage", check.Value.ShortName, check.Value.SkinID);
											container.Add(new CuiElement
												{
													Parent = "Inventory" + ".place" + $".{i}",
													Components =
													{
														new CuiRawImageComponent { Png = invIcon, FadeIn = 0.5f },
														new CuiRectTransformComponent { AnchorMin = "0.175 0.175", AnchorMax = "0.825 0.825", OffsetMax = "0 0" }
													}
												});
												
												container.Add(new CuiButton
													{
														RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
														Button = { Color = "0 0 0 0", Command = $"case take {check.Value.ID}" },
														Text = { Text = $"{check.Value.Amount}", Color = "1 1 1 0.5", Align = TextAnchor.LowerRight, FontSize = 12, Font = "robotocondensed-regular.ttf" }
													}, "Inventory" + ".place" + $".{i}");
													
													maxx += 116;
													minx += 116;
													i++;
									}
									
									foreach (var x in container)
									{
										foreach (var y in x.Components)
										{
											if (y is CuiScrollViewComponent scroll)
											scroll.ContentTransform.OffsetMin = $"0 {Mathf.Clamp(miny, float.MinValue, -300)}";
										}
									}
									
									CuiHelper.AddUi(player, container);
			}
		#endregion
		
        #region Images
			private static ImageUI _imageUI;
			/// <summary>
			/// Класс для управления загрузкой и хранением изображений
			/// </summary>
			public class ImageUI
			{
				// Путь к папке с изображениями относительно data директории
				private const String _path = "TPSystem/TPCase/";
				// Полный путь для отображения в сообщениях об ошибках
				private const String _printPath = "data/" + _path;
				
				// Словарь всех изображений, используемых плагином
				public readonly Dictionary<String, ImageData> _images = new()
				{
					{ "MAIN_FON", new ImageData() },              // Основной фон интерфейса
					{ "BACKGROUND_HELP", new ImageData() },       // Фон окна помощи
					{ "CONTENT_INVENTORY", new ImageData() },     // Фон инвентаря
					{ "BACKGROUND_ITEM", new ImageData() },       // Фон предмета
					{ "kW6iVdn.png", new ImageData() },          // Изображение кейса "Мешок"
					{ "65eSjiO.png", new ImageData() },          // Изображение кейса "Ящик"
					{ "nmrGCON.png", new ImageData() },          // Изображение кейса "Сумка"
					{ "wood.png", new ImageData() },          // Изображение предмета из кейса
					{ "stone.png", new ImageData() },          // Изображение предмета из кейса
					{ "dEeCJst.png", new ImageData() }           // Изображение кейса "Бочка"
				};
				
				/// <summary>
				/// Статусы загрузки изображений
				/// </summary>
				public enum ImageStatus
				{
					NotLoaded,  // Не загружено
					Loaded,     // Загружено успешно
					Failed      // Ошибка загрузки
				}
				
				/// <summary>
				/// Данные изображения
				/// </summary>
				public class ImageData
				{
					public ImageStatus Status = ImageStatus.NotLoaded;
					public string Id { get; set; }  // ID изображения в файловом хранилище
				}
				
				/// <summary>
				/// Получает ID загруженного изображения по имени
				/// </summary>
				/// <param name="name">Имя изображения</param>
				/// <returns>ID изображения или null, если не найдено</returns>
				public string GetImage(string name)
				{
					ImageData image;
					if (_images.TryGetValue(name, out image) && image.Status == ImageStatus.Loaded)
						return image.Id;
					return null;
				}
				
				public void DownloadImage()
				{
					// Ищем первое изображение, которое еще не загружено
					KeyValuePair<string, ImageData>? image = null;
					foreach (KeyValuePair<string, ImageData> img in _images)
					{
						if (img.Value.Status == ImageStatus.NotLoaded)
						{
							image = img;
							break;
						}
					}
					
					// Если есть изображение для загрузки, запускаем процесс
					if (image != null)
					{
						ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value));
					}
					else
					{
						// Проверяем, есть ли изображения, которые не удалось загрузить
						List<String> failedImages = new List<string>();
						
						foreach (KeyValuePair<String, ImageData> img in _images)
						{
							if (img.Value.Status == ImageStatus.Failed)
							{
								failedImages.Add(img.Key);
							}
						}
						
						// Если есть неудачные загрузки, выводим ошибку и выгружаем плагин
						if (failedImages.Count > 0)
						{
							String images = String.Join(", ", failedImages);
							inst.PrintError($"Не удалось загрузить следующие изображения: {images}. Возможно, вы не загрузили их в папку '{_printPath}'.");
							Interface.Oxide.UnloadPlugin(inst.Name);
						}
						else
						{
							// Все изображения загружены успешно
							inst.Puts($"{_images.Count} изображений успешно загружено!");
						}
					}
				}
				
				/// <summary>
				/// Выгружает все загруженные изображения из памяти сервера
				/// </summary>
				public void UnloadImages()
				{
					// Удаляем каждое загруженное изображение из файлового хранилища
					foreach (KeyValuePair<string, ImageData> item in _images)
						if (item.Value.Status == ImageStatus.Loaded)
							if (item.Value?.Id != null)
								FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
					
					// Очищаем словарь изображений
					_images?.Clear();
				}
				
				private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
				{
					// Создаем путь к локальному файлу изображения
					string fileName = image.Key;
					
					// Если это не системное изображение (содержит .png), используем имя как есть
					// Если это системное изображение, добавляем расширение .png
					if (!fileName.Contains(".png"))
					{
						fileName += ".png";
					}
					
					string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + _path + fileName;
					
					// Загружаем изображение с помощью UnityWebRequest
					using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
					{
						yield return www.SendWebRequest();
						
						// Проверяем на ошибки загрузки
						if (www.isNetworkError || www.isHttpError)
						{
							image.Value.Status = ImageStatus.Failed;
							inst.PrintWarning($"Не удалось загрузить изображение {image.Key}: {www.error}");
						}
						else
						{
							try
							{
								// Получаем текстуру из загруженных данных
								Texture2D tex = DownloadHandlerTexture.GetContent(www);
								if (tex != null)
								{
									// Сохраняем изображение в файловое хранилище сервера
									image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
									image.Value.Status = ImageStatus.Loaded;
									inst.Puts($"Изображение {image.Key} успешно загружено");
									UnityEngine.Object.DestroyImmediate(tex);
								}
								else
								{
									image.Value.Status = ImageStatus.Failed;
									inst.PrintWarning($"Не удалось получить текстуру для изображения {image.Key}");
								}
							}
							catch (System.Exception ex)
							{
								image.Value.Status = ImageStatus.Failed;
								inst.PrintError($"Ошибка при обработке изображения {image.Key}: {ex.Message}");
							}
						}
						
						// Продолжаем загрузку следующего изображения
						DownloadImage();
					}
				}
			}
		#endregion
		
        /// <summary>
        /// Создает путь к локальному изображению кейса
        /// </summary>
        /// <param name="fileName">Имя файла изображения</param>
        /// <returns>Полный путь к файлу или null, если имя файла пустое</returns>
        private string GetLocalCaseImage(string fileName)
        {
            // Проверяем, что имя файла не пустое
            if (string.IsNullOrEmpty(fileName)) 
                return null;
            
            // Убираем расширение .png, если оно есть
            var name = fileName;
            if (name.EndsWith(".png")) 
                name = name.Substring(0, name.Length - 4);
            
            // Создаем полный путь к файлу изображения
            return "file://" + Interface.Oxide.DataDirectory + "/TPSystem/TPCase/" + name + ".png";
		}
		
	}
}