using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Networking;
using System.IO;
namespace Oxide.Plugins
{
	[Info("TPMenuInfo", "pluginfuel.ru", "20.0.2")]
	class TPMenuInfo : RustPlugin
	{
		#region Classes
			private static TPMenuInfo _;
			internal class FAQSection
			{
				public string Label;
				public string InsideText;
				public float PanelDownOffset;
			}
			internal class Command
			{
				public string Description;
				public string Text;
			}
			internal class HelpSection
			{
				public string TextOnButton;
				public int DrawOrder;
				public List<HelpSubSection> SubSections;
				internal class HelpSubSection
				{
					public string Label;
					public string InternalText;
					public float DownOffset;
				}
				
			}
			internal class CategoryButton
			{
				public string Text;
			}
		#endregion
		#region Fields
			[PluginReference] Plugin TPMenuSystem;
			private const string GRADIENT_RIGHT = "assets/content/ui/ui.background.transparent.linearltr.tga";
			private const string GRADIENTDOWN_COLOR = "0 0 0 0.7";
			private const string WHITE_TRANSPARENT_BACKGROUND = "1 1 1 0.3";
			private const string ORANGE_COLOR = "0.9490196 0.5019608 0.05490196 1";
			private const string BACKGROUND_COLOR = "0.3568628 0.3568628 0.3568628 0.75";
			private const string TEXT_COLOR = "1 1 1 1";
			private const string RED_COLOR = "0.6901961 0.3490196 0.3490196 0.8";
			private const string Layer = "ui.MenuBase.bg";
			private readonly Dictionary<string, CategoryButton> _categoryButtons = new()
			{
				["help"] = new()
				{
					Text = "ПОМОЩЬ",
				},
				["commands"] = new()
				{
					Text = "КОМАНДЫ"
				},
				["faq"] = new()
				{
					Text = "ЧАСТЫЕ ВОПРОСЫ"
				},
				["rules"] = new()
				{
					Text = "ПРАВИЛА"
				}
			};
		#endregion
		#region Hooks
			void OnServerInitialized()
			{
				_ = this;
				_imageUI = new ImageUI();
				_imageUI.DownloadImage();
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
		#region Methods
		#endregion
		#region UI
			#region Main
				private void UI_DrawMain(BasePlayer player)
				{
					var container = new CuiElementContainer();
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
						container.Add(new CuiButton
							{
								RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
								Button = { Close = "Menu_UI", Color = "0 0 0 0" },
								Text = { Text = "" }
							}, "lay" + ".Main");
							CuiHelper.AddUi(player, container);
							UI_DrawSections(player, "help");
							UI_DrawHelp(player);
				}
				private void UI_DrawSections(BasePlayer player, string activeSection)
				{
					CuiHelper.DestroyUi(player, Layer + ".categories.div");
					var container = new CuiElementContainer();
					container.Add(new CuiPanel
						{
							CursorEnabled = false,
							Image = { Color = "1 1 1 0" },
							RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301.93 90", OffsetMax = "301.887 153.2" }
						}, "lay" + ".Main", Layer + ".categories.div");
						float minx = -301.9093f;
						float maxx = -155.0386f;
						float miny = -17.89599f;
						float maxy = 17.89601f;
						foreach (var x in _categoryButtons)
						{
							container.Add(new CuiPanel
								{
									CursorEnabled = false,
									Image = { Color = "0 0 0 0" },
									RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{minx} {miny}", OffsetMax = $"{maxx} {maxy}" }
								}, Layer + ".categories.div", Layer + ".main.div" + ".categories.div" + $".{x.Key}");
								
								container.Add(new CuiElement
									{
										Parent = Layer + ".main.div" + ".categories.div" + $".{x.Key}",
										Components =
										{
											new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_BUTTON") },
											new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
										}
									});
									
									container.Add(new CuiButton
										{
											Button = { Color = "0 0 0 0", Command = activeSection == x.Key ? "" : $"mb.info.category {x.Key}" },
											Text = { Text = x.Value.Text, Font = "robotocondensed-bold.ttf", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
											RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-73.436 -14.867", OffsetMax = "73.434 17.976" }
										}, Layer + ".main.div" + ".categories.div" + $".{x.Key}");
										
										container.Add(new CuiPanel
											{
												Image = { Color = activeSection == x.Key ? "1.00 0.07 0.32 1.00" : "0 0 0 0" },
												RectTransform = { AnchorMin = "0.1 0.13", AnchorMax = "0.9 0.2", OffsetMax = "0 0" }
											}, Layer + ".main.div" + ".categories.div" + $".{x.Key}");
											
											minx += 152.389f;
											maxx += 151.959f;
						}
						
						CuiHelper.AddUi(player, container);
				}
			#endregion
			
			#region Rules
				
				private void UI_DrawRules(BasePlayer player)
				{
					CuiHelper.DestroyUi(player, Layer + ".main.div" + ".info");
					var container = new CuiElementContainer();
					container.Add(new CuiElement()
						{
							Name = Layer + ".main.div" + ".info",
							Parent = "lay" + ".Main",
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
										AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "0 -100", OffsetMax = "0 0"
									},
									HorizontalScrollbar = null,
									VerticalScrollbar = new()
									{
										Invert = false,
										AutoHide = false,
										HandleSprite = null,
										Size = 2,
										HandleColor = "1.00 0.07 0.32 1.00",
										HighlightColor = "1.00 0.07 0.32 1.00",
										PressedColor = "1.00 0.07 0.32 1.00",
										TrackSprite = null,
										TrackColor = "0 0 0 0.4"
									}
								},
								new CuiRectTransformComponent() { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-923 -200", OffsetMax = "298 90" }
							}
						});
						container.Add(new CuiPanel()
							{
								Image = { Color = "0 0 0 0" },
								RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-1000 -10000", OffsetMax = "1000 0" }
							}, Layer + ".main.div" + ".info");
							float miny = -30;
							float maxy = 0;
							int i = 0;
							foreach (var x in cfg.RulesStrings)
							{
								int newLinesAmount = x.Split("\n").Length;
								container.Add(new CuiElement
									{
										Parent = Layer + ".main.div" + ".info",
										Components = {
											new CuiTextComponent { Text = x, Font = "robotocondensed-regular.ttf", FontSize = 13, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
											new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"11.37 {miny - 100}", OffsetMax = $"603.63 {maxy}" }
										}
									});
									
									
									
									miny -= 28.18f * Mathf.Max(1, newLinesAmount);
									maxy -= 28.18f * Mathf.Max(1, newLinesAmount);
							}
							
							(container[0].Components[0] as CuiScrollViewComponent).ContentTransform.OffsetMin = $"0 {Mathf.Min(miny, -350.4366f)}";
							if (miny > -350.4366)
							{
								(container[0].Components[0] as CuiScrollViewComponent).VerticalScrollbar = null;
							}
							CuiHelper.AddUi(player, container);
				}
				
			#endregion
			
			#region FAQ
				
				private void UI_DrawFAQ(BasePlayer player, string activeSection)
				{
					CuiHelper.DestroyUi(player, Layer + ".main.div" + ".info");
					var container = new CuiElementContainer();
					container.Add(new CuiPanel
						{
							CursorEnabled = false,
							Image = { Color = "0 0 0 0" },
							RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301.931 -200", OffsetMax = "301.889 90" }
						}, "lay" + ".Main", Layer + ".main.div" + ".info");
						
						float minx = -301.91f;
						float maxx = 295.91f;
						float miny = 101f;
						float maxy = 145f;
						foreach (var x in cfg.FAQSections)
						{
							float additionalOffset = 0;
							container.Add(new CuiButton()
								{
									Button = { Color = "0 0 0 0", Command = $"mb.info.faqsection {x.Key}{(activeSection == x.Key ? " close" : "")}" },
									RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-301.91 {miny}", OffsetMax = $"301.91 {maxy}" }
								}, Layer + ".main.div" + ".info", Layer + ".main.div" + ".info" + ".Faqs.div" + ".items" + $".{x.Key}");
								
								container.Add(new CuiElement
									{
										Parent = Layer + ".main.div" + ".info" + ".Faqs.div" + ".items" + $".{x.Key}",
										Components =
										{
											new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_FAQ") },
											new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
										}
									});
									
									container.Add(new CuiPanel
										{
											CursorEnabled = false,
											Image = { Color = "1 1 1 1", Sprite = "assets/icons/connection.png" },
											RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-290.7 -8.1", OffsetMax = "-274.5 8.1" }
										}, Layer + ".main.div" + ".info" + ".Faqs.div" + ".items" + $".{x.Key}", Layer + ".main.div" + ".info" + ".Faqs.div" + ".items" + $".{x.Key}" + ".icon");
										
										container.Add(new CuiElement
											{
												Name = Layer + ".main.div" + ".info" + ".Faqs.div" + ".items" + $".{x.Key}" + ".text",
												Parent = Layer + ".main.div" + ".info" + ".Faqs.div" + ".items" + $".{x.Key}",
												Components = {
													new CuiTextComponent { Text = x.Value.Label, Font = "robotocondensed-bold.ttf", FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
													new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-261.753 -20.914", OffsetMax = "258.587 20.914" }
												}
											});
											
											container.Add(new CuiButton
												{
													Button = { Color = "1 1 1 1", Sprite = activeSection == x.Key ? "assets/icons/dir_left.png" : "assets/icons/dir_right.png", Command = $"mb.info.faqsection {x.Key}{(activeSection == x.Key ? " close" : "")}" },
													Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
													RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "271.049 -10.251", OffsetMax = "291.551 10.252" }
												}, Layer + ".main.div" + ".info" + ".Faqs.div" + ".items" + $".{x.Key}", Layer + ".main.div" + ".info" + ".Faqs.div" + ".items" + $".{x.Key}" + ".open");
												
												if (activeSection == x.Key)
												{
													container.Add(new CuiPanel()
														{
															Image = { Color = "0 0 0 0" },
															RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-301.91 {x.Value.PanelDownOffset - 21}", OffsetMax = "301.91 -21" }
														}, Layer + ".main.div" + ".info" + ".Faqs.div" + ".items" + $".{x.Key}", Layer + ".main.div" + ".info" + ".Faqs.div" + ".items" + $".{x.Key}" + ".opened");
														
														container.Add(new CuiLabel()
															{
																Text = { Text = x.Value.InsideText, Align = TextAnchor.UpperLeft, FontSize = 11, Font = "robotocondensed-regular.ttf" },
																RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 12", OffsetMax = "-4 -12" }
															}, Layer + ".main.div" + ".info" + ".Faqs.div" + ".items" + $".{x.Key}" + ".opened");
															additionalOffset += -x.Value.PanelDownOffset;
												}
												
												miny -= 148.316f + additionalOffset;
												maxy -= 48.316f + additionalOffset;
						}
						
						CuiHelper.AddUi(player, container);
				}
				
			#endregion
			
			#region Commands
				
				private void UI_DrawCommands(BasePlayer player)
				{
					CuiHelper.DestroyUi(player, Layer + ".main.div" + ".info");
					var container = new CuiElementContainer();
					container.Add(new CuiElement()
						{
							Name = Layer + ".main.div" + ".info",
							Parent = "lay" + ".Main",
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
										AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "0 -100", OffsetMax = "0 0"
									},
									HorizontalScrollbar = null,
									VerticalScrollbar = new()
									{
										Invert = false,
										AutoHide = false,
										HandleSprite = null,
										Size = 2,
										HandleColor = "1.00 0.07 0.32 1.00",
										HighlightColor = "1.00 0.07 0.32 1.00",
										PressedColor = "1.00 0.07 0.32 1.00",
										TrackSprite = null,
										TrackColor = "0 0 0 0.4"
									}
								},
								new CuiRectTransformComponent() { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -200", OffsetMax = "298 90" }
							}
						});
						container.Add(new CuiPanel()
							{
								Image = { Color = "0 0 0 0" },
								RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-1000 -10000", OffsetMax = "1000 0" }
							}, Layer + ".main.div" + ".info");
							float minx = -307.61f;
							float maxx = -291f;
							float miny = -30;
							float maxy = -30;
							
							float minyCommand = -30.23857f;
							float maxyCommand = 1.50607f;
							int i = 0;
							
							foreach (var x in cfg.Commands)
							{
								container.Add(new CuiPanel
									{
										CursorEnabled = false,
										Image = { Color = "1 1 1 1" },
										RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"{minx} {miny}", OffsetMax = $"{maxx} {maxy}" }
									}, Layer + ".main.div" + ".info", Layer + ".main.div" + ".commands.div" + $".{i}");
									
									container.Add(new CuiElement
										{
											Name = Layer + ".main.div" + ".commands.div" + $".{i}" + ".label",
											Parent = Layer + ".main.div" + ".commands.div" + $".{i}",
											Components = {
												new CuiTextComponent { Text = x.Key, Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
												new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-0.3 -1.371", OffsetMax = "603.52 20.21" }
											}
										});
										
										minyCommand = -30.23857f;
										maxyCommand = 1.50607f;
										float totalOffset = 0;
										int j = 0;
										
										foreach (var y in x.Value)
										{
											container.Add(new CuiPanel
												{
													CursorEnabled = false,
													Image = { Color = "1 1 1 0" },
													RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-0.075 {minyCommand}", OffsetMax = $"603.745 {maxyCommand}" }
												}, Layer + ".main.div" + ".commands.div" + $".{i}", Layer + ".main.div" + ".commands.div" + $".{i}" + ".commands" + $".{j}");
												
												container.Add(new CuiPanel
													{
														CursorEnabled = false,
														Image = { Color = "0 0 0 0" },
														RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-301 -15.366", OffsetMax = "110 15.366" }
													}, Layer + ".main.div" + ".commands.div" + $".{i}" + ".commands" + $".{j}", Layer + ".main.div" + ".commands.div" + $".{i}" + ".commands" + $".{j}" + ".text.bg");
													
													container.Add(new CuiElement
														{
															Parent = Layer + ".main.div" + ".commands.div" + $".{i}" + ".commands" + $".{j}" + ".text.bg",
															Components =
															{
																new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_TITLE") },
																new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
															}
														});
														
														container.Add(new CuiElement
															{
																Parent = Layer + ".main.div" + ".commands.div" + $".{i}" + ".commands" + $".{j}" + ".text.bg",
																Components = {
																	new CuiTextComponent { Text = y.Description, Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
																	new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-190.743 -15.366", OffsetMax = "225.541 15.366" }
																}
															});
															
															container.Add(new CuiElement
																{
																	Name = Layer + ".main.div" + ".commands.div" + $".{i}" + ".commands" + $".{j}" + ".bind.bg",
																	Parent = Layer + ".main.div" + ".commands.div" + $".{i}" + ".commands" + $".{j}",
																	Components =
																	{
																		new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_BUTTON") },
																		new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "113.892 -15.366", OffsetMax = "280.91 15.366" },
																	}
																});
																
																container.Add(new CuiPanel
																	{
																		CursorEnabled = false,
																		Image = { Color = "0 0 0 0.1" },
																		RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
																	}, Layer + ".main.div" + ".commands.div" + $".{i}" + ".commands" + $".{j}" + ".bind.bg");
																	
																	container.Add(new CuiElement
																		{
																			Parent = Layer + ".main.div" + ".commands.div" + $".{i}" + ".commands" + $".{j}" + ".bind.bg",
																			Components = {
																				new CuiInputFieldComponent { Text = y.Text, Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", NeedsKeyboard = true, ReadOnly = true, Autofocus = false },
																				new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-73.51 -15.366", OffsetMax = "73.51 15.366" }
																			}
																		});
																		j++;
																		minyCommand -= 34.573f;
																		maxyCommand -= 34.573f;
																		totalOffset -= 34.573f;
										}
										
										i++;
										miny += totalOffset - 30f;
										maxy += totalOffset - 30f;
							}
							
							(container[0].Components[0] as CuiScrollViewComponent).ContentTransform.OffsetMin =
							$"0 {Mathf.Min(-350.4366f, miny + 32)}";
							if (miny > -350.4366)
							{
								(container[0].Components[0] as CuiScrollViewComponent).VerticalScrollbar = null;
							}
							CuiHelper.AddUi(player, container);
				}
				
			#endregion
			
			#region Help
				
				private void UI_DrawHelp(BasePlayer player)
				{
					var startSection = cfg.HelpSections.First();
					UI_DrawHelpSections(player, startSection.Key);
					UI_DrawHelpPage(player, startSection.Key);
				}
				
				private void UI_DrawHelpSections(BasePlayer player, string activeSection)
				{
					CuiHelper.DestroyUi(player, Layer + ".main.div" + ".infoCommand");
					var container = new CuiElementContainer();
					container.Add(new CuiPanel
						{
							CursorEnabled = false,
							Image = { Color = "0 0 0 0" },
							RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-300 -200", OffsetMax = "-177.878 90" }
						}, "lay" + ".Main", Layer + ".main.div" + ".infoCommand");
						
						float minx = -62.02518f;
						float maxx = 82.02482f;
						float miny = 116.6622f;
						float maxy = 145.626f;
						foreach (var x in cfg.HelpSections)
						{
							container.Add(new CuiPanel
								{
									CursorEnabled = false,
									Image = { Color = "0 0 0 0" },
									RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{minx} {miny}", OffsetMax = $"{maxx} {maxy}" }
								}, Layer + ".main.div" + ".infoCommand", Layer + ".main.div" + ".sections.div" + $".{x.Key}");
								
								container.Add(new CuiElement
									{
										Parent = Layer + ".main.div" + ".sections.div" + $".{x.Key}",
										Components =
										{
											new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_BUTTON") },
											new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
										}
									});
									
									container.Add(new CuiButton
										{
											Button = { Color = "0 0 0 0", Command = activeSection == x.Key ? "" : $"mb.info.helpsection {x.Key}" },
											Text = { Text = x.Value.TextOnButton, Font = "robotocondensed-bold.ttf", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
											RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
										}, Layer + ".main.div" + ".sections.div" + $".{x.Key}");
										
										container.Add(new CuiPanel
											{
												Image = { Color = activeSection == x.Key ? "1.00 0.07 0.32 1.00" : "0 0 0 0" },
												RectTransform = { AnchorMin = "0.02 0.2", AnchorMax = "0.04 0.8", OffsetMax = "0 0" }
											}, Layer + ".main.div" + ".sections.div" + $".{x.Key}");
											
											
											miny -= 35.964f;
											maxy -= 35.964f;
						}
						
						CuiHelper.AddUi(player, container);
				}
				
				private void UI_DrawHelpPage(BasePlayer player, string pageKey)
				{
					CuiHelper.DestroyUi(player, Layer + ".main.div" + ".info");
					var container = new CuiElementContainer();
					container.Add(new CuiElement()
						{
							Name = Layer + ".main.div" + ".info",
							Parent = "lay" + ".Main",
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
										AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "0 -100", OffsetMax = "0 0"
									},
									HorizontalScrollbar = null,
									VerticalScrollbar = new()
									{
										Invert = false,
										AutoHide = false,
										HandleSprite = null,
										Size = 2,
										HandleColor = "1.00 0.07 0.32 1.00",
										HighlightColor = "1.00 0.07 0.32 1.00",
										PressedColor = "1.00 0.07 0.32 1.00",
										TrackSprite = null,
										TrackColor = "0 0 0 0.4"
									}
								},
								new CuiRectTransformComponent() { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -200", OffsetMax = "298 90" }
							}
						});
						container.Add(new CuiPanel()
							{
								Image = { Color = "0 0 0 0" },
								RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-1000 -10000", OffsetMax = "10000 0" }
							}, Layer + ".main.div" + ".info");
							
							
							float miny = -30;
							float maxy = 0;
							
							foreach (var x in cfg.HelpSections[pageKey].SubSections)
							{
								container.Add(new CuiPanel()
									{
										Image = { Color = "0 0 0 0" },
										RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"-214 {miny}", OffsetMax = $"237 {maxy}" }
									}, Layer + ".main.div" + ".info", Layer + ".main.div" + ".subsections.div" + $".subsection.{x.Label}");
									container.Add(new CuiPanel
										{
											CursorEnabled = false,
											Image = { Color = "0 0 0 0" },
											RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-235.943 -22", OffsetMax = "200 -0.24" }
										}, Layer + ".main.div" + ".subsections.div" + $".subsection.{x.Label}", Layer + ".main.div" + ".subsections.div" + $".subsection.{x.Label}" + ".header.bg");
										
										container.Add(new CuiElement
											{
												Parent = Layer + ".main.div" + ".subsections.div" + $".subsection.{x.Label}" + ".header.bg",
												Components =
												{
													new CuiRawImageComponent { Png = _imageUI.GetImage("BACKGROUND_TITLE") },
													new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" },
												}
											});
											
											container.Add(new CuiElement
												{
													Parent = Layer + ".main.div" + ".subsections.div" + $".subsection.{x.Label}" + ".header.bg",
													Components = {
														new CuiTextComponent { Text = x.Label, Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
														new CuiRectTransformComponent { AnchorMin = "0.02 0", AnchorMax = "1 1", OffsetMax = "0 0" }
													}
												});
												
												container.Add(new CuiElement
													{
														Parent = Layer + ".main.div" + ".subsections.div" + $".subsection.{x.Label}",
														Components = {
															new CuiTextComponent { Text = x.InternalText, Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
															new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-233.867 -50", OffsetMax = "210 -23" }
														}
													});
													miny -= x.DownOffset + 15f;
													maxy -= x.DownOffset + 15f;
							}
							
							(container[0].Components[0] as CuiScrollViewComponent).ContentTransform.OffsetMin = $"0 {Mathf.Min(miny, -319.4366f)}";
							if (miny > -319.4366)
							{
								(container[0].Components[0] as CuiScrollViewComponent).VerticalScrollbar = null;
							}
							CuiHelper.AddUi(player, container);
				}
				
			#endregion
			
		#endregion
		
		#region Commands
			
			[ConsoleCommand("mb.info.externalopen")]
			private void cmdExternalOpen(ConsoleSystem.Arg arg)
			{
				if (arg.Player() == null || arg.Args.IsNullOrEmpty())
				return;
				
				var key = arg.Args[0];
				
				UI_DrawMain(arg.Player());
				
				if (cfg.HelpSections.ContainsKey(key))
				{
					UI_DrawSections(arg.Player(), "help");
					UI_DrawHelp(arg.Player());
					UI_DrawHelpSections(arg.Player(), key);
					UI_DrawHelpPage(arg.Player(), key);
				}
				else if (cfg.FAQSections.ContainsKey(key))
				{
					UI_DrawSections(arg.Player(), "faq");
					UI_DrawFAQ(arg.Player(), key);
				}
				else if (key == "commands")
				{
					UI_DrawSections(arg.Player(), "commands");
					UI_DrawCommands(arg.Player());
				}
				else if (key == "rules")
				{
					UI_DrawSections(arg.Player(), "rules");
					UI_DrawRules(arg.Player());
				}
				else
				throw new ArgumentOutOfRangeException(key);
			}
			
			[ConsoleCommand("mb.info.faqsection")]
			private void cmdFAQSection(ConsoleSystem.Arg arg)
			{
				if (arg.Player() == null || arg.Args.IsNullOrEmpty())
					return;
				
				var player = arg.Player();
				
				if (arg.HasArgs(2))
				{
					UI_DrawFAQ(player, "");
					return;
				}
				UI_DrawFAQ(player, arg.Args[0]);
			}
			[ConsoleCommand("mb.info.open")]
			private void cmdOpen(ConsoleSystem.Arg arg)
			{
				if (arg.Player() == null)
				return;
				
				UI_DrawMain(arg.Player());
			}
			[ConsoleCommand("mb.info.category")]
			private void cmdCategory(ConsoleSystem.Arg arg)
			{
				if (arg.Player() == null || arg.Args.IsNullOrEmpty())
				return;
				
				var player = arg.Player();
				var section = arg.Args[0];
				switch (section)
				{
					case "help":
					UI_DrawHelp(player);
					break;
					case "commands":
					UI_DrawCommands(player);
					CuiHelper.DestroyUi(player, Layer + ".main.div" + ".infoCommand");
					break;
					case "rules":
					UI_DrawRules(player);
					CuiHelper.DestroyUi(player, Layer + ".main.div" + ".infoCommand");
					break;
					case "faq":
					UI_DrawFAQ(player, "");
					CuiHelper.DestroyUi(player, Layer + ".main.div" + ".infoCommand");
					break;
					default:
					throw new ArgumentOutOfRangeException(
					$"Player {player.userID} entered {nameof(cmdCategory)} but section {section} not found.");
				}
				UI_DrawSections(player, section);
			}
			
			[ConsoleCommand("mb.info.helpsection")]
			private void cmdHelpSection(ConsoleSystem.Arg arg)
			{
				if (arg.Player() == null || arg.Args.IsNullOrEmpty())
				return;
				var player = arg.Player();
				var section = arg.Args[0];
				
				UI_DrawHelpSections(player, section);
				UI_DrawHelpPage(player, section);
			}
			
		#endregion
		
		#region Config
			
			private ConfigData cfg;
			
			public class ConfigData
			{
				[JsonProperty("Разделы в информации")] public Dictionary<string, HelpSection> HelpSections;
				[JsonProperty("Разделы в командах")] public Dictionary<string, List<Command>> Commands;
				[JsonProperty("Разделы в FAQ")] public Dictionary<string, FAQSection> FAQSections;
				[JsonProperty("Строки правил")] public List<string> RulesStrings;

			}
			
			protected override void LoadDefaultConfig()
			{
				var config = new ConfigData
				{
					RulesStrings = new()
					{
						"124124124",
						"124124124",
						"12354213525",
						"12451235235",
						"5235235"
					},
					FAQSections = new()
					{
						["promocodes"] = new()
						{
							Label = "ГДЕ МОЖНО НАЙТИ ПРОМОКОДЫ?",
							InsideText = "Промокоды можно найти в наших социальных сетях. Там мы публикуем новости, акции и промокоды. Также, на сайте  каждый вайп, мы выкладываем промокоды для фанатов сервера.",
							PanelDownOffset = -50
						},
						["shitpost"] = new()
						{
							Label = "когда инфо?",
							InsideText = "Да почти готово, ща правила доделаю и четенько",
							PanelDownOffset = -50
						}
					},
					Commands = new()
					{
						["ОСНОВНЫЕ"] = new()
						{
							new ()
							{
								Description = "Открыть раздел Menu",
								Text = "bind <key> menu"
							},
							new ()
							{
								Description = "Открыть раздел Menu #2",
								Text = "bind <key> menu test"
							},
						},
						["ДРУЗЬЯ"] = new()
						{
							new ()
							{
								Description = "Открыть раздел друзей",
								Text = "bind <key> menu"
							},
							new ()
							{
								Description = "Открыть раздел друзей #2",
								Text = "bind <key> menu test"
							},
						}
					},
					HelpSections = new()
					{
						["menu"] = new()
						{
							TextOnButton = "МЕНЮ",
							DrawOrder = 0,
							SubSections = new()
							{
								new()
								{
									Label = "ЧТО ЭТО ТАКОЕ?",
									InternalText = "Ивент \"Спутник\" - это главный ивент сервера, в котором можно получить ценный лут и Volt's молнии",
									DownOffset = 50
								},
								new()
								{
									Label = "ОПИСАНИЕ ИВЕНТА",
									InternalText = "Два раза в день (12:00 и 18:00) автоматически запускается событие. Во время его начала на экране появляется уведомление о падении обломков \"Спутника\". Через некоторое время в чате появляется информация о квадратах, в которых упали обломки. Также, на G-MAP отображаются точки, выделенные красным кругом."
								}
							}
						},
						["friends"] = new()
						{
							TextOnButton = "ДРУЗЬЯ",
							DrawOrder = 1,
							SubSections = new()
							{
								new()
								{
									Label = "Чё то про друзейВ",
									InternalText = "друг в беде не бросит",
									DownOffset = 70
								},
								new()
								{
									Label = "<color=red>оу</color>",
									InternalText = "настройка текста выполнена успешно"
								}
							}
						}
					}
				};
				SaveConfig(config);
			}
			
			protected override void LoadConfig()
			{
				base.LoadConfig();
				cfg = Config.ReadObject<ConfigData>();
				SaveConfig(cfg);
			}
			
			private void SaveConfig(object config)
			{
				Config.WriteObject(config, true);
			}
		#endregion
		
		#region Images
			private static ImageUI _imageUI;
			private class ImageUI
			{
				private const String _path = "TPSystem/TPHelp/";
				private const String _printPath = "data/" + _path;
				private readonly Dictionary<String, ImageData> _images = new()
				{
					{ "MAIN_FON", new ImageData() },
					{ "BACKGROUND_TITLE", new ImageData() },
					{ "BACKGROUND_MINI_TITLE", new ImageData() },
					{ "BACKGROUND_BUTTON", new ImageData() },
					{ "BACKGROUND_FAQ", new ImageData() }
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
					// Load from local file system for all images
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
