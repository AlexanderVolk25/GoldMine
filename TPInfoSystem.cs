using System.Linq;
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
namespace Oxide.Plugins
{
	[Info("TPInfoSystem", "pluginfuel.ru", "20.0.2")]
	class TPInfoSystem : RustPlugin
	{
		#region Вар
			public string Layer = "Info_UI";
			[PluginReference] Plugin TPPermissions, ImageLibrary, TPWipeSchedule, TPMenuSystem;
			private static TPInfoSystem _;
			public class CommandSettings
			{
				[JsonProperty("Название отображаемое в меню")] public string DisplayName;
				[JsonProperty("Выполняемая команда в меню")] public string Command;
				[JsonProperty("Текст")] public string Text;
			}
			public class InfoSettings
			{
				[JsonProperty("Это баннер?")] public bool BannerEnable;
				[JsonProperty("Картинка баннера")] public string ImageBannerUrl;
				[JsonProperty("Информация")] public string Text;
				[JsonProperty("Картинка информации")] public string ImageInfoUrl;
			}
		#endregion
		#region Конфиг
			Configuration config;
			class Configuration
			{
				[JsonProperty("При заходе на сервер, требуется ли отображать страницу информации?")] public bool InfoEnable = true;
				[JsonProperty("Первый заголовок в окне с информацией")] public string ServerName = "SANYA RUST X1 SOLO АЛЕЛА СЕМПАЙ";
				[JsonProperty("Первый текст с информацией")] public string Text1 = "Текст заполнитель - это текст, который имеет некоторые характеристики реального письменного текста, но является случайным набором слов или сгенерирован иным образом. Его можно использовать для отображения образца шрифтов, создание текста для тестирования или обхода.";
				[JsonProperty("Когда будет следующий вайп")] public string NextWipe = "26 Апреля";
				[JsonProperty("Когда был последний вайп")] public string LastWipe = "10 Апреля";
				[JsonProperty("Заголовок телеграма в окне с информацией")] public string TGTitle = "TG";
				[JsonProperty("Заголовок вк в окне с информацией")] public string VKTitle = "VK";
				[JsonProperty("Заголовок дискорда в окне с информацией")] public string DSTitle = "DS";
				[JsonProperty("Включить фейк онлайн?")] public bool FakeOnlineEnable = false;
				[JsonProperty("Значение фейк онлайна")] public int FakeOnlineValue = 50;
				[JsonProperty("Кнопки в инфо")] public Dictionary<string, string> command = new Dictionary<string, string>();
				[JsonProperty("Настройки нижнего поля с информацией")] public Dictionary<int, InfoSettings> info;
				[JsonProperty("Текст отсутствия привилегий")] public string NoPrivilegeText = "У вас нет активных привилегий!\nКупите их на сайте goldmine.gamestores.app";
				public static Configuration GetNewConfig()
				{
					return new Configuration
					{
						command = new Dictionary<string, string>()
						{
							["Команды"] = "commands",
							["Правила"] = "rules",
							["Вопросы"] = "faq"
						},
						info = new Dictionary<int, InfoSettings>()
						{
							[0] = new InfoSettings
							{
								BannerEnable = false,
								ImageBannerUrl = null,
								Text = "Текст заполнитель - это текст, который имеет некоторые характеристики реального письменного текста, но является случайным набором слов или сгенерирован иным образом. Его можно использовать для отображения образца шрифтов, создание текста для тестирования или обхода.",
								ImageInfoUrl = "INFO_IMAGE"
							},
							[1] = new InfoSettings
							{
								BannerEnable = true,
								ImageBannerUrl = "BANNER_IMAGE",
								Text = null,
								ImageInfoUrl = null
							}
						},
						NoPrivilegeText = "У вас нет активных привилегий!\nКупите их на сайте goldmine.gamestores.app"
					};
				}
			}
			protected override void LoadConfig()
			{
				base.LoadConfig();
				try
				{
					config = Config.ReadObject<Configuration>();
					if (config?.info == null) LoadDefaultConfig();
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
				
				foreach (var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);
			}
			
			void OnPlayerConnected(BasePlayer player)
			{
				if (config.InfoEnable)
                InfoUI(player, true);
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
			
		#endregion
		
        #region Команды
			[ConsoleCommand("command")]
			void ConsoleCommand(ConsoleSystem.Arg args)
			{
				var player = args.Player();
				CuiHelper.DestroyUi(player, Layer);
				player.SendConsoleCommand("chat.say /help");
				player.SendConsoleCommand($"mb.info.category {args.Args[0]}");
			}
			
			[ConsoleCommand("skipBanner")]
			void ConsoleBanner(ConsoleSystem.Arg args)
			{
				var player = args.Player();
				BannerInfoUI(player, int.Parse(args.Args[0]));
			}
			
			[ConsoleCommand("privilage")]
			void ConsolePrivilage(ConsoleSystem.Arg args)
			{
				var player = args.Player();
				PrivilageUI(player);
			}
		#endregion
		
        #region Интерфейс
			void InfoUI(BasePlayer player, bool start = false)
			{
				CuiHelper.DestroyUi(player, Layer);
				var container = new CuiElementContainer();
				
				if (start == true)
				{
					
					container.Add(new CuiPanel
						{
							CursorEnabled = true,
							RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
							Image = { Color = "0 0 0 0", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
						}, "Overlay", Layer);
						
						container.Add(new CuiPanel
							{
								RectTransform = { AnchorMin = "0.220 0.2", AnchorMax = $"0.77 0.8", OffsetMax = "0 0" },
								Image = { Color = "0 0 0 0.9" }
							}, Layer, ".Mains");
				}
				container.Add(new CuiElement
							{
								Name = "lay" + ".Main",
								Parent = ".Mains",
								Components =
								{
									new CuiRawImageComponent { Png = _imageUI.GetImage("MAIN_FON") },
									new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
								}
							});
				
				container.Add(new CuiPanel
					{
						RectTransform = { AnchorMin = "0.450 0.50", AnchorMax = "0.73 0.523" },
						Image = { Color = "0.09 0.09 0.11 1" }
					}, "lay" + ".Main", ".Progress");
					
				container.Add(new CuiPanel
					{
						RectTransform = { AnchorMin = "0 0", AnchorMax = $"{(config.FakeOnlineEnable ? (float)(BasePlayer.activePlayerList.Count() + config.FakeOnlineValue) / ConVar.Server.maxplayers : (float)BasePlayer.activePlayerList.Count() / ConVar.Server.maxplayers)} 1" },
						Image = { Color = "0 0.84 0.47 1" }
					}, ".Progress");
							
							container.Add(new CuiButton
								{
									RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
									Button = { Close = start == true ? Layer : "Menu_UI", Color = "0 0 0 0" },
									Text = { Text = "" }
								}, "lay" + ".Main");
								
								container.Add(new CuiPanel
									{
										RectTransform = { AnchorMin = "0.245 0.63", AnchorMax = "0.485 0.774" },
										Image = { Color = "0 0 0 0" }
									}, "lay" + ".Main", ".Perm");
									
									container.Add(new CuiButton
												{
													RectTransform = { AnchorMin = "0.351 0.085", AnchorMax = "0.71 0.32" },
													Button = { Command = "privilage", Color = "0 0 0 0" },
													Text = { Text = "              Все привилегии", Color = "1 1 1 0.4", FontSize = 9, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
												}, ".Perm");
												
												string priv = TPPermissions?.Call("PermApi", player)?.ToString() ?? "";
												container.Add(new CuiLabel
													{
														RectTransform = { AnchorMin = "0.36 0.4", AnchorMax = "0.9 0.83", OffsetMax = "0 0" },
														Text = { Text = $"Активная привилегия:\n<b><size=13><color=#fff>{priv}</color></size></b>", Color = "1 1 1 0.4", FontSize = 8, Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf" }
													}, ".Perm");
													
													container.Add(new CuiLabel
														{
															RectTransform = { AnchorMin = "0.512 0.644", AnchorMax = "0.562 0.664", OffsetMax = "0 0" },
															Text = { Text = config.TGTitle, Color = "1 1 1 0.7", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
														}, "lay" + ".Main");
														
														container.Add(new CuiElement
															{
																Parent = "lay" + ".Main",
																Components =
																{
																	new CuiRawImageComponent { Png = _imageUI.GetImage("QR_TG") },
																	new CuiRectTransformComponent { AnchorMin = "0.512 0.67", AnchorMax = "0.562 0.754", OffsetMax = "0 0" },
																}
															});
															
															container.Add(new CuiLabel
																{
																	RectTransform = { AnchorMin = "0.605 0.644", AnchorMax = "0.65 0.664", OffsetMax = "0 0" },
																	Text = { Text = config.DSTitle, Color = "1 1 1 0.7", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
																}, "lay" + ".Main");
																
																container.Add(new CuiElement
																	{
																		Parent = "lay" + ".Main",
																		Components =
																		{
																			new CuiRawImageComponent { Png = _imageUI.GetImage("QR_DS") },
																			new CuiRectTransformComponent { AnchorMin = "0.605 0.67", AnchorMax = "0.65 0.754", OffsetMax = "0 0" },
																		}
																	});
																	
																	container.Add(new CuiLabel
																		{
																			RectTransform = { AnchorMin = "0.692 0.644", AnchorMax = "0.742 0.664", OffsetMax = "0 0" },
																			Text = { Text = config.VKTitle, Color = "1 1 1 0.7", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
																		}, "lay" + ".Main");
																		
																		container.Add(new CuiElement
																			{
																				Parent = "lay" + ".Main",
																				Components =
																				{
																					new CuiRawImageComponent { Png = _imageUI.GetImage("QR_VK") },
																					new CuiRectTransformComponent { AnchorMin = "0.692 0.67", AnchorMax = "0.742 0.754", OffsetMax = "0 0" },
																				}
																			});
																			
																			container.Add(new CuiPanel
																				{
																					RectTransform = { AnchorMin = "0.434 0.53", AnchorMax = "0.742 0.56" },
																					Image = { Color = "1 1 1 0" }
																				}, "lay" + ".Main", ".OnlineName");
																				
																				container.Add(new CuiLabel
																					{
																						RectTransform = { AnchorMin = "0.045 0", AnchorMax = "1 1", OffsetMax = "0 0" },
																						Text = { Text = $"Онлайн сервера: <b>{(config.FakeOnlineEnable ? BasePlayer.activePlayerList.Count() + config.FakeOnlineValue : BasePlayer.activePlayerList.Count())}/{ConVar.Server.maxplayers}</b>", Color = "1 1 1 0.5", FontSize = 11, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
																					}, ".OnlineName");
																					
																					container.Add(new CuiLabel
																						{
																							RectTransform = { AnchorMin = "0 0", AnchorMax = "0.945 1", OffsetMax = "0 0" },
																							Text = { Text = $"{config.ServerName}", Color = "1 1 1 0.5", FontSize = 11, Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf" }
																						}, ".OnlineName");
																						
																						container.Add(new CuiPanel
																							{
																								RectTransform = { AnchorMin = "0.434 0.416", AnchorMax = "0.742 0.467" },
																								Image = { Color = "0 0 0 0" }
																							}, "lay" + ".Main", ".Wipe");
																							
																							var nextWipeText = TPWipeSchedule?.Call("API_GET_NEXT_WIPE") as string ?? config.NextWipe;
																							var lastWipeText = TPWipeSchedule?.Call("API_GET_LAST_WIPE") as string ?? config.LastWipe;
																							
																							container.Add(new CuiLabel
																								{
																									RectTransform = { AnchorMin = "0.06 0", AnchorMax = "1 1", OffsetMax = "0 0" },
																									Text = { Text = $"Вайп был: <b><color=#ff1152>{lastWipeText}</color></b>", Color = "1 1 1 0.5", FontSize = 11, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
																								}, ".Wipe");
																								
																								container.Add(new CuiLabel
																									{
																										RectTransform = { AnchorMin = "0 0", AnchorMax = "0.937 1", OffsetMax = "0 0" },
																										Text = { Text = $"Следующий вайп: <b><color=#00d679>{nextWipeText}</color></b>", Color = "1 1 1 0.5", FontSize = 11, Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf" }
																									}, ".Wipe");
																									
																									container.Add(new CuiPanel
																										{
																											RectTransform = { AnchorMin = "0.263 0.45", AnchorMax = "0.349 0.575" },
																											Image = { Color = "0 0 0 0" }
																										}, "lay" + ".Main", ".Commands");
																										
																										float width = 1f, height = 0.275f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
																										foreach (var check in config.command)
																										{
																											container.Add(new CuiButton
																												{
																													RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0" },
																													Button = { Color = "0 0 0 0", Command = $"command {check.Value}" },
																													Text = { Text = "" }
																												}, ".Commands", "Text");
																												
																												container.Add(new CuiLabel
																													{
																														RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
																														Text = { Text = check.Key, Color = "1 1 1 0.4", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
																													}, "Text");
																													
																													xmin += width;
																													if (xmin + width >= 0)
																													{
																														xmin = startxBox;
																														ymin -= height + 0.085f;
																													}
																										}
																										
																										CuiHelper.AddUi(player, container);
																										BannerInfoUI(player, 0);
			}
			
			void BannerInfoUI(BasePlayer player, int page)
			{
				CuiHelper.DestroyUi(player, ".Info");
				var container = new CuiElementContainer();
				var check = config.info.FirstOrDefault(z => z.Key == page);
				
				container.Add(new CuiPanel
					{
						RectTransform = { AnchorMin = "0.22 0.178", AnchorMax = "0.785 0.36" },
						Image = { Color = "0 0 0 0" }
					}, "lay" + ".Main", ".Info");
					
					container.Add(new CuiButton
						{
							RectTransform = { AnchorMin = "0 0", AnchorMax = "0.033 1" },
							Button = { Command = page != 0 ? $"skipBanner {page - 1}" : "", Color = "0 0 0 0" },
							Text = { Text = "" }
						}, ".Info");
						
						container.Add(new CuiButton
							{
								RectTransform = { AnchorMin = "0.964 0", AnchorMax = "0.999 1" },
								Button = { Command = config.info.Count() > (page + 1) ? $"skipBanner {page + 1}" : "", Color = "0 0 0 0" },
								Text = { Text = "" }
							}, ".Info");
							
							if (check.Value.BannerEnable)
							{
								container.Add(new CuiPanel
									{
										RectTransform = { AnchorMin = "0.045 0", AnchorMax = "0.952 0.99" },
										Image = { Color = "0 0 0 0" }
									}, ".Info", ".ImageBanner");
									
									container.Add(new CuiElement
										{
											Parent = ".ImageBanner",
											Components =
											{
												new CuiRawImageComponent { Png = !string.IsNullOrEmpty(check.Value.ImageBannerUrl) ? _imageUI.GetImage(check.Value.ImageBannerUrl) : null },
												new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
											}
										});
							}
							else
							{
								container.Add(new CuiPanel
									{
										RectTransform = { AnchorMin = "0.07 0.08", AnchorMax = "0.235 0.91" },
										Image = { Color = "0 0 0 0" }
									}, ".Info", ".ImageInfo");
									
									container.Add(new CuiElement
										{
											Parent = ".ImageInfo",
											Components =
											{
												new CuiRawImageComponent { Png = !string.IsNullOrEmpty(check.Value.ImageInfoUrl) ? _imageUI.GetImage(check.Value.ImageInfoUrl) : null },
												new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
											}
										});
										
										container.Add(new CuiLabel
											{
												RectTransform = { AnchorMin = "0.27 0", AnchorMax = "0.78 1", OffsetMax = "0 0" },
												Text = { Text = check.Value.Text, Color = "1 1 1 0.4", FontSize = 11, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
											}, ".Info");
							}
							
							CuiHelper.AddUi(player, container);
			}
			
			void PrivilageUI(BasePlayer player)
			{
				var container = new CuiElementContainer();
				
				container.Add(new CuiElement
					{
						Name = ".Privilage",
						Parent = "lay" + ".Main",
						Components =
						{
							new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_COMMAND_INFO") },
							new CuiRectTransformComponent { AnchorMin = "0.25 0.2", AnchorMax = "0.745 0.77", OffsetMax = "0 0" },
						}
					});
					
					container.Add(new CuiButton
						{
							RectTransform = { AnchorMin = "0.96 0.94", AnchorMax = "1 1" },
							Button = { Close = ".Privilage", Color = "0 0 0 0" },
							Text = { Text = "" }
						}, ".Privilage");
						
						var countPriv = TPPermissions?.Call("PermUIApi", player);
						if (countPriv == null)
						{
							container.Add(new CuiLabel
								{
									RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.925", OffsetMax = "0 0" },
									Text = { Text = $"<b><size=20><color=#fff>{config.NoPrivilegeText}</color></size></b>", Color = "1 1 1 0.4", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
								}, ".Privilage");
						}
						else
						{
							List<api> priv = JsonConvert.DeserializeObject<List<api>>((string)TPPermissions?.Call("PermUIApi", player));
							
							float width = 0.323f, height = 0.1f, startxBox = 0f, startyBox = 0.925f - height, xmin = startxBox, ymin = startyBox;
							foreach (var check in priv)
							{
								container.Add(new CuiButton
									{
										RectTransform = { AnchorMin = xmin + " " + ymin, AnchorMax = (xmin + width) + " " + (ymin + height * 1), OffsetMax = "0 0" },
										Button = { Color = "0 0 0 0", Command = $"command" },
										Text = { Text = "" }
									}, ".Privilage", ".Priv");
									
									container.Add(new CuiElement
										{
											Parent = ".Priv",
											Components =
											{
												new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", check.Image) },
												new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.2 1", OffsetMax = "0 0" },
											}
										});
										
										container.Add(new CuiLabel
											{
												RectTransform = { AnchorMin = "0.22 0", AnchorMax = "1 1", OffsetMax = "0 0" },
												Text = { Text = $"<b><color=#fff><size=12>{check.Name}</size></color></b>\n{check.Time}", Color = "1 1 1 0.4", FontSize = 10, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
											}, ".Priv");
											
											xmin += width + 0.01f;
											if (xmin + width >= 1)
											{
												xmin = startxBox;
												ymin -= height + 0.085f;
											}
							}
						}
						
						CuiHelper.AddUi(player, container);
			}
		#endregion
		
        #region Хелпер
			class api
			{
				public string Name;
				public string Time;
				public string Image;
			}
		#endregion
		
        #region Images
			private static ImageUI _imageUI;
			private class ImageUI
			{
				private const String _path = "TPSystem/TPInfo/";
				private const String _printPath = "data/" + _path;
				private readonly Dictionary<String, ImageData> _images = new()
				{
					{ "MAIN_FON", new ImageData() },
					{ "BACKGROUND_COMMAND_INFO", new ImageData() },
					{ "QR_TG", new ImageData() },
					{ "QR_VK", new ImageData() },
					{ "QR_DS", new ImageData() },
					{ "BANNER_IMAGE", new ImageData() },
					{ "INFO_IMAGE", new ImageData() },
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
						ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value.Key, image.Value.Value));
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
				
				private IEnumerator ProcessDownloadImage(string imageName, ImageData imageData)
				{
					string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + _path + imageName + ".png";
					using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
					{
						yield return www.SendWebRequest();
						if (www.isNetworkError || www.isHttpError)
						{
							imageData.Status = ImageStatus.Failed;
							_.PrintWarning($"Не удалось загрузить изображение {imageName}: {www.error}");
						}
						else
						{
							try
							{
								Texture2D tex = DownloadHandlerTexture.GetContent(www);
								if (tex != null)
								{
									imageData.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
									imageData.Status = ImageStatus.Loaded;
									_.Puts($"Изображение {imageName} успешно загружено");
									UnityEngine.Object.DestroyImmediate(tex);
								}
								else
								{
									imageData.Status = ImageStatus.Failed;
									_.PrintWarning($"Не удалось получить текстуру для изображения {imageName}");
								}
							}
							catch (System.Exception ex)
							{
								imageData.Status = ImageStatus.Failed;
								_.PrintError($"Ошибка при обработке изображения {imageName}: {ex.Message}");
							}
						}
						DownloadImage();
					}
				}
			}
		#endregion
	}
}