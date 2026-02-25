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
namespace Oxide.Plugins
{
	[Info("TPWipeBlock", "pluginfuel.ru", "20.0.2")]
	class TPWipeBlock : RustPlugin
	{
		[PluginReference] private Plugin ImageLibrary, Duel, ArenaTournament, TPMenuSystem;
		private static TPWipeBlock _;
		private static ConfigData config;
		private string CONF_IgnorePermission = "tpwipeblock.block.ignore";
		private class ConfigData
		{
			[JsonProperty(PropertyName = "Блокировка предметов")]
			public Dictionary<int, List<string>> items;
		}
		private ConfigData GetDefaultConfig()
		{
			return new ConfigData
			{
				items = new Dictionary<int, List<string>>
				{
					[7200] = new List<string>()
					{
						"crossbow",
						"shotgun.waterpipe",
						"flamethrower",
						"bucket.helmet",
						"pistol.revolver",
						"riot.helmet"
					},
					[14400] = new List<string>()
					{
						"pistol.python",
						"pistol.semiauto",
						"shotgun.double",
						"coffeecan.helmet",
						"pistol.m92",
						"roadsign.jacket"
					},
					[21600] = new List<string>()
					{
						"rifle.semiauto",
						"shotgun.pump",
						"smg.2",
						"smg.mp5",
						"smg.thompson",
						"shotgun.spas12"
					},
					[36000] = new List<string>()
					{
						"rifle.m39",
						"metal.facemask",
						"rifle.bolt",
						"grenade.f1",
						"hmlmg",
						"metal.plate.torso"
					},
					[64800] = new List<string>()
					{
						"heavy.plate.helmet",
						"heavy.plate.jacket",
						"heavy.plate.pants",
						"rifle.ak.ice",
						"metal.plate.torso.icevest",
						"metal.facemask.icemask"
					},
					[86400] = new List<string>()
					{
						"rifle.ak",
						"rifle.lr300",
						"rifle.l96",
						"grenade.beancan",
						"explosive.satchel",
						"ammo.rifle.explosive"
					},
					[1008000] = new List<string>()
					{
						"lmg.m249",
						"rocket.launcher",
						"explosive.timed",
						"rifle.ak.diver",
						"multiplegrenadelauncher",
						"homingmissile.launcher"
					},
				}
			};
		}
		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<ConfigData>();
				if (config == null)
				{
					LoadDefaultConfig();
				}
			}
			catch
			{
				LoadDefaultConfig();
			}
			SaveConfig();
		}
		protected override void LoadDefaultConfig()
		{
			config = GetDefaultConfig();
		}
		protected override void SaveConfig()
		{
			Config.WriteObject(config);
		}
		object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId itemContainer, int num, int num2)
		{
			if (inventory == null || item == null) return null;
			var player = inventory.GetComponent<BasePlayer>();
			if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission)) return null;
			var container = inventory.FindContainer(itemContainer);
			if (container == null || container.entityOwner == null) return null;
			if ((container.entityOwner is AutoTurret || container.entityOwner is MLRS))
			{
				var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
				if (isBlocked == false)
				{
					MessBlockUi(player, item.info.shortname);
					timer.Once(0.8f, () =>
						{
							CuiHelper.DestroyUi(player, Layer);
						});
						return true;
				}
			}
			return null;
		}
		object CanAcceptItem(ItemContainer container, Item item)
		{
			if (container == null || item == null || container.entityOwner == null) return null;
			if (container.entityOwner is AutoTurret || container.entityOwner is MLRS)
			{
				var player = item.GetOwnerPlayer();
				if (player == null) return null;
				if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission)) return null;
				var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
				if (isBlocked == false)
				{
					MessBlockUi(player, item.info.shortname);
					timer.Once(0.8f, () =>
						{
							CuiHelper.DestroyUi(player, Layer);
						});
						return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
				}
			}
			return null;
		}
		private object CanWearItem(PlayerInventory inventory, Item item)
		{
			var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
			if (!player.userID.IsSteamId())
			{
				return null;
			}
			if (playerOnDuel(player)) return null;
			if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission))
			return null;
			var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
			if (isBlocked == false)
			{
				if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
				return null;
				MessBlockUi(player, item.info.shortname);
				timer.Once(0.8f, () =>
					{
						CuiHelper.DestroyUi(player, Layer);
					});
			}
			return isBlocked;
		}
		private object CanEquipItem(PlayerInventory inventory, Item item)
		{
			var player = inventory.gameObject.ToBaseEntity() as BasePlayer;
			if (player == null) return null;
			if (playerOnDuel(player)) return null;
			if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission))
			return null;
			var isBlocked = IsBlocked(item.info.shortname) > 0 ? false : (bool?)null;
			if (isBlocked == false)
			{
				if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
				return null;
				MessBlockUi(player, item.info.shortname);
				timer.Once(3.8f, () =>
					{
						CuiHelper.DestroyUi(player, Layer);
					});
			}
			return isBlocked;
		}
		object OnWeaponReload(BaseProjectile projectile, BasePlayer player)
		{
			if (!player.userID.IsSteamId())
			{
				return null;
			}
			
			if (playerOnDuel(player)) return null;
			if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission))
			return null;
			if (player.GetComponent<NPCPlayer>() != null || player.GetComponent<BaseNpc>() != null || player.IsNpc)
			return null;
			var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?)null;
			if (isBlocked == false)
			{
				MessBlockUi(player, projectile.primaryMagazine.ammoType.shortname);
				timer.Once(2f, () =>
					{
						CuiHelper.DestroyUi(player, Layer);
					});
					return isBlocked;
			}
			
			return null;
		}
		object OnMagazineReload(BaseProjectile projectile, IAmmoContainer desiredAmount, BasePlayer player)
		{
			if (!player.userID.IsSteamId())
			{
				return null;
			}
			
			if (playerOnDuel(player)) return null;
			if (permission.UserHasPermission(player.UserIDString, CONF_IgnorePermission))
			return null;
			NextTick(() =>
				{
					var isBlocked = IsBlocked(projectile.primaryMagazine.ammoType) > 0 ? false : (bool?)null;
					if (isBlocked == false)
					{
						projectile.primaryMagazine.contents = 0;
						projectile.SendNetworkUpdate();
						player.SendNetworkUpdate();
						MessBlockUi(player, projectile.primaryMagazine.ammoType.shortname);
						timer.Once(2f, () =>
							{
								CuiHelper.DestroyUi(player, Layer);
							});
					}
				});
				return null;
		}
		private bool playerOnDuel(BasePlayer player)
		{
			if (plugins.Find("ArenaTournament") && (bool)plugins.Find("ArenaTournament").Call("IsOnTournament", (ulong)player.userID)) return true;
			if (plugins.Find("Duel") && (bool)plugins.Find("Duel").Call("IsPlayerOnActiveDuel", player)) return true;
			if (plugins.Find("OneVSOne") && (bool)plugins.Find("OneVSOne").Call("IsEventPlayer", player)) return true;
			return false;
		}
		private void OnServerInitialized()
		{
			_ = this;
			_imageUI = new ImageUI();
			_imageUI.DownloadImage();
			permission.RegisterPermission(CONF_IgnorePermission, this);
			foreach (var check in config.items.SelectMany(p => p.Value))
			{
				// Убираем загрузку изображений с внешних URL - используем только ImageLibrary для стандартных предметов
				// ImageLibrary.Call("AddImage", "https://rustexplore.com/images/130/" + check + ".png", check);
			}
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
		
		private void MessBlockUi(BasePlayer player, string shortname)
		{
			CuiHelper.DestroyUi(player, Layer);
			CuiElementContainer container = new CuiElementContainer();
			container.Add(new CuiPanel
				{
					Image = { Color = HexToRustFormat("#534E489E") },
					RectTransform =
					{AnchorMin = "0.5 0.9", AnchorMax = "0.5 0.9", OffsetMin = "-120 -25", OffsetMax = "120 50"},
					CursorEnabled = false,
				}, "Overlay", Layer);
				container.Add(new CuiElement
					{
						Parent = Layer,
						Name = Layer + ".BlockItem",
						Components =
						{
							new CuiImageComponent {Color = "1 1 1 0.1"},
							new CuiRectTransformComponent { AnchorMin = "0.01586128 0.05839238", AnchorMax = "0.2891653 0.9208925" }
						}
					});
					container.Add(new CuiElement
						{
							Parent = Layer + ".BlockItem",
							Components =
							{
								new CuiRawImageComponent
								{
									Png = (string) ImageLibrary.Call("GetImage", $"{shortname}")
								},
								new CuiRectTransformComponent
								{AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 1", OffsetMax = "-5 -1"}
							}
						});
						
						container.Add(new CuiElement
							{
								Parent = Layer,
								Components =
								{
									new CuiTextComponent()
									{
										Color = "1 1 1 1",
										Text = "Предмет заблокирован, для получения дополнительной информации пишите /block",
										FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf"
									},
									new CuiRectTransformComponent {AnchorMin = "0.3204 0.0833925", AnchorMax = "0.9802345 0.9458925"},
								}
							});
							
							CuiHelper.AddUi(player, container);
		}
		
        private const string Layer = "lay";
        void BlockUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiElementContainer container = new CuiElementContainer();
			
            container.Add(new CuiElement
				{
					Name = Layer + ".Main",
					Parent = ".Mains",
					Components =
					{
						new CuiRawImageComponent { Png = _imageUI.GetImage("MAIN_FON"), Color = "1 1 1 1" },
						new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
					}
				});
				
				container.Add(new CuiButton
					{
						RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
						Button = { Close = "Menu_UI", Color = "0 0 0 0" },
						Text = { Text = "" }
					}, Layer + ".Main");
					
					container.Add(new CuiElement
						{
							Parent = Layer + ".Main",
							Name = "BlockItems",
							Components =
							{
								new CuiScrollViewComponent
								{
									Horizontal = false,
									Vertical = true,
									MovementType = UnityEngine.UI.ScrollRect.MovementType.Unrestricted,
									Elasticity = 0,
									Inertia = true,
									DecelerationRate = 0.24f,
									ScrollSensitivity = 20,
									ContentTransform = new()
									{
										AnchorMin = "0.5 1", AnchorMax = "0.5 1"
									},
								},
								new CuiRectTransformComponent() { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-350 -222", OffsetMax = "350 195" }
							}
						});
						
						float currentY = -8f; // Начинаем с отступа сверху
						float titleHeight = 30f; // Увеличиваем высоту заголовка
						float itemWidth = 77f, itemHeight = 76f, gap = 3f;
						int columns = 10, p = 0;
						
						foreach (var check in config.items)
						{
							p++;
							container.Add(new CuiPanel
								{
									RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"-350 {-currentY - titleHeight}", OffsetMax = $"350 {-currentY}" },
									Image = { Color = "1 1 1 0" }
								}, "BlockItems", "Title" + p);
								
								var text = IsBlocked(check.Value.ElementAt(0)) > 0 ? $"{FormatShortTime(TimeSpan.FromSeconds(IsBlocked(check.Value.ElementAt(0))))}" : "разблокированно";
								container.Add(new CuiLabel
									{
										RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-10 0" },
										Text = { Text = $"<b>{p} этап</b> - {text}", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.8", Font = "robotocondensed-bold.ttf" },
									}, "Title" + p);
									
									
									currentY += titleHeight + gap;
									List<string> items = check.Value;
									
									int totalRows = Mathf.CeilToInt(items.Count / (float)columns);
									
									for (int i = 0; i < items.Count; i++)
									{
										int row = i / columns;
										int col = i % columns;
										
										float x = -350 + col * (itemWidth + gap);
										float y = currentY + row * (itemHeight + gap);
										
										var item = items[i];
										
										container.Add(new CuiPanel
											{
												RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"{x} {-y - itemHeight}", OffsetMax = $"{x + itemWidth} {-y}" },
												Image = { Color = "0 0 0 0" }
											}, "BlockItems", "Items" + i + "_" + p);
											
											container.Add(new CuiElement
												{
													Parent = "Items" + i + "_" + p,
													Components =
													{
														new CuiRawImageComponent { Png = _imageUI.GetImage("ITEM_BACKGROUND"), FadeIn = 1f },
														new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
													}
												});
												
												var color = IsBlocked(item) > 0 ? "1 1 1 0.2" : "1 1 1 1";
												var itemImage = ItemManager.FindItemDefinition(item).itemid;
												container.Add(new CuiElement
													{
														Parent = "Items" + i + "_" + p,
														Components =
														{
															new CuiImageComponent {ItemId = itemImage, Color = color, FadeIn = 1f },
															new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "15 15", OffsetMax = "-15 -15" }
														}
													});
									}
									currentY += totalRows * (itemHeight + gap) + gap + 10f; // Добавляем отступ между этапами
						}
						
						// Исправляем расчет размера контента
						foreach (var el in container)
						{
							foreach (var comp in el.Components)
							{
								if (comp is CuiScrollViewComponent scroll)
								{
									float scrollHeight = -(currentY + 50f); // Добавляем отступ снизу
									scroll.ContentTransform.OffsetMin = $"0 {scrollHeight}";
									scroll.ContentTransform.OffsetMax = $"0 0";
								}
							}
						}
						
						CuiHelper.AddUi(player, container);
		}
		
        private double IsBlocked(string shortName)
        {
            if (!config.items.SelectMany(p => p.Value).Contains(shortName))
			return 0;
            var blockTime = config.items.FirstOrDefault(p => p.Value.Contains(shortName)).Key;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            return lefTime > 0 ? lefTime : 0;
		}
		
        private bool BlockTimeGui(string shortName)
        {
            var blockTime = config.items.FirstOrDefault(p => p.Value.Contains(shortName)).Key;
            var lefTime = (UnBlockTime(blockTime)) - CurrentTime();
            if (lefTime > 0)
            {
                return true;
			}
			
            return false;
		}
        private double UnBlockTime(int amount) => SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + amount;
        private double IsBlocked(ItemDefinition itemDefinition) => IsBlocked(itemDefinition.shortname);
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }
		
        public static string FormatShortTime(TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
			result = $"{time.Days}д ";
            if (time.Hours != 0 || result != string.Empty)
			result += $"{time.Hours:D2}ч ";
            if (time.Minutes != 0 || result != string.Empty)
			result += $"{time.Minutes:D2}м ";
            result += $"{time.Seconds:D2}с";
            return result.Trim();
		}
		
        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
			}
			
            var str = hex.Trim('#');
			
            if (str.Length == 6)
			str += "FF";
			
            if (str.Length != 8)
            {
                throw new Exception(hex);
			}
			
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
			
            Color color = new Color32(r, g, b, a);
			
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
		}
		
        #region Images
			private static ImageUI _imageUI;
			private class ImageUI
			{
				private const String _path = "TPSystem/TPBlock/";
				private const String _printPath = "data/" + _path;
				private readonly Dictionary<String, ImageData> _images = new()
				{
					{ "MAIN_FON", new ImageData() },
					{ "ITEM_BACKGROUND", new ImageData() },
					{ "CASTLE_ICON", new ImageData() },
					{ "BACKGROUND_BLOK_1", new ImageData() },
					{ "BACKGROUND_BLOK_2", new ImageData() },
					{ "BACKGROUND_BLOK_3", new ImageData() },
					{ "BACKGROUND_BLOK_4", new ImageData() },
					{ "BACKGROUND_BLOK_5", new ImageData() },
					{ "BACKGROUND_BLOK_6", new ImageData() }
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
					string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + _path + image.Key + ".png";
					using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
					{
						yield return www.SendWebRequest();
						if (www.isNetworkError || www.isHttpError)
						{
							image.Value.Status = ImageStatus.Failed;
							_.PrintWarning($"Не удалось загрузить изображение {image.Key}: {www.error}");
						}
						else
						{
							try
							{
								Texture2D tex = DownloadHandlerTexture.GetContent(www);
								if (tex != null)
								{
									image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
									image.Value.Status = ImageStatus.Loaded;
									_.Puts($"Изображение {image.Key} успешно загружено");
									UnityEngine.Object.DestroyImmediate(tex);
								}
								else
								{
									image.Value.Status = ImageStatus.Failed;
									_.PrintWarning($"Не удалось получить текстуру для изображения {image.Key}");
								}
							}
							catch (System.Exception ex)
							{
								image.Value.Status = ImageStatus.Failed;
								_.PrintError($"Ошибка при обработке изображения {image.Key}: {ex.Message}");
							}
						}
						DownloadImage();
					}
				}
			}
		#endregion
	}
	
}
