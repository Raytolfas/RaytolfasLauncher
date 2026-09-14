// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.ModLoaders.FabricMC;

namespace RaytolfasLauncher
{
    public class ModpackInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("gameVersion")]
        public string GameVersion { get; set; } = "1.20.1";

        [JsonPropertyName("loader")]
        public string Loader { get; set; } = "Fabric";

        [JsonPropertyName("loaderVersion")]
        public string LoaderVersion { get; set; } = "";

        [JsonPropertyName("created")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public string RootPath { get; set; } = "";

        [JsonIgnore]
        public string VersionPath { get; set; } = "";

        [JsonIgnore]
        public string ModsPath => Path.Combine(RootPath, "mods");

        [JsonIgnore]
        public string ResourcepacksPath => Path.Combine(RootPath, "resourcepacks");

        [JsonIgnore]
        public string ShaderpacksPath => Path.Combine(RootPath, "shaderpacks");

        [JsonIgnore]
        public int ModsCount
        {
            get
            {
                try
                {
                    return Directory.Exists(ModsPath)
                        ? Directory.GetFiles(ModsPath, "*.jar").Length
                        : 0;
                }
                catch { return 0; }
            }
        }

        [JsonIgnore]
        public int ResourcepacksCount
        {
            get
            {
                try
                {
                    return Directory.Exists(ResourcepacksPath)
                        ? Directory.GetFileSystemEntries(ResourcepacksPath).Length
                        : 0;
                }
                catch { return 0; }
            }
        }

        [JsonIgnore]
        public int ShaderpacksCount
        {
            get
            {
                try
                {
                    return Directory.Exists(ShaderpacksPath)
                        ? Directory.GetFileSystemEntries(ShaderpacksPath).Length
                        : 0;
                }
                catch { return 0; }
            }
        }

        [JsonIgnore]
        public string Subtitle => $"{GameVersion} • {Loader} ({string.Format(LocalizationManager.Get("modpacks.mods_count", CurrentDisplayLanguage), ModsCount)})";

        public static string? CurrentDisplayLanguage { get; set; }

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;
    }

    public class ModpackFileItem
    {
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string Category { get; set; } = "mods";
        public string FileSizeFormatted { get; set; } = "";
    }


    public static class ModpackManager
    {
        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "Modpack_" + DateTime.Now.Ticks : sanitized;
        }

        public static string GetModpacksFolder(string minecraftPath)
        {
            string path = Path.Combine(minecraftPath, "modpacks");
            Directory.CreateDirectory(path);
            return path;
        }

        public static List<ModpackInfo> GetModpacks(string minecraftPath)
        {
            var list = new List<ModpackInfo>();
            string modpacksFolder = GetModpacksFolder(minecraftPath);

            try
            {
                foreach (string dir in Directory.GetDirectories(modpacksFolder))
                {
                    string id = Path.GetFileName(dir);
                    string metaPath = Path.Combine(dir, "modpack.json");
                    ModpackInfo? info = null;

                    if (File.Exists(metaPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(metaPath);
                            info = JsonSerializer.Deserialize<ModpackInfo>(json, jsonOptions);
                        }
                        catch { }
                    }

                    if (info == null)
                    {
                        info = new ModpackInfo
                        {
                            Id = id,
                            Name = id,
                            GameVersion = "1.20.1",
                            Loader = "Fabric",
                            Created = Directory.GetCreationTimeUtc(dir)
                        };
                    }

                    info.Id = id;
                    info.RootPath = dir;
                    info.VersionPath = Path.Combine(minecraftPath, "versions", id);
                    list.Add(info);
                }
            }
            catch { }

            return list.OrderByDescending(p => p.Created).ToList();
        }

        public static async Task<ModpackInfo> CreateModpackAsync(
            string minecraftPath,
            string name,
            string gameVersion,
            string loader,
            string loaderVersion = "")
        {
            string id = SanitizeFolderName(name);
            string modpackDir = Path.Combine(GetModpacksFolder(minecraftPath), id);
            Directory.CreateDirectory(modpackDir);
            Directory.CreateDirectory(Path.Combine(modpackDir, "mods"));
            Directory.CreateDirectory(Path.Combine(modpackDir, "resourcepacks"));
            Directory.CreateDirectory(Path.Combine(modpackDir, "shaderpacks"));
            Directory.CreateDirectory(Path.Combine(modpackDir, "config"));

            var info = new ModpackInfo
            {
                Id = id,
                Name = name,
                GameVersion = gameVersion,
                Loader = loader,
                LoaderVersion = loaderVersion,
                Created = DateTime.UtcNow,
                RootPath = modpackDir,
                VersionPath = Path.Combine(minecraftPath, "versions", id)
            };

            string metaJson = JsonSerializer.Serialize(info, jsonOptions);
            await File.WriteAllTextAsync(Path.Combine(modpackDir, "modpack.json"), metaJson);

            await CreateVersionJsonAsync(minecraftPath, id, gameVersion, loader, loaderVersion);

            return info;
        }

        private static async Task CreateVersionJsonAsync(
            string minecraftPath,
            string id,
            string gameVersion,
            string loader,
            string loaderVersion)
        {
            string versionDir = Path.Combine(minecraftPath, "versions", id);
            Directory.CreateDirectory(versionDir);
            string versionJsonPath = Path.Combine(versionDir, $"{id}.json");

            string inheritsFrom = gameVersion;
            string mainClass = "net.minecraft.client.main.Main";

            if (loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase))
            {
                string versionsRoot = Path.Combine(minecraftPath, "versions");
                string prefix = "fabric-loader-";
                string suffix = $"-{gameVersion}";

                string? existingFabric = Directory.Exists(versionsRoot)
                    ? Directory.GetDirectories(versionsRoot)
                        .Select(Path.GetFileName)
                        .FirstOrDefault(d => d != null && d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && d.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    : null;

                if (!string.IsNullOrEmpty(existingFabric))
                {
                    inheritsFrom = existingFabric;
                    mainClass = "net.fabricmc.loader.impl.launch.knot.KnotClient";
                }
                else
                {
                    try
                    {
                        using var client = new HttpClient();
                        var fabric = new FabricInstaller(client);
                        var loaders = await fabric.GetLoaders(gameVersion);
                        var stable = loaders.FirstOrDefault(l => l.Stable) ?? loaders.FirstOrDefault();
                        if (stable != null && !string.IsNullOrEmpty(stable.Version))
                        {
                            inheritsFrom = await fabric.Install(gameVersion, stable.Version, new MinecraftPath(minecraftPath));
                            mainClass = "net.fabricmc.loader.impl.launch.knot.KnotClient";
                        }
                        else
                        {
                            inheritsFrom = gameVersion;
                        }
                    }
                    catch
                    {
                        inheritsFrom = gameVersion;
                    }
                }
            }
            else if (loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
            {
                string versionsRoot = Path.Combine(minecraftPath, "versions");
                string? existingForge = Directory.Exists(versionsRoot)
                    ? Directory.GetDirectories(versionsRoot)
                        .Select(Path.GetFileName)
                        .FirstOrDefault(d => d != null && d.Contains("forge", StringComparison.OrdinalIgnoreCase) && d.Contains(gameVersion, StringComparison.OrdinalIgnoreCase))
                    : null;

                inheritsFrom = existingForge ?? gameVersion;
            }
            else if (loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
            {
                string versionsRoot = Path.Combine(minecraftPath, "versions");
                string? existingNeo = Directory.Exists(versionsRoot)
                    ? Directory.GetDirectories(versionsRoot)
                        .Select(Path.GetFileName)
                        .FirstOrDefault(d => d != null && d.Contains("neoforge", StringComparison.OrdinalIgnoreCase) && d.Contains(gameVersion, StringComparison.OrdinalIgnoreCase))
                    : null;

                inheritsFrom = existingNeo ?? gameVersion;
            }
            else if (loader.Equals("Quilt", StringComparison.OrdinalIgnoreCase))
            {
                string versionsRoot = Path.Combine(minecraftPath, "versions");
                string? existingQuilt = Directory.Exists(versionsRoot)
                    ? Directory.GetDirectories(versionsRoot)
                        .Select(Path.GetFileName)
                        .FirstOrDefault(d => d != null && d.Contains("quilt", StringComparison.OrdinalIgnoreCase) && d.Contains(gameVersion, StringComparison.OrdinalIgnoreCase))
                    : null;

                if (!string.IsNullOrEmpty(existingQuilt))
                {
                    inheritsFrom = existingQuilt;
                    mainClass = "org.quiltmc.loader.impl.launch.knot.KnotClient";
                }
                else
                {
                    inheritsFrom = gameVersion;
                }
            }
            else
            {
                inheritsFrom = gameVersion;
                mainClass = "net.minecraft.client.main.Main";
            }

            var rootObj = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["inheritsFrom"] = inheritsFrom,
                ["time"] = DateTime.UtcNow.ToString("s") + "Z",
                ["releaseTime"] = DateTime.UtcNow.ToString("s") + "Z",
                ["type"] = "custom",
                ["mainClass"] = mainClass,
                ["libraries"] = new object[] { }
            };

            string jsonContent = JsonSerializer.Serialize(rootObj, jsonOptions);
            await File.WriteAllTextAsync(versionJsonPath, jsonContent);
        }

        public static void DeleteModpack(string minecraftPath, ModpackInfo pack)
        {
            try
            {
                if (Directory.Exists(pack.RootPath))
                    Directory.Delete(pack.RootPath, true);
            }
            catch { }

            try
            {
                string vPath = Path.Combine(minecraftPath, "versions", pack.Id);
                if (Directory.Exists(vPath))
                    Directory.Delete(vPath, true);
            }
            catch { }
        }

        public static List<ModpackFileItem> GetInstalledFiles(ModpackInfo pack, string categoryFilter = "all")
        {
            var result = new List<ModpackFileItem>();

            void ScanDir(string dir, string cat)
            {
                if (!Directory.Exists(dir)) return;
                foreach (string file in Directory.GetFiles(dir))
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        result.Add(new ModpackFileItem
                        {
                            FileName = fi.Name,
                            FullPath = fi.FullName,
                            Category = cat,
                            FileSizeFormatted = FormatFileSize(fi.Length)
                        });
                    }
                    catch { }
                }
            }

            if (categoryFilter == "all" || categoryFilter == "mods")
                ScanDir(pack.ModsPath, "mods");
            if (categoryFilter == "all" || categoryFilter == "resourcepacks")
                ScanDir(pack.ResourcepacksPath, "resourcepacks");
            if (categoryFilter == "all" || categoryFilter == "shaderpacks")
                ScanDir(pack.ShaderpacksPath, "shaderpacks");

            return result.OrderBy(f => f.FileName).ToList();
        }

        public static void DeleteFile(string fullPath)
        {
            try
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch { }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }

        public static async Task<List<ModrinthProjectHit>> SearchModrinthAsync(
            string query,
            string projectType = "mod",
            string gameVersion = "",
            string loader = "")
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "RaytolfasLauncher/0.0.4.1");

            var facetsList = new List<string>();

            if (!string.IsNullOrWhiteSpace(projectType))
                facetsList.Add($"[\"project_type:{projectType}\"]");

            if (!string.IsNullOrWhiteSpace(gameVersion) && gameVersion != "Все" && gameVersion != "All")
                facetsList.Add($"[\"versions:{gameVersion}\"]");

            if (projectType == "mod" && !string.IsNullOrWhiteSpace(loader) && !loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase))
                facetsList.Add($"[\"categories:{loader.ToLowerInvariant()}\"]");

            string facetsParam = facetsList.Count > 0
                ? $"&facets=[{string.Join(",", facetsList)}]"
                : "";

            string url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query ?? "")}&limit=25{facetsParam}";

            try
            {
                var response = await client.GetStringAsync(url);
                var searchResult = JsonSerializer.Deserialize<ModrinthSearchResponse>(response, jsonOptions);
                return searchResult?.Hits ?? new List<ModrinthProjectHit>();
            }
            catch
            {
                return new List<ModrinthProjectHit>();
            }
        }

        public static async Task<string> DownloadModrinthProjectAsync(
            string projectId,
            string gameVersion,
            string loader,
            string destinationDirectory,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "RaytolfasLauncher/0.0.4.1");

            string versionsUrl = $"https://api.modrinth.com/v2/project/{projectId}/version";
            var queryParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(gameVersion) && gameVersion != "Все")
                queryParts.Add($"game_versions=[\"{gameVersion}\"]");

            if (!string.IsNullOrWhiteSpace(loader) && !loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase))
                queryParts.Add($"loaders=[\"{loader.ToLowerInvariant()}\"]");

            if (queryParts.Count > 0)
                versionsUrl += "?" + string.Join("&", queryParts);

            var json = await client.GetStringAsync(versionsUrl, ct);
            var versions = JsonSerializer.Deserialize<List<ModrinthProjectVersion>>(json, jsonOptions);

            if (versions == null || versions.Count == 0)
            {
                json = await client.GetStringAsync($"https://api.modrinth.com/v2/project/{projectId}/version", ct);
                versions = JsonSerializer.Deserialize<List<ModrinthProjectVersion>>(json, jsonOptions);
            }

            if (versions == null || versions.Count == 0)
                throw new InvalidOperationException("Не найдено совместимых файлов для загрузки на Modrinth.");

            var latestVer = versions[0];
            var primaryFile = latestVer.Files.FirstOrDefault(f => f.Primary) ?? latestVer.Files.FirstOrDefault();

            if (primaryFile == null || string.IsNullOrEmpty(primaryFile.Url))
                throw new InvalidOperationException("У данной версии отсутствуют доступные для загрузки файлы.");

            Directory.CreateDirectory(destinationDirectory);
            string destinationFile = Path.Combine(destinationDirectory, primaryFile.Filename);

            using var response = await client.GetAsync(primaryFile.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? primaryFile.Size;

            using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
            using var destStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long bytesReadTotal = 0;
            int bytesRead;

            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await destStream.WriteAsync(buffer, 0, bytesRead, ct);
                bytesReadTotal += bytesRead;

                if (totalBytes > 0 && progress != null)
                {
                    progress.Report((double)bytesReadTotal / totalBytes * 100.0);
                }
            }

            return primaryFile.Filename;
        }
    }
}
