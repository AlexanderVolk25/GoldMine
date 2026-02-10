// protected by Rustfuscator
#pragma warning disable
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using Oxide.Core;
using System.Collections;
using System;
using UnityEngine.Networking;
using System.IO;
using ConVar;
namespace Oxide.Plugins
{
	[Info("TPMenuSystem", "pluginfuel.ru", "20.0.2")]
	class TPMenuSystem : RustPlugin
	{
		#region Вар
			[PluginReference] Plugin TPInfoSystem, TPShop, UniqueCupboard, TPMiningFarm, TPBaraxolka, MBKits, IQEconomic,TPEconomic, ImageLibrary, TPRulesSystem, TPWipeBlock, TPKits, TPCaseSystem, TPStatsSystem, TPTeleportation, TPReportSystem, TPBPass, GameStoresRUST, TPLotterySystem, SkinDrop, TPChat, TPWipeSchedule, StripesEvent, TPRaid, TPSkillSystem, DailyRewards, TPRaidAlert, TPReward, TPClan, TPMenuInfo, TPPrm, TPNotifications;
			public string Layer = "Menu_UI";
			private static TPMenuSystem _;
			
			Dictionary<ulong, string> activeButton = new Dictionary<ulong, string>();
			
			public class Settings
			{
				[JsonProperty("Название отображаемое в меню")] public string DisplayName;
				[JsonProperty("Выполняемая команда в меню")] public string Command;
				[JsonProperty("Изображение которое будет отображаться на кнопке")] public string Url;
			}
		#endregion
		
        #region Конфиг
			Configuration config;
			class Configuration
			{
				[JsonProperty("Настройки навигации меню")] public List<Settings> settings;
				
				public static Configuration GetNewConfig()
				{
					return new Configuration
					{
						settings = new List<Settings>()
						{
							new Settings {
								DisplayName = "Боевой пропуск",
								Command = "pass",
								Url = ""
							},
							new Settings {
								DisplayName = "Правила",
								Command = "rules",
								Url = ""
							},
							new Settings {
								DisplayName = "Барахолка",
								Command = "tpbaraxolka",
								Url = ""
							},
							new Settings {
								DisplayName = "Скилы",
								Command = "skill",
								Url = ""
							},
							new Settings {
								DisplayName = "Вайп блок",
								Command = "block",
								Url = ""
							},
							new Settings {
								DisplayName = "Наборы",
								Command = "kits",
								Url = ""
							},
							new Settings {
								DisplayName = "Кейсы",
								Command = "case1",
								Url = ""
							},
							new Settings {
								DisplayName = "Статистика",
								Command = "stat",
								Url = ""
							},
							new Settings {
								DisplayName = "Телепортация",
								Command = "teleport",
								Url = ""
							},
							new Settings {
								DisplayName = "Лотерея",
								Command = "lot",
								Url = ""
							},
							new Settings {
								DisplayName = "Скин дроп",
								Command = "drop",
								Url = ""
							},
							new Settings {
								DisplayName = "Вайп",
								Command = "wipe",
								Url = ""
							},
							new Settings {
								DisplayName = "Привязка",
								Command = "bot",
								Url = ""
							},
							new Settings {
								DisplayName = "Чат",
								Command = "chat",
								Url = ""
							},
							new Settings {
								DisplayName = "Информация",
								Command = "help",
								Url = ""
							},
							new Settings {
								DisplayName = "Корзина",
								Command = "store",
								Url = ""
							},
							new Settings {
								DisplayName = "Оповещения",
								Command = "notifications",
								Url = ""
							},
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
					if (config?.settings == null) LoadDefaultConfig();
				}
				catch
				{
					PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
					LoadDefaultConfig();
				}
				
				NextTick(SaveConfig);
			}
			
			protected override void LoadDefaultConfig() => config = Configuration.GetNewConfig();
			protected override void SaveConfig() => Config.WriteObject(config);
		#endregion
		
        #region Хуки
			void OnServerInitialized()
			{
				_ = this;
				
				_imageUI = new ImageUI();
				_imageUI.DownloadImage();
				
				try
				{
					if (config?.settings != null && !config.settings.Exists(s => s != null && s.Command == "pass"))
					{
						config.settings.Insert(0, new Settings { DisplayName = "Боевой пропуск", Command = "pass", Url = "" });
						SaveConfig();
					}
				}
				catch { }
				
				foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
			}
			
			
			void Unload()
			{
				if (_imageUI != null)
				{
					_imageUI.UnloadImages();
					_imageUI = null;
				}
				_ = null;
			}
			
			void OnPlayerConnected(BasePlayer player)
			{
				CuiHelper.DestroyUi(player, Layer);
				if (!activeButton.ContainsKey(player.userID))
                activeButton[player.userID] = "info";
                
                // Загружаем аватарку игрока при подключении
                if (ImageLibrary != null)
                {
                	timer.Once(1f, () => {
                		if (player != null && player.IsConnected)
                		{
                			var existingImage = (string)ImageLibrary?.Call("GetImage", player.UserIDString);
                			if (string.IsNullOrEmpty(existingImage))
                			{
                				// Загружаем аватарку из Steam
                				ImageLibrary?.Call("AddImage", $"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/{player.UserIDString.Substring(player.UserIDString.Length - 2)}/{player.UserIDString}_full.jpg", player.UserIDString, 0UL);
                			}
                		}
                	});
                }
			}
		#endregion
		
        #region Команды
			[ChatCommand("menu")]
			void ChatMenu(BasePlayer player)
			{
				MenuUI(player);
			}
			
			[ChatCommand("info")]
			void ChatInfo(BasePlayer player) => MenuUI(player, "info");
			
			[ChatCommand("skill")]
			void ChatSkill(BasePlayer player) => MenuUI(player, "skill");
			
			[ChatCommand("block")]
			void ChatBlock(BasePlayer player) => MenuUI(player, "block");
			
			[ChatCommand("kits")]
			void ChatKits(BasePlayer player) => MenuUI(player, "kits");
			
			[ChatCommand("case")]
			void ChatCase(BasePlayer player) => MenuUI(player, "case");
			
			[ChatCommand("stat")]
			void ChatStat(BasePlayer player) => MenuUI(player, "stat");
			
			[ChatCommand("tpmenu")]
			void ChatTeleport(BasePlayer player) => MenuUI(player, "teleport");
			
			[ChatCommand("lot")]
			void ChatLot(BasePlayer player) => MenuUI(player, "lot");
			
			[ChatCommand("tpbaraxolka")]
			void Chattpbaraxolka(BasePlayer player) => MenuUI(player, "tpbaraxolka");
			
			[ChatCommand("shop")]
			void Chattpshop(BasePlayer player) => MenuUI(player, "tpshop");
			
			[ChatCommand("mainingfarm")]
			void ChatMainingFarm(BasePlayer player) => MenuUI(player, "mainingfarm");
			
			[ChatCommand("drop")]
			void ChatDrop(BasePlayer player) => MenuUI(player, "drop");
			
			[ChatCommand("clans")]
			void ChatClans(BasePlayer player) => MenuUI(player, "clans");
			
			[ChatCommand("wipe")]
			void ChatWipe(BasePlayer player) => MenuUI(player, "wipe");
			
			[ChatCommand("chat")]
			void ChatChat(BasePlayer player) => MenuUI(player, "chat");
			
			[ChatCommand("report")]
			void ChatReport(BasePlayer player) => MenuUI(player, "report");
			
			[ChatCommand("bot")]
			void ChatBot(BasePlayer player) => MenuUI(player, "alert");
			
			[ChatCommand("pass")]
			void ChatPass(BasePlayer player) => MenuUI(player, "pass");
			
			[ChatCommand("rules")]
			void ChatRules(BasePlayer player) => MenuUI(player, "rules");
			
			[ChatCommand("stripe")]
			void ChatStripe(BasePlayer player) => MenuUI(player, "stripe");
			
			[ChatCommand("help")]
			void ChatHelp(BasePlayer player) => MenuUI(player, "help");
			
			[ChatCommand("refreshavatar")]
			void ChatRefreshAvatar(BasePlayer player)
			{
				RefreshPlayerAvatar(player);
				player.ChatMessage("Аватарка обновляется... Подождите 2 секунды.");
			}
			
			[ConsoleCommand("menu")]
			void ConsoleMenu(ConsoleSystem.Arg args)
			{
				var player = args.Player();
				activeButton[player.userID] = args.Args[0];
				ButtonUI(player);
				UI(player, args.Args[0]);
			}
		#endregion
		
        #region Интерфейс
			void MenuUI(BasePlayer player, string name = "")
			{
				if (name != "")
                activeButton[player.userID] = name;
				CuiHelper.DestroyUi(player, Layer);
				CuiElementContainer container = new CuiElementContainer();
				
				container.Add(new CuiPanel
					{
						CursorEnabled = true,
						RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
						Image = { Color = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
					}, "OverlayNonScaled", Layer);
					
					container.Add(new CuiPanel
						{
							RectTransform = { AnchorMin = "0.283 0.2", AnchorMax = $"0.85 0.8", OffsetMax = "0 0" },
							Image = { Color = "0 0 0 0.9" }
						}, Layer, ".Mains");
						
						CuiHelper.AddUi(player, container);
						ButtonUI(player);
						var command = name == "" ? activeButton[player.userID] : name;
						UI(player, command);
						
			}
			
			void UI(BasePlayer player, string name, string command = "")
			{
				DestroyUI(player);
				if (name == "info")
				{
					TPInfoSystem?.Call("InfoUI", player);
				}
				if (name == "tpshop")
				{
					TPShop?.Call("DeleteUserInShow", player);
					TPShop?.Call("TPShopUI", player);
				}
				if (name == "rules")
				{
					TPRulesSystem?.Call("RulesUI", player);
				}
				if (name == "block")
				{
					TPWipeBlock?.Call("BlockUi", player);
				}
				if (name == "kits")
				{
					MBKits?.Call("OpenMBKitsForPlayer", player);
				}
				if (name == "case")
				{
					TPCaseSystem?.Call("ChatCase", player);
				}
				if (name == "stat")
				{
					TPStatsSystem?.Call("PlayerTopInfo", player, (ulong)player.userID);
				}
				if (name == "teleport")
				{
					TPTeleportation?.Call("DDrawMenu", player);
				}
				if (name == "report")
				{
					TPReportSystem?.Call("ReportUI", player);
				}
				if (name == "tpbaraxolka")
				{
					TPBaraxolka?.Call("DrawNPCUI", player);
				}
				if (name == "chat")
				{
					TPChat?.Call("ChatCommandOpenedUI", player);
				}
				if (name == "pass")
				{
					TPBPass?.Call("SeasonUI", player);
				}
				if (name == "store")
				{
					GameStoresRUST?.Call("InitializeStore", player, 0);
				}
				if (name == "lot")
				{
					TPLotterySystem?.Call("LotteryUI", player, 0);
				}
				if (name == "drop")
				{
					SkinDrop?.Call("SkinDropUI", player);
				}
				if (name == "wipe")
				{
					TPWipeSchedule?.Call("BuildUI", player);
				}
				if (name == "alert")
				{
					TPRaidAlert?.Call("AlertUI", player);
				}
				if (name == "prm")
				{
					TPPrm?.Call("GrandUI_OpenEmbedded", player, ".Mains");
				}
				if (name == "skill")
				{
					TPSkillSystem?.Call("UI_DrawResearch", player);
				}
				if (name == "stripe")
				{
					StripesEvent?.Call("OpenEvent", player);
				}
				if (name == "daily")
				{
					DailyRewards?.Call("OpenAwardsUI", player);
				}
				if (name == "mainingfarm")
				{
					TPMiningFarm?.Call("chatMiningFarm", player);
				}
				if (name == "clans")
				{
					TPClan?.Call("ClanMainUi", player);
				}
				if (name == "cup")
				{
					UniqueCupboard?.Call("OpenMenu", player);
				}
				if (name == "help")
				{
					TPMenuInfo?.Call("UI_DrawMain", player);
				}
				if (name == "notifications")
				{
					TPNotifications?.Call("OpenNotificationsUI", player);
				}
			}
			void ButtonUI(BasePlayer player)
			{
				CuiHelper.DestroyUi(player, Layer + ".Main");
				var container = new CuiElementContainer();
				
				container.Add(new CuiElement
					{
						Name = Layer + ".Main",
						Parent = Layer,
						Components =
						{
							new CuiRawImageComponent { Png = _imageUI.GetImage("MAIN_BLOCK") },
							new CuiRectTransformComponent { AnchorMin = $"0.1466 0.19", AnchorMax = $"0.27 0.814", OffsetMax = "0 0" }
						}
					});
					
					string avatarImage = "";
					if (ImageLibrary != null)
					{
						avatarImage = (string)ImageLibrary?.Call("GetImage", player.UserIDString) ?? "";
						
						
						if (string.IsNullOrEmpty(avatarImage))
						{
							
							ImageLibrary?.Call("AddImage", $"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/{player.UserIDString.Substring(player.UserIDString.Length - 2)}/{player.UserIDString}_full.jpg", player.UserIDString, 0UL);
							
							
							avatarImage = "";
							
							
							timer.Once(2f, () => {
								if (player != null && player.IsConnected)
									ButtonUI(player);
							});
						}
					}
					else
					{
						PrintWarning("ImageLibrary плагин не найден! Аватарки игроков не будут отображаться.");
					}
					
					container.Add(new CuiElement
						{
							Name = Layer + ".ImgAvater",
							Parent = Layer ,
							Components =
							{
								new CuiRawImageComponent
								{
									Png = !string.IsNullOrEmpty(avatarImage) ? avatarImage : _imageUI?.GetImage("AVATAR_DEFAULT") ?? "",
								},
								
								new CuiRectTransformComponent
								{
									AnchorMin = "0.152 0.74",
									AnchorMax = "0.189 0.809",
								}
							}

						});
						container.Add(new CuiElement
							{
								Name = "PlayerName",
								Parent = Layer + ".Main",
								Components =
								{
									new CuiTextComponent
									{
										Text = $"{player.displayName}",
										FontSize = 12,
										Align = TextAnchor.MiddleLeft,
										Color = "1 1 1 1",
									},
									
									new CuiRectTransformComponent
									{
										AnchorMin = "0.4 0.94",
										AnchorMax = "1 0.97",
									},
									
								}
							});
							container.Add(new CuiElement
								{
									Name = "PlayerBalance",
									Parent = Layer + ".Main",
									Components =
									{
										new CuiTextComponent
										{
											Text = GetPlayerBalance(player),
											FontSize = 11,
											Align = TextAnchor.MiddleLeft,
											Color = "1 1 1 1",
										},
										
										new CuiRectTransformComponent
										{
											AnchorMin = "0.5 0.90",
											AnchorMax = "1 0.93",
										},
										
									}
								});
								
								float width = 0.224f, height = 0.0523f, startxBox = 0.005f, startyBox = 0.86f - height, xmin = startxBox, ymin = startyBox;
								if (config?.settings != null)
								{
									foreach (var check in config.settings)
									{
									container.Add(new CuiButton
										{
											RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1) },
											Button = { Color = "1 1 1 0", Command = $"menu {check.Command}" },
											Text = { Text = "" }
										}, Layer + ".Main", "Button");
										
										container.Add(new CuiElement
											{
												Name = "ButtonImage",
												Parent = "Button",
												Components =
												{
													new CuiRawImageComponent { Png =  _imageUI.GetImage("BUTTON_MAIN") },
													new CuiRectTransformComponent { AnchorMin = "0.15 0.1", AnchorMax = "0.8 0.83" }
												}
											});
											
											container.Add(new CuiElement
												{
													Parent = "ButtonImage",
													Components =
													{
														new CuiRawImageComponent { Png = _imageUI.GetImage($"{check.Url}") },
														new CuiRectTransformComponent { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7"}
													}
												});
												
												var color = activeButton[player.userID] == check.Command ? "1 1 1 1" : "0 0 0 0";
												container.Add(new CuiElement
													{
														Parent = "ButtonImage",
														Components =
														{
															new CuiRawImageComponent { Png = _imageUI.GetImage("BUTTON_ACTIVE"), Color = color },
															new CuiRectTransformComponent { AnchorMin = "-0.15 0.2", AnchorMax = "-0.06 0.8"}
														}
													});
													
													container.Add(new CuiElement
														{
															Name = "Button" + "Text",
															Parent = "Button",
															Components =
															{
																new CuiRawImageComponent { Png = _imageUI.GetImage("SHOW_TEXT") },
																new CuiRectTransformComponent { AnchorMin = "0.95 0.1", AnchorMax = "4.15 0.85", OffsetMax = "0 0", OffsetMin = "0 0" }
															}
														});
														
														container.Add(new CuiButton
															{
																RectTransform = { AnchorMin = "0.08 0", AnchorMax = "1 1" },
																Button = { Color = "0 0 0 0", Command = $"menu {check.Command}" },
																Text = { Text = check.DisplayName, Color = "1 1 1 0.4", FontSize = 12, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
															}, "Button" + "Text");
															
											xmin += width;
											if (xmin + width >= 0)
										{
											xmin = startxBox;
											ymin -= height - 0.005f;
										}
									}
								}
								
								CuiHelper.AddUi(player, container);
			}
		#endregion 
		
        #region Хелпер
			void DestroyUI(BasePlayer player)
			{
				CuiHelper.DestroyUi(player, "lay" + ".Main");
				CuiHelper.DestroyUi(player, "lay" + ".Command");
				CuiHelper.DestroyUi(player, "Alert_UI" + ".Main");
				CuiHelper.DestroyUi(player, "Command");
				CuiHelper.DestroyUi(player, ".Commands");
				CuiHelper.DestroyUi(player, "Rules_UI" + ".Main");
				CuiHelper.DestroyUi(player, "MainStats" + ".Main");
				CuiHelper.DestroyUi(player, "ui.kits" + ".Main");
				CuiHelper.DestroyUi(player, "TPMENULAYER");
				CuiHelper.DestroyUi(player, "TPMENULAYER1");
				CuiHelper.DestroyUi(player, "TPMENULAYER2");
				CuiHelper.DestroyUi(player, ".SGUI");
				CuiHelper.DestroyUi(player, "UI_TPCHAT_CONTEXT");
				CuiHelper.DestroyUi(player, "UI_TPBaraxolka_External");
				CuiHelper.DestroyUi(player, "UI_TPBaraxolka_Internal");
				CuiHelper.DestroyUi(player, "TPSystem.Content");
				CuiHelper.DestroyUi(player, "UI_GameStoresRUST_Bucket");
				CuiHelper.DestroyUi(player, "UI_GameStoresRUST_Store");
				CuiHelper.DestroyUi(player, "TPMiningFarm.Content");
				CuiHelper.DestroyUi(player, "MAIN_SHOP_LAYER");
				CuiHelper.DestroyUi(player, "UI_CASES_MENU");
				CuiHelper.DestroyUi(player, "UC_MainMenu");
				CuiHelper.DestroyUi(player, "RA_MainMenu");
				CuiHelper.DestroyUi(player, "UI.DailyRewards");
				CuiHelper.DestroyUi(player, "UI_CupLayerEvent");
				CuiHelper.DestroyUi(player, "FClan.Layer");
				CuiHelper.DestroyUi(player, ".Progress");
				CuiHelper.DestroyUi(player, "UI_BATTLEPASS_MAINLAYER");
				CuiHelper.DestroyUi(player, "TPPrm__BG");
				CuiHelper.DestroyUi(player, "TPPrm__main");
				CuiHelper.DestroyUi(player, "ER_Close_Top");
				CuiHelper.DestroyUi(player, "UI_Info");
				CuiHelper.DestroyUi(player, "UI_Items");
				CuiHelper.DestroyUi(player, ".Imagess");
				CuiHelper.DestroyUi(player, "TPBPass_UI");
				CuiHelper.DestroyUi(player, "MBKits_UI");
				CuiHelper.DestroyUi(player, "MBKits_Inv_UI");
				CuiHelper.DestroyUi(player, "TPNotifications_UI");
			}
			
			string GetPlayerBalance(BasePlayer player)
			{
				
				if (IQEconomic != null)
				{
					var balance = IQEconomic?.Call("API_GET_BALANCE", player.userID);
					if (balance != null)
					{
						return $"{balance:F2}";
					}
				}
				
				
				if (TPEconomic != null)
				{
					var balance = TPEconomic?.Call("API_GET_BALANCE", player.userID);
					if (balance != null)
					{
						return $"{balance:F2}";
					}
				}
				
				
				return "0";
			}
			
			[HookMethod("UpdateUIBalance")]
			public void UpdateUIBalance(BasePlayer player)
			{
				
				if (player != null && player.IsConnected)
				{
					ButtonUI(player);
				}
			}
			
			[HookMethod("RefreshPlayerAvatar")]
			public void RefreshPlayerAvatar(BasePlayer player)
			{
				
				if (player != null && player.IsConnected && ImageLibrary != null)
				{
					
					ImageLibrary?.Call("RemoveImage", player.UserIDString);
					
					
					ImageLibrary?.Call("AddImage", $"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/avatars/{player.UserIDString.Substring(player.UserIDString.Length - 2)}/{player.UserIDString}_full.jpg", player.UserIDString, 0UL);
					
					
					timer.Once(2f, () => {
						if (player != null && player.IsConnected)
							ButtonUI(player);
					});
				}
			}
		#endregion
		
        #region Images
			private static ImageUI _imageUI;
			private class ImageUI
			{
				private const String _path = "TPSystem/TPMenu/";
				private const String _printPath = "data/" + _path;
				private readonly Dictionary<String, ImageData> _images = new()
				{
					{ "MAIN_BLOCK", new ImageData() },
					{ "SHOW_TEXT", new ImageData() },
					{ "BUTTON_MAIN", new ImageData() },
					{ "BUTTON_ACTIVE", new ImageData() },
					{ "COMMAND_INFO", new ImageData() },
					{ "BUTTON_INFO", new ImageData() },
					{ "BUTTON_SHOP", new ImageData() },
					{ "BUTTON_MINING", new ImageData() },
					{ "BUTTON_KIT", new ImageData() },
					{ "BUTTON_REPORT", new ImageData() },
					{ "BUTTON_BLOCK", new ImageData() },
					{ "BUTTON_TELEPORT", new ImageData() },
					{ "BUTTON_STATS", new ImageData() },
					{ "BUTTON_CASE", new ImageData() },
					{ "BUTTON_SKIN", new ImageData() },
					{ "BUTTON_WIPE", new ImageData() },
					{ "BUTTON_BOT", new ImageData() },
					{ "BUTTON_CHAT", new ImageData() },
					{ "BUTTON_STORE", new ImageData() }
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