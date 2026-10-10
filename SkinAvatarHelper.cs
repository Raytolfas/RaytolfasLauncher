using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace RaytolfasLauncher
{
    public static class SkinAvatarHelper
    {
        private static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private static readonly ConcurrentDictionary<string, Bitmap> memoryCache = new ConcurrentDictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        static SkinAvatarHelper()
        {
            client.DefaultRequestHeaders.Add("User-Agent", "RaytolfasLauncher/0.0.5");
        }

        public static async Task<Bitmap?> GetAvatarAsync(
            string username,
            string? cacheFolder = null,
            string? accountType = null,
            string? uuid = null,
            bool enableElyBy = false)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            string key = username.Trim();
            foreach (var p in new[] { "🔑", "🦋", "👤" })
            {
                if (key.StartsWith(p))
                    key = key.Substring(p.Length);
            }
            key = key.Trim();
            if (string.IsNullOrWhiteSpace(key))
                return null;

            string type = (accountType ?? "offline").Trim().ToLowerInvariant();
            string cacheKey = $"{key}_{type}";

            if (memoryCache.TryGetValue(cacheKey, out var cached))
                return cached;

            string? localFile = null;
            if (!string.IsNullOrWhiteSpace(cacheFolder))
            {
                string avatarDir = Path.Combine(cacheFolder, "avatars");
                try { Directory.CreateDirectory(avatarDir); } catch { }
                localFile = Path.Combine(avatarDir, $"{key}_{type}.png");

                if (File.Exists(localFile))
                {
                    try
                    {
                        var fi = new FileInfo(localFile);
                        if (fi.Length != 549 && fi.Length > 120)
                        {
                            var bmp = new Bitmap(localFile);
                            memoryCache[cacheKey] = bmp;
                            return bmp;
                        }
                        else
                        {
                            try { File.Delete(localFile); } catch { }
                        }
                    }
                    catch { }
                }
            }

            bool shouldTryElyByFirst = type == "elyby" || (enableElyBy && type != "microsoft");
            if (shouldTryElyByFirst)
            {
                var bmp = await TryFetchElyBySkin(key, cacheKey, localFile);
                if (bmp != null)
                    return bmp;
            }

            var providers = new List<string>();
            providers.Add($"https://minotar.net/helm/{Uri.EscapeDataString(key)}/64");
            providers.Add($"https://mineskin.eu/helm/{Uri.EscapeDataString(key)}/64");
            if (!string.IsNullOrWhiteSpace(uuid))
            {
                string cleanUuid = uuid.Replace("-", "").Trim();
                providers.Add($"https://crafatar.com/avatars/{cleanUuid}?size=64&overlay=true");
                providers.Add($"https://minotar.net/helm/{cleanUuid}/64");
            }
            providers.Add($"https://mc-heads.net/avatar/{Uri.EscapeDataString(key)}/64");

            foreach (var url in providers)
            {
                try
                {
                    byte[] bytes = await client.GetByteArrayAsync(url);
                    if (bytes != null && bytes.Length > 120 && bytes.Length != 549)
                    {
                        return CacheAndReturn(bytes, cacheKey, localFile);
                    }
                }
                catch { }
            }

            if (!shouldTryElyByFirst && type != "microsoft")
            {
                var bmp = await TryFetchElyBySkin(key, cacheKey, localFile);
                if (bmp != null)
                    return bmp;
            }

            return null;
        }

        private static async Task<Bitmap?> TryFetchElyBySkin(string username, string cacheKey, string? localFile)
        {
            try
            {
                var elyUrl = $"http://skinsystem.ely.by/skins/{Uri.EscapeDataString(username)}.png";
                var elyBytes = await client.GetByteArrayAsync(elyUrl);
                if (elyBytes != null && elyBytes.Length > 200)
                {
                    var headBytes = ExtractHeadFromSkin(elyBytes);
                    if (headBytes != null && headBytes.Length > 0)
                    {
                        return CacheAndReturn(headBytes, cacheKey, localFile);
                    }
                }
            }
            catch { }
            return null;
        }

        private static Bitmap? CacheAndReturn(byte[] bytes, string key, string? localFile)
        {
            try
            {
                if (localFile != null)
                {
                    try { File.WriteAllBytes(localFile, bytes); } catch { }
                }

                using var ms = new MemoryStream(bytes);
                var bmp = new Bitmap(ms);
                memoryCache[key] = bmp;
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? ExtractHeadFromSkin(byte[] skinBytes)
        {
            try
            {
                using var original = SKBitmap.Decode(skinBytes);
                if (original == null || original.Width < 64 || original.Height < 32)
                    return null;

                using var headBm = new SKBitmap(64, 64);
                using (var canvas = new SKCanvas(headBm))
                {
                    canvas.Clear(SKColors.Transparent);
                    using var paint = new SKPaint { FilterQuality = SKFilterQuality.None };
                    var srcHead = new SKRectI(8, 8, 16, 16);
                    var dst = new SKRect(0, 0, 64, 64);
                    canvas.DrawBitmap(original, srcHead, dst, paint);

                    var srcHat = new SKRectI(40, 8, 48, 16);
                    canvas.DrawBitmap(original, srcHat, dst, paint);
                }

                using var img = SKImage.FromBitmap(headBm);
                using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                return data.ToArray();
            }
            catch
            {
                return null;
            }
        }
    }
}
