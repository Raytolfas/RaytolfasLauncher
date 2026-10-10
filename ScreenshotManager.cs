// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using System.ComponentModel;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace RaytolfasLauncher
{
    public class ScreenshotItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string DateFormatted => CreatedAt.ToString("dd.MM.yyyy HH:mm");
        public string FileSizeFormatted { get; set; } = string.Empty;

        private Bitmap? _thumbnail;
        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (_thumbnail != value)
                {
                    _thumbnail = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public static class ScreenshotManager
    {
        public static List<ScreenshotItem> GetScreenshots(string minecraftPath)
        {
            var list = new List<ScreenshotItem>();
            string dir = Path.Combine(minecraftPath, "screenshots");
            if (!Directory.Exists(dir))
                return list;

            try
            {
                var files = Directory.GetFiles(dir, "*.*")
                    .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(File.GetCreationTimeUtc);

                foreach (var file in files)
                {
                    var fi = new FileInfo(file);
                    list.Add(new ScreenshotItem
                    {
                        FilePath = file,
                        FileName = fi.Name,
                        CreatedAt = fi.CreationTime,
                        FileSizeFormatted = FormatBytes(fi.Length),
                        Thumbnail = null
                    });
                }
            }
            catch { }

            return list;
        }

        public static Bitmap? LoadThumbnail(string filePath, int decodeWidth = 320)
        {
            try
            {
                if (!File.Exists(filePath)) return null;
                using var stream = File.OpenRead(filePath);
                return Bitmap.DecodeToWidth(stream, decodeWidth);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<bool> CopyToClipboardAsync(TopLevel? topLevel, string filePath)
        {
            try
            {
                if (topLevel?.Clipboard == null || !File.Exists(filePath))
                    return false;

                var dataObject = new DataObject();
                dataObject.Set(DataFormats.Files, new[] { filePath });
                dataObject.Set(DataFormats.Text, filePath);
                await topLevel.Clipboard.SetDataObjectAsync(dataObject);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool DeleteScreenshot(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}
