// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace RaytolfasLauncher
{
    public static class ModrinthIconHelper
    {
        private static readonly HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        private static readonly ConcurrentDictionary<string, Bitmap> memoryCache = new(StringComparer.OrdinalIgnoreCase);

        static ModrinthIconHelper()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "RaytolfasLauncher/0.1.0 (github.com/Raytolfas/RaytolfasLauncher)");
        }

        public static async Task LoadIconAsync(ModrinthProjectHit hit, string baseDir)
        {
            if (string.IsNullOrWhiteSpace(hit.IconUrl))
                return;

            string url = hit.IconUrl.Trim();
            if (memoryCache.TryGetValue(url, out var cached))
            {
                await Dispatcher.UIThread.InvokeAsync(() => hit.IconBitmap = cached);
                return;
            }

            try
            {
                string cacheFolder = Path.Combine(baseDir, "cache", "icons");
                Directory.CreateDirectory(cacheFolder);

                string hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
                string diskPath = Path.Combine(cacheFolder, $"{hash}.png");

                if (File.Exists(diskPath))
                {
                    try
                    {
                        using var fs = File.OpenRead(diskPath);
                        var diskBmp = Bitmap.DecodeToWidth(fs, 96);
                        if (diskBmp != null)
                        {
                            memoryCache[url] = diskBmp;
                            await Dispatcher.UIThread.InvokeAsync(() => hit.IconBitmap = diskBmp);
                            return;
                        }
                    }
                    catch
                    {
                        try { File.Delete(diskPath); } catch { }
                    }
                }

                var bytes = await httpClient.GetByteArrayAsync(url);
                if (bytes != null && bytes.Length > 0)
                {
                    try { await File.WriteAllBytesAsync(diskPath, bytes); } catch { }
                    using var ms = new MemoryStream(bytes);
                    var bmp = Bitmap.DecodeToWidth(ms, 96);
                    if (bmp != null)
                    {
                        memoryCache[url] = bmp;
                        await Dispatcher.UIThread.InvokeAsync(() => hit.IconBitmap = bmp);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
