using System;
using System.Globalization;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("FuelPanel", "pluginfuel.ru", "20.0.2")]
    public class FuelPanel : RustPlugin
    {
        #region Fields
        private static ImageUI _imageUI;
        private Dictionary<ulong, bool> PanelVisibility = new Dictionary<ulong, bool>();
        private List<string> EventNames = new List<string>();
        private readonly string defaultSprite = "assets/content/ui/ui.icon.rust.png";
        private Dictionary<string, string> EventAnMinX = new Dictionary<string, string>();
        private Dictionary<string, string> EventAnMaxX = new Dictionary<string, string>();
        private readonly string Layer = "GRPLayer";
        private readonly string Layer1 = "GRPLayer_Store";
        private readonly string MessageLayer = "FuelPanelMessage";
        #endregion

        #region [GUIBUILDER]
        protected CuiElement Panel(string name, string anMin, string anMax, string color, string parent, float fadeout, string png, bool cursor, string offsetmin, string offsetmax)
        {
            var Element = new CuiElement()
            
            {
                Name = name,
                Parent = parent,
                FadeOut = fadeout,
                Components =
                {
                    new CuiRawImageComponent { Png = png, Color = color },
                    new CuiRectTransformComponent { AnchorMin = anMin, AnchorMax = anMax, OffsetMin = offsetmin, OffsetMax = offsetmax }
                }
            };
            if (cursor)
            {
                Element.Components.Add(new CuiNeedsCursorComponent());
            }
            return Element;
        }
        protected CuiElement Text(string name, string parent, string color, string text, TextAnchor pos, int fsize, string anMin, string anMax, string fname = "robotocondensed-bold.ttf")
        {
            var Element = new CuiElement()
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiTextComponent() { Color = color, Text = text, Align = pos, Font = fname, FontSize = fsize },
                    new CuiRectTransformComponent{ AnchorMin = anMin, AnchorMax = anMax }
                }
            };
            return Element;
        }
        protected CuiElement Button(string name, string parent, string sprite, string command, string color, string anMin, string anMax)
        {
            var Element = new CuiElement()
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiButtonComponent { Command = command, Color = color, Sprite = sprite },
                    new CuiRectTransformComponent{ AnchorMin = anMin, AnchorMax = anMax }
                }
            };
            return Element;
        }
        #endregion

        #region Config

        private PluginConfig cfg;

        public class PluginConfig
        {
            [JsonProperty("Основные настройки")]
            public Settings MainSettings = new Settings();

            [JsonProperty("Сообщения")]
            public MessageSettings SettingsMessages = new MessageSettings();


            public class Settings
            {
                [JsonProperty("Включить показ кнопки магазина?")]
                public bool EnableStore = true;

                [JsonProperty("Показывать статистику игроков?")]
                public bool ShowPlayerStats = true;

            }

            public class MessageSettings
            {
                [JsonProperty("Включить сообщения")]
                public bool EnableMessages = true;
                [JsonProperty("Время обновления сообщений")]
                public float RefreshTimer = 30f;
                [JsonProperty("Размер текста для автосообщений")]
                public int TextSize = 12;
                [JsonProperty("Показывать статистику игроков в сообщениях")]
                public bool ShowPlayerStatsInMessages = true;
                [JsonProperty("Цвет текста")]
                public string TextColor = "white";
                [JsonProperty("Цвет цифр")]
                public string NumberColor = "red";
                [JsonProperty("Список сообщений", ObjectCreationHandling = ObjectCreationHandling.Replace)]
               public List<string> Messages = new List<string>
                {
                    "<color=white>Играй, строй, выживай! ОнлайнGoldMine::</color> <color=red>{total}</color> <color=white>| Спящих:</color> <color=red>{sleeping}</color>",
                    "<color=white>Добро пожаловать на сервер GoldMine! Сейчас онлайн:</color> <color=red>{total}</color> <color=white>игроков</color>",
                    "<color=white>Статистика: Всего</color> <color=red>{total}</color> <color=white>| Спящих</color> <color=red>{sleeping}</color> <color=white>| Подключающихся</color> <color=red>{connecting}</color>"};
            }
        }


        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
        }

        #endregion

        #region Player Statistics

        /// <summary>
        /// Получение статистики игроков
        /// </summary>
        private (int total, int active, int sleeping, int connecting) GetPlayerStatistics()
        {
            int total = 0;
            int active = 0;
            int sleeping = 0;
            int connecting = 0;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected)
                    continue;

                total++;

                if (player.IsReceivingSnapshot)
                {
                    connecting++;
                }
                else if (player.IsSleeping())
                {
                    sleeping++;
                }
                else
                {
                    active++;
                }
            }

            return (total, active, sleeping, connecting);
        }

        /// <summary>
        /// Форматирование сообщения с подстановкой статистики
        /// </summary>
        private string FormatMessageWithStats(string message)
        {
            if (!cfg.SettingsMessages.ShowPlayerStatsInMessages)
                return message;

            var stats = GetPlayerStatistics();
            
            // Заменяем плейсхолдеры на цветные значения
            string formattedMessage = message
                .Replace("{total}", $"<color={cfg.SettingsMessages.NumberColor}>{stats.total}</color>")
                .Replace("{active}", $"<color={cfg.SettingsMessages.NumberColor}>{stats.active}</color>")
                .Replace("{sleeping}", $"<color={cfg.SettingsMessages.NumberColor}>{stats.sleeping}</color>")
                .Replace("{connecting}", $"<color={cfg.SettingsMessages.NumberColor}>{stats.connecting}</color>");

            return formattedMessage;
        }

        #endregion

        #region Lang [Локализация]

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"PanelHelpMessage", "Вы должны выбрать один из вариантов:\n/panel <color=green>on</color> - включает показ панели\n/panel <color=red>off</color> - выключает показ панели" },
            {"PanelOff", "Вы <color=red>выключили</color> показ панели" },
            {"PanelOn", "Вы <color=green>включили</color> показ панели" },
        };

        #endregion

        #region Hooks
        void InitializeLang()
        {
            lang.RegisterMessages(Messages, this, "ru");
            Messages = lang.GetMessages("ru", this);
        }
        void Loaded()
        {
            cfg = Config.ReadObject<PluginConfig>();
            Config.WriteObject(cfg);
            EventNames.Add("plane"); // 0
            EventNames.Add("heli"); // 1
            EventNames.Add("ch47"); // 2
            EventNames.Add("cargo"); // 3

            EventAnMinX.Add("0.29", "0.39"); // 0
            EventAnMinX.Add("0.365", "0.31"); // 1
            EventAnMinX.Add("0.448", "0.31"); // 2
            EventAnMinX.Add("0.53", "0.32"); // 3

            EventAnMaxX.Add("0.333", "0.61"); // 0
            EventAnMaxX.Add("0.41", "0.69"); // 1
            EventAnMaxX.Add("0.493", "0.69"); // 2
            EventAnMaxX.Add("0.575", "0.68"); // 3

            foreach(BasePlayer p in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(p);
            }
        }
        void OnServerInitialized()
        {
            InitializeLang();
            _imageUI = new ImageUI(this);
            _imageUI.DownloadImage();
            
            // Запускаем таймер сообщений только если они включены
            if (cfg.SettingsMessages.EnableMessages)
            {
                InvokeHandler.Instance.InvokeRepeating(DrawMessage, cfg.SettingsMessages.RefreshTimer, cfg.SettingsMessages.RefreshTimer);
            }

            foreach (var player in BasePlayer.activePlayerList) OnPlayerConnected(player);
        }
        void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(DrawMessage);
            _imageUI?.UnloadImages();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, Layer1);
                CuiHelper.DestroyUi(player, MessageLayer);
            }
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (entity is CargoPlane || entity is PatrolHelicopter || entity is CargoShip || entity is CH47Helicopter)
            {
                var tag = entity is CargoPlane ? "plane" : entity is PatrolHelicopter ? "heli" : entity is CH47Helicopter ? "ch47" : entity is CargoShip ? "cargo" : "";
                timer.Once(1f, () => { foreach (var players in BasePlayer.activePlayerList) DrawEvents(players, tag); });
            }
            else return;
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }
            if (!PanelVisibility.ContainsKey(player.userID)) { PanelVisibility.Add(player.userID, true); }

            NextTick(() => 
            {
                DrawMessage();
                DrawMenu(player);
                if (cfg.MainSettings.EnableStore)
                {
                    DrawStoreMenu(player);
                }

            });
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            timer.Once(1f, () => {
                foreach (var players in BasePlayer.activePlayerList)
                {
                    if (PanelVisibility[players.userID] == false) return;
                    
                }
            });
        }
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || entity.net == null)  return;
            if (entity is CargoPlane)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "plane");
            if (entity is PatrolHelicopter)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "heli");
            if (entity is CargoShip)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "cargo");
            if (entity is CH47Helicopter)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "ch47");
        }
        #endregion

        #region Custom Bools
        bool HasEntity(string name)
        {
            if (name == "plane") 
            {
                foreach (var check in BaseNetworkable.serverEntities) { if (check is CargoPlane) { return true; } }
            }
            if (name == "heli") 
            {
                foreach (var check in BaseNetworkable.serverEntities) { if (check is PatrolHelicopter) { return true; } }
            }
            if (name == "ch47") 
            {
                foreach (var check in BaseNetworkable.serverEntities) { if (check is CH47Helicopter) { return true; } }
            }
            if (name == "cargo") 
            {
                foreach (var check in BaseNetworkable.serverEntities) { if (check is CargoShip) { return true; } }
            }

            return false;
        }

        #endregion

        #region UI

        public void RefreshUIForAllPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null && player.IsConnected)
                {
                    DrawMessage();
                    DrawMenu(player);
                    if (cfg.MainSettings.EnableStore)
                    {
                        DrawStoreMenu(player);
                    }
                }
            }
        }

        void DrawMessage()
        {
            // Проверяем, включены ли сообщения
            if (!cfg.SettingsMessages.EnableMessages)
                return;

            foreach (var players in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(players, MessageLayer);
                var container = new CuiElementContainer();

                container.Add(Panel(MessageLayer, "0.3453124 -0.0009259344", "0.6416667 0.0287037", HexToRustFormat("#FFFFFF00"), "Hud", 0, "", false, "0 0", "0 0"));

                // Получаем случайное сообщение и форматируем его со статистикой
                string originalMessage = cfg.SettingsMessages.Messages[new System.Random().Next(cfg.SettingsMessages.Messages.Count)];
                string formattedMessage = FormatMessageWithStats(originalMessage);

                container.Add(Text(MessageLayer + ".Text", MessageLayer, "0 0 0 1", formattedMessage, TextAnchor.MiddleCenter, cfg.SettingsMessages.TextSize, "0 0", "1 1"));
                CuiHelper.AddUi(players, container);
            }
        }

        void DrawEvents(BasePlayer player, string name)
        {
            string anMinX = "";
            string EventAnMinY = "";
            string anMaxX = "";
            string EventAnMaxY = "";
            if (PanelVisibility[player.userID] == false) return;
            var container = new CuiElementContainer();
            for (int i = 0; i < EventNames.Count; i++)
            {
                if (EventNames[i] == name)
                {
                    anMinX = EventAnMinX.ElementAt(i).Key;
                    EventAnMinY = EventAnMinX.ElementAt(i).Value;
                    anMaxX = EventAnMaxX.ElementAt(i).Key;
                    EventAnMaxY = EventAnMaxX.ElementAt(i).Value;
                }
            }

            CuiHelper.DestroyUi(player, Layer + "." + name);
            string imageName = HasEntity(name) ? $"{name}_called" : name;
            string imageId = _imageUI?.GetImage(imageName);
            container.Add(Panel(Layer + "." + name, $"{anMinX} {EventAnMinY}", $"{anMaxX} {EventAnMaxY}", "", Layer, 0.1f, imageId ?? "", false, "", ""));
            CuiHelper.AddUi(player, container);
        }

        private void DrawStoreMenu(BasePlayer player)
        {
            if (!cfg.MainSettings.EnableStore) return;
            CuiHelper.DestroyUi(player, Layer1);
            var container = new CuiElementContainer();

            /*string storeImage = _imageUI?.GetImage("store") ?? "";
            container.Add(Panel(Layer1, "0.001041666 0.9648147", "0.001041666 0.9648147", HexToRustFormat("#FFFFFF00"), "Hud", 0f, "", false, "10 -4", "313 23"));
            container.Add(Panel(Layer1 + ".Store", "0.001849664 0.02469136", "0.08985749 1.012346", "", Layer1, 0f, storeImage, false, "0 0", "0 0"));
            container.Add(Button(Layer1 + ".button", Layer1 + ".Store", defaultSprite, "chat.say /store", "0 0 0 0", "0 0", "1 1"));*/

            CuiHelper.AddUi(player, container);
        }
        private void DrawMenu(BasePlayer player)
        {
            if (PanelVisibility[player.userID] == false) return;

            var time = TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm");

            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.000571666 0.9748147", AnchorMax = "0.007041666 0.9648147", OffsetMin = "-25 -5", OffsetMax = "243 21" },
                Image = { Color = "0 0 0 0" }
            }, "OverlayNonScaled", Layer); 

            string backgroundImage = _imageUI?.GetImage("background1");
            if (!string.IsNullOrEmpty(backgroundImage))
            {
                container.Add(new CuiElement
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiRawImageComponent { Png = backgroundImage, FadeIn = 1f },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                    }
                });
            }


            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.12 0", AnchorMax = $"0.26 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0.0", Command = "chat.say /menu" },
                Text = { Text = $"Меню", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf" }
            }, Layer);





            
            string planeImage = _imageUI?.GetImage(HasEntity("plane") ? "plane_called" : "plane") ?? "";
            string heliImage = _imageUI?.GetImage(HasEntity("heli") ? "heli_called" : "heli") ?? "";
            string ch47Image = _imageUI?.GetImage(HasEntity("ch47") ? "ch47_called" : "ch47") ?? "";
            string cargoImage = _imageUI?.GetImage(HasEntity("cargo") ? "cargo_called" : "cargo") ?? "";
            
            container.Add(Panel(Layer + ".plane", "0.29 0.39", "0.333 0.61", "", Layer, 0.1f, planeImage, false, "0 0", "0 0"));
            container.Add(Panel(Layer + ".heli", "0.365 0.31", "0.41 0.69", "", Layer, 0.1f, heliImage, false, "0 0", "0 0"));
            container.Add(Panel(Layer + ".ch47", "0.448 0.31", "0.493 0.69", "", Layer, 0.1f, ch47Image, false, "0 0", "0 0"));
            container.Add(Panel(Layer + ".cargo", "0.53 0.32", "0.575 0.68", "", Layer, 0.1f, cargoImage, false, "0 0", "0 0"));

            CuiHelper.AddUi(player, container);
        }

        #endregion
        private void ShowIco(BasePlayer player, float sec, string png)
        {
            PrintWarning("1");
            CuiHelper.DestroyUi(player, "icc");
            var container = new CuiElementContainer();

            string imageId = _imageUI?.GetImage(png);
            if (string.IsNullOrEmpty(imageId)) return;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.818 0", AnchorMax = "0.918 1" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "icc");

            container.Add(new CuiElement
            {
                Parent = "icc",
                Components =
                {
                    new CuiRawImageComponent { Png = imageId, FadeIn = 1f },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });

            timer.Once(sec, () => {
                CuiHelper.DestroyUi(player, "icc");
            });
            CuiHelper.AddUi(player, container);
        }

        #region Helpers

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
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
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        #endregion

        #region ImageUI

        private class ImageUI
        {
            private readonly FuelPanel _plugin;
            private const String _path = "TPSystem/FuelPanel/Images/";
            private const String _printPath = "data/" + _path;
            private readonly Dictionary<String, ImageData> _images = new()
            {
                { "background1", new ImageData() },
                { "store", new ImageData() },
                { "plane", new ImageData() },
                { "heli", new ImageData() },
                { "ch47", new ImageData() },
                { "cargo", new ImageData() },
                { "plane_called", new ImageData() },
                { "heli_called", new ImageData() },
                { "ch47_called", new ImageData() },
                { "cargo_called", new ImageData() }
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

            public ImageUI(FuelPanel plugin)
            {
                _plugin = plugin;
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
                    var imagePair = image.Value;
                    ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(imagePair.Key, imagePair.Value));
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
                        _plugin.PrintError($"Не удалось загрузить следующие изображения: {images}. Возможно, вы не загрузили их в папку '{_printPath}'.");
                    }
                    else
                    {
                        _plugin.Puts($"{_images.Count} изображений успешно загружено!");
                    }
                    
                    // Перерисовываем UI для всех игроков после загрузки изображений
                    _plugin.NextTick(() => _plugin.RefreshUIForAllPlayers());
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

            private IEnumerator ProcessDownloadImage(string name, ImageData imageData)
            {
                string url = "file://" + Oxide.Core.Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + _path + name + ".png";

                using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return www.SendWebRequest();

                    if (www.isNetworkError || www.isHttpError)
                    {
                        imageData.Status = ImageStatus.Failed;
                    }
                    else
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(www);
                        imageData.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                        imageData.Status = ImageStatus.Loaded;
                        UnityEngine.Object.DestroyImmediate(tex);
                    }

                    DownloadImage();
                }
            }
        }

        #endregion

        #region Chat Commands

        /// <summary>
        /// Команда для получения статистики игроков
        /// </summary>
        [ChatCommand("players")]
        private void PlayersCommand(BasePlayer player, string command, string[] args)
        {
            var stats = GetPlayerStatistics();
            string textColor = cfg.SettingsMessages.TextColor;
            string numberColor = cfg.SettingsMessages.NumberColor;
            
            string message = $"<color={textColor}>Статистика игроков сервера GoldMine:</color>\n" +
                           $"<color={textColor}>Всего онлайн:</color> <color={numberColor}>{stats.total}</color>\n" +
                           $"<color={textColor}>Активных:</color> <color={numberColor}>{stats.active}</color>\n" +
                           $"<color={textColor}>Спящих:</color> <color={numberColor}>{stats.sleeping}</color>\n" +
                           $"<color={textColor}>Подключающихся:</color> <color={numberColor}>{stats.connecting}</color>";
            
            SendReply(player, message);
        }

        /// <summary>
        /// Команда для переключения панели
        /// </summary>
        [ChatCommand("panel")]
        private void PanelCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, Messages["PanelHelpMessage"]);
                return;
            }

            switch (args[0].ToLower())
            {
                case "on":
                    if (!PanelVisibility.ContainsKey(player.userID))
                        PanelVisibility.Add(player.userID, true);
                    else
                        PanelVisibility[player.userID] = true;
                    
                    DrawMenu(player);
                    if (cfg.MainSettings.EnableStore)
                        DrawStoreMenu(player);
                    
                    SendReply(player, Messages["PanelOn"]);
                    break;

                case "off":
                    if (!PanelVisibility.ContainsKey(player.userID))
                        PanelVisibility.Add(player.userID, false);
                    else
                        PanelVisibility[player.userID] = false;
                    
                    CuiHelper.DestroyUi(player, Layer);
                    CuiHelper.DestroyUi(player, Layer1);
                    
                    SendReply(player, Messages["PanelOff"]);
                    break;

                default:
                    SendReply(player, Messages["PanelHelpMessage"]);
                    break;
            }
        }

        /// <summary>
        /// Команда для управления сообщениями (только для администраторов)
        /// </summary>
        [ChatCommand("bloodmessages")]
        private void MessagesCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "У вас нет прав для выполнения этой команды.");
                return;
            }

            if (args.Length == 0)
            {
                string status = cfg.SettingsMessages.EnableMessages ? "включены" : "отключены";
                SendReply(player, $"Сообщения сейчас {status}. Использование: /bloodmessages <on|off>");
                return;
            }

            switch (args[0].ToLower())
            {
                case "on":
                    cfg.SettingsMessages.EnableMessages = true;
                    Config.WriteObject(cfg);
                    
                    // Запускаем таймер сообщений
                    InvokeHandler.Instance.InvokeRepeating(DrawMessage, cfg.SettingsMessages.RefreshTimer, cfg.SettingsMessages.RefreshTimer);
                    
                    SendReply(player, "Сообщения FuelPanel включены.");
                    break;

                case "off":
                    cfg.SettingsMessages.EnableMessages = false;
                    Config.WriteObject(cfg);
                    
                    // Останавливаем таймер и очищаем сообщения
                    InvokeHandler.Instance.CancelInvoke(DrawMessage);
                    foreach (var p in BasePlayer.activePlayerList)
                    {
                        if (p != null && p.IsConnected)
                            CuiHelper.DestroyUi(p, MessageLayer);
                    }
                    
                    SendReply(player, "Сообщения FuelPanel отключены.");
                    break;

                default:
                    SendReply(player, "Использование: /bloodmessages <on|off>");
                    break;
            }
        }

        #endregion
    }
}