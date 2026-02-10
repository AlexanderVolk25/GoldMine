using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System.Collections;
using System;
using UnityEngine.Networking;
using System.IO;
namespace Oxide.Plugins
{
	[Info("TPBPass", "pluginfuel.ru", "20.0.2")]
	public class TPBPass : RustPlugin
	{
		#region Вар
			private string Layer = "TPBPass_UI";
			private static TPBPass _;
			[PluginReference] Plugin ImageLibrary, TPMenuSystem;
			private Dictionary<ulong, int> activeLevel = new Dictionary<ulong, int>();
			private Dictionary<ulong, DataBase> DB = new Dictionary<ulong, DataBase>();
			Timer Timer = null;
		#endregion
		#region Класс
			public class Settings
			{
				[JsonProperty("Необходимое кол-во xp для апа уровня")] public float Xp;
				[JsonProperty("Пермишен для платного TPBPass")] public string PermUse;
				[JsonProperty("Изображение в информации сезонного пропуска")] public string InfoImage;
				[JsonProperty(PropertyName = "Получение XP за добычу ресурсов")] public Dictionary<string, float> resourcesXP;
				[JsonProperty(PropertyName = "Получение XP за убийство (разрушение)")] public Dictionary<string, float> destroyXP;
				[JsonProperty(PropertyName = "Получение XP за лутание ящика")] public Dictionary<string, float> lootXP;
			}
			public class DataBase
			{
				public float Xp;
				public int Level;
				public List<int> LevelList = new List<int>();
			}
			public class ItemsList
			{
				[JsonProperty("На каком уровне будет предемет")] public int Level;
				[JsonProperty("Бесплатная или платная награда")] public bool Use;
				[JsonProperty("Отображение названия предмета в интерфейсе")] public string DisplayName;
				[JsonProperty("Короткое название предмета")] public string ShortName;
				[JsonProperty("SkinID предмета")] public ulong SkinID;
				[JsonProperty("Выполняемая команда (если не предмет)")] public string Command;
				[JsonProperty("Изображение предмета (в основном используется для команд)")] public string Url;
				[JsonProperty("Кол-во предмета")] public int Amount;
			}
		#endregion
		#region Конфиг
			private Configuration config;
			private class Configuration
			{
				[JsonProperty("Настройки")] public Settings settings = new Settings();
				[JsonProperty("Список предметов")] public List<ItemsList> items = new List<ItemsList>();
				public static Configuration GetNewCong()
				{
					return new Configuration
					{
						settings = new Settings()
						{
							Xp = 100,
							PermUse = "TPBPass.use"
						},
						items = new List<ItemsList>()
						{
							new ItemsList()
							{
								Level = 1,
								Use = false,
								DisplayName = "дерева",
								ShortName = "wood",
								SkinID = 0,
								Command = null,
								Url = null,
								Amount = 1000
							}
						}
					};
				}
				
			}
			protected override void LoadConfig()
			{
				base.LoadConfig();
				try
				{
					config = Config.ReadObject<Configuration>();
					if (config?.items == null) LoadDefaultConfig();
				}
				
				catch
				{
					PrintWarning($"Что то с этим конфигом не так! 'oxide/config/{Name}', создаём новую конфигурацию!");
					LoadDefaultConfig();
				}
				
				NextTick(SaveConfig);
			}
			
			protected override void LoadDefaultConfig() => config = Configuration.GetNewCong();
			protected override void SaveConfig() => Config.WriteObject(config);
		#endregion
		
        #region Хуки
			private void OnServerInitialized()
			{
				_ = this;
				
				_imageUI = new ImageUI();
				_imageUI.DownloadImage();
				
				if (!permission.PermissionExists(config.settings.PermUse, this))
                permission.RegisterPermission(config.settings.PermUse, this);
				
				foreach (ItemsList item in config.items)
                ImageLibrary.Call("AddImage", item.Url, item.Url);
				
				foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
			}
			
			private void Unload()
			{
				foreach (var check in DB)
                SaveDataBase(check.Key);
				
				foreach (BasePlayer player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, Layer);
				
				if (_imageUI != null)
				{
					_imageUI.UnloadImages();
					_imageUI = null;
				}
				_ = null;
			}
			
			private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
			{
				if (entity != null && (entity.PrefabName.Contains("patrol") || entity.PrefabName.Contains("bradley") || entity.PrefabName.Contains("ch47")) && info?.InitiatorPlayer != null) entity._name = info.InitiatorPlayer.UserIDString ?? "UNKNOWN";
			}
			
			private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
			{
				if (entity == null || info == null) return;
				
				BasePlayer player = info.InitiatorPlayer;
				if (player == null)
				{
					if (entity._name?.Length == 17) player = BasePlayer.FindByID(ulong.Parse(entity._name));
					if (player == null) return;
				}
				
				string shortName = entity.ShortPrefabName;
				if (entity is BuildingBlock)
                shortName = "tier" + (int)((entity as BuildingBlock).grade);
				float amount;
				if (!config.settings.destroyXP.TryGetValue(shortName, out amount)) return;
				if (entity.ToPlayer() != null && entity.ToPlayer().userID == player.userID) return;
				AddXp(player, amount);
			}
			
			private void OnPlayerConnected(BasePlayer player)
			{
				if (!activeLevel.ContainsKey(player.userID))
                activeLevel[player.userID] = 1;
				CreateDataBase(player);
			}
			
			private void OnLootEntity(BasePlayer player, BaseEntity entity)
			{
				if (entity.name == "LOOTED") return;
				entity.name = "LOOTED";
				float amount;
				if (!config.settings.lootXP.TryGetValue(entity.ShortPrefabName, out amount)) return;
				
				AddXp(player, amount);
			}
			
			private void OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
			{
				float amount;
				if (!config.settings.resourcesXP.TryGetValue(entity.ShortPrefabName, out amount)) return;
				AddXp(player, amount);
			}
			
			private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
			{
				string shortName = "";
				switch (item.info.shortname)
				{
					case "wood":
                    shortName = "tree";
                    break;
					case "sulfur.ore":
                    shortName = "sulfur-ore";
                    break;
					case "metal.ore":
                    shortName = "metal-ore";
                    break;
					case "stones":
                    shortName = "stones";
                    break;
				}
				
				float amount;
				if (!config.settings.resourcesXP.TryGetValue(shortName, out amount)) return;
				
				AddXp(player, amount);
			}
		#endregion
		
        #region Дата
			private void CreateDataBase(BasePlayer player)
			{
				var DataBase = Interface.Oxide.DataFileSystem.ReadObject<DataBase>($"TPSystem/{Name}/{player.userID}");
				
				if (!DB.ContainsKey(player.userID))
                DB.Add(player.userID, new DataBase());
				
				DB[player.userID] = DataBase ?? new DataBase();
			}
			
			private void SaveDataBase(ulong userId) => Interface.Oxide.DataFileSystem.WriteObject($"TPSystem/{Name}/{userId}", DB[userId]);
		#endregion
		
        #region Команды
			[ConsoleCommand("pass")]
			void ConsolePass(ConsoleSystem.Arg arg)
			{
				BasePlayer player = arg.Player();
				if (player != null && arg.HasArgs(1))
				{
					if (arg.Args[0] == "active")
					{
						int level = int.Parse(arg.Args[1]);
						ItemsList item = config.items.FirstOrDefault(i => i.Level == level);
						if (item != null)
						{
							activeLevel[player.userID] = item.Level;
							RewardLevelUI(player, activeLevel[player.userID]);
							SeasonRewardsUI(player, int.Parse(arg.Args[2]));
						}
						else
						{
							SendReply(player, $"На уровне {level} нет наград");
						}
					}
					if (arg.Args[0] == "info")
					{
						ShowInforamtionUI(player);
					}
					if (arg.Args[0] == "skip")
					{
						SeasonRewardsUI(player, int.Parse(arg.Args[1]));
					}
					if (arg.Args[0] == "take")
					{
						ItemsList item = config.items.FirstOrDefault(i => i.Level == int.Parse(arg.Args[1]));
						
						if (DB[player.userID].LevelList.Contains(item.Level))
						{
							string text = "";
							if (!string.IsNullOrEmpty(item.Command))
							{
								Server.Command(item.Command.Replace("%STEAMID%", player.UserIDString));
								text = $"Вы получили услугу: <color=#ee3e61>{item.DisplayName}</color>";
							}
							if (!string.IsNullOrEmpty(item.ShortName))
							{
								player.inventory.GiveItem(ItemManager.CreateByName(item.ShortName, item.Amount));
								text = $"Вы получили: {item.DisplayName} - в размере: {item.Amount}";
							}
							DB[player.userID].LevelList.Remove(item.Level);
							SeasonUI(player);
							SendReply(player, text);
						}
					}
				}
			}
		#endregion
		
        #region Интерфейс
			private void SeasonUI(BasePlayer player)
			{
				CuiHelper.DestroyUi(player, ".Progress");
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
					
					container.Add(new CuiPanel
						{
							RectTransform = { AnchorMin = "0.561 0.685", AnchorMax = "0.873 0.725", OffsetMax = "0 0" },
							Image = { Color = HexToCuiColor("#29292e", 100) }
						}, ".Mains", ".Progress");
						
						container.Add(new CuiPanel
							{
								RectTransform = { AnchorMin = "0 0", AnchorMax = $"{Mathf.Clamp01(0.02f + ((float)DB[player.userID].Xp / config.settings.Xp))} 1", OffsetMax = "0 0" },
								Image = { Color = HexToCuiColor("#41414a", 100) }
							}, ".Progress");
							
							container.Add(new CuiButton
								{
									RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
									Button = { Close = "Menu_UI", Color = "0 0 0 0" },
									Text = { Text = "" }
								}, Layer);
								
								container.Add(new CuiPanel
									{
										RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-354 -180", OffsetMax = "354 180" },
										Image = { Color = "0 0 0 0" }
									}, Layer, "MainPass");
									
									container.Add(new CuiPanel
										{
											RectTransform = { AnchorMin = "0.408 0.425", AnchorMax = "0.747 0.686", OffsetMax = "0 0" },
											Image = { Color = "0 0 0 0" }
										}, Layer, "PassInfo");
										
										container.Add(new CuiPanel
											{
												RectTransform = { AnchorMin = "0.3 0.67", AnchorMax = "0.96 0.97", OffsetMax = "0 0" },
												Image = { Color = "0 0 0 0" }
											}, "PassInfo", "PassProgress");
											
											container.Add(new CuiLabel
												{
													RectTransform = { AnchorMin = "0 0.1", AnchorMax = "0.125 1", OffsetMax = "0 0" },
													Text = { Text = $"{DB[player.userID].Level}", Color = HexToCuiColor("#414140ff", 100), FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
												}, "PassProgress");
												
												container.Add(new CuiLabel
													{
														RectTransform = { AnchorMin = "0.16 0.5", AnchorMax = "1 1", OffsetMax = "0 0" },
														Text = { Text = "ПРОГРЕСС", Color = HexToCuiColor("#c4c4c4", 90), FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
													}, "PassProgress");
													
													container.Add(new CuiPanel
														{
															RectTransform = { AnchorMin = "0.94 0.5", AnchorMax = "1 1", OffsetMin = "0 6", OffsetMax = "0 -6" },
															Image = { Sprite = "assets/icons/xp.png", Color = HexToCuiColor("#a9a8a6", 100) }
														}, "PassProgress");
														
														container.Add(new CuiLabel
															{
																RectTransform = { AnchorMin = "0.17 0.5", AnchorMax = "0.93 1", OffsetMax = "0 0" },
																Text = { Text = $"{DB[player.userID].Xp.ToString("0.0000")}", Color = HexToCuiColor("#c4c4c4", 90), FontSize = 12, Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf" }
															}, "PassProgress");
															
															container.Add(new CuiLabel
																{
																	RectTransform = { AnchorMin = "0.16 0", AnchorMax = "1 0.3", OffsetMax = "0 0" },
																	Text = { Text = $"{DB[player.userID].Level} LVL", Color = HexToCuiColor("#c4c4c4", 90), FontSize = 8, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
																}, "PassProgress");
																
																container.Add(new CuiLabel
																	{
																		RectTransform = { AnchorMin = "0.17 0", AnchorMax = "1 0.3", OffsetMax = "0 0" },
																		Text = { Text = $"{DB[player.userID].Level + 1} LVL", Color = HexToCuiColor("#c4c4c4", 90), FontSize = 8, Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf" }
																	}, "PassProgress");
																	
																	container.Add(new CuiButton
																		{
																			RectTransform = { AnchorMin = "0.458 0.087", AnchorMax = "0.726 0.215", OffsetMax = "0 0" },
																			Button = { Color = "0 0 0 0", Command = "pass info" },
																			Text = { Text = "Подробнее", Color = HexToCuiColor("#c4c4c4", 100), FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
																		}, "PassInfo");
																		
																		CuiHelper.AddUi(player, container);
																		activeLevel[player.userID] = DB[player.userID].Level >= config.items.Count() ? config.items.Count() : DB[player.userID].Level + 1;
																		SeasonRewardsUI(player, DB[player.userID].Level / 6);
																		if (config.items.Count > 0)
																		RewardLevelUI(player, DB[player.userID].Level >= config.items.Count() ? config.items.Count() : DB[player.userID].Level + 1);
			}
			
			private void SeasonRewardsUI(BasePlayer player, int page)
			{
				CuiHelper.DestroyUi(player, "RewardsDefault");
				CuiHelper.DestroyUi(player, "Skip");
				CuiHelper.DestroyUi(player, "Back");
				CuiElementContainer container = new CuiElementContainer();
				
				container.Add(new CuiPanel
					{
						RectTransform = { AnchorMin = "0.223 0.243", AnchorMax = "0.7765 0.403", OffsetMax = "0 0" },
						Image = { Color = "0 0 0 0" }
					}, Layer, "RewardsDefault");
					
					container.Add(new CuiButton
						{
							RectTransform = { AnchorMin = "0.196 0.243", AnchorMax = "0.2185 0.403", OffsetMax = "0 0" },
							Button = { Color = "0 0 0 0", Command = page != 0 ? $"pass skip {page - 1}" : "", Material = "assets/content/ui/uibackgroundblur.mat" },
							Text = { Text = "<", Color = HexToCuiColor("#c4c4c4", 70), FontSize = 18, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
						}, Layer, "Back");
						
						container.Add(new CuiButton
							{
								RectTransform = { AnchorMin = "0.785 0.243", AnchorMax = "0.808 0.403", OffsetMax = "0 0" },
								Button = { Color = "0 0 0 0", Command = config.items.Count() > (page + 1) * 6 ? $"pass skip {page + 1}" : "", Material = "assets/content/ui/uibackgroundblur.mat" },
								Text = { Text = ">", Color = HexToCuiColor("#c4c4c4", 70), FontSize = 18, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
							}, Layer, "Skip");
							
							int startLevel = page * 6 + 1;
							float width = 0.16f, height = 1f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
							foreach (int i in Enumerable.Range(0, 6))
							{
								ItemsList item = config.items.Skip(page * 6).Take(6).ElementAtOrDefault(i);
								container.Add(new CuiButton
									{
										RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
										Button = { Color = "0 0 0 0", Command = $"pass active {startLevel + i} {page}" },
										Text = { Text = "" }
									}, "RewardsDefault", "Item");
									if (item != null)
									{
										container.Add(new CuiButton
											{
												RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
												Button = { Color = "0 0 0 0", Command = $"pass active {item.Level} {page}" },
												Text = { Text = "" }
											}, "RewardsDefault", "Item");
											
											bool active = activeLevel[player.userID] == item.Level;
											bool dbLevel = DB[player.userID].LevelList.Contains(item.Level);
											
											string image = active ? "PREM_REWARD" : dbLevel && !item.Use ? "TAKE_REWARD" : null;
											
											if (image != null)
											{
												container.Add(new CuiElement
													{
														Parent = "Item",
														Components =
														{
															new CuiRawImageComponent { Png = _imageUI.GetImage(image) },
															new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
														}
													});
											}
											
											if (item.Url != null)
											{
												container.Add(new CuiElement
													{
														Parent = "Item",
														Components =
														{
															new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", item.Url), Color = "1 1 1 0.3"  },
															new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "8 8", OffsetMax = "-8 -8" },
														}
													});
											}
											else
											{
												container.Add(new CuiElement
													{
														Parent = "Item",
														Components =
														{
															new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(item.ShortName).itemid, SkinId = item.SkinID, Color = "1 1 1 0.3"  },
															new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "8 8", OffsetMax = "-8 -8" },
														}
													});
											}
											
											if (item.Use && !permission.UserHasPermission(player.UserIDString, config.settings.PermUse))
											{
												container.Add(new CuiPanel
													{
														RectTransform = { AnchorMin = "0.805 0.8", AnchorMax = "1 1", OffsetMin = "3 3", OffsetMax = "-3 -3" },
														Image = { Sprite = "assets/icons/lock.png", Color = active ? HexToCuiColor("#a52640", 100) : HexToCuiColor("#c4c4c4", 40) }
													}, "Item");
											}
									}
									
									container.Add(new CuiPanel
										{
											RectTransform = { AnchorMin = "0 0.78", AnchorMax = "0.23 1", OffsetMax = "0 0" },
											Image = { Color = "0 0 0 0" }
										}, "Item", "Level");
										
										container.Add(new CuiLabel
											{
												RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
												Text = { Text = $"{startLevel + i}", Color = HexToCuiColor("#a9a8a6", 30), FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
											}, "Level");
											
											xmin += width + 0.0094f;
							}
							CuiHelper.AddUi(player, container);
			}
			
			private void RewardLevelUI(BasePlayer player, int level)
			{
				CuiHelper.DestroyUi(player, "RewardLevel");
				CuiElementContainer container = new CuiElementContainer();
				ItemsList item = config.items.FirstOrDefault(i => i.Level == level);
				
				container.Add(new CuiPanel
					{
						RectTransform = { AnchorMin = "0.2562 0.425", AnchorMax = "0.403 0.686", OffsetMax = "0 0" },
						Image = { Color = "0 0 0 0" }
					}, Layer, "RewardLevel");
					
					container.Add(new CuiLabel
						{
							RectTransform = { AnchorMin = "0.078 0.786", AnchorMax = "0.223 0.92", OffsetMax = "0 0" },
							Text = { Text = $"{level}", Color = HexToCuiColor("#414140ff", 100), FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
						}, "RewardLevel");
						
						string name = item.Amount > 1 ? $"{item.Amount} {item.DisplayName}" : $"{item.DisplayName}";
						container.Add(new CuiLabel
							{
								RectTransform = { AnchorMin = "0.27 0.78", AnchorMax = "0.89 0.96", OffsetMax = "0 0" },
								Text = { Text = $"<b><size=9>Приз:</size></b>\n{name}", Color = HexToCuiColor("#a9a8a6", 100), FontSize = 8, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
							}, "RewardLevel");
							
							if (item.Url != null)
							{
								container.Add(new CuiElement
									{
										Parent = "RewardLevel",
										Components =
										{
											new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", item.Url), Color = "1 1 1 1"  },
											new CuiRectTransformComponent { AnchorMin = "0.21 0.22", AnchorMax = "0.79 0.76", OffsetMin = "6 6", OffsetMax = "-6 -6" },
										}
									});
							}
							else
							{
								container.Add(new CuiElement
									{
										Parent = "RewardLevel",
										Components =
										{
											new CuiImageComponent { ItemId = ItemManager.FindItemDefinition(item.ShortName).itemid, SkinId = item.SkinID, Color = "1 1 1 1"  },
											new CuiRectTransformComponent { AnchorMin = "0.21 0.22", AnchorMax = "0.79 0.76", OffsetMin = "6 6", OffsetMax = "-6 -6" },
										}
									});
							}
							
							string text = item.Use && !permission.UserHasPermission(player.UserIDString, config.settings.PermUse) ? "Платно" : DB[player.userID].LevelList.Contains(item.Level) ? "Получить" : "Недоступно";
							container.Add(new CuiButton
								{
									RectTransform = { AnchorMin = "0.08 0.09", AnchorMax = "0.91 0.21", OffsetMax = "0 0" },
									Button = { Color = "0 0 0 0", Command = DB[player.userID].LevelList.Contains(item.Level) ? $"pass take {item.Level}" : "", Material = "assets/content/ui/uibackgroundblur.mat" },
									Text = { Text = text, Color = HexToCuiColor("#c4c4c4", 100), FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
								}, "RewardLevel");
								
								CuiHelper.AddUi(player, container);
			}
			
			private void ShowInforamtionUI(BasePlayer player)
			{
				CuiHelper.DestroyUi(player, "Information_UI");
				CuiElementContainer container = new CuiElementContainer();
				
				container.Add(new CuiPanel
					{
						CursorEnabled = true,
						RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
						Image = { Color = "0 0 0 0.3", Material = "assets/content/ui/uibackgroundblur.mat" }
					}, "Overlay", "Information_UI");
					
					container.Add(new CuiButton
						{
							RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
							Button = { Color = "0 0 0 0.9", Close = "Information_UI" }
						}, "Information_UI");
						
						container.Add(new CuiLabel
							{
								RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7", OffsetMax = "0 0" },
								Text = { Text = "<b><size=30>SEASONPASS</size></b>\n\nЗа различные игровые действия такие как фарм, убийства игроков\nлутание ящиков и бочек, вы будете получать xp\nи постепенно разблокировать уровень.\nЗа каждый разблокированный уровень вас ждет награда.\nНекоторые награды недоступны, получить к ним доступ можно, купив подписку в донат магазине.\nВайп seasonpass происходит каждые две недели.", Color = "1 1 1 0.2", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf" }
							}, "Information_UI");
							
							CuiHelper.AddUi(player, container);
			}
		#endregion
		
        #region Хелпер
			public string HexToCuiColor(string HEX, float Alpha = 100)
			{
				if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";
				
				string str = HEX.Trim('#');
				byte r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
				byte g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
				byte b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
				
				return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
			}
			
			private void AddXp(BasePlayer player, float amount)
			{
				var db = DB[player.userID];
				db.Xp += amount;
				
				while (db.Xp >= config.settings.Xp)
				{
					db.Level++;
					db.LevelList.Add(db.Level);
					db.Xp -= config.settings.Xp;
					player.SendConsoleCommand($"note.inv 605467368 1 \"Уровень улучшен\"");
					Effect x = new Effect("assets/bundled/prefabs/fx/notice/stack.world.fx.prefab", player, 0, new Vector3(), new Vector3());
					EffectNetwork.Send(x, player.Connection);
				}
				player.SendConsoleCommand($"note.inv 605467368 1 \"+{amount} XP\"");
			}
		#endregion
		
        #region Images
			private static ImageUI _imageUI;
			private class ImageUI
			{
				private const String _path = "TPSystem/TPBPass/images/";
				private const String _printPath = "data/" + _path;
				private readonly Dictionary<String, ImageData> _images = new()
				{
					{ "MAIN_FON", new ImageData() },
					{ "PREM_REWARD", new ImageData() },
					{ "TAKE_REWARD", new ImageData() },
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
					
					if (image != null)
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
					string url = "\x66\x69\x6C\x65\x3A\x2F\x2F" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + _path + image.Key + "\x2E\x70\x6E\x67";
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