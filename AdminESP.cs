using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("AdminESP", "pluginfuel.ru", "2.0.1")]
    [Description("ESP для администраторов")]
    public class AdminESP : RustPlugin
    {
        private Dictionary<ulong, PlayerSetting> PlayerSettings = new Dictionary<ulong, PlayerSetting>();
        private HashSet<BasePlayer> statsUIDisabled = new HashSet<BasePlayer>();
        private const string AdminPermission = "adminesp.use";
        private const string MainUI = "AdminESP";
        private static AdminESP Instance;

        public class PlayerSetting
        {
            public bool Enabled = false;
            public float UpdateTime = 0.15f;
            public int PlayerDistance = 500;
            public bool ShowAdmins = true;
            public bool DrawNames = true;
            public bool DrawBoxes = false;
            public bool DrawEyeLine = true;
            public int EyeLineDistance = 50;
            public bool Sleepers = true;
        }

        #region Консольные команды
        [ConsoleCommand("adminesp.toggle")]
        private void cmdConsoleEnabledESP(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
                return;

            if (!PlayerSettings.TryGetValue(player.userID, out var data))
            {
                SendReply(player, "Настройки не найдены");
                return;
            }

            data.Enabled = !data.Enabled;
            
            if (data.Enabled)
            {
                SendReply(player, "ESP включен");
                if (player.GetComponent<ESPPlayer>() == null)
                    player.gameObject.AddComponent<ESPPlayer>()?.Init(data);
            }
            else
            {
                SendReply(player, "ESP выключен");
                var esp = player.GetComponent<ESPPlayer>();
                if (esp != null)
                    UnityEngine.Object.Destroy(esp);
            }
        }

        [ConsoleCommand("adminespUI_toggle")]
        private void cmdConsoleEnabledESPUI(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null || args.Args == null || args.Args.Length < 2) 
                return;
            
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
                return;

            if (!PlayerSettings.TryGetValue(player.userID, out var data))
                return;

            var type = args.Args[0];
            var value = args.Args[1];
            bool success = false;

            switch (type.ToLower())
            {
                case "updatetime":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float updateTime) && 
                        updateTime >= 0.1f && updateTime <= 15f)
                    {
                        data.UpdateTime = updateTime;
                        success = true;
                    }
                    break;
                    
                case "playerdistance":
                    if (int.TryParse(value, out int distance) && distance >= 50 && distance <= 500)
                    {
                        data.PlayerDistance = distance;
                        success = true;
                    }
                    break;
                    
                case "showadmins":
                    if (bool.TryParse(value, out bool showAdmins))
                    {
                        data.ShowAdmins = showAdmins;
                        success = true;
                    }
                    break;
                    
                case "drawnames":
                    if (bool.TryParse(value, out bool drawNames))
                    {
                        data.DrawNames = drawNames;
                        success = true;
                    }
                    break;
                    
                case "drawboxes":
                    if (bool.TryParse(value, out bool drawBoxes))
                    {
                        data.DrawBoxes = drawBoxes;
                        success = true;
                    }
                    break;
                    
                case "draweyeline":
                    if (bool.TryParse(value, out bool drawEyeLine))
                    {
                        data.DrawEyeLine = drawEyeLine;
                        success = true;
                    }
                    break;
                    
                case "sleeping":
                    if (bool.TryParse(value, out bool sleepers))
                    {
                        data.Sleepers = sleepers;
                        success = true;
                    }
                    break;
                    
                case "eyelinedistance":
                    if (int.TryParse(value, out int eyeDistance) && eyeDistance >= 10 && eyeDistance <= 200)
                    {
                        data.EyeLineDistance = eyeDistance;
                        success = true;
                    }
                    break;
                    
                case "enable":
                    if (bool.TryParse(value, out bool enabled))
                    {
                        data.Enabled = enabled;
                        if (enabled)
                        {
                            if (player.GetComponent<ESPPlayer>() == null)
                                player.gameObject.AddComponent<ESPPlayer>()?.Init(data);
                        }
                        else
                        {
                            var esp = player.GetComponent<ESPPlayer>();
                            if (esp != null)
                                UnityEngine.Object.Destroy(esp);
                        }
                        success = true;
                    }
                    break;
            }

            if (success)
                CrateMainMenu(player);
        }

        [ConsoleCommand("adminEsp_mainmenu")]
        private void cmdMainMenuESP(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;
            
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission)) 
                return;

            if (statsUIDisabled.Add(player))
            {
                CrateMainMenu(player);
            }
            else
            {
                CuiHelper.DestroyUi(player, $"{MainUI}MenuPanel");
                statsUIDisabled.Remove(player);
            }
        }
        #endregion

        #region Чат команды
        [ChatCommand("ae")]
        private void cmdAdminESP(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                SendReply(player, "У вас нет доступа к этой команде");
                return;
            }

            if (!PlayerSettings.TryGetValue(player.userID, out var data))
                return;

            if (args == null || args.Length == 0)
            {
                data.Enabled = !data.Enabled;
                
                if (data.Enabled)
                {
                    SendReply(player, "ESP включен");
                    if (player.GetComponent<ESPPlayer>() == null)
                        player.gameObject.AddComponent<ESPPlayer>()?.Init(data);
                }
                else
                {
                    SendReply(player, "ESP выключен");
                    var esp = player.GetComponent<ESPPlayer>();
                    if (esp != null)
                        UnityEngine.Object.Destroy(esp);
                }
                return;
            }

            if (args.Length >= 1 && args[0] == "settings")
            {
                if (args.Length == 1)
                {
                    statsUIDisabled.Add(player);
                    CrateMainMenu(player);
                }
                else if (args.Length == 3)
                {
                    HandleSettingCommand(player, data, args[1].ToLower(), args[2]);
                }
            }
            else if (args.Length >= 1 && args[0] == "help")
            {
                ShowHelp(player, data);
            }
        }

        private void HandleSettingCommand(BasePlayer player, PlayerSetting data, string setting, string value)
        {
            switch (setting)
            {
                case "updatetime":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float updateTime))
                    {
                        data.UpdateTime = Mathf.Clamp(updateTime, 0.1f, 15f);
                        SendReply(player, $"Время обновления изменено до {data.UpdateTime:F1} с.");
                    }
                    break;
                    
                case "playerdistance":
                    if (int.TryParse(value, out int distance))
                    {
                        data.PlayerDistance = Mathf.Clamp(distance, 50, 500);
                        SendReply(player, $"Дистанция видимости изменена до {data.PlayerDistance} м.");
                    }
                    break;
                    
                case "showadmins":
                    if (bool.TryParse(value, out bool showAdmins))
                    {
                        data.ShowAdmins = showAdmins;
                        SendReply(player, $"Отображение админов изменено на {data.ShowAdmins}");
                    }
                    break;
                    
                case "drawnames":
                    if (bool.TryParse(value, out bool drawNames))
                    {
                        data.DrawNames = drawNames;
                        SendReply(player, $"Отображение имен изменено на {data.DrawNames}");
                    }
                    break;
                    
                case "drawboxes":
                    if (bool.TryParse(value, out bool drawBoxes))
                    {
                        data.DrawBoxes = drawBoxes;
                        SendReply(player, $"Отображение боксов изменено на {data.DrawBoxes}");
                    }
                    break;
                    
                case "sleeping":
                    if (bool.TryParse(value, out bool sleepers))
                    {
                        data.Sleepers = sleepers;
                        SendReply(player, $"Отображение спящих изменено на {data.Sleepers}");
                    }
                    break;
                    
                case "draweyeline":
                    if (bool.TryParse(value, out bool drawEyeLine))
                    {
                        data.DrawEyeLine = drawEyeLine;
                        SendReply(player, $"Отображение взгляда изменено на {data.DrawEyeLine}");
                    }
                    break;
                    
                case "eyelinedistance":
                    if (int.TryParse(value, out int eyeDistance))
                    {
                        data.EyeLineDistance = Mathf.Clamp(eyeDistance, 10, 200);
                        SendReply(player, $"Длина линии взгляда изменена до {data.EyeLineDistance}");
                    }
                    break;
            }
        }

        private void ShowHelp(BasePlayer player, PlayerSetting data)
        {
            SendReply(player, "ESP Настройки:\n"
                + $"\n/ae settings UpdateTime {data.UpdateTime:F1} - частота обновления данных (0.1-15 сек)"
                + $"\n/ae settings PlayerDistance {data.PlayerDistance} - максимальная дистанция отображения (50-500 метров)"
                + $"\n/ae settings ShowAdmins {data.ShowAdmins} - показывать админов (true/false)"
                + $"\n/ae settings DrawNames {data.DrawNames} - показывать имена игроков (true/false)"
                + $"\n/ae settings DrawBoxes {data.DrawBoxes} - показывать боксы игроков (true/false)"
                + $"\n/ae settings DrawEyeLine {data.DrawEyeLine} - показывать взгляд игроков (true/false)"
                + $"\n/ae settings Sleeping {data.Sleepers} - показывать спящих игроков (true/false)"
                + $"\n/ae settings EyeLineDistance {data.EyeLineDistance} - длина линии взгляда (10-200 метров)");
        }
        #endregion

        #region Инициализация и загрузка
        private void OnServerInitialized()
        {
            Instance = this;
            LoadData();
            permission.RegisterPermission(AdminPermission, this);
            
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(1f, () => OnPlayerConnected(player));
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                if (PlayerSettings.ContainsKey(player.userID))
                    PlayerSettings.Remove(player.userID);
                return;
            }

            if (!PlayerSettings.ContainsKey(player.userID))
            {
                PlayerSettings[player.userID] = new PlayerSetting();
            }

            if (PlayerSettings[player.userID].Enabled)
            {
                if (player.GetComponent<ESPPlayer>() == null)
                    player.gameObject.AddComponent<ESPPlayer>()?.Init(PlayerSettings[player.userID]);
            }
            
            CrateButtonMenu(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            
            var esp = player.GetComponent<ESPPlayer>();
            if (esp != null)
                UnityEngine.Object.Destroy(esp);
                
            statsUIDisabled.Remove(player);
            DestroyUI(player);
        }

        private void LoadData()
        {
            try
            {
                PlayerSettings = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerSetting>>(Name);
                if (PlayerSettings == null)
                    PlayerSettings = new Dictionary<ulong, PlayerSetting>();
            }
            catch
            {
                PlayerSettings = new Dictionary<ulong, PlayerSetting>();
            }
        }

        private void SaveData()
        {
            if (PlayerSettings != null)
                Interface.Oxide.DataFileSystem.WriteObject(Name, PlayerSettings);
        }
        #endregion

        #region UI Система
        private void CrateButtonMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MainUI);
            
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
                return;

            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.2" },
                RectTransform = { AnchorMin = "0 0.025", AnchorMax = "0.05 0.053" }
            }, "Hud.Menu", MainUI);

            elements.Add(new CuiElement
            {
                Parent = MainUI,
                Components =
                {
                    new CuiTextComponent { Text = "ESP", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            elements.Add(new CuiElement
            {
                Parent = MainUI,
                Components =
                {
                    new CuiButtonComponent { Color = "1 1 1 0", Command = "adminEsp_mainmenu" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            CuiHelper.AddUi(player, elements);
        }

        private void CrateMainMenu(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, $"{MainUI}MenuPanel");
            
            if (!PlayerSettings.TryGetValue(player.userID, out var data))
                return;

            var elements = new CuiElementContainer();

            // Главная панель
            elements.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0" },
                RectTransform = { AnchorMin = "0.052 0.045", AnchorMax = "0.3 0.26" },
                CursorEnabled = true
            }, "Hud.Menu", $"{MainUI}MenuPanel");

            // Заголовок
            elements.Add(new CuiElement
            {
                Parent = $"{MainUI}MenuPanel",
                Components =
                {
                    new CuiTextComponent { Text = "НАСТРОЙКА ESP", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0.8", AnchorMax = "1 1" }
                }
            });

            // Контейнер настроек
            elements.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.2" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.8" }
            }, $"{MainUI}MenuPanel", $"{MainUI}CurrentPanel");

            // Создаем 8 панелей для настроек (2x4)
            var positions = GetPositions(2, 4, 0.01f, 0.05f);
            for (int i = 0; i < 8; i++)
            {
                elements.Add(new CuiPanel
                {
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = positions[i].AnchorMin, AnchorMax = positions[i].AnchorMax }
                }, $"{MainUI}CurrentPanel", $"{MainUI}CurrentPanel.{i}");
            }

            // Кнопка включения/выключения
            elements.Add(new CuiButton
            {
                Button = { Color = !data.Enabled ? "0.60 0.82 0.55 0.5" : "0.94 0.43 0.44 0.5",
                          Command = $"adminespUI_toggle enable {!data.Enabled}" },
                RectTransform = { AnchorMin = "0 -0.2", AnchorMax = "1 0" },
                Text = { Text = data.Enabled ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, $"{MainUI}MenuPanel");

            // Настройки
            CreateSettingUI(elements, "Спящие:", data.Sleepers, "sleeping", $"{MainUI}CurrentPanel.0");
            CreateSliderUI(elements, "Обновление:", data.UpdateTime.ToString("0.0"), 
                          data.UpdateTime - 0.1f, data.UpdateTime + 0.1f, "updatetime", $"{MainUI}CurrentPanel.1");
            CreateSliderUI(elements, "Дистанция:", data.PlayerDistance.ToString(), 
                          data.PlayerDistance - 50, data.PlayerDistance + 50, "playerdistance", $"{MainUI}CurrentPanel.2");
            CreateSettingUI(elements, "Админы:", data.ShowAdmins, "showadmins", $"{MainUI}CurrentPanel.3");
            CreateSettingUI(elements, "Имена:", data.DrawNames, "drawnames", $"{MainUI}CurrentPanel.4");
            CreateSettingUI(elements, "Боксы:", data.DrawBoxes, "drawboxes", $"{MainUI}CurrentPanel.5");
            CreateSettingUI(elements, "Линия взгляда:", data.DrawEyeLine, "draweyeline", $"{MainUI}CurrentPanel.6");
            CreateSliderUI(elements, "Длина линии:", data.EyeLineDistance.ToString(), 
                          data.EyeLineDistance - 10, data.EyeLineDistance + 10, "eyelinedistance", $"{MainUI}CurrentPanel.7");

            CuiHelper.AddUi(player, elements);
        }

        private void CreateSettingUI(CuiElementContainer elements, string label, bool value, string command, string parent)
        {
            elements.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiTextComponent { Text = label, FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.5 1" }
                }
            });

            elements.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "1 0.997" },
                Button = { Color = !value ? "0.60 0.82 0.55 0.5" : "0.94 0.43 0.44 0.5",
                          Command = $"adminespUI_toggle {command} {!value}" },
                Text = { Text = value ? "ВЫКЛ" : "ВКЛ", Align = TextAnchor.MiddleCenter, FontSize = 12 }
            }, parent);
        }

        private void CreateSliderUI(CuiElementContainer elements, string label, string value, float minValue, float maxValue, string command, string parent)
        {
            elements.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiTextComponent { Text = label, FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0.5 1" }
                }
            });

            elements.Add(new CuiElement
            {
                Parent = parent,
                Components =
                {
                    new CuiTextComponent { Text = value, FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "1 1" }
                }
            });

            elements.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.85 0", AnchorMax = "1 0.997" },
                Button = { Color = "1 1 1 0.5", Command = $"adminespUI_toggle {command} {maxValue}" },
                Text = { Text = "+", Align = TextAnchor.MiddleCenter, FontSize = 18 }
            }, parent);

            elements.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.65 0.997" },
                Button = { Color = "1 1 1 0.5", Command = $"adminespUI_toggle {command} {minValue}" },
                Text = { Text = "-", Align = TextAnchor.MiddleCenter, FontSize = 20 }
            }, parent);
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MainUI);
            CuiHelper.DestroyUi(player, $"{MainUI}MenuPanel");
        }
        #endregion

        #region ESP Player Component
        private class ESPPlayer : BaseEntity
        {
            private BasePlayer player;
            private PlayerSetting data;
            private float lastUpdate;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
            }

            public void Init(PlayerSetting settings)
            {
                data = settings;
            }

            private void FixedUpdate()
            {
                if (player == null || player.IsSleeping() || data == null || !player.IsConnected)
                {
                    OnDestroy();
                    return;
                }

                lastUpdate += Time.deltaTime;
                if (lastUpdate < data.UpdateTime)
                    return;

                lastUpdate = 0;
                
                // Активные игроки
                foreach (var target in BasePlayer.activePlayerList)
                {
                    if (target == null || target == player || target.transform == null)
                        continue;
                        
                    ProcessPlayer(target, false);
                }
                
                // Спящие игроки
                if (data.Sleepers)
                {
                    foreach (var target in BasePlayer.sleepingPlayerList)
                    {
                        if (target == null || target == player || target.transform == null)
                            continue;
                            
                        ProcessPlayer(target, true);
                    }
                }
            }

            private void ProcessPlayer(BasePlayer target, bool isSleeper)
            {
                var distance = Vector3.Distance(target.transform.position, player.transform.position);
                if (distance > data.PlayerDistance)
                    return;
                    
                if (target.IsAdmin && !data.ShowAdmins)
                    return;

                var displayDistance = Mathf.FloorToInt(distance);
                var color = isSleeper ? Color.white : Color.yellow;
                var prefix = isSleeper ? "СПИТ\n" : "";

                // Имя игрока
                if (data.DrawNames && distance > 2)
                {
                    var message = $"{prefix}{target.displayName} ({displayDistance} м.)\nHP: {Mathf.FloorToInt(target.health)}";
                    DrawText(target, color, message);
                }

                // Бокс
                if (data.DrawBoxes && distance > 2 && (!isSleeper || distance < 20))
                {
                    DrawBox(target, color);
                }

                // Линия взгляда
                if (data.DrawEyeLine && !isSleeper)
                {
                    DrawEyeLine(target, Color.green);
                }
            }

            private void DrawText(BasePlayer target, Color color, string message)
            {
                if (player.Connection.authLevel < 2)
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    
                player.SendConsoleCommand("ddraw.text", data.UpdateTime + Time.deltaTime, color, 
                                         target.eyes.position + new Vector3(0, 0.4f, 0), message);
                                         
                if (player.Connection.authLevel < 2)
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
            }

            private void DrawBox(BasePlayer target, Color color)
            {
                if (player.Connection.authLevel < 2)
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);

                var center = target.transform.position + new Vector3(0f, 1f, 0f);
                var height = BasePlayer.GetHeight(target.modelState.ducked);
                
                player.SendConsoleCommand("ddraw.box", data.UpdateTime + Time.deltaTime, color, center, height);

                if (player.Connection.authLevel < 2)
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
            }

            private void DrawEyeLine(BasePlayer target, Color color)
            {
                if (player.Connection.authLevel < 2)
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    
                var start = target.eyes.position;
                var end = target.eyes.position + target.eyes.HeadRay().direction * data.EyeLineDistance;
                player.SendConsoleCommand("ddraw.line", data.UpdateTime + Time.deltaTime, color, start, end);
                
                if (player.Connection.authLevel < 2)
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
            }

            public void DestroyComponent() => OnDestroy();
            
            private void OnDestroy()
            {
                if (this != null)
                    Destroy(this);
            }
        }
        #endregion

        #region Вспомогательные методы
        private void Unload()
        {
            // Уничтожаем все ESPPlayer компоненты
            var espPlayers = UnityEngine.Object.FindObjectsOfType<ESPPlayer>();
            foreach (var esp in espPlayers)
                UnityEngine.Object.Destroy(esp);
            
            // Очищаем UI
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
                
            SaveData();
        }

        private class Position
        {
            public float Xmin;
            public float Xmax;
            public float Ymin;
            public float Ymax;

            public string AnchorMin => $"{Xmin:F4} {Ymin:F4}";
            public string AnchorMax => $"{Xmax:F4} {Ymax:F4}";
        }

        private List<Position> GetPositions(int columns, int rows, float colPadding = 0, float rowPadding = 0, bool columnsFirst = false)
        {
            if (columns == 0 || rows == 0)
                return new List<Position>();

            var result = new List<Position>();
            var colsDiv = 1f / columns;
            var rowsDiv = 1f / rows;
            
            if (colPadding == 0) colPadding = colsDiv / 2;
            if (rowPadding == 0) rowPadding = rowsDiv / 2;

            for (int j = rows; j >= 1; j--)
            {
                for (int i = 1; i <= columns; i++)
                {
                    result.Add(new Position
                    {
                        Xmin = (i - 1) * colsDiv + colPadding / 2f,
                        Xmax = i * colsDiv - colPadding / 2f,
                        Ymin = (j - 1) * rowsDiv + rowPadding / 2f,
                        Ymax = j * rowsDiv - rowPadding / 2f
                    });
                }
            }

            return result;
        }
        #endregion
    }
}