
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

namespace Oxide.Plugins
{
	[Info("StripesEvent", "pluginfuel.ru", "20.0.2")]
	public class StripesEvent : RustPlugin
	{
		[PluginReference] private Plugin Notifications, ImageLibrary;

		private static StripesEvent _;

		public enum TypeEvent
		{
			Gather = 1,
			Kill = 3,
			Loot = 4,
			Craft = 5,
			GatherBonus = 2
		}


		public Dictionary<ulong, List<DataEvent>> DataFile = new Dictionary<ulong, List<DataEvent>>();

		public class DataEvent
		{
			public string EventName;
			public string ShortNameTarget;
			public int EventType;
			public int AmountTarget;
			public int AmountSuc;
			public bool IsSuc;
		}


		public class ItemStripes
		{
			[JsonProperty("ШортНейм предмета или команда ( Аргумент {steamid} в команде заменит на steamid64 игрока! )")]
			public string PrizeEvent;

			[JsonProperty("СкинИД предмета")]
			public ulong PrizeSkinID;

			[JsonProperty("Количество предмета")]
			public int Amount;

			[JsonProperty("Выполнять команду или же выдача предмета? ( true - Команда // false - Предмет )")]
			public bool ItemOrCommands;
		}

		public class EventSettings
		{
			[JsonProperty("Название нашивки")]
			public string NameStripes;

			[JsonProperty("Картинка")]
			public string ImageStripes;

			[JsonProperty("Тип нашивки ( 5 видов - 1 - Gather, 2 - GatherBonus, 3 - Kill, 4 - Loot, 5 - Craft )")]
			public int TypeStripes;

			[JsonProperty("Предмет, который необходимо добыть, сломать, залутать, или же скрафтить")]
			public string TargetItem;

			[JsonProperty("Количество предмета, которое необходимо добыть, сломать, залутать, или же скрафтить")]
			public int TargetAmount;

			[JsonProperty("Описание нашивки")]
			public string CommentsSripes;

			[JsonProperty("Описание призов")]
			public string CommentsPrize;

			[JsonProperty("Призы, которые получит игрок после выполнения данной нашивки")]
			public List<ItemStripes> ListPrize { get; set; }

		}

		public class VKSettings
		{
			[JsonProperty("Оповещать ли администрации о полностью пройденном ивенте в ВК?")]
			public bool NotificationAdmins;

			[JsonProperty("Вк админов ( ПРИМЕР: 1111111,222222,33333 )")]
			public string ListAdmins;

			[JsonProperty("VK TOKEN")] public string VKToken;

			[JsonProperty("Текст оповещения")]
			public string MessageNotification;
		}


		public class MainSettings
		{
			[JsonProperty("Включить выдачу баланса на магазин? ( Тут 2 выбора - либо после выполнения нашивок вам в вк отправляется сообщение, потом уже вы перекидываете ему реальные деньги, либо же выдается баланс на магазин АВТОМАТИЧЕСКИ )")]
			public bool GiveBalance;

			[JsonProperty("Номер магазина! ( для GameStores )")]
			public string ShopID;

			[JsonProperty("Секретный ключ ( для GameStores )")]
			public string APIKey;

			[JsonProperty("Количество рублей, которое будет выдаваться при сдаче всех нашивок")]
			public int MoneyRub;

			[JsonProperty("Оповещать ли игрока через плагин Notifications (RustPlugin.ru) о том, что он выполнил нашивку?")]
			public bool Notif;

			[JsonProperty("Текст оповещения")]
			public string DescNotif;

			[JsonProperty("Картинка оповещения")]
			public string ImageNotif;

			[JsonProperty("Текст возле кнопки")]
			public string TextInButton;

			[JsonProperty("Вайп даты")]
			public bool WipeData;

			[JsonProperty("Сообщение, когда игрок сдает одну нашивку")]
			public string MessageToTake;

			[JsonProperty("Сообщение, когда игрок сдал все нашивки")]
			public string MessageToAllTake;
		}


		private ConfigData _config;

		class ConfigData
		{
			[JsonProperty("Настройка плагина")]
			public MainSettings SettingsMain;

			[JsonProperty("Настройка оповещений")]
			public VKSettings SettingsVK;

			[JsonProperty("Настройка нашивок")]
			public List<EventSettings> SettingsStipes;

			[JsonProperty("Текст помощи")] public string HelpText;

			public static ConfigData GetNewCong()
			{
				ConfigData newConfig = new ConfigData();
				newConfig.HelpText = "TEST HELP 123\n321\nTEST HELP 321";
				newConfig.SettingsMain = new MainSettings()
				{
					GiveBalance = true,
					ShopID = "SHOPID",
					APIKey = "KEY",
					Notif = true,
					DescNotif = "",
					ImageNotif = "",
					TextInButton = "Откройте все {1} нашивок и нажмите кнопку ''ПОЛУЧИТЬ''",
					WipeData = true,
					MoneyRub = 1500,
					MessageToTake = "Поздравляю! Вы прошли нашивку - {1} и получили приз с него!",
					MessageToAllTake = "Поздравляю! Вы полностью прошли все нашивки, и заслужили приз. Проверьте баланса магазина, или отпишите администратору, что бы он выдал вам приз!"
				};
				newConfig.SettingsVK = new VKSettings()
				{
					NotificationAdmins = false,
					ListAdmins = "11111, 11111",
					VKToken = "VK TOKEN",
					MessageNotification = "Игрок {1} выполнил все нашивки и нажал кнопку ЗАБРАТЬ",
				};
				newConfig.SettingsStipes = new List<EventSettings>()
				{
					new EventSettings()
					{
						NameStripes = "Приготовления к рейду",
						ImageStripes = "https://i.imgur.com/20xxq2k.png",
						TypeStripes = 2,
						TargetAmount = 50,
						TargetItem = "sulfur.ore",
						CommentsSripes = "Добыть 50 твёрдой серной породы",
						CommentsPrize = "Награда: Набор ресурсов",
						ListPrize = new List<ItemStripes>()
						{
							new ItemStripes()
							{
								PrizeEvent = "scrap",
								Amount = 500,
								PrizeSkinID = 0,
								ItemOrCommands = false,
							},
							new ItemStripes()
							{
								PrizeEvent = "sulfur",
								Amount = 1500,
								PrizeSkinID = 0,
								ItemOrCommands = false,
							},
							new ItemStripes()
							{
								PrizeEvent = "metal.fragments",
								Amount = 2500,
								PrizeSkinID = 0,
								ItemOrCommands = false
							}
						}
					},
				};
				return newConfig;
			}
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<ConfigData>();
			}
			catch
			{
				LoadDefaultConfig();
			}

			NextTick(SaveConfig);
		}

		protected override void LoadDefaultConfig() => _config = ConfigData.GetNewCong();
		protected override void SaveConfig() => Config.WriteObject(_config);



		void OnNewSave()
		{
			if (_config.SettingsMain.WipeData)
			{
				DataFile?.Clear();
				DataFile = new Dictionary<ulong, List<DataEvent>>();
				Interface.Oxide.DataFileSystem.WriteObject(Name, DataFile);
				Interface.Oxide.ReloadPlugin(Name);
			}
		}
		void OnServerInitialized()
		{
			try
			{
				DataFile = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, List<DataEvent>>>(Name);
			}
			catch (Exception ex)
			{

				PrintError($"Failed to load stripes file (is the file corrupt?). Please contact developer. Error: ({ex.Message})");
				DataFile = new Dictionary<ulong, List<DataEvent>>();
			}

			_ = this;

			_imageUI = new ImageUI();
			_imageUI.DownloadImage();

			foreach (var variable in BasePlayer.activePlayerList)
			{
				OnPlayerConnected(variable);
			}

			foreach (var key in _config.SettingsStipes)
			{
				ImageLibrary.Call("AddImage", key.ImageStripes, key.ImageStripes);
			}

			if (_config.SettingsMain.Notif)
			{
				Notifications?.Call("AddImage", "stripes", _config.SettingsMain.ImageNotif);
			}

			timer.Every(60, () =>
			{
				if (DataFile != null)
					Interface.Oxide.DataFileSystem.WriteObject(Name, DataFile);
			});

		}


		void EventProcess(BasePlayer player)
		{
			foreach (var variable in _config.SettingsStipes)
			{
				var data = DataFile[player.userID];
				var find = data.FirstOrDefault(p => p.EventName == variable.NameStripes);
				if (find == null)
				{
					data.Add(new DataEvent
					{
						EventName = variable.NameStripes,
						EventType = variable.TypeStripes,
						AmountTarget = variable.TargetAmount,
						AmountSuc = 0,
						ShortNameTarget = variable.TargetItem
					});
				}
			}
		}

		void OnPlayerConnected(BasePlayer player)
		{
			if (player == null) return;
			if (!DataFile.ContainsKey(player.userID))
			{
				DataFile.Add(player.userID, new List<DataEvent>());
			}
			EventProcess(player);
		}

		void Unload()
		{
			if (_imageUI != null)
			{
				_imageUI.UnloadImages();
				_imageUI = null;
			}
			_ = null;
			if (DataFile != null)
				Interface.Oxide.DataFileSystem.WriteObject(Name, DataFile);
		}

		void OnServerSave()
		{
			if (DataFile != null)
				Interface.Oxide.DataFileSystem.WriteObject(Name, DataFile);
		}

		public string Layer = "UI_CupLayerEvent";

		private static string HexToCuiColor(string hex)
		{
			if (string.IsNullOrEmpty(hex))
			{
				hex = "#FFFFFFFF";
			}

			var str = hex.Trim('#');
			if (str.Length == 6) str += "FF";
			if (str.Length != 8)
			{
				throw new Exception(hex);
				throw new InvalidOperationException("Cannot convert a wrong format.");
			}

			var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
			var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
			var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
			var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
			Color color = new Color32(r, g, b, a);
			return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
		}
		private void UI_Help(BasePlayer player)
		{
			CuiElementContainer container = new CuiElementContainer();

			container.Add(new CuiElement
			{
				Name = Layer + ".help",
				Parent = Layer,
				Components =
				{
					new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_HELP") },
					new CuiRectTransformComponent { AnchorMin = "0.24 0.23", AnchorMax = "0.77 0.74", OffsetMax = "0 0" },
				}
			});

			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
				Button = { Color = "1 1 1 0" },
				Text = { Text = _config.HelpText, Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
			}, Layer + ".help");

			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.94 0.89", AnchorMax = "0.97 0.95" },
				Button = { Close = Layer + ".help", Color = "0 0 0 0" },
				Text = { Text = "" }
			}, Layer + ".help");

			CuiHelper.AddUi(player, container);
		}
		void OpenEvent(BasePlayer player)
		{
			if (!DataFile.ContainsKey(player.userID))
			{
				OnPlayerConnected(player);
			}

			CuiHelper.DestroyUi(player, Layer);
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

			container.Add(new CuiLabel
			{
				RectTransform = { AnchorMin = "0.259 0.675", AnchorMax = "0.6439585 0.71" },
				Text = { Text = _config.SettingsMain.TextInButton.Replace("{1}", _config.SettingsStipes.Count.ToString()), FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "0.7607843 0.7450981 0.7411765 1", Font = "robotocondensed-bold.ttf" }
			}, Layer);
			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.68 0.675", AnchorMax = "0.747 0.71" },
				Button = { Color = "0 0 0 0", Command = "stripes.event takeall", FadeIn = 0.1f },
				Text = { Text = $"   Получить", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#bdb9b6"), Font = "robotocondensed-bold.ttf" }
			}, Layer);
			container.Add(new CuiButton
			{
				RectTransform = { AnchorMin = "0.6068 0.675", AnchorMax = "0.673 0.71" },
				Button = { Color = "0 0 0 0", Command = "stripes.event help", FadeIn = 0.1f },
				Text = { Text = $" Помощь", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#bdb9b6"), Font = "robotocondensed-bold.ttf" }
			}, Layer);

			CuiHelper.AddUi(player, container);
			OpenStripesList(player, 1);
		}


		void OpenStripesList(BasePlayer player, int page)
		{
			CuiElementContainer container = new CuiElementContainer();
			CuiHelper.DestroyUi(player, Layer + ".Back");
			CuiHelper.DestroyUi(player, Layer + ".ScrollContent");

			// Вычисляем количество рядов
			int totalStripes = _config.SettingsStipes.Count;
			int rows = (int)Math.Ceiling(totalStripes / 3f);
			
			// Вычисляем общую высоту контента
			float stripHeight = 0.23f;
			float stripGap = 0.008f;
			float totalContentHeight = rows * (stripHeight + stripGap);
			
			// Создаем ScrollView контейнер для прокрутки нашивок
			container.Add(new CuiElement
			{
				Name = Layer + ".ScrollContent",
				Parent = Layer,
				Components =
				{
					new CuiScrollViewComponent
					{
						Vertical = true,
						Horizontal = false,
						MovementType = ScrollRect.MovementType.Elastic,
						Elasticity = 0.1f,
						Inertia = true,
						DecelerationRate = 0.135f,
						ScrollSensitivity = 50,
						ContentTransform = new CuiRectTransform
						{
							AnchorMin = "0 1",
							AnchorMax = "1 1",
							OffsetMin = $"0 {-totalContentHeight * 1000}",
							OffsetMax = "0 0"
						},
						VerticalScrollbar = new CuiScrollbar
						{
							Size = 10,
							AutoHide = false
						}
					},
					new CuiRectTransformComponent
					{
						AnchorMin = "0.259 0.23",
						AnchorMax = "0.747 0.65"
					}
				}
			});

			for (int i = 0; i < 20; i++)
			{
				CuiHelper.DestroyUi(player, Layer + $".{i}.EventStrip");
			}

			// Позиционирование нашивок по горизонтали (X координаты)
			float minx = 0;        // Начальная позиция X для первой колонки
			float maxx = 0.32f;    // Конечная позиция X для первой колонки (ширина нашивки в процентах)

			// Позиционирование нашивок по вертикали (Y координаты)
			float miny = 1f;       // Начальная позиция Y (начинаем сверху)
			float maxy = 1f;       // Конечная позиция Y
			int i1 = 0;

			foreach (var check in _config.SettingsStipes.Select((i, t) => new { A = i, B = t }))
			{
				// Переход на новый ряд после каждых 3 нашивок
				if (i1 != 0 && i1 % 3 == 0)
				{
					miny -= (stripHeight + stripGap);   // Сдвиг вниз для следующего ряда
					maxy -= (stripHeight + stripGap);
					minx = 0;      // Возврат к первой колонке
					maxx = 0.32f;
				}
				
				container.Add(new CuiPanel
				{
					RectTransform = { AnchorMin = $"{minx} {miny - stripHeight}", AnchorMax = $"{maxx} {maxy}" },
					Image = { Color = "0 0 0 0" }
				}, Layer + ".ScrollContent", Layer + $".{check.B}.EventStrip");

				container.Add(new CuiElement
				{
					Parent = Layer + $".{check.B}.EventStrip",
					Components =
					{
						new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_BLOCK") },
						new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
					}
				});
				var list = DataFile[player.userID].FindAll(p => p.EventName == check.A.NameStripes);
				if (list.Count <= 0)
				{
					PrintWarning("List Count is null || zero. Please Contact Developer!");
					CuiHelper.AddUi(player, container);
					return;
				}
				container.Add(new CuiElement
				{
					FadeOut = 0.3f,
					Parent = Layer + $".{check.B}.EventStrip",
					Name = Layer + $".{check.B}.EventStripImg",
					Components =
					{
						new CuiRawImageComponent { FadeIn = 0.3f, Png = (string) ImageLibrary.Call("GetImage", check.A.ImageStripes) },
						new CuiRectTransformComponent { AnchorMin = "0.06 0.6", AnchorMax = "0.94 0.94" }
					}
				});
				container.Add(new CuiButton
				{
					RectTransform = { AnchorMin = "0.04 0.23", AnchorMax = "0.96 0.5628405", OffsetMax = "0 0" },
					Button = { Color = "0 0 0 0", FadeIn = 0.1f },
					Text = { Text = $"<b><color=#ededed>{check.A.NameStripes}</color></b>\n{check.A.CommentsSripes}\n{check.A.CommentsPrize}", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.1", Font = "robotocondensed-regular.ttf" }
				}, Layer + $".{check.B}.EventStrip", Layer + $".{check.B}.EventStripTextName");

				var listdata = list.FirstOrDefault(p => p.EventName == check.A.NameStripes);

				if (listdata != null && listdata.AmountSuc >= listdata.AmountTarget && !listdata.IsSuc)
				{
					container.Add(new CuiPanel
					{
						RectTransform = { AnchorMin = "0.03 0.05", AnchorMax = "0.97 0.22", OffsetMax = "0 0" },
						Image = { Color = "0 0 0 0", FadeIn = 0.1f },
					}, Layer + $".{check.B}.EventStrip", Layer + $".{check.B}.EventStripPrize");

					container.Add(new CuiElement
					{
						Parent = Layer + $".{check.B}.EventStripPrize",
						Components =
						{
							new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_BUTTON_TAKE") },
							new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
						}
					});

					container.Add(new CuiButton
					{
						RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
						Button = { Color = "0 0 0 0", Command = $"stripes.event take {check.A.NameStripes}" },
						Text = { Text = $"Забрать награду", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = "robotocondensed-regular.ttf" }
					}, Layer + $".{check.B}.EventStripPrize");
				}
				else if (listdata != null && listdata.AmountSuc < listdata.AmountTarget)
				{
					container.Add(new CuiPanel
					{
						RectTransform = { AnchorMin = "0.03 0.05", AnchorMax = "0.97 0.22", OffsetMax = "0 0" },
						Image = { Color = "0 0 0 0", FadeIn = 0.1f },
					}, Layer + $".{check.B}.EventStrip", Layer + $".{check.B}.EventStripPrize");

					container.Add(new CuiElement
					{
						Parent = Layer + $".{check.B}.EventStripPrize",
						Components =
						{
							new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_BUTTON_PROCESS") },
							new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
						}
					});

					container.Add(new CuiLabel
					{
						RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
						Text = { Text = $"{listdata.AmountSuc} из {listdata.AmountTarget}", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.7607843 0.7450981 0.7411765 1", Font = "robotocondensed-regular.ttf" }
					}, Layer + $".{check.B}.EventStripPrize");
				}
				else if (listdata != null && listdata.IsSuc)
				{
					container.Add(new CuiPanel
					{
						RectTransform = { AnchorMin = "0.03 0.05", AnchorMax = "0.97 0.22", OffsetMax = "0 0" },
						Image = { Color = "0 0 0 0", FadeIn = 0.1f },
					}, Layer + $".{check.B}.EventStrip", Layer + $".{check.B}.EventStripPrize");

					container.Add(new CuiElement
					{
						Parent = Layer + $".{check.B}.EventStripPrize",
						Components =
						{
							new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_BUTTON_PROCESS") },
							new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
						}
					});

					container.Add(new CuiLabel
					{
						RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
						Text = { Text = $"Получено", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.7607843 0.7450981 0.7411765 1", Font = "robotocondensed-regular.ttf" }
					}, Layer + $".{check.B}.EventStripPrize");

					container.Add(new CuiElement
					{
						Parent = Layer + $".{check.B}.EventStrip",
						Components =
						{
							new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_BLOCK_COMPLETE") },
							new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
						}
					});
				}

				maxx += 0.33f;  // Переход к следующей колонке
				minx += 0.33f;
				i1++;
			}

			CuiHelper.AddUi(player, container);
		}

		[ConsoleCommand("stripes.event")]
		void StripesCommand(ConsoleSystem.Arg args)
		{
			BasePlayer player = args.Player();
			int page = 1;
			if (int.TryParse(args.Args[0], out page) && page > 0 && (page - 1) * 9 <= _config.SettingsStipes.Count)
			{
				OpenStripesList(player, page);
			}

			if (args.Args[0] == "help")
			{
				UI_Help(player);
			}
			else if (args.Args[0] == "take")
			{
				var findconfig = _config.SettingsStipes.FirstOrDefault(p => p.NameStripes == string.Join(" ", args.Args.Skip(1).ToArray()));
				if (findconfig == null) return;
				var finddata = DataFile[player.userID].FirstOrDefault(p => p.EventName == string.Join(" ", args.Args.Skip(1).ToArray()));
				if (finddata == null) return;
				if (finddata.AmountSuc < finddata.AmountTarget) return;
				if (finddata.IsSuc) return;
				foreach (var item in findconfig.ListPrize)
				{
					if (item.ItemOrCommands)
					{
						GiveCommands(player, item.PrizeEvent);
					}
					else
					{
						var itemtarget = ItemManager.CreateByName(item.PrizeEvent, item.Amount, item.PrizeSkinID);
						player.GiveItem(itemtarget, BaseEntity.GiveItemReason.PickedUp);
					}
				}
				player.ChatMessage(_config.SettingsMain.MessageToTake.Replace("{1}", findconfig.NameStripes));
				finddata.IsSuc = true;
			}
			else if (args.Args[0] == "takeall")
			{
				var find = DataFile[player.userID].FindAll(p => p.IsSuc == true);
				if (find.Count < _config.SettingsStipes.Count) return;
				if (_config.SettingsMain.GiveBalance)
				{
					MoneyPlus(player.userID, _config.SettingsMain.MoneyRub);
					DataFile[player.userID].Clear();
					EventProcess(player);
					player.ChatMessage(_config.SettingsMain.MessageToAllTake);
				}
				else
				{
					if (_config.SettingsVK.NotificationAdmins)
					{
						SendVkMessage(_config.SettingsVK.ListAdmins, _config.SettingsVK.MessageNotification.Replace("{1}", $"[{player.userID}] {player.displayName}"));
					}
					DataFile[player.userID].Clear();
					EventProcess(player);
					player.ChatMessage(_config.SettingsMain.MessageToAllTake);
				}
			}
		}

		private string URLEncode(string input)
		{
			if (input.Contains("#")) input = input.Replace("#", "%23");
			if (input.Contains("$")) input = input.Replace("$", "%24");
			if (input.Contains("+")) input = input.Replace("+", "%2B");
			if (input.Contains("/")) input = input.Replace("/", "%2F");
			if (input.Contains(":")) input = input.Replace(":", "%3A");
			if (input.Contains(";")) input = input.Replace(";", "%3B");
			if (input.Contains("?")) input = input.Replace("?", "%3F");
			if (input.Contains("@")) input = input.Replace("@", "%40");
			return input;
		}

		void GetCallback(int number, string param, string message)
		{

		}
		private string app = "v=5.92";

		private System.Random random = new System.Random();
		private string RandomId() => random.Next(Int32.MinValue, Int32.MaxValue).ToString();
		private void SendVkMessage(string reciverID, string msg) => webrequest.Enqueue("https://api.vk.com/method/messages.send?user_ids=" + reciverID + "&message=" + URLEncode(msg) + "&"+app + "&random_id=" + RandomId() + "&access_token=" + _config.SettingsVK.VKToken, null, (code, response) => GetCallback(code, response, "Сообщение"), this);


		void MoneyPlus(ulong userId, int amount)
		{
			ExecuteApiRequest(new Dictionary<string, string>()
			{
				{ "action", "moneys" },
				{ "type", "plus" },
				{ "steam_id", userId.ToString() },
				{ "amount", amount.ToString() }
			});
		}
		void ExecuteApiRequest(Dictionary<string, string> args)
		{
			string url = $"http://gamestores.ru/api?shop_id={_config.SettingsMain.ShopID}&secret={_config.SettingsMain.APIKey}" +
						 $"{string.Join("", args.Select(arg => $"&{arg.Key}={arg.Value}").ToArray())}";
			webrequest.EnqueueGet(url, (i, s) =>
			{
				if (i != 200)
				{
					LogToFile("ATMBox", $"Код ошибки: {i}, подробности:\n{s}", this);
				}
				else
				{
					if (s.Contains("fail"))
					{
						return;
					}
				}
			}, this);
		}

		void GiveCommands(BasePlayer player, string text)
		{
			string command = text.Replace("{steamid}", player.UserIDString);
			ConsoleSystem.Arg arg = new ConsoleSystem.Arg(ConsoleSystem.Option.Server, command);
			arg.cmd.Call(arg);
		}

		public bool CheckStripes(BasePlayer player, string shortname, TypeEvent type)
		{
			if (!DataFile.ContainsKey(player.userID))
			{
				OnPlayerConnected(player);
			}
			var data = DataFile[player.userID].FirstOrDefault(p => p.ShortNameTarget == shortname && p.EventType == (int)type && p.IsSuc == false);
			if (data != null)
			{
				return true;
			}

			return false;
		}

		public void Progress(BasePlayer player, string shortname, int amount)
		{
			var data = DataFile[player.userID];
			if (string.IsNullOrEmpty(shortname)) return;
			if (data.Count > 0)
			{
				var key = data.FirstOrDefault(p => p.ShortNameTarget == shortname);
				if (key != null)
				{
					if (key.AmountSuc < key.AmountTarget && key.IsSuc == false)
					{
						key.AmountSuc += amount;
						if (key.AmountSuc >= key.AmountTarget)
						{
							key.AmountSuc = key.AmountTarget;
							if (_config.SettingsMain.Notif)
							{
								Notifications.Call("ShowNotify", player.userID, 5f, "ОПОВЕЩЕНИЕ", _config.SettingsMain.DescNotif, "stripes", null);
							}
						}
					}
				}
			}
		}

		void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
		{
			if (entity == null || item == null) return;
			BasePlayer player = entity.ToPlayer();
			if (player == null) return;
			if (CheckStripes(player, item.info.shortname, TypeEvent.GatherBonus))
			{
				Progress(player, item.info.shortname, 1);
			}
		}

		void OnCollectiblePickup(Item item, BasePlayer player)
		{
			if (item == null || player == null) return;
			if (player != null)
			{
				if (CheckStripes(player, item.info.shortname, TypeEvent.Gather))
				{
					Progress(player, item.info.shortname, item.amount);
				}
			}
		}

		object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
		{
			if (entity == null || item == null) return null;
			BasePlayer player = entity.ToPlayer();
			if (player == null) return null;
			if (CheckStripes(player, item.info.shortname, TypeEvent.Gather))
			{
				Progress(player, item.info.shortname, item.amount);
			}

			return null;
		}

		private List<string> containerNames = new List<string>
		{
			"crate_basic",
			"crate_elite",
			"crate_mine",
			"crate_tools",
			"crate_normal",
			"crate_normal_2",
			"crate_normal_2_food",
			"crate_normal_2_medical",
			"crate_underwater_advanced",
			"crate_underwater_basic",
			"foodbox",
			"minecart",
			"bradley_crate",
			"heli_crate",
			"codelockedhackablecrate",
			"supply_drop",
			"presentdrop"
		};

		public List<StorageContainer> ListContainer = new List<StorageContainer>();

		object CanLootEntity(BasePlayer player, StorageContainer container)
		{
			if (container == null || player == null) return null;
			if (!containerNames.Contains(container.ShortPrefabName)) return null;
			if (ListContainer.Contains(container)) return null;
			if (container.inventory.itemList.Count == 0) return null;
			foreach (var key in container.inventory.itemList)
			{
				if (CheckStripes(player, key.info.shortname, TypeEvent.Loot))
				{
					Progress(player, key.info.shortname, key.amount);
				}
			}
			ListContainer.Add(container);
			return null;
		}


		private Dictionary<NetworkableId, string> LastHeliHit = new Dictionary<NetworkableId, string>();

		object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || info == null) return null;
			if (entity is BaseHelicopter && info.Initiator is BasePlayer)
				LastHeliHit[entity.net.ID] = info.InitiatorPlayer.UserIDString;
			return null;
		}

		void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			if (entity == null || info == null) return;
			if (entity is BaseHelicopter)
			{
				if (LastHeliHit.ContainsKey(entity.net.ID))
				{
					if (LastHeliHit[entity.net.ID] != null)
					{
						BasePlayer player = BasePlayer.Find(LastHeliHit[entity.net.ID]);
						if (player != null)
						{
							var entnames = entity.ShortPrefabName;
							if (CheckStripes(player, entnames, TypeEvent.Kill))
							{
								LastHeliHit.Remove(entity.net.ID);
								Progress(player, entnames, 1);
							}
						}
					}
				}
			}
			var entname = entity.ShortPrefabName;
			BasePlayer players = info.InitiatorPlayer;
			if (players != null)
			{
				if (CheckStripes(players, entname, TypeEvent.Kill))
				{
					Progress(players, entname, 1);
				}
			}
		}

		#region Images
		private static ImageUI _imageUI;
		private class ImageUI
		{
			private const String _path = "TPSystem/TPStripe/";
			private const String _printPath = "data/" + _path;
			private readonly Dictionary<String, ImageData> _images = new()
			{
				{ "MAIN_FON", new ImageData() },
				{ "BACKGROUND_HELP", new ImageData() },
				{ "BACKGROUND_BLOCK", new ImageData() },
				{ "BACKGROUND_BUTTON_PROCESS", new ImageData() },
				{ "BACKGROUND_BUTTON_TAKE", new ImageData() },
				{ "BACKGROUND_BLOCK_COMPLETE", new ImageData() }
			};

			private enum ImageStatus
			{
				NotLoaded,
				Loaded,
				Failed
			}

			private class ImageData
			{
				public ImageStatus Status = ImageStatus.NotLoaded;
				public string Id { get; set; }
			}

			public string GetImage(string name)
			{
				ImageData image;
				if (_images.TryGetValue(name, out image) && image.Status == ImageStatus.Loaded)
					return image.Id;
				return null;
			}

			public void DownloadImage()
			{
				KeyValuePair<string, ImageData>? image = null;
				foreach (KeyValuePair<string, ImageData> img in _images)
				{
					if (img.Value.Status == ImageStatus.NotLoaded)
					{
						image = img;
						break;
					}
				}

				if (image.HasValue)
				{
					ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value));
				}
				else
				{
					List<String> failedImages = new List<string>();

					foreach (KeyValuePair<String, ImageData> img in _images)
					{
						if (img.Value.Status == ImageStatus.Failed)
						{
							failedImages.Add(img.Key);
						}
					}

					if (failedImages.Count > 0)
					{
						String images = String.Join(", ", failedImages);
						_.PrintError($"Не удалось загрузить следующие изображения: {images}. Возможно, вы не загрузили их в папку '{_printPath}'.");
						Interface.Oxide.UnloadPlugin(_.Name);
					}
					else
					{
						_.Puts($"{_images.Count} изображений успешно загружено!");
					}
				}
			}

			public void UnloadImages()
			{
				foreach (KeyValuePair<string, ImageData> item in _images)
					if (item.Value.Status == ImageStatus.Loaded)
						if (item.Value?.Id != null)
							FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

				_images?.Clear();
			}

			private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
			{
				string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + _path + image.Key + ".png";

				using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
				{
					yield return www.SendWebRequest();

					if (www.isNetworkError || www.isHttpError)
					{
						image.Value.Status = ImageStatus.Failed;
					}
					else
					{
						Texture2D tex = DownloadHandlerTexture.GetContent(www);
						image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
						image.Value.Status = ImageStatus.Loaded;
						UnityEngine.Object.DestroyImmediate(tex);
					}

					DownloadImage();
				}
			}
		}
		#endregion
	}
}