// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.
//
// Raytolfas Launcher is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RaytolfasLauncher
{
    public class ModrinthSearchResultItem
    {
        public string ProjectId { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ProjectType { get; set; } = string.Empty;
        public string? IconUrl { get; set; }
        public int Downloads { get; set; }
        public List<string> Categories { get; set; } = new List<string>();

        public string DisplayCategory => Categories != null && Categories.Count > 0 ? string.Join(", ", Categories) : ProjectType;
        public string DownloadsFormatted => Downloads >= 1000000 ? $"{Downloads / 1000000.0:0.#}M" : (Downloads >= 1000 ? $"{Downloads / 1000.0:0.#}K" : Downloads.ToString());
    }

    public static class ModrinthCatalogService
    {
        private static readonly HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        static ModrinthCatalogService()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "RaytolfasLauncher/0.1.0 (github.com/Raytolfas/RaytolfasLauncher)");
        }

        public static async Task<List<ModrinthSearchResultItem>> SearchAsync(string query = "", string projectType = "mod", string? gameVersion = null, int limit = 20)
        {
            try
            {
                var facets = new List<string>();
                if (!string.IsNullOrWhiteSpace(projectType) && projectType != "all")
                {
                    facets.Add($"[\"project_type:{projectType}\"]");
                }
                if (!string.IsNullOrWhiteSpace(gameVersion))
                {
                    facets.Add($"[\"versions:{gameVersion}\"]");
                }

                string facetsQuery = facets.Count > 0 ? $"&facets=[{string.Join(",", facets)}]" : "";
                string url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}&limit={limit}{facetsQuery}";

                var res = await httpClient.GetAsync(url);
                if (!res.IsSuccessStatusCode)
                    return new List<ModrinthSearchResultItem>();

                var json = await res.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<ModrinthSearchResponse>(json);
                if (data == null || data.Hits == null)
                    return new List<ModrinthSearchResultItem>();

                var results = new List<ModrinthSearchResultItem>();
                foreach (var hit in data.Hits)
                {
                    results.Add(new ModrinthSearchResultItem
                    {
                        ProjectId = hit.ProjectId,
                        Slug = hit.Slug,
                        Title = hit.Title,
                        Description = hit.Description,
                        ProjectType = hit.ProjectType,
                        IconUrl = hit.IconUrl,
                        Downloads = hit.Downloads,
                        Categories = hit.DisplayCategories
                    });
                }
                return results;
            }
            catch
            {
                return new List<ModrinthSearchResultItem>();
            }
        }

        public static async Task<bool> InstallProjectAsync(string projectId, string minecraftDir, string? gameVersion = null, Action<string>? statusCallback = null)
        {
            try
            {
                statusCallback?.Invoke("Поиск версий...");
                string versionsUrl = $"https://api.modrinth.com/v2/project/{projectId}/version";
                var res = await httpClient.GetAsync(versionsUrl);
                if (!res.IsSuccessStatusCode)
                    return false;

                var json = await res.Content.ReadAsStringAsync();
                var versions = JsonSerializer.Deserialize<List<ModrinthProjectVersion>>(json);
                if (versions == null || versions.Count == 0)
                    return false;

                ModrinthProjectVersion? selected = null;
                if (!string.IsNullOrWhiteSpace(gameVersion))
                {
                    selected = versions.Find(v => v.GameVersions != null && v.GameVersions.Contains(gameVersion));
                }
                if (selected == null)
                {
                    selected = versions[0];
                }

                if (selected.Files == null || selected.Files.Count == 0)
                    return false;

                var file = selected.Files.Find(f => f.Primary) ?? selected.Files[0];

                string targetSubfolder = "mods";
                string filenameLower = file.Filename.ToLowerInvariant();
                if (filenameLower.EndsWith(".zip") && !filenameLower.Contains("fabric") && !filenameLower.Contains("forge"))
                {
                    targetSubfolder = "resourcepacks";
                }

                string targetDir = Path.Combine(minecraftDir, targetSubfolder);
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                string destPath = Path.Combine(targetDir, file.Filename);
                statusCallback?.Invoke($"Скачивание {file.Filename}...");

                var fileBytes = await httpClient.GetByteArrayAsync(file.Url);
                await File.WriteAllBytesAsync(destPath, fileBytes);
                statusCallback?.Invoke("Установлено!");
                return true;
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"Ошибка: {ex.Message}");
                return false;
            }
        }
    }
}
