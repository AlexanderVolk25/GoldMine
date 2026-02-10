using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("Image Downloader", "pluginfuel.ru", "2.0.0")]
    [Description("Universal image downloader and cache for all plugins")]
    public class ImageDownloader : RustPlugin
    {
        private const string CacheFolder = "ImageCache";
        private const string CacheMetaFile = "ImageCache/meta";

        private static Dictionary<string, string> ImageCache = new();
        private static Dictionary<string, string> UrlCache = new();
        private static ImageDownloader instance;

        private void Loaded()
        {
            instance = this;
        }

        private void OnServerInitialized()
        {
            Directory.CreateDirectory($"oxide/data/{CacheFolder}");
            
            UrlCache = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>(CacheMetaFile)
                ?? new Dictionary<string, string>();

            Puts("[ImageDownloader] v2.0.0 Initialized. Universal image cache ready.");
        }

        private void Unload()
        {
            instance = null;
        }

        /// <summary>
        /// Загружает изображение и возвращает FileStorage ID
        /// Универсальный метод для всех плагинов
        /// </summary>
        [HookMethod("GetImageID")]
        public string GetImageID(string imageKey, string imageUrl)
        {
            if (string.IsNullOrEmpty(imageKey) || string.IsNullOrEmpty(imageUrl))
                return null;

            // Если уже загружено, возвращаем ID
            if (ImageCache.TryGetValue(imageKey, out string cachedId))
                return cachedId;

            // Запускаем загрузку
            ServerMgr.Instance.StartCoroutine(LoadImage(imageKey, imageUrl));

            return null; // Вернет null пока загружается
        }

        /// <summary>
        /// Загружает несколько изображений
        /// </summary>
        [HookMethod("GetImageIDs")]
        public Dictionary<string, string> GetImageIDs(Dictionary<string, string> images)
        {
            Dictionary<string, string> result = new();

            foreach (var img in images)
            {
                string id = GetImageID(img.Key, img.Value);
                if (!string.IsNullOrEmpty(id))
                    result[img.Key] = id;
            }

            return result;
        }

        /// <summary>
        /// Проверяет, загружено ли изображение
        /// </summary>
        [HookMethod("HasImage")]
        public bool HasImage(string imageKey)
        {
            return ImageCache.ContainsKey(imageKey);
        }

        /// <summary>
        /// Получает FileStorage ID изображения
        /// </summary>
        [HookMethod("GetImage")]
        public string GetImage(string imageKey)
        {
            return ImageCache.TryGetValue(imageKey, out string id) ? id : null;
        }

        /// <summary>
        /// Загружает изображение из локальной папки
        /// </summary>
        [HookMethod("LoadLocalImage")]
        public string LoadLocalImage(string imageKey, string localPath)
        {
            if (!File.Exists(localPath))
            {
                PrintError($"[ImageDownloader] Local image not found: {localPath}");
                return null;
            }

            try
            {
                byte[] imageData = File.ReadAllBytes(localPath);
                string id = FileStorage.server.Store(
                    imageData,
                    FileStorage.Type.png,
                    CommunityEntity.ServerInstance.net.ID
                ).ToString();

                ImageCache[imageKey] = id;
                return id;
            }
            catch (Exception ex)
            {
                PrintError($"[ImageDownloader] Failed to load local image {imageKey}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Загружает изображение из папки данных
        /// </summary>
        [HookMethod("LoadImageFromData")]
        public string LoadImageFromData(string imageKey, string folderPath, string fileName)
        {
            string fullPath = $"oxide/data/{folderPath}/{fileName}";
            return LoadLocalImage(imageKey, fullPath);
        }

        private IEnumerator LoadImage(string imageKey, string imageUrl)
        {
            string filePath = $"oxide/data/{CacheFolder}/{imageKey}.png";

            // Проверяем локальный кэш
            if (File.Exists(filePath))
            {
                byte[] cachedData = File.ReadAllBytes(filePath);
                string id = FileStorage.server.Store(
                    cachedData,
                    FileStorage.Type.png,
                    CommunityEntity.ServerInstance.net.ID
                ).ToString();

                ImageCache[imageKey] = id;
                yield break;
            }

            // Проверяем, нужно ли скачивать
            bool needsDownload = true;

            if (UrlCache.TryGetValue(imageKey, out string oldUrl) &&
                oldUrl == imageUrl &&
                File.Exists(filePath))
                needsDownload = false;

            if (needsDownload)
            {
                UnityWebRequest www = UnityWebRequestTexture.GetTexture(imageUrl);
                www.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    PrintError($"[ImageDownloader] Failed to download {imageKey}: {www.error}");
                    yield break;
                }

                Texture2D tex = DownloadHandlerTexture.GetContent(www);
                if (tex != null)
                {
                    byte[] pngData = tex.EncodeToPNG();
                    File.WriteAllBytes(filePath, pngData);
                    UrlCache[imageKey] = imageUrl;
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                else
                {
                    PrintError($"[ImageDownloader] Failed to get texture for {imageKey}");
                    yield break;
                }
            }

            // Загружаем в FileStorage
            byte[] imageBytes = File.ReadAllBytes(filePath);
            string imageId = FileStorage.server.Store(
                imageBytes,
                FileStorage.Type.png,
                CommunityEntity.ServerInstance.net.ID
            ).ToString();

            ImageCache[imageKey] = imageId;

            // Сохраняем метаданные
            Interface.Oxide.DataFileSystem.WriteObject(CacheMetaFile, UrlCache);
        }

        /// <summary>
        /// Очищает кэш
        /// </summary>
        [ConsoleCommand("imagecache.clear")]
        private void ClearCacheCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin)
            {
                arg.ReplyWith("You don't have permission");
                return;
            }

            ImageCache.Clear();
            UrlCache.Clear();

            string folder = $"oxide/data/{CacheFolder}";
            if (Directory.Exists(folder))
            {
                foreach (string file in Directory.GetFiles(folder, "*.png"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                }
            }

            Interface.Oxide.DataFileSystem.WriteObject(CacheMetaFile, UrlCache);
            arg.ReplyWith("[ImageDownloader] Cache cleared");
        }

        /// <summary>
        /// Показывает статус кэша
        /// </summary>
        [ConsoleCommand("imagecache.status")]
        private void StatusCommand(ConsoleSystem.Arg arg)
        {
            arg.ReplyWith($"[ImageDownloader] Cached images: {ImageCache.Count}");
            foreach (var img in ImageCache)
            {
                arg.ReplyWith($"  - {img.Key}: {img.Value}");
            }
        }
    }
}
