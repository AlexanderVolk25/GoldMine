using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using Oxide.Core;
using System.Linq;
using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.IO;

namespace Oxide.Plugins
{
    [Info("MBKits", "pluginfuel.ru", "20.0.2")]
    [Description("MBKits")]
    public class MBKits : RustPlugin
    {
        [PluginReference] private Plugin MBCoins, Economics, ServerRewards;

        private Dictionary<string, KitData> kits = new();

        private Dictionary<string, string> kitContainerNames = new();

        private Dictionary<ulong, List<ChatErrorEntry>> playerErrors = new();

        private bool BuildModeDisableUIBackground = false;

        private Dictionary<ulong, Dictionary<string, double>> kitCooldowns = new();

        private Dictionary<ulong, Dictionary<string, int>> kitUses = new();

        private Dictionary<string, KitData> autoKits = new();

        private string kitsFile => $"MBSystem/MBKits/MBKits_kits";
        private string cooldownFile => $"MBSystem/MBKits/MBKits_cooldowns";
        private string usesFile => $"MBSystem/MBKits/MBKits_uses";
        private string autoKitsFile => $"MBSystem/MBKits/MBKits_autokits";

        #region Config

        private PluginConfig config;

        private class PluginConfig
        {
            [JsonProperty("NPC Image URL")]
            public string NpcImageUrl = "https://i.ibb.co/r2Ft9sJQ/MBNPC.png";

            [JsonProperty("Scrollbar Handle Color")]
            public string ScrollbarHandleColor = "1 0 0 0.6";

        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<PluginConfig>();
                if (config == null) throw new Exception();
            }
            catch
            {
                PrintWarning("Config corrupted, generating new one");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        #endregion

        #region Data
        private class KitData
        {
            public string Name;
            public float Price = 0f;
            public int EconomyType = 0;
            public string CurrencyName = "";
            public string ImageUrl = "https://i.postimg.cc/tJrNqhNX/42808.png";  // URL картинки набора
            public string Permission = "";
            public string CooldownText = "1h";
            public bool HideIfNoPermission = false;
            public int MaxUses = 0;
            public List<ItemData> Items = new();

        }

        private class ItemData
        {
            public string ShortName;
            public int Amount;
            public ulong SkinID = 0;

            public int AmmoAmount;
            public string AmmoShortName;

            public string Container;
            public int Slot;

            public List<ItemData> Contents = new List<ItemData>();

            [JsonProperty("Command (Player identifier {playerid})")]
            public string CustomCommand;
            public string CustomImage;
        }

        private class ChatErrorEntry
        {
            public string Text;
            public string ImageKey;
            public DateTime Timestamp;
        }

        private double GetCooldownSeconds(string text)
        {
            if (TryParseTime(text.ToLower(), out var ts))
                return ts.TotalSeconds;

            return 0;
        }

        [HookMethod("API_GetKitItems")]
        public List<object> API_GetKitItems(string kitName)
        {
            if (string.IsNullOrEmpty(kitName)) return null;
            kitName = kitName.ToLower();

            if (!kits.TryGetValue(kitName, out var kit))
                return null;

            var result = new List<object>();
            foreach (var item in kit.Items)
            {
                result.Add(new Dictionary<string, object>
                {
                    ["ShortName"] = item.ShortName,
                    ["Amount"] = item.Amount
                });
            }
            return result;
        }

        private static bool TryParseTime(string input, out TimeSpan result)
        {
            int d = 0, h = 0, m = 0, s = 0;
            Match matchD = Regex.Match(input, @"(\d+?)d");
            Match matchH = Regex.Match(input, @"(\d+?)h");
            Match matchM = Regex.Match(input, @"(\d+?)m");
            Match matchS = Regex.Match(input, @"(\d+?)s");

            if (matchD.Success) d = int.Parse(matchD.Groups[1].Value);
            if (matchH.Success) h = int.Parse(matchH.Groups[1].Value);
            if (matchM.Success) m = int.Parse(matchM.Groups[1].Value);
            if (matchS.Success) s = int.Parse(matchS.Groups[1].Value);

            if (!matchD.Success && !matchH.Success && !matchM.Success && !matchS.Success)
            {
                result = TimeSpan.Zero;
                return false;
            }

            result = new TimeSpan(d, h, m, s);
            return true;
        }

        private void LoadKits()
        {
            kits = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, KitData>>(kitsFile) ?? new();
        }

        private void SaveKits()
        {
            Interface.Oxide.DataFileSystem.WriteObject(kitsFile, kits);
        }

        private void LoadCooldowns()
        {
            kitCooldowns = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, double>>>(cooldownFile) ?? new();
        }

        private void SaveCooldowns()
        {
            Interface.Oxide.DataFileSystem.WriteObject(cooldownFile, kitCooldowns);
        }

        private void LoadUses()
        {
            kitUses = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, int>>>(usesFile) ?? new();
        }

        private void SaveUses()
        {
            Interface.Oxide.DataFileSystem.WriteObject(usesFile, kitUses);
        }

        private void SaveAutoKits()
        {
            Interface.Oxide.DataFileSystem.WriteObject(autoKitsFile, autoKits);
        }

        private void LoadAutoKits()
        {
            try
            {
                autoKits = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, KitData>>(autoKitsFile);

                foreach (var kit in autoKits.Values)
                {
                    if (!string.IsNullOrEmpty(kit.Permission))
                    {
                        if (!permission.PermissionExists(kit.Permission))
                            permission.RegisterPermission(kit.Permission, this);
                    }
                }
            }
            catch
            {
                autoKits = new Dictionary<string, KitData>();
            }
        }

        #endregion

        void Init()
        {
            LoadKits();
            SaveKits();
            LoadCooldowns();
            LoadUses(); 
            LoadAutoKits();
            permission.RegisterPermission("mbkits.admin", this);
        }

        private void OnServerInitialized()
        {
            RegisterConfigImages();

            LocalImageLoader.LoadAll(this);

            foreach (var kit in kits.Values)
            {
                if (!string.IsNullOrEmpty(kit.Permission) &&
                    !permission.PermissionExists(kit.Permission))
                {
                    permission.RegisterPermission(kit.Permission, this);
                }
            }

            PrintWarning("[MBKits] Images loading started...");
        }

        // === КОМАНДА ДЛЯ ПЕРЕЗАГРУЗКИ КАРТИНОК ===
        // Используйте: mbkits.reload в консоли для перезагрузки всех картинок
        [ConsoleCommand("mbkits.reload")]
        private void ReloadImagesCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin)
            {
                arg.ReplyWith("You don't have permission to use this command");
                return;
            }

            PrintWarning("[MBKits] Reloading images...");
            RegisterConfigImages();
            LocalImageLoader.LoadAll(this);
            PrintWarning("[MBKits] Images reloaded!");
        }


        private object SetBuildModeFromHub(bool state)
        {
            BuildModeDisableUIBackground = state;
            return null;
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "MBKits_UI");
                CuiHelper.DestroyUi(player, "MBKits_Inv_UI");
            }

        }

        private void OnPlayerConnected(BasePlayer player) 
        {

            ShowIntroImage(player);
        }

        private void OnNewSave()
        {
            kitUses.Clear();
            SaveUses();

            kitCooldowns.Clear();
            SaveCooldowns();

            Puts("[MBKits] Kit uses and cooldowns reset due to wipe.");
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            foreach (var kit in autoKits.Values)
                GiveAutoRespawnKit(player, kit);
        }

        #region LocalImageLoader

        private string GetSafeImageKey(string url)
        {
            try
            {
                string file = Path.GetFileNameWithoutExtension(url);
                return string.IsNullOrEmpty(file) ? Guid.NewGuid().ToString() : file.ToLower();
            }
            catch
            {
                return Guid.NewGuid().ToString();
            }
        }

        private Dictionary<string, string> ImageURLs = new Dictionary<string, string>
        {
            { "MBKits", "https://i.ibb.co/yF3npmQx/MBKits-Os.png" },
            { "MBKits_inv", "https://i.ibb.co/b5JTZVVF/MBKits-Inv-Os.png" },
            { "MBKitsBG_Default", "https://i.ibb.co/HpX7V5Kh/MBKits-BG-Default.png" },
            { "error_icon_invalid_kits", "https://i.ibb.co/sTvDbG6/error-icon-invalid.png" },
            { "success_icon_kits", "https://i.ibb.co/4ZMkCDbS/success-icon.png" }
        };

        private void RegisterConfigImages()
        {
            void Add(string key, string url)
            {
                if (!string.IsNullOrEmpty(url))
                {
                    ImageURLs[key] = url;
                }
            }

            Add("MB_NPC", config.NpcImageUrl);

            // === ЗАГРУЗКА КАРТИНОК ИЗ ПАПКИ ДЛЯ НАБОРОВ ===
            // Картинки берутся из: oxide/data/MBSystem/MBKits/Images/название_кита.png
            foreach (var kit in kits.Values)
            {
                string kitName = kit.Name.ToLower();
                string localImagePath = $"oxide/data/MBSystem/MBKits/Images/{kitName}.png";
                
                // Если картинка есть в локальной папке, используем её
                if (File.Exists(localImagePath))
                {
                    ImageURLs[kitName] = localImagePath;  // Без префикса kit_
                }
                else
                {
                    // Если картинка не найдена, выводим сообщение
                    PrintWarning($"[MBKits] ВНИМАНИЕ: Картинка для кита '{kit.Name}' не найдена!");
                    PrintWarning($"[MBKits] Добавьте файл: oxide/data/MBSystem/MBKits/Images/{kitName}.png");
                }

                foreach (var item in kit.Items)
                {
                    if (!string.IsNullOrEmpty(item.CustomImage))
                    {
                        string key = GetSafeImageKey(item.CustomImage);
                        Add($"item_{key}", item.CustomImage);
                    }
                }
            }
        }

        private class LocalImageLoader
        {
            private const string CacheFolder = "MBSystem/MBKits/Images";
            private const string CacheMetaFile = "MBSystem/MBKits/ImageCache";

            private static Dictionary<string, string> UrlCache = new();
            public static Dictionary<string, string> Images = new();

            public static void LoadAll(MBKits plugin)
            {
                UrlCache = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>(CacheMetaFile)
                    ?? new Dictionary<string, string>();

                Directory.CreateDirectory($"oxide/data/{CacheFolder}");

                // Сначала загружаем локальные изображения синхронно
                LoadLocalImagesSync(plugin);

                // Потом загружаем остальные асинхронно
                ServerMgr.Instance.StartCoroutine(Load(plugin));
            }

            private static void LoadLocalImagesSync(MBKits plugin)
            {
                foreach (var entry in plugin.ImageURLs)
                {
                    string key = entry.Key;
                    string url = entry.Value;
                    string filePath = $"oxide/data/{CacheFolder}/{key}.png";

                    // Если URL содержит путь к файлу (начинается с oxide/data), загружаем синхронно
                    if (url.StartsWith("oxide/data"))
                    {
                        if (File.Exists(url))
                        {
                            byte[] png = File.ReadAllBytes(url);

                            string id = FileStorage.server.Store(
                                png,
                                FileStorage.Type.png,
                                CommunityEntity.ServerInstance.net.ID
                            ).ToString();

                            Images[key] = id;
                        }
                        else
                        {
                            plugin.PrintError($"[MBKits] Local image not found: {url}");
                        }
                    }
                    // Проверяем есть ли картинка в локальной папке кэша
                    else if (File.Exists(filePath))
                    {
                        byte[] png = File.ReadAllBytes(filePath);

                        string id = FileStorage.server.Store(
                            png,
                            FileStorage.Type.png,
                            CommunityEntity.ServerInstance.net.ID
                        ).ToString();

                        Images[key] = id;
                    }
                }
            }

            private static IEnumerator Load(MBKits plugin)
            {
                foreach (var entry in plugin.ImageURLs)
                {
                    string key = entry.Key;
                    string url = entry.Value;
                    string filePath = $"oxide/data/{CacheFolder}/{key}.png";

                    // Пропускаем, если уже загружено синхронно
                    if (Images.ContainsKey(key))
                    {
                        continue;
                    }

                    // Если нет локальной картинки, пытаемся скачать с сайта
                    bool needsDownload = true;

                    if (UrlCache.TryGetValue(key, out string oldUrl) &&
                        oldUrl == url &&
                        File.Exists(filePath))
                        needsDownload = false;

                    if (needsDownload)
                    {
                        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
                        www.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                        yield return www.SendWebRequest();

                        if (www.isNetworkError || www.isHttpError)
                        {
                            plugin.PrintError($"[MBKits] Failed to download image: {key} | Error: {www.error}");
                            continue;
                        }

                        Texture2D tex = DownloadHandlerTexture.GetContent(www);
                        if (tex != null)
                        {
                            File.WriteAllBytes(filePath, tex.EncodeToPNG());
                            UrlCache[key] = url;
                            UnityEngine.Object.DestroyImmediate(tex);
                        }
                        else
                        {
                            plugin.PrintError($"[MBKits] Failed to get texture for: {key}");
                            continue;
                        }
                    }

                    byte[] pngData = File.ReadAllBytes(filePath);

                    string imageId = FileStorage.server.Store(
                        pngData,
                        FileStorage.Type.png,
                        CommunityEntity.ServerInstance.net.ID
                    ).ToString();

                    Images[key] = imageId;
                }

                Interface.Oxide.DataFileSystem.WriteObject(CacheMetaFile, UrlCache);

                CleanupUnusedImages(plugin);
            }

            private static void CleanupUnusedImages(MBKits plugin)
            {
                string folder = $"oxide/data/{CacheFolder}";

                if (!Directory.Exists(folder))
                    return;

                var files = Directory.GetFiles(folder, "*.png");

                foreach (string file in files)
                {
                    string filename = Path.GetFileNameWithoutExtension(file);

                    if (!plugin.ImageURLs.ContainsKey(filename))
                    {
                        try
                        {
                            File.Delete(file);
                            plugin.Puts($"[MBKits] Removed unused image: {filename}.png");

                            if (UrlCache.ContainsKey(filename))
                                UrlCache.Remove(filename);
                        }
                        catch (Exception ex)
                        {
                            plugin.PrintError($"[MBKits] Cleanup error {filename}: {ex.Message}");
                        }
                    }
                }

                Interface.Oxide.DataFileSystem.WriteObject(CacheMetaFile, UrlCache);
            }

            public static string Get(string name)
            {
                return Images.TryGetValue(name, out var id) ? id : null;
            }
        }

        #endregion

        #region Preloading images to the player before connecting

        private void ShowIntroImage(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "MBKits_IntroUI");

            var container = new CuiElementContainer();

            string[] imageKeys = new[]
            {
                "MBKits",
                "MBKits_inv",
                "MBKitsBG_Default",
                "error_icon_invalid_kits",
                "success_icon_kits",
            };

            float startY = 0f;
            float height = 0.2f;

            for (int i = 0; i < imageKeys.Length; i++)
            {
                string key = imageKeys[i];
                string imageId = LocalImageLoader.Get(key);

                if (string.IsNullOrEmpty(imageId))
                {
                    PrintWarning($"[MBKits] Image '{key}' not loaded");
                    continue;
                }

                container.Add(new CuiElement
                {
                    Name = $"MBKits_IntroUI_{i}",
                    Parent = ".Mains",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = imageId,
                            Color = "1 1 1 1"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"-1 {startY + (i * height)}",
                            AnchorMax = $"-0.7 {startY + ((i + 1) * height)}"
                        }
                    }
                });
            }

            CuiHelper.AddUi(player, container);

            timer.Once(5f, () =>
            {
                for (int i = 0; i < imageKeys.Length; i++)
                    CuiHelper.DestroyUi(player, $"MBKits_IntroUI_{i}");
            });
        }
        #endregion

        #region MBKitsUI

        [HookMethod("OpenMBKitsForPlayer")]
        private void ShowKitsUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "MBKits_UI");

            var container = new CuiElementContainer();

            string root = container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-228 -124", OffsetMax = "216 120" },
                CursorEnabled = true
            }, ".Mains", "MBKits_UI");

            /*if (!BuildModeDisableUIBackground)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0.0", Material = "assets/content/ui/uibackgroundblur.mat" },
                    RectTransform = { AnchorMin = "-1 -1", AnchorMax = "2 2" },
                    CursorEnabled = true
                }, root);
            }*/

            string imageId = LocalImageLoader.Get("MBKits");
            if (!string.IsNullOrEmpty(imageId))
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = root,
                    Components =
                    {
                        new CuiRawImageComponent { Png = imageId },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }
                });
            }

            string npcImageId = LocalImageLoader.Get("");
            if (!string.IsNullOrEmpty(npcImageId))
            {
                container.Add(new CuiElement
                {
                    Parent = root,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = npcImageId
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "-0.03 0.004", AnchorMax = "0.314 1"
                        }
                    }
                });
            }

            if (!BuildModeDisableUIBackground)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Close = "Menu_UI" },
                    RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                    Text = { Text = "", FontSize = 0 }
                }, root);
            }

           string centerPanel = container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-318 -165", OffsetMax = "390 165" },
                CursorEnabled = true
            }, root);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.441 0.86", AnchorMax = "0.75 0.955" },
                Text = { Text = Msg("MB_Kits_Player", player.userID.ToString()), Color = "1 1 1 0.0", Align = TextAnchor.MiddleLeft, FontSize = 30 }
            }, centerPanel);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.341 0.8", AnchorMax = "0.9 0.868" },
                Text = { Text = Msg("", player.userID.ToString()), Color = "1 1 1 0.0", Align = TextAnchor.MiddleLeft, FontSize = 21, Font = "permanentmarker.ttf" }
            }, centerPanel);

            container.Add(new CuiLabel
            {
                Text = { Text = Msg("MB_Close_Kits", player.userID.ToString()), FontSize = 13, Color = "1 1 1 0.4", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.2 -0.06", AnchorMax = "0.8 -0.02" }
            }, centerPanel);

            // === РАЗМЕР И РАССТОЯНИЕ МЕЖДУ НАБОРАМИ ===
            float blockHeight = 120f;      // Высота одного набора (уменьшено с 152 на 120)
            float blockMargin = 8f;        // Расстояние между наборами (в пикселях)
            float verticalShift = 3f;      // Вертикальный сдвиг (обычно не меняется)
            
            // ИНСТРУКЦИЯ ПО ИЗМЕНЕНИЮ:
            // - Уменьшите blockHeight для более компактного вида (например: 100, 110, 120)
            // - Увеличьте blockHeight для более крупных наборов (например: 150, 160, 180)
            // - Измените blockMargin для изменения расстояния между наборами

            var kitList = kits.Values .Where(k => string.IsNullOrEmpty(k.Permission) || !k.HideIfNoPermission || permission.UserHasPermission(player.UserIDString, k.Permission)) .ToList();
            int total = kitList.Count;
            float totalWidth = total * (blockHeight + blockMargin);
            float minVisibleWidth = 500f;
            float scrollContentWidth = Mathf.Max(totalWidth, minVisibleWidth);

            container.Add(new CuiElement
            {
                Name = "MBMenuScrollableKits",
                Parent = centerPanel,
                Components =
                {
                    new CuiScrollViewComponent
                    {
                        MovementType = ScrollRect.MovementType.Elastic,
                        Vertical = false,
                        Horizontal = true,
                        Inertia = true,
                        Elasticity = 0.10f,           // Увеличено для более плавной прокрутки по 5
                        DecelerationRate = 0.5f,     // Увеличено для замедления прокрутки
                        ScrollSensitivity = 15f,      // Уменьшено с 30 на 5 для прокрутки по 5 наборов
                        // === НАСТРОЙКА ПРОКРУТКИ ===
                        // ScrollSensitivity - чувствительность прокрутки (меньше = медленнее)
                        // Elasticity - упругость (больше = более плавно)
                        // DecelerationRate - замедление (больше = медленнее останавливается)
                        ContentTransform = new CuiRectTransform
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 1",
                            OffsetMin = $"0 0",
                            OffsetMax = $"{scrollContentWidth} 0"
                        },
                        VerticalScrollbar = new CuiScrollbar
                        {
                            Size = 5f,
                            AutoHide = false,
                            HandleColor = config.ScrollbarHandleColor,
                            TrackColor = "0.1 0.1 0.1 0"
                        },
                        // === ГОРИЗОНТАЛЬНЫЙ СКРОЛЛБАР ===
                        // Закомментируйте эти строки чтобы скрыть бегунок прокрутки
                        /*
                        HorizontalScrollbar = new CuiScrollbar
                        {
                            Size = 5f,
                            AutoHide = false,
                            HandleColor = config.ScrollbarHandleColor,
                            TrackColor = "0.1 0.1 0.1 0"
                        }
                        */
                    },
                    new CuiRectTransformComponent
                    {
                        // === ПОЗИЦИЯ И РАЗМЕР НАБОРОВ ===
                        // AnchorMin = "-0.05 0.15" - левый край (-0.05) и нижний край (0.15)
                        // AnchorMax = "0.961 0.85" - правый край (0.961) и верхний край (0.85)
                        // 
                        // ДЛЯ ИЗМЕНЕНИЯ ШИРИНЫ: меняйте -0.05 (влево) и 0.961 (вправо)
                        // ДЛЯ ИЗМЕНЕНИЯ ВЫСОТЫ: меняйте 0.15 (вниз) и 0.85 (вверх)
                        // Примеры:
                        // - Выше: измените 0.15 на 0.05 и 0.85 на 0.95
                        // - Ниже: измените 0.15 на 0.25 и 0.85 на 0.75
                        AnchorMin = "-0.05 0.15",
                        AnchorMax = "0.961 0.75"
                    },
                    new CuiNeedsCursorComponent()
                }
            });

            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "0 1",
                    OffsetMin = $"0 0",
                    OffsetMax = $"{scrollContentWidth} 0"
                }
            }, "MBMenuScrollableKits", "MBMenuKitsContent");

            for (int i = 0; i < total; i++)
            {
                var kit = kitList[i];

                float leftOffset = i * (blockHeight + blockMargin);
                float rightOffset = leftOffset + blockHeight;

                string bgImg = LocalImageLoader.Get("MBKitsBG_Default");
                string containerName = $"KitContainer_{i}";
                kitContainerNames[kit.Name.ToLower()] = containerName;


                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = $"{leftOffset} 0", OffsetMax = $"{rightOffset} 0" }
                }, "MBMenuKitsContent", containerName);

                if (!string.IsNullOrEmpty(bgImg))
                {
                    container.Add(new CuiElement
                    {
                        Name = $"KitBG_{i}",
                        Parent = containerName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = bgImg,
                                Sprite = "assets/content/textures/generic/fulltransparent.tga"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1"
                            }
                        }
                    });
                }

                string kitImg = LocalImageLoader.Get(kit.Name.ToLower());  // Без префикса kit_

                if (!string.IsNullOrEmpty(kitImg))
                {
                    container.Add(new CuiElement
                    {
                        Parent = containerName,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = kitImg,
                                Sprite = "assets/content/textures/generic/fulltransparent.tga"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.18 0.22",
                                AnchorMax = "0.75 0.85"
                            }
                        }
                    });
                }

                container.Add(new CuiLabel
                {
                    Text = { Text = kit.Name.ToUpper(), FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.9" },
                    RectTransform = { AnchorMin = "0.05 0.85", AnchorMax = "0.95 0.98" }
                }, containerName);

                bool hasPermission = string.IsNullOrEmpty(kit.Permission) || permission.UserHasPermission(player.UserIDString, kit.Permission);

                double cooldownUntil = 0;
                bool isOnCooldown = kitCooldowns.TryGetValue(player.userID, out var cooldowns) &&
                    cooldowns.TryGetValue(kit.Name.ToLower(), out cooldownUntil) &&
                    cooldownUntil > Facepunch.Math.Epoch.Current;


                if (!hasPermission)
                {
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0.3 0.3 0.3 0" },
                        RectTransform = { AnchorMin = "0.05 0.04", AnchorMax = "0.72 0.18" },
                        Text = { Text = Msg("MB_NoAccess", player.UserIDString), FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" }
                    }, containerName);
                }
                
                else if (isOnCooldown)
                {
                    double timeLeft = cooldownUntil - Facepunch.Math.Epoch.Current;
                    if (timeLeft < 0) timeLeft = 0;
                    TimeSpan ts = TimeSpan.FromSeconds(timeLeft);
                    string cooldownText = string.Format("{0:D2}h {1:D2}m", ts.Hours, ts.Minutes);

                    container.Add(new CuiButton
                    {
                        Button = { Color = "0.8 0.6 0.1 0" },
                        RectTransform = { AnchorMin = "0.05 0.04", AnchorMax = "0.72 0.18" },
                        Text = { Text = cooldownText, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 0.4 0.4 1" }
                    }, containerName);
                }
                else
                {
                    bool isBuyKit =
                        kit.EconomyType != 0 &&
                        kit.Price > 0f &&
                        !string.IsNullOrEmpty(kit.CurrencyName);

                    string cmd = isBuyKit
                        ? $"mbkits.buykit {kit.Name}"
                        : $"mbkits.givekit {kit.Name}";

                    string text;

                    if (isBuyKit)
                    {
                        text =
                            $"{Msg("MB_Buy", player.UserIDString)}  " +
                            $"<color=#ffd479>{kit.Price}</color> " +
                            $"<color=#cfcfcf>{kit.CurrencyName}</color>";
                    }
                    else
                    {
                        text = Msg("MB_Claim", player.UserIDString);
                    }

                    container.Add(new CuiButton
                    {
                        Button =
                        {
                            Color = isBuyKit ? "0.25 0.6 0.25 0" : "0.2 0.6 0.2 0",
                            Command = cmd
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.05 0.04",
                            AnchorMax = "0.72 0.18"
                        },
                        Text =
                        {
                            Text = text,
                            FontSize = 14,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, containerName, $"KitButton_{kit.Name}");


                }

                container.Add(new CuiButton
                {
                    Button = { Color = "0.2 0.2 0.8 0", Command = $"mbkits.viewkit {kit.Name}" },
                    RectTransform = { AnchorMin = "0.75 0.04", AnchorMax = "0.948 0.18" },
                }, containerName);

            }

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region MBKitsInvUI

        private void ShowKitsInvUI(BasePlayer player, KitData kit)
        {
            CuiHelper.DestroyUi(player, "MBKits_Inv_UI");
            CuiHelper.DestroyUi(player, "MBKits_UI");

            var container = new CuiElementContainer();

            string root = container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-228 -124", OffsetMax = "216 120" },
                CursorEnabled = true
            }, ".Mains", "MBKits_Inv_UI");

            if (!BuildModeDisableUIBackground)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0.0", Material = "assets/content/ui/uibackgroundblur.mat" },
                    RectTransform = { AnchorMin = "-1 -1", AnchorMax = "2 2" },
                    CursorEnabled = true
                }, root);
            }

            string imageId = LocalImageLoader.Get("MBKits_inv");
            if (!string.IsNullOrEmpty(imageId))
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = root,
                    Components =
                    {
                        new CuiRawImageComponent { Png = imageId },
                        new CuiRectTransformComponent
                        { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                    }
                });
            }

            string npcImageId = LocalImageLoader.Get("");
            if (!string.IsNullOrEmpty(npcImageId))
            {
                container.Add(new CuiElement
                {
                    Parent = root,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = npcImageId
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "-0.03 0.004", AnchorMax = "0.314 1"
                        }
                    }
                });
            }

            if (!BuildModeDisableUIBackground)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0", Close = "Menu_UI" },
                    RectTransform = { AnchorMin = "0.8 0.804", AnchorMax = "0.817 0.832" },
                    Text = { Text = "", FontSize = 0 }
                }, root);
            }

            string centerPanel = container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-418 -165", OffsetMax = "240 165" },
                CursorEnabled = true
            }, root);

            container.Add(new CuiLabel
            {
                Text = { Text = Msg("MB_Close_Kits", player.userID.ToString()), FontSize = 13, Color = "1 1 1 0.4", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.2 -0.06", AnchorMax = "0.8 -0.02" }
            }, centerPanel);

            container.Add(new CuiButton
            {
                Button = { Command = "mbkits_inv.back", Color = "0.2 0.5 0.8 0" },
                RectTransform = { AnchorMin = "0.865 0.04", AnchorMax = "0.947 0.12" },
                Text = { Text = Msg("MB_Back", player.UserIDString), FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, centerPanel);

            container.Add(new CuiLabel
            {
                Text = { Text = $"{kit.Name.ToUpper()}", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.9" },
                RectTransform = { AnchorMin = "0.411 0.8411", AnchorMax = "0.5345 0.897" }
            }, centerPanel);

            string textPrefix = Msg("CooldownPrefix", player.UserIDString);
            string textPrefix_2 = Msg("CooldownPrefix_2", player.UserIDString);
            string textH = Msg("TimeHours", player.UserIDString);
            string textM = Msg("TimeMinutes", player.UserIDString);
            string textS = Msg("TimeSeconds", player.UserIDString);

            string FormatCooldown(TimeSpan time)
            {
                return $"{(int)time.TotalHours}{textH} {(int)time.Minutes}{textM} {(int)time.Seconds}{textS}";
            }

            double cooldownUntil = 0;
            bool isOnCooldown = kitCooldowns.TryGetValue(player.userID, out var cooldowns) &&
                cooldowns.TryGetValue(kit.Name.ToLower(), out cooldownUntil) &&
                cooldownUntil > Facepunch.Math.Epoch.Current;

            string cooldownText;

            if (isOnCooldown)
            {
                double remaining = cooldownUntil - Facepunch.Math.Epoch.Current;

                if (remaining < 0) remaining = 0;
                TimeSpan remainingTime = TimeSpan.FromSeconds(remaining);
                cooldownText = $"{textPrefix}: {FormatCooldown(remainingTime)}";
            }
            else
            {
                TimeSpan fullTime = TimeSpan.FromSeconds(GetCooldownSeconds(kit.CooldownText));
                cooldownText = $"{textPrefix_2}: {FormatCooldown(fullTime)}";

            }

            container.Add(new CuiLabel
            {
                Text = { Text = cooldownText, FontSize = 13, Align = TextAnchor.MiddleLeft, Color = isOnCooldown ? "1 0.4 0.4 1" : "1 1 1 0.7" },
                RectTransform = { AnchorMin = "0.547 0.8411", AnchorMax = "0.792 0.897" }
            }, centerPanel);

            var main = kit.Items.Where(i => i.Container == "main").ToList();
            for (int i = 0; i < 24; i++)
            {
                int row = i / 6;
                int col = i % 6;

                float xMin = 0.412f + col * 0.0645f;
                float yMax = 0.827f - row * 0.1075f;
                float xMax = xMin + 0.0585f;
                float yMin = yMax - 0.0955f;

                AddItemSlot(container, centerPanel, $"Main_{i}", xMin, yMin, xMax, yMax, i < main.Count ? main[i] : null);
            }

            var wear = kit.Items.Where(i => i.Container == "wear").ToList();
            for (int i = 0; i < 7; i++)
            {
                float xMin = 0.38f + i * 0.0645f;
                float yMin = 0.298f;
                float xMax = xMin + 0.0585f;
                float yMax = yMin + 0.0955f;

                AddItemSlot(container, centerPanel, $"Wear_{i}", xMin, yMin, xMax, yMax, i < wear.Count ? wear[i] : null);
            }

            var belt = kit.Items.Where(i => i.Container == "belt").ToList();
            for (int i = 0; i < 6; i++)
            {
                float xMin = 0.412f + i * 0.0645f;
                float yMin = 0.187f;
                float xMax = xMin + 0.0585f;
                float yMax = yMin + 0.0955f;

                AddItemSlot(container, centerPanel, $"Belt_{i}", xMin, yMin, xMax, yMax, i < belt.Count ? belt[i] : null);
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region ErrorMessage

        private void AddErrorMessage(BasePlayer player, string text, string imageKey)
        {
            ulong id = player.userID;

            if (!playerErrors.ContainsKey(id))
                playerErrors[id] = new List<ChatErrorEntry>();

            var entry = new ChatErrorEntry
            {
                Text = text,
                ImageKey = imageKey,
                Timestamp = DateTime.UtcNow
            };

            playerErrors[id].Add(entry);

            if (playerErrors[id].Count > 5)
                playerErrors[id].RemoveAt(0);

            RedrawErrorUI(player);

            timer.Once(3f, () =>
            {
                if (playerErrors.TryGetValue(id, out var list) && list.Contains(entry))
                {
                    list.Remove(entry);
                    RedrawErrorUI(player);
                }
            });
        }

        private void RedrawErrorUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "MB_ErrorChatUI");

            if (!playerErrors.TryGetValue(player.userID, out var messages) || messages.Count == 0)
                return;

            var container = new CuiElementContainer();

            string parent = container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "5 96", OffsetMax = "200 500" }
            }, "OverlayNonScaled", "MB_ErrorChatUI");

            float blockHeight = 50f;
            float spacing = 5f;
            float yStart = 0f;

            for (int i = 0; i < messages.Count; i++)
            {
                var error = messages[messages.Count - 1 - i];
                float offsetY = yStart + i * (blockHeight + spacing);

                string panel = container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = $"0 {offsetY}", OffsetMax = $"0 {offsetY + blockHeight}" }
                }, parent);

                container.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = LocalImageLoader.Get(error.ImageKey),
                            Color = "1 1 1 1"
                        },
                        new CuiRectTransformComponent
                        { AnchorMin = "0 0", AnchorMax = "1 1", }
                    }
                });

                string titleKey = error.ImageKey switch
                {
                    "success_icon_kits" => "SuccessTitle",
                    "error_icon_invalid_kits" => "ErrorTitle",
                    "error_icon_expired" => "ErrorTitle",
                    _ => "ErrorTitle"
                };

                string title = Msg(titleKey, player.UserIDString);

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.31 0.56", AnchorMax = "1 0.9" },
                    Text = { Text = title, Align = TextAnchor.MiddleLeft, FontSize = 12, Color = "1 1 1 0.9" }
                }, panel);

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.31 0", AnchorMax = "0.98 0.54" },
                    Text = { Text = error.Text, Align = TextAnchor.UpperLeft, FontSize = 9, Color = "1 1 1 0.8" }
                }, panel);
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Command

        [ConsoleCommand("mbkits.viewkit")]
        private void CmdViewKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !arg.HasArgs()) return;

            string kitName = arg.Args[0].ToLower();
            if (!kits.TryGetValue(kitName, out var kit)) return;

            ShowKitsInvUI(player, kit);
        }

        private float GetBalanceByEconomy(ulong userId, int economyType)
        {
            switch (economyType)
            {
                case 1:
                    return MBCoins != null ? Convert.ToSingle(MBCoins.Call("API_GET_BALANCE", userId)) : 0f;

                case 2:
                {
                    if (Economics == null) return 0f;
                    var balObj = Economics.Call("Balance", userId);
                    if (balObj is double d) return (float)d;
                    if (balObj is int i) return i;
                    if (balObj is float f) return f;
                    return 0f;
                }

                case 3:
                {
                    if (ServerRewards == null) return 0f;
                    var pointsObj = ServerRewards.Call("CheckPoints", userId);
                    if (pointsObj is int pi) return pi;
                    if (pointsObj is double pd) return (float)pd;
                    if (pointsObj is float pf) return pf;
                    return 0f;
                }

                default:
                    return 0f;
            }
        }

        private bool DeductBalanceByEconomy(ulong userId, int economyType, float amount)
        {
            if (amount <= 0f) return false;

            switch (economyType)
            {
                case 1:
                    if (MBCoins == null) return false;
                    MBCoins.Call("API_PUT_BALANCE_MINUS", userId, amount);
                    return true;

                case 2:
                    if (Economics == null) return false;
                    Economics.Call("Withdraw", userId, (double)amount);
                    return true;

                case 3:
                    if (ServerRewards == null) return false;
                    ServerRewards.Call("TakePoints", userId, (int)Mathf.CeilToInt(amount));
                    return true;

                default:
                    return false;
            }
        }


        [ConsoleCommand("mbkits.buykit")]
        private void CmdBuyKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !arg.HasArgs()) return;

            string kitName = arg.Args[0].ToLower();
            if (!kits.TryGetValue(kitName, out var kit))
                return;

            if (kit.EconomyType == 0 || kit.Price <= 0f)
                return;

            if (!string.IsNullOrEmpty(kit.Permission) && !permission.UserHasPermission(player.UserIDString, kit.Permission))
                return;

            double now = Facepunch.Math.Epoch.Current;
            if (kitCooldowns.TryGetValue(player.userID, out var cooldowns) &&
                cooldowns.TryGetValue(kitName, out var cooldownUntil) &&
                cooldownUntil > now)
            {
                double remaining = cooldownUntil - now;
                var ts = TimeSpan.FromSeconds(Math.Max(0, remaining));
                AddErrorMessage(player,
                    string.Format(Msg("CooldownPrefix", player.UserIDString) + ": {0:D2}м {1:D2}с", ts.Minutes, ts.Seconds),
                    "error_icon_expired");
                return;
            }

            if (!CanFitAllItems(player, kit))
            {
                AddErrorMessage(player, Msg("KitInventoryFull", player.UserIDString), "error_icon_invalid_kits");
                return;
            }

            if (kit.MaxUses > 0)
            {
                if (!kitUses.TryGetValue(player.userID, out var uses))
                {
                    uses = new Dictionary<string, int>();
                    kitUses[player.userID] = uses;
                }

                uses.TryGetValue(kitName, out var count);
                if (count >= kit.MaxUses)
                {
                    AddErrorMessage(player, Msg("KitMaxUsesReached", player.UserIDString), "error_icon_invalid_kits");
                    return;
                }
            }

            float bal = GetBalanceByEconomy(player.userID, kit.EconomyType);

            if (bal < kit.Price)
            {
                AddErrorMessage(player, Msg("KitNotEnoughBalance", player.UserIDString), "error_icon_invalid_kits");
                return;
            }

            if (!DeductBalanceByEconomy(player.userID, kit.EconomyType, kit.Price))
            {
                AddErrorMessage(player, Msg("PurchaseUnavailable", player.UserIDString), "error_icon_invalid_kits");
                return;
            }

            if (kit.MaxUses > 0)
            {
                kitUses[player.userID][kitName] = kitUses[player.userID].TryGetValue(kitName, out var c) ? c + 1 : 1;
                SaveUses();
            }

            foreach (var entry in kit.Items)
            {
                if (!string.IsNullOrEmpty(entry.CustomCommand))
                {
                    string cmd = entry.CustomCommand
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{name}", player.displayName);
                    Server.Command(cmd);
                }
            }

            GiveKitToPlayer(player, kit);
            AddErrorMessage(player, Msg("KitSuccess", player.UserIDString), "success_icon_kits");

            if (!kitCooldowns.TryGetValue(player.userID, out cooldowns))
            {
                cooldowns = new Dictionary<string, double>();
                kitCooldowns[player.userID] = cooldowns;
            }

            cooldowns[kitName] = now + GetCooldownSeconds(kit.CooldownText);
            SaveCooldowns();

            UpdateKitButton(player, kitName);
        }



        [ConsoleCommand("mbkits.givekit")]
        private void CmdGiveKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !arg.HasArgs()) return;

            string kitName = arg.Args[0].ToLower();
            if (!kits.TryGetValue(kitName, out var kit))
                return;

            if (!string.IsNullOrEmpty(kit.Permission) && !permission.UserHasPermission(player.UserIDString, kit.Permission))
                return;

            double now = Facepunch.Math.Epoch.Current;
            double cooldownUntil = 0;
            if (kitCooldowns.TryGetValue(player.userID, out var cooldowns))
            {
                cooldowns.TryGetValue(kitName, out cooldownUntil);
            }

            if (cooldownUntil > now)
            {
                double remaining = cooldownUntil - now;
                TimeSpan remainingTime = TimeSpan.FromSeconds(remaining);
                AddErrorMessage(player,
                    string.Format(Msg("CooldownPrefix", player.UserIDString) + ": {0:D2}м {1:D2}с",
                    remainingTime.Minutes, remainingTime.Seconds),
                    "error_icon_expired");
                return;
            }

            if (!CanFitAllItems(player, kit))
            {
                AddErrorMessage(player, Msg("KitInventoryFull", player.UserIDString), "error_icon_invalid_kits");
                return;
            }

            if (kit.MaxUses > 0)
            {
                if (!kitUses.TryGetValue(player.userID, out var uses))
                {
                    uses = new Dictionary<string, int>();
                    kitUses[player.userID] = uses;
                }

                if (uses.TryGetValue(kitName, out var count) && count >= kit.MaxUses)
                {
                    AddErrorMessage(player, Msg("KitMaxUsesReached", player.UserIDString), "error_icon_invalid_kits");
                    return;
                }

                uses[kitName] = count + 1;
                SaveUses();
            }

            foreach (var entry in kit.Items)
            {
                if (!string.IsNullOrEmpty(entry.CustomCommand))
                {
                    string cmd = entry.CustomCommand
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{name}", player.displayName);
                    Server.Command(cmd);
                }
            }

            GiveKitToPlayer(player, kit);

            AddErrorMessage(player, Msg("KitSuccess", player.UserIDString), "success_icon_kits");

            EffectNetwork.Send(new Effect("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player, 0, Vector3.up, Vector3.zero)
            {
                scale = UnityEngine.Random.Range(0f, 1f)
            });

            if (!kitCooldowns.TryGetValue(player.userID, out cooldowns))
            {
                cooldowns = new Dictionary<string, double>();
                kitCooldowns[player.userID] = cooldowns;
            }

            double cooldownSec = GetCooldownSeconds(kit.CooldownText);
            cooldowns[kitName] = now + cooldownSec;
            SaveCooldowns();

            UpdateKitButton(player, kitName);
        }

        private void UpdateKitButton(BasePlayer player, string kitName)
        {
            var kit = kits[kitName];
            string containerName = kitContainerNames[kitName.ToLower()];

            CuiHelper.DestroyUi(player, $"KitButton_{kitName}");

            var container = new CuiElementContainer();

            double now = Facepunch.Math.Epoch.Current;
            double cooldownUntil = 0;
            bool isOnCooldown = kitCooldowns.TryGetValue(player.userID, out var cooldowns) &&
                cooldowns.TryGetValue(kitName.ToLower(), out cooldownUntil) &&
                cooldownUntil > now;

            if (isOnCooldown)
            {
                double remaining = cooldownUntil - now;
                if (remaining < 1) remaining = 1;

                TimeSpan ts = TimeSpan.FromSeconds(remaining);
                string cooldownText = FormatShortTime(ts, player);

                container.Add(new CuiButton
                {
                    Button = { Color = "0.8 0.6 0.1 0" },
                    RectTransform = { AnchorMin = "0.05 0.04", AnchorMax = "0.72 0.18" },
                    Text = { Text = cooldownText, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 0.4 0.4 1" }
                }, containerName, $"KitButton_{kitName}");
            }

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("mbkits_inv.back")]
        private void CmdKitsBackInv(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;
            if (player == null) return;

            CuiHelper.DestroyUi(player, "MBKits_Inv_UI");
            ShowKitsUI(player);
        }

        [ChatCommand("kits")]
        private void CmdOpenKits(BasePlayer player)
        {
            ShowKitsUI(player);
        }

        [ChatCommand("dkit")]
        private void CmdKit(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "mbkits.admin"))
            {
                player.ChatMessage(Msg("NoAdminPermission", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Usage: /dkit add <name> | /dkit remove <name>");
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                {
                    if (args.Length < 2)
                    {
                        player.ChatMessage(Msg("KitAddUsage", player.UserIDString));
                        return;
                    }

                    string kitName = args[1].ToLower();
                    string permName = args.Length >= 3 ? args[2].ToLower() : $"mbkits.{kitName}";

                    if (!permission.PermissionExists(permName))
                        permission.RegisterPermission(permName, this);

                    var kit = new KitData
                    {
                        Name = kitName,
                        Permission = permName,
                        CooldownText = "1h"
                    };

                    foreach (var item in player.inventory.containerMain.itemList)
                        if (item != null) kit.Items.Add(CreateItemData(item, "main"));

                    foreach (var item in player.inventory.containerBelt.itemList)
                        if (item != null) kit.Items.Add(CreateItemData(item, "belt"));

                    foreach (var item in player.inventory.containerWear.itemList)
                        if (item != null) kit.Items.Add(CreateItemData(item, "wear"));

                    kits[kitName] = kit;
                    SaveKits();
                    player.ChatMessage(string.Format(Msg("KitAdded", player.UserIDString), kitName, permName));
                    break;
                }

                case "remove":
                {
                    if (args.Length < 2)
                    {
                        player.ChatMessage(Msg("KitRemoveUsage", player.UserIDString));
                        return;
                    }

                    string kitName = args[1].ToLower();

                    if (!kits.Remove(kitName))
                    {
                        player.ChatMessage(string.Format(Msg("KitNotFound", player.UserIDString), kitName));
                        return;
                    }

                    int count = 0;
                    foreach (var entry in kitCooldowns)
                    {
                        if (entry.Value.Remove(kitName))
                            count++;
                    }

                    SaveKits();
                    SaveCooldowns();

                    player.ChatMessage(string.Format(Msg("KitRemoved", player.UserIDString), kitName, count));
                    break;
                }

                default:
                {
                    player.ChatMessage("Usage: /dkit add <name> | /dkit remove <name>");
                    break;
                }
            }
        }

        [ChatCommand("akit")]
        private void CmdAutoKit(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "mbkits.admin"))
            {
                player.ChatMessage(Msg("AutoKit_NoPermission", player.UserIDString));
                return;
            }

            if (args.Length < 2)
            {
                player.ChatMessage(Msg("AutoKit_Usage_All", player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                {
                    string kitName = args[1].ToLower();
                    string perm = $"mbkits.autokit.{kitName}";

                    if (!permission.PermissionExists(perm))
                        permission.RegisterPermission(perm, this);

                    var kit = new KitData
                    {
                        Name = kitName,
                        CooldownText = "0s",
                        Permission = perm,
                        HideIfNoPermission = true
                    };

                    foreach (var item in player.inventory.containerMain.itemList)
                        if (item != null) kit.Items.Add(CreateItemData(item, "main"));

                    foreach (var item in player.inventory.containerBelt.itemList)
                        if (item != null) kit.Items.Add(CreateItemData(item, "belt"));

                    foreach (var item in player.inventory.containerWear.itemList)
                        if (item != null) kit.Items.Add(CreateItemData(item, "wear"));

                    autoKits[kitName] = kit;
                    SaveAutoKits();

                    player.ChatMessage(string.Format(Msg("AutoKit_Created", player.UserIDString), kitName, perm));
                    break;
                }

                case "remove":
                {
                    string kitName = args[1].ToLower();

                    if (!autoKits.ContainsKey(kitName))
                    {
                        player.ChatMessage(string.Format(Msg("AutoKit_NotFound", player.UserIDString), kitName));
                        return;
                    }

                    autoKits.Remove(kitName);
                    SaveAutoKits();

                    player.ChatMessage(string.Format(Msg("AutoKit_Removed", player.UserIDString), kitName));
                    break;
                }

            }
        }

        #endregion

        #region Give

        private ItemData CreateItemData(Item item, string container)
        {
            var data = new ItemData
            {
                ShortName = item.info.shortname,
                Amount = item.amount,
                SkinID = item.skin,
                Container = container,
                Slot = item.position,
                Contents = new List<ItemData>(),
                AmmoAmount = 0,
                AmmoShortName = null
            };

            var projectile = item.GetHeldEntity() as BaseProjectile;
            if (projectile != null && projectile.primaryMagazine != null && projectile.primaryMagazine.ammoType != null)
            {
                data.AmmoAmount = projectile.primaryMagazine.contents;
                data.AmmoShortName = projectile.primaryMagazine.ammoType.shortname;
            }

            if (item.contents != null)
            {
                foreach (var sub in item.contents.itemList)
                {
                    if (sub == null) continue;

                    var subData = new ItemData
                    {
                        ShortName = sub.info.shortname,
                        Amount = sub.amount,
                        SkinID = sub.skin,
                        Container = "contents",
                        Slot = sub.position,
                        AmmoAmount = 0,
                        AmmoShortName = null
                    };

                    var subProjectile = sub.GetHeldEntity() as BaseProjectile;
                    if (subProjectile != null && subProjectile.primaryMagazine != null && subProjectile.primaryMagazine.ammoType != null)
                    {
                        subData.AmmoAmount = subProjectile.primaryMagazine.contents;
                        subData.AmmoShortName = subProjectile.primaryMagazine.ammoType.shortname;
                    }

                    data.Contents.Add(subData);
                }
            }

            return data;
        }

        private string FormatShortTime(TimeSpan time, BasePlayer player)
        {
            string result = "";
            if (time.Days > 0) result += $"{time.Days}{Msg("TimeDays", player.UserIDString)} ";
            if (time.Hours > 0) result += $"{time.Hours}{Msg("TimeHours", player.UserIDString)} ";
            if (time.Minutes > 0) result += $"{time.Minutes}{Msg("TimeMinutes", player.UserIDString)} ";
            if (time.Seconds > 0) result += $"{time.Seconds}{Msg("TimeSeconds", player.UserIDString)}";
            return result.Trim();
        }

        private bool CanFitAllItems(BasePlayer player, KitData kit)
        {
            int totalAvailable = player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count
                            + player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count
                            + player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count;

            int totalItems = kit.Items.Count;

            return totalAvailable >= totalItems;
        }

        private void GiveKitToPlayer(BasePlayer player, KitData kit)
        {
            foreach (var entry in kit.Items)
            {
                var definition = ItemManager.FindItemDefinition(entry.ShortName);
                if (definition == null) continue;

                var item = ItemManager.Create(definition, entry.Amount, entry.SkinID);
                if (item == null) continue;

                if (entry.Contents != null && entry.Contents.Count > 0 && item.contents != null)
                {
                    foreach (var sub in entry.Contents)
                    {
                        var subDef = ItemManager.FindItemDefinition(sub.ShortName);
                        if (subDef == null) continue;

                        var mod = ItemManager.Create(subDef, sub.Amount, sub.SkinID);
                        if (mod == null) continue;

                        if (mod.contents != null && sub.AmmoAmount > 0)
                        {
                            var ammoInsideDef = ItemManager.FindItemDefinition("ammo.pistol");
                            if (ammoInsideDef != null)
                            {
                                var ammoInside = ItemManager.Create(ammoInsideDef, sub.AmmoAmount);
                                ammoInside?.MoveToContainer(mod.contents);
                            }
                        }

                        mod.MoveToContainer(item.contents, sub.Slot);
                    }
                }

                var projectile = item.GetHeldEntity() as BaseProjectile;
                if (projectile != null && projectile.primaryMagazine != null)
                {
                    if (!string.IsNullOrEmpty(entry.AmmoShortName) && entry.AmmoAmount > 0)
                    {
                        var ammoDef = ItemManager.FindItemDefinition(entry.AmmoShortName);
                        if (ammoDef != null)
                        {
                            projectile.primaryMagazine.ammoType = ammoDef;
                            projectile.primaryMagazine.contents =
                                Mathf.Clamp(entry.AmmoAmount, 0, projectile.primaryMagazine.capacity);

                            projectile.SendNetworkUpdate();
                        }
                    }
                }

                ItemContainer primary = entry.Container switch
                {
                    "belt" => player.inventory.containerBelt,
                    "wear" => player.inventory.containerWear,
                    _ => player.inventory.containerMain
                };

                ItemContainer[] fallback =
                {
                    player.inventory.containerMain,
                    player.inventory.containerBelt,
                    player.inventory.containerWear
                };

                if (item.MoveToContainer(primary, entry.Slot))
                    continue;

                bool moved = false;

                for (int i = 0; i < primary.capacity; i++)
                {
                    if (primary.GetSlot(i) == null)
                    {
                        if (item.MoveToContainer(primary, i))
                        {
                            moved = true;
                            break;
                        }
                    }
                }

                if (!moved)
                {
                    foreach (var alt in fallback)
                    {
                        if (alt == primary) continue;

                        for (int i = 0; i < alt.capacity; i++)
                        {
                            if (alt.GetSlot(i) == null)
                            {
                                if (item.MoveToContainer(alt, i))
                                {
                                    moved = true;
                                    break;
                                }
                            }
                        }

                        if (moved)
                            break;
                    }
                }
            }
        }

        private void AddItemSlot(CuiElementContainer container, string parent, string name, float xMin, float yMin, float xMax, float yMax, ItemData item)
        {
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}" },
                Image = { Color = "0 0 0 0" }
            }, parent, name);

            if (item == null) 
                return;

            if (!string.IsNullOrEmpty(item.CustomImage))
            {
                string imgId = LocalImageLoader.Get(item.CustomImage);

                if (!string.IsNullOrEmpty(imgId))
                {
                    container.Add(new CuiElement
                    {
                        Parent = name,
                        Components =
                        {
                            new CuiRawImageComponent { Png = imgId },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "4 4", OffsetMax = "-4 -4" }
                        }
                    });
                }

                container.Add(new CuiLabel
                {
                    Text = { Text = $"x{item.Amount}", FontSize = 10, Align = TextAnchor.LowerRight, Color = "1 1 1 0.7" },
                    RectTransform = { AnchorMin = "0 0.05", AnchorMax = "0.9 1" }
                }, name);

                return;
            }

            int itemId = ItemManager.FindItemDefinition(item.ShortName)?.itemid ?? 0;
            ulong skinId = item.SkinID;

            container.Add(new CuiElement
            {
                Parent = name,
                Components =
                {
                    new CuiImageComponent { ItemId = itemId, SkinId = skinId },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "4 4", OffsetMax = "-4 -4" }
                }
            });

            container.Add(new CuiLabel
            {
                Text = {
                    Text = $"x{item.Amount}",
                    FontSize = 10,
                    Align = TextAnchor.LowerRight,
                    Color = "1 1 1 0.7"
                },
                RectTransform = { AnchorMin = "0 0.05", AnchorMax = "0.9 1" }
            }, name);
        }

        private void GiveAutoRespawnKit(BasePlayer player, KitData kit)
        {
            if (!string.IsNullOrEmpty(kit.Permission) &&
                !permission.UserHasPermission(player.UserIDString, kit.Permission))
            {
                return;
            }

            RemoveItemById(player, 963906841);
            RemoveItemById(player, 795236088);

            if (!CanFitAllItems(player, kit))
                return;

            GiveKitToPlayer(player, kit);

            foreach (var entry in kit.Items)
            {
                if (!string.IsNullOrEmpty(entry.CustomCommand))
                {
                    string cmd = entry.CustomCommand
                        .Replace("{playerid}", player.UserIDString)
                        .Replace("{name}", player.displayName);

                    Server.Command(cmd);
                }
            }
        }

        private void RemoveItemById(BasePlayer player, int itemId)
        {
            foreach (var container in new[] 
            { 
                player.inventory.containerMain, 
                player.inventory.containerBelt, 
                player.inventory.containerWear 
            })
            {
                if (container == null) continue;

                for (int i = container.itemList.Count - 1; i >= 0; i--)
                {
                    var item = container.itemList[i];
                    if (item.info.itemid == itemId)
                    {
                        item.Remove();
                    }
                }
            }
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MB_Close_Kits"] = "Click on empty space to close",
                ["MB_Kits_Player"] = "KITS",
                ["MB_Kits_Player_2"] = "<color=#FFD700>Being the best</color> is easy with KITS!",
                ["MB_Back"] = "Back",
                ["MB_Buy"] = "Buy",
                ["MB_Claim"] = "Claim",
                ["MB_Unavailable"] = "Unavailable",
                ["MB_NoAccess"] = "Access denied",
                ["CooldownPrefix"] = "Cooldown",
                ["CooldownPrefix_2"] = "Kit can be claimed once every",
                ["TimeHours"] = "h",
                ["TimeMinutes"] = "m",
                ["TimeSeconds"] = "s",
                ["TimeDays"] = "d",
                ["KitSuccess"] = "Kit successfully claimed!",
                ["KitInventoryFull"] = "Inventory is full. Please clear space for the kit.",
                ["ErrorTitle"] = "Error",
                ["SuccessTitle"] = "Success",
                ["NoAdminPermission"] = "You do not have permission to manage kits.",
                ["KitAddUsage"] = "Usage: /kit add <name>",
                ["KitRemoveUsage"] = "Usage: /kit remove <name>",
                ["KitAdded"] = "Kit '{0}' saved with permission: '{1}'.",
                ["KitRemoved"] = "Kit '{0}' removed. Cooldown cleared for {1} players.",
                ["KitNotFound"] = "Kit '{0}' not found.",
                ["KitMaxUsesReached"] = "Max uses reached for this kit.",
                ["AutoKit_NoPermission"] = "You do not have permission to manage autokits.",
                ["AutoKit_Usage_All"] = "Usage: /akit add <name> | /akit remove <name>",
                ["AutoKit_Created"] = "Hidden autokit '{0}' created. Permission: {1}",
                ["AutoKit_NotFound"] = "Autokit '{0}' not found.",
                ["AutoKit_Removed"] = "Autokit '{0}' removed.",

                ["KitNotEnoughBalance"] = "Not enough currency to purchase this kit.",
                ["PurchaseUnavailable"] = "Purchase unavailable (MBCoins not found).",




            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MB_Close_Kits"] = "Натисніть на порожнє місце, щоб закрити",
                ["MB_Kits_Player"] = "НАБОРИ",
                ["MB_Kits_Player_2"] = "<color=#FFD700>Бути найкращим</color> — просто з цими наборами!",
                ["MB_Back"] = "Назад",
                ["MB_Buy"] = "Купити",
                ["MB_Claim"] = "Отримати",
                ["MB_Unavailable"] = "Недоступно",
                ["MB_NoAccess"] = "Немає доступу",
                ["CooldownPrefix"] = "Відкат",
                ["CooldownPrefix_2"] = "Набір можна взяти 1 раз на",
                ["TimeHours"] = "г",
                ["TimeMinutes"] = "хв",
                ["TimeSeconds"] = "с",
                ["TimeDays"] = "дн",
                ["KitSuccess"] = "Набір успішно видано!",
                ["KitInventoryFull"] = "Інвентар заповнений. Звільніть місце для набору.",
                ["ErrorTitle"] = "Помилка",
                ["SuccessTitle"] = "Успішно",
                ["NoAdminPermission"] = "У тебе немає прав для керування наборами.",
                ["KitAddUsage"] = "Використай: /kit add <назва>",
                ["KitRemoveUsage"] = "Використай: /kit remove <назва>",
                ["KitAdded"] = "Набір '{0}' збережено з доступом: '{1}'.",
                ["KitRemoved"] = "Набір '{0}' видалено. Відкат скинуто у {1} гравців.",
                ["KitNotFound"] = "Набір '{0}' не знайдено.",
                ["KitMaxUsesReached"] = "Ви досягли максимальної кількості цього набору.",
                ["AutoKit_Usage"] = "Використання: /akit add <назва> [permission]",
                ["AutoKit_NoPermission"] = "У тебе немає прав для створення автокітів.",
                ["AutoKit_Usage_All"] = "Використання: /akit add <назва> | /akit remove <назва>",
                ["AutoKit_Created"] = "Прихований автокіт '{0}' створено. Permission: {1}",
                ["AutoKit_NotFound"] = "Автокіт '{0}' не знайдено.",
                ["AutoKit_Removed"] = "Автокіт '{0}' видалено.",

                ["KitNotEnoughBalance"] = "Недостатньо валюти для покупки набору.",
                ["PurchaseUnavailable"] = "Покупка недоступна (MBCoins не знайдено).",




            }, this, "uk");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MB_Close_Kits"] = "Нажмите на пустое место, чтобы закрыть",
                ["MB_Kits_Player"] = "НАБОРЫ",
                ["MB_Kits_Player_2"] = "<color=#FFD700>Быть лучшим</color> — просто с этими наборами!",
                ["MB_Back"] = "Назад",
                ["MB_Buy"] = "Купить",
                ["MB_Claim"] = "ПОЛУЧИТЬ",
                ["MB_Unavailable"] = "НЕДОСТУПНО",
                ["MB_NoAccess"] = "Нет доступа",
                ["CooldownPrefix"] = "Откат",
                ["CooldownPrefix_2"] = "Набор можно взять 1 раз в",
                ["TimeHours"] = "ч",
                ["TimeMinutes"] = "м",
                ["TimeSeconds"] = "с",
                ["TimeDays"] = "д",
                ["KitSuccess"] = "Набор успешно выдан!",
                ["KitInventoryFull"] = "Инвентарь заполнен. Освободите место для набора.",
                ["ErrorTitle"] = "Ошибка",
                ["SuccessTitle"] = "Успех",
                ["NoAdminPermission"] = "У тебя нет прав для управления наборами.",
                ["KitAddUsage"] = "Используй: /kit add <название>",
                ["KitRemoveUsage"] = "Используй: /kit remove <название>",
                ["KitAdded"] = "Набор '{0}' сохранён с правом доступа: '{1}'.",
                ["KitRemoved"] = "Набор '{0}' удалён. Кулдаун сброшен у {1} игроков.",
                ["KitNotFound"] = "Набор '{0}' не найден.",
                ["KitMaxUsesReached"] = "Вы достигли максимального количества этого набора.",
                ["AutoKit_NoPermission"] = "У тебя нет прав для создания автокитов.",
                ["AutoKit_Usage_All"] = "Использование: /akit add <название> | /akit remove <название>",
                ["AutoKit_Created"] = "Скрытый автокит '{0}' создан. Permission: {1}",
                ["AutoKit_NotFound"] = "Автокит '{0}' не найден.",
                ["AutoKit_Removed"] = "Автокит '{0}' удалён.",

                ["KitNotEnoughBalance"] = "Недостаточно валюты для покупки набора.",
                ["PurchaseUnavailable"] = "Покупка недоступна (MBCoins не найден).",




            }, this, "ru");

        }
        private string Msg(string key, string userid = null, params object[] args)
        {
            var message = lang.GetMessage(key, this, userid);
            return args != null && args.Length > 0 ? string.Format(message, args) : message;
        }
        
        #endregion
    }
}
   