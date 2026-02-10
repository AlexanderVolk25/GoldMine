using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core;
using System.Collections;
using UnityEngine.Networking;
using System.IO;
namespace Oxide.Plugins
{
	[Info("TPWipeSchedule", "pluginfuel.ru", "20.0.2")]
	public class TPWipeSchedule : RustPlugin
	{
		#region Fields⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠
			private string Layer = "UI_WipeSchedule";
			private static TPWipeSchedule _;
			[PluginReference] Plugin TPMenuSystem;
			public enum Types
			{
				None,
				GLOBAL_WIPE,
				WIPE
			}
			private Dictionary<int, string> DaysOfWeek = new Dictionary<int, string>()
			{
				[1] = "ПН",
				[2] = "ВТ",
				[3] = "СР",
				[4] = "ЧТ",
				[5] = "ПТ",
				[6] = "СБ",
				[7] = "ВС",
			};
			public List<DayClass> DaysList = new List<DayClass>();
			public class DayClass
			{
				public int day;
				public string color;
				public Types types;
				public string description;
			}
		#endregion
		#region Config⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠
			private ConfigData config;
			private class ConfigData
			{
				[JsonProperty("Раз во сколько секунд обновлять календарь?")]
				public int Delay;
				[JsonProperty("Цвет дней в нынешнем месяце (изображение)")]
				public string ActiveImage;
				[JsonProperty("Текст справа")]
				public string txtmenu;
				[JsonProperty("Расположение текста")]
				public UnityEngine.TextAnchor txtmenuAlign;
				[JsonProperty("Настройка")]
				public Dictionary<int, WipeClass> wipe;
			}
			private class WipeClass
			{
				[JsonProperty("Тип")]
				public Types type;
				[JsonProperty("Изображение с цветом кнопки")]
				public string image;
				[JsonProperty("Описание")]
				public string description;
			}
			private ConfigData GetDefaultConfig()
			{
				return new ConfigData
				{
					Delay = 7200,
					ActiveImage = "BACKGROUND_ACTIVE",
					txtmenu = "ИНФА О ВАПАХ DROP RUST:\n\n<color=#73A473>Вайп без чертежей:</color> раз в 5 дней! Меняется карта и ничего не скидывается кроме статистики сервера - /stats, вайп блокировки предметов и оружия - /wipe\n\n<color=#C74D43>Глобальный вайп:</color> раз в 10 дней! Меняется карта, сбрасываются навыки вашего РПГ - /rpg, денежные бонусы - /bonus, все изученные вами чертежи вместе со статистикой в целом.\n\n*Период времени проведения вайпа по техническим причинам может меняться, но в целом отталкиваться от текущей даты, поэтому просим вас зарание ознакамливаться с информцией в группе нашего сервера <color=#FFAA00AA>vk.com</color>",
					txtmenuAlign = new UnityEngine.TextAnchor(),
					wipe = new Dictionary<int, WipeClass>
					{
						{
							1, new WipeClass
							{
								image = "BACKGROUND_GLOBAL",
								type = Types.GLOBAL_WIPE,
								description = "ГЛОБАЛЬНЫЙ ВАЙП"
							}
						},
						{
							9, new WipeClass
							{
								image = "BACKGROUND_WIPE",
								type = Types.WIPE,
								description = "ВАЙП КАРТЫ"
							}
						},
						{
							16, new WipeClass
							{
								image = "BACKGROUND_GLOBAL",
								type = Types.GLOBAL_WIPE,
								description = "ГЛОБАЛЬНЫЙ ВАЙП"
							}
						},
						{
							23, new WipeClass
							{
								image = "BACKGROUND_WIPE",
								type = Types.WIPE,
								description = "ВАЙП КАРТЫ"
							}
						},
						{
							30, new WipeClass
							{
								image = "BACKGROUND_WIPE",
								type = Types.WIPE,
								description = "ВАЙП КАРТЫ"
							}
						}
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
				PrintError("Configuration file is corrupt(or not exists), creating new one!");
				config = GetDefaultConfig();
			}
			protected override void SaveConfig() => Config.WriteObject(config);
		#endregion
		#region API
			[HookMethod("API_GET_NEXT_WIPE")]
			public string API_GET_NEXT_WIPE()
			{
				DateTime now = DateTime.Now.Date;
				var next = GetWipeDatesAroundNow(now)
				.Where(d => d > now)
				.OrderBy(d => d)
				.FirstOrDefault();
				if (next == default(DateTime))
				return string.Empty;
				return next.ToString("dd MMMM", CultureInfo.GetCultureInfo("ru-RU"));
			}
			[HookMethod("API_GET_LAST_WIPE")]
			
			public string API_GET_LAST_WIPE()
			{
				DateTime now = DateTime.Now.Date;
				var last = GetWipeDatesAroundNow(now)
				.Where(d => d <= now)
				.OrderByDescending(d => d)
				.FirstOrDefault();
				if (last == default(DateTime))
				return string.Empty;
				return last.ToString("dd MMMM", CultureInfo.GetCultureInfo("ru-RU"));
			}
			private IEnumerable<DateTime> GetWipeDatesAroundNow(DateTime reference)
			{
				var result = new List<DateTime>();
				if (config?.wipe == null || config.wipe.Count == 0)
				return result;
				void AddMonth(int year, int month)
				{
					int daysInMonth = DateTime.DaysInMonth(year, month);
					foreach (var kvp in config.wipe)
					
					{
						int day = kvp.Key;
						if (day <= 0 || day > daysInMonth) continue;
						result.Add(new DateTime(year, month, day));
					}
				}
				
				int year = reference.Year;
				int month = reference.Month;
				AddMonth(year, month);
				int prevMonth = month == 1 ? 12 : month - 1;
				int prevYear = month == 1 ? year - 1 : year;
				AddMonth(prevYear, prevMonth);
				int nextMonth = month == 12 ? 1 : month + 1;
				
				int nextYear = month == 12 ? year + 1 : year;
				AddMonth(nextYear, nextMonth);
				return result;
			}
		#endregion
		#region Hooks⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠
			private void OnServerInitialized()
			{
				_ = this;
				_imageUI = new ImageUI();
				_imageUI.DownloadImage();

				PrintWarning("  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
				PrintWarning($"     {Name} v{Version} loading");
				PrintWarning($"        Plugin loaded - OK");
				PrintWarning("  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
				
				cmd.AddConsoleCommand("UI_Schedule", this, nameof(CmdConsoleSchedule));
				
				CalculateTable();
				
				timer.Every(config.Delay, () => CalculateTable());
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
			
			
			
			private void CmdConsoleSchedule(ConsoleSystem.Arg args)
			{
				var player = args.Player();
				
				int index = 0;
				if (!args.HasArgs(1) || !int.TryParse(args.Args[0], out index)) return;
				
				var check = DaysList[index];
				if (check.types != Types.None)
				{
					var container = new CuiElementContainer();
					container.Add(new CuiLabel
						{
							RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
							Text = { Text = $"{check.description}", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 8, Font = "robotocondensed-regular.ttf" }
						}, Layer + $".Day.Of.{index}", Layer + $".Day.Of.{index}.Text");
						CuiHelper.DestroyUi(player, Layer + $".Day.Of.{index}.Text");
						CuiHelper.AddUi(player, container);
				}
			}
		#endregion
		
        #region Interface⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠
			
			public const string MenuLayer = "XMenu";
			public const string MenuItemsLayer = "XMenu.MenuItems";
			public const string MenuSubItemsLayer = "XMenu.MenuSubItems";
			public const string MenuContent = "XMenu.Content";
			
			void BuildUI(BasePlayer player)
			{
				var container = new CuiElementContainer();
				var monthName = FirstUpper(DateTime.Now.ToString("MMMM", CultureInfo.GetCultureInfo("ru-RU")));
				
				container.Add(new CuiElement
					{
						Name = "lay" + ".Main",
						Parent = ".Mains",
						Components = 
						{
							new CuiRawImageComponent { Png = _imageUI.GetImage("MAIN_FON"), Color = "1 1 1 1" },
							new CuiRectTransformComponent { AnchorMin = "-0.315 -0.27", AnchorMax = "1.3 1.275", OffsetMax = "0 0" },
						}
					});
					
					container.Add(new CuiPanel
						{
							RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
							Image = { Color = "0 0 0 0" }
						}, "lay" + ".Main", Layer + ".BG");
						
						container.Add(new CuiButton
							{
								RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
								Button = { Close = "Menu_UI", Color = "0 0 0 0" },
								Text = { Text = "" }
							}, "lay" + ".Main");
							
							
							container.Add(new CuiPanel
								{
									RectTransform = { AnchorMin = "0.378 0.14", AnchorMax = "0.67 0.814" },
									Image = { Color = "1 1 1 0" },
									CursorEnabled = true
								}, Layer + ".BG", Layer);
								
								#region Loop
									var xDaysSwitch = 0f;
									for (int i = 1; i <= 7; i++)
									{
										container.Add(new CuiLabel
											{
												RectTransform = { AnchorMin = $"{0.03 + xDaysSwitch} 0.74", AnchorMax = $"{0.124 + xDaysSwitch} 0.78" },
												Text = { Text = DaysOfWeek[i], Align = TextAnchor.MiddleCenter, FontSize = 13, Color = "1 1 1 0.6", Font = "robotocondensed-regular.ttf" }
											}, Layer);
											
											xDaysSwitch += 0.114f;
									}
									
									var ySwitch = 0.63f;
									var xSwitch = 0.033f;
									for (int i = 0; i < DaysList.Count; i++)
									{
										var check = DaysList[i];
										
										container.Add(new CuiButton
											{
												RectTransform = { AnchorMin = $"{xSwitch} {ySwitch}", AnchorMax = $"{xSwitch+0.09} {ySwitch+0.07}"/*, OffsetMin = $"{xSwitch} {ySwitch - 157}", OffsetMax = $"{xSwitch + 35} {ySwitch - 120}"*/ },
												Button = { Color = "0 0 0 0", Command = $"UI_Schedule {i}", FadeIn = 1f },
												Text = { Text = "" }
											}, Layer, Layer + $".Day.Of.{i}");
											
											if (check.color != "0 0 0 0")
											{
												container.Add(new CuiElement
													{
														Parent = Layer + $".Day.Of.{i}",
														Components = 
														{
															new CuiRawImageComponent { Png = _imageUI.GetImage(check.color) },
															new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
														}
													});
											}
											
											container.Add(new CuiLabel
												{
													RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
													Text = { Text = $"{check.day}", Color = "1 1 1 0.6", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12 }
												}, Layer + $".Day.Of.{i}", Layer + $".Day.Of.{i}.Text");
												
												xSwitch += 0.114f;
												
												if ((i + 1) % 7 == 0)
												{
													xSwitch = 0.033f;
													ySwitch -= 0.085f;
												}
									}
								#endregion
								
								container.Add(new CuiPanel
									{
										RectTransform = { AnchorMin = "0.95 0.63", AnchorMax = "0.97 0.69" },
										Image = { Color = "0 0 0 0" }
									}, Layer, Layer + ".GlobalWipe");
									container.Add(new CuiLabel
										{
											RectTransform = { AnchorMin = "1 0", AnchorMax = "23 1" },
											Text = { Text = "- Глобал вайп с чертежами", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
										}, Layer + ".GlobalWipe");
										container.Add(new CuiPanel
											{
												RectTransform = { AnchorMin = "0.95 0.69", AnchorMax = "0.97 0.75" },
												Image = { Color = "0 0 0 0", }
											}, Layer, Layer + ".Wipe");
											container.Add(new CuiLabel
												{
													RectTransform = { AnchorMin = "1 0", AnchorMax = "23 1" },
													Text = { Text = "- Вайп без удаления чертежей", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
												}, Layer + ".Wipe");
												container.Add(new CuiLabel      
													{       
														RectTransform = { AnchorMin = "0.90 0.165", AnchorMax = "1.5 0.6" },        
														Text = { Text = $"{config.txtmenu}", Align = config.txtmenuAlign, FontSize = 12, Font = "robotocondensed-regular.ttf" }     
													}, Layer);
													
													CuiHelper.AddUi(player, container);
			}
		#endregion
		
        #region Utils⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠
			private void CalculateTable()
			{
				DaysList.Clear();
				
				Calendar myCal = CultureInfo.InvariantCulture.Calendar;
				DateTime myDT = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, myCal);
				
				var PreviousMonth = myDT.AddMonths(-1);
				var DaysInPreviousMonth = (int)DateTime.DaysInMonth(PreviousMonth.Year, PreviousMonth.Month);
				
				int j = Convert.ToInt32(myCal.GetDayOfWeek(myDT)) - 1;
				
				j = j == -1 ? 6 : j;
				
				var LastDay = new DateTime(PreviousMonth.Year, PreviousMonth.Month, DaysInPreviousMonth);
				var backDays = LastDay.AddDays(-j + 1);
				for (int m = 0; m < j; m++)
				{
					DaysList.Add(new DayClass
						{
							day = backDays.Day,
							color = "0 0 0 0",
							description = string.Empty,
							types = Types.None
						});
						backDays = backDays.AddDays(1);
				}
				
				int month = myCal.GetMonth(myDT);
				while (myCal.GetMonth(myDT) == month)
				{
					var check = config.wipe.Where(x => x.Key == myDT.Day).FirstOrDefault().Value != null;
					
					DaysList.Add(new DayClass
						{
							day = myDT.Day,
							color = check ? config.wipe[myDT.Day].image : config.ActiveImage,
							description = check ? config.wipe[myDT.Day].description : string.Empty,
							types = check ? config.wipe[myDT.Day].type : Types.None
						});
						
						myDT = myDT.AddDays(1);
						j--;
				}
				
				if (DaysList.Count < 42)
				{
					var DaysToEndTable = 42 - DaysList.Count;
					
					for (int i = 1; i <= DaysToEndTable; i++)
					{
						DaysList.Add(new DayClass
							{
								day = i,
								color = "0 0 0 0",
								description = string.Empty,
								types = Types.None
							});
					}
				}
			}
			
			public string FirstUpper(string str)
			{
				str = str.ToLower();
				string[] s = str.Split(' ');
				for (int i = 0; i < s.Length; i++)
				{
					if (s[i].Length > 1)
                    s[i] = s[i].Substring(0, 1).ToUpper() + s[i].Substring(1, s[i].Length - 1);
					else s[i] = s[i].ToUpper();
				}
				return string.Join(" ", s);
			}
		#endregion
		
        #region Images
			private static ImageUI _imageUI;
			private class ImageUI
			{
				private const String _path = "TPSystem/TPWipe/";
				private const String _printPath = "data/" + _path;
				private readonly Dictionary<String, ImageData> _images = new()
				{
					{ "MAIN_FON", new ImageData() },
					{ "BACKGROUND_ACTIVE", new ImageData() },
					{ "BACKGROUND_GLOBAL", new ImageData() },
					{ "BACKGROUND_WIPE", new ImageData() }
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
                    if(item.Value.Status == ImageStatus.Loaded)
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
