// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.
//
// Raytolfas Launcher is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Raytolfas Launcher is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Raytolfas Launcher. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaytolfasLauncher
{
    public class AccountData
    {
        public string Username { get; set; } = "";
        public string UUID { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string ClientToken { get; set; } = "";
        public string Type { get; set; } = "Offline";
        public string MicrosoftAccountIdentifier { get; set; } = "";
    }

    public class LauncherSettings
    {
        public List<AccountData> Accounts { get; set; } = new List<AccountData>();
        public int SelectedAccountIndex { get; set; } = 0;
        public int SelectedRam { get; set; } = 4096;
        public string? JavaPath { get; set; }
        public string? SelectedJavaProfileId { get; set; } = "auto";
        public int WindowWidth { get; set; } = 854;
        public int WindowHeight { get; set; } = 480;
        public bool IsFullScreen { get; set; } = false;
        public string JvmArgs { get; set; } = "-XX:+UseG1GC";
        public string MinecraftPath { get; set; } = "";
        public string? LastVersion { get; set; }
        public string? LastServerName { get; set; } = "";
        public string? LastServerIp { get; set; } = "";
        public bool ShowReleases { get; set; } = true;
        public bool ShowSnapshots { get; set; } = false;
        public bool ShowModded { get; set; } = true;
        public bool ShowDiscordStatus { get; set; } = true;
        public bool HideLauncherOnPlay { get; set; } = true;
        public bool ShowAccountAvatar { get; set; } = true;
        public bool OpenLogWindowOnLaunch { get; set; } = false;
        public bool EnableElyBySkins { get; set; } = false;
        public string Language { get; set; } = LocalizationManager.DefaultLanguage;
        public string CustomBackgroundPath { get; set; } = "";

        public void Save()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_settings.json");
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
            }
        }
    }

    public class LoaderInstallOption
    {
        public string LoaderType { get; set; } = "";
        public string GameVersion { get; set; } = "";
        public string LoaderVersion { get; set; } = "";
        public string VersionId { get; set; } = "";
        public bool IsRecommended { get; set; }
        public bool IsLatest { get; set; }

        public string DisplayName
        {
            get
            {
                if (LoaderType == "Forge")
                {
                    string badge = IsRecommended ? "recommended" : IsLatest ? "latest" : "build";
                    return $"{GameVersion} / {LoaderVersion} ({badge})";
                }

                return $"{GameVersion} / loader {LoaderVersion}";
            }
        }
    }

    public class JavaProfile
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? JavaPath { get; set; }
    }

    public class ModrinthSearchResponse
    {
        [JsonPropertyName("hits")]
        public List<ModrinthProjectHit> Hits { get; set; } = new List<ModrinthProjectHit>();
    }

    public class ModrinthProjectHit
    {
        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = "";

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("icon_url")]
        public string? IconUrl { get; set; }

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }

        [JsonPropertyName("display_categories")]
        public List<string> DisplayCategories { get; set; } = new List<string>();

        [JsonPropertyName("versions")]
        public List<string> Versions { get; set; } = new List<string>();

        [JsonPropertyName("latest_version")]
        public string? LatestGameVersion { get; set; }

        public string CategoriesText => DisplayCategories.Count == 0 ? "modpack" : string.Join(", ", DisplayCategories);
        public string MetaText => $"{Downloads:N0} скачиваний • {Author}";
    }

    public class ModrinthProjectVersion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("version_number")]
        public string VersionNumber { get; set; } = "";

        [JsonPropertyName("version_type")]
        public string VersionType { get; set; } = "";

        [JsonPropertyName("loaders")]
        public List<string> Loaders { get; set; } = new List<string>();

        [JsonPropertyName("game_versions")]
        public List<string> GameVersions { get; set; } = new List<string>();

        [JsonPropertyName("files")]
        public List<ModrinthVersionFile> Files { get; set; } = new List<ModrinthVersionFile>();

        public string DisplayName
        {
            get
            {
                string loaders = Loaders.Count == 0 ? "unknown loader" : string.Join(", ", Loaders);
                string gameVersions = GameVersions.Count == 0 ? "unknown MC" : string.Join(", ", GameVersions);
                return $"{VersionNumber} • {loaders} • {gameVersions}";
            }
        }
    }

    public class ModrinthVersionFile
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("filename")]
        public string Filename { get; set; } = "";

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }
    }

    public class ModrinthGameVersionTag
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("version_type")]
        public string VersionType { get; set; } = "";

        [JsonPropertyName("major")]
        public bool Major { get; set; }
    }

    public class ModrinthPackIndex
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonPropertyName("game")]
        public string Game { get; set; } = "";

        [JsonPropertyName("versionId")]
        public string VersionId { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("files")]
        public List<ModrinthPackFile> Files { get; set; } = new List<ModrinthPackFile>();

        [JsonPropertyName("dependencies")]
        public Dictionary<string, string> Dependencies { get; set; } = new Dictionary<string, string>();
    }

    public class ModrinthPackFile
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("downloads")]
        public List<string> Downloads { get; set; } = new List<string>();

        [JsonPropertyName("fileSize")]
        public long FileSize { get; set; }

        [JsonPropertyName("env")]
        public ModrinthPackEnvironment? Environment { get; set; }
    }

    public class ModrinthPackEnvironment
    {
        [JsonPropertyName("client")]
        public string? Client { get; set; }

        [JsonPropertyName("server")]
        public string? Server { get; set; }
    }

    public class GitHubReleaseInfo
    {
        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; set; } = new List<GitHubReleaseAsset>();
    }

    public class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";
    }

    public class ElyByProfileResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }
}
