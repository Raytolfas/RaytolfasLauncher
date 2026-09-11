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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Installers;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;
using DiscordRPC;
using XboxAuthNet.Game.Accounts;

namespace RaytolfasLauncher
{
    public partial class MainWindow : Window
    {
        public LauncherSettings Settings { get; set; } = new LauncherSettings();
        private MinecraftLauncher? launcher;
        private LauncherSettings settings = new LauncherSettings();
        private readonly DiscordRpcClient discordClientID = new DiscordRpcClient("1472589510742118400");
        private readonly string currentVersion = "0.0.4";
        private readonly string updateUrl = "https://raw.githubusercontent.com/Raytolfas/Assets/refs/heads/main/RaytolfasLauncherMC/update.json";
        private const string ElyByProfileApiBaseUrl = "https://authserver.ely.by";
        private const string AuthlibInjectorLatestReleaseApiUrl = "https://api.github.com/repos/yushijinhun/authlib-injector/releases/latest";
        private string settingsFolder = PlatformHelper.GetDefaultSettingsFolder();
        private string legacySettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RaytolfasLauncher");
        private string settingsPath = string.Empty;
        private string legacySettingsPath = string.Empty;
        private string microsoftAccountsPath = string.Empty;
        private string legacyMicrosoftAccountsPath = Path.Combine(PlatformHelper.GetDefaultMinecraftFolder(), "cml_accounts.json");
        private DiscordRpcClient? discordClient;
        private readonly List<JavaProfile> javaProfiles = new List<JavaProfile>();
        private readonly HttpClient apiClient = new HttpClient();
        private const string JavaProfileAutoId = "auto";
        private const string JavaProfileCustomId = "custom";
        private bool isShuttingDown;
        private string currentLanguage = LocalizationManager.DefaultLanguage;
        private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly List<LoaderInstallOption> availableLoaderOptions = new List<LoaderInstallOption>();
        private FabricInstaller? fabricInstaller;
        private bool suppressOptionEvents;
        private TrayIcon? trayIcon;

        private string ElyByToolsFolder => Path.Combine(settingsFolder, "tools", "elyby");
        private string ElyByAuthlibInjectorPath => Path.Combine(ElyByToolsFolder, "authlib-injector.jar");

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                this.Closing += MainWindow_Closing;

                apiClient.DefaultRequestHeaders.Add("User-Agent", $"RaytolfasLauncher/{currentVersion}");
                fabricInstaller = new FabricInstaller(apiClient);

                settingsPath = Path.Combine(settingsFolder, "settings.json");
                legacySettingsPath = Path.Combine(legacySettingsFolder, "settings.json");
                microsoftAccountsPath = Path.Combine(settingsFolder, "microsoft_accounts.json");
                MigrateLegacySettingsFile();
                MigrateLegacyMicrosoftAccountsFile();
                VersionText.Text = $"v{currentVersion}";

                suppressOptionEvents = true;
                LoadSettingsFromFile();
                suppressOptionEvents = false;
                InitializeLanguageSelector();
                ApplyLocalization();

                string mcPath = string.IsNullOrWhiteSpace(settings?.MinecraftPath)
                    ? PlatformHelper.GetDefaultMinecraftFolder()
                    : settings.MinecraftPath;

                launcher = new MinecraftLauncher(new MinecraftPath(mcPath));

                UpdateAccountList();
                LoadVersions();
                LoadAvatar();
                InitializeModCenterDefaults();
                InitTrayIcon();

                RamSlider.PropertyChanged += (s, e) =>
                {
                    if (e.Property == Slider.ValueProperty)
                    {
                        RamValueText.Text = $"{(int)RamSlider.Value} MB";
                    }
                };

                InitDiscordRPC();

                this.Loaded += async (s, e) =>
                {
                    await CheckForUpdates();
                };
            }
            catch (Exception ex)
            {
                string startupLanguage = LocalizationManager.NormalizeLanguage(settings.Language);
                RayMessageBox.Show(LocalizationManager.Get("launch.startup_critical", startupLanguage)
                    .Replace("{0}", ex.Message)
                    .Replace("{1}", ex.StackTrace ?? ""), "Critical Error", this);
            }
        }

        private string T(string key) => LocalizationManager.Get(key, currentLanguage);

        private string T(string key, params object[] args) => string.Format(T(key), args);

        private void InitializeLanguageSelector()
        {
            if (LanguageBox == null)
                return;

            string selectedLanguage = LocalizationManager.NormalizeLanguage(currentLanguage);
            LanguageBox.Items.Clear();

            foreach (var option in LocalizationManager.SupportedLanguages)
            {
                LanguageBox.Items.Add(new ComboBoxItem
                {
                    Content = option.DisplayName,
                    Tag = option.Code
                });
            }

            foreach (var rawItem in LanguageBox.Items)
            {
                if (rawItem is ComboBoxItem item && string.Equals(item.Tag?.ToString(), selectedLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageBox.SelectedItem = item;
                    break;
                }
            }

            if (LanguageBox.SelectedIndex == -1 && LanguageBox.Items.Count > 0)
                LanguageBox.SelectedIndex = 1;
        }

        private void ApplyLocalization()
        {
            Title = T("window.title");
            AccountSectionLabel.Text = T("main.account");
            VersionSectionLabel.Text = T("main.version");
            CheckBoxForce.Content = T("main.reinstall_files");
            LaunchBtn.Content = T("main.play");
            StatusLabel.Text = T("main.status.preparing");

            SettingsTitleText.Text = T("settings.title");
            BackgroundLabel.Text = T("settings.background");
            SelectBackgroundButton.Content = T("settings.select");
            GameFolderLabel.Text = T("settings.game_folder");
            BrowsePathButton.Content = T("settings.browse");
            OptionsLabel.Text = T("settings.options");
            CbDiscordRPC.Content = T("settings.discord");
            CbHideLauncher.Content = T("settings.hide_launcher");
            CbShowAvatar.Content = T("settings.show_avatar");
            CbOpenLogWindow.Content = T("settings.open_logs");
            CbElyBySkins.Content = T("settings.elyby_skins");
            LanguageLabel.Text = T("settings.language");
            JavaProfileLabel.Text = T("settings.java_profile");
            JavaPathLabel.Text = T("settings.java_path");
            ResolutionLabel.Text = T("settings.resolution");
            CbFullScreen.Content = T("settings.fullscreen");
            JavaArgsLabel.Text = T("settings.java_args");
            VersionFiltersLabel.Text = T("settings.show_versions");
            CbReleases.Content = T("settings.releases");
            CbSnapshots.Content = T("settings.snapshots");
            CbModded.Content = T("settings.modded");
            RamLabel.Text = T("settings.ram");
            SaveSettingsButton.Content = T("settings.save");

            ModCenterTitleText.Text = T("modcenter.title");
            ModCenterSubtitleText.Text = T("modcenter.subtitle");
            LoadersTabButton.Content = T("modcenter.tab.loaders");
            OpenModpacksButton.Content = T("modcenter.tab.pack");

            RootFolderMenuItem.Header = T("folders.menu.root");
            ModsFolderMenuItem.Header = T("folders.menu.mods");
            SavesFolderMenuItem.Header = T("folders.menu.saves");
            ScreenshotsFolderMenuItem.Header = T("folders.menu.screenshots");
            LoaderTypeLabel.Text = T("modcenter.loader");
            LoaderGameVersionLabel.Text = T("modcenter.minecraft");
            LoaderVersionLabel.Text = T("modcenter.loader_build");
            RefreshLoaderButton.Content = T("modcenter.refresh");
            InstallLoaderButton.Content = T("modcenter.install_version");
            OpenModpacksButton.Content = T("modcenter.tab.pack");

            ApplyAvatarVisibility();
            InitializeJavaProfiles();
            SetModCenterStatus("");
            if (settings.ShowDiscordStatus)
                SetDiscordStatus(T("discord.state.launcher"), T("discord.details.choosing_version"));
        }

        private void ApplyAvatarVisibility()
        {
            if (PlayerAvatarContainer == null)
                return;

            bool showAvatar = settings.ShowAccountAvatar;
            PlayerAvatarContainer.IsVisible = showAvatar;
            if (!showAvatar)
            {
                PlayerAvatar.Source = null;
            }
            else if (AccountSelector?.SelectedItem is ComboBoxItem item && item.Tag is AccountData account)
            {
                UpdateAvatar(account.Username);
            }
        }

        private void WriteLog(LogWindow? logWindow, string text)
        {
            logWindow?.WriteLog(text);
        }

        private async void CheckUpdateBtn_Click(object? sender, RoutedEventArgs e)
        {
            await CheckForUpdates(isManual: true);
        }

        private async Task<bool> CheckForUpdates(bool isManual = false)
        {
            string separator = updateUrl.Contains('?') ? "&" : "?";
            string versionUrl = $"{updateUrl}{separator}_t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "RaytolfasLauncher");
                client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MustRevalidate = true
                };
                client.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");

                var json = await client.GetStringAsync(versionUrl);
                var info = JsonSerializer.Deserialize<UpdateInfo>(json, jsonOptions);

                if (info != null)
                {
                    Version latestVersion = new Version(info.Version);
                    Version localVersion = new Version(currentVersion);

                    if (latestVersion > localVersion)
                    {
                        string targetDownloadUrl = info.GetPlatformDownloadUrl();
                        var upWin = new UpdateWindow(currentVersion, info.Version, info.Changelog, targetDownloadUrl, currentLanguage);
                        await upWin.ShowDialog(this);
                        return true;
                    }
                    else if (isManual)
                    {
                        await RayMessageBox.ShowAsync(this, T("update.latest"), T("update.latest_title"));
                    }
                }
            }
            catch (Exception ex)
            {
                if (isManual)
                    await RayMessageBox.ShowAsync(this, T("update.check_error", ex.Message), T("launch.message.error_title"));
            }
            return false;
        }

        public class UpdateInfo
        {
            [System.Text.Json.Serialization.JsonPropertyName("version")]
            public string Version { get; set; } = "";

            [System.Text.Json.Serialization.JsonPropertyName("download_url")]
            public string DownloadUrl { get; set; } = "";

            [System.Text.Json.Serialization.JsonPropertyName("windows_url")]
            public string? WindowsUrl { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("download_url_windows")]
            public string? DownloadUrlWindows { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("linux_url")]
            public string? LinuxUrl { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("download_url_linux")]
            public string? DownloadUrlLinux { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("changelog")]
            public string Changelog { get; set; } = "";

            public string GetPlatformDownloadUrl()
            {
                if (PlatformHelper.IsLinux)
                {
                    if (!string.IsNullOrWhiteSpace(LinuxUrl))
                        return LinuxUrl;
                    if (!string.IsNullOrWhiteSpace(DownloadUrlLinux))
                        return DownloadUrlLinux;
                }

                if (PlatformHelper.IsWindows)
                {
                    if (!string.IsNullOrWhiteSpace(WindowsUrl))
                        return WindowsUrl;
                    if (!string.IsNullOrWhiteSpace(DownloadUrlWindows))
                        return DownloadUrlWindows;
                }

                return DownloadUrl;
            }
        }

        private void UpdateAccountList()
        {
            AccountSelector.SelectionChanged -= AccountSelector_SelectionChanged;
            AccountSelector.Items.Clear();
            foreach (var acc in settings.Accounts)
            {
                string icon = acc.Type switch
                {
                    "Microsoft" => "🔑 ",
                    "ElyBy" => "🦋 ",
                    _ => "👤 "
                };
                AccountSelector.Items.Add(new ComboBoxItem
                {
                    Content = $"{icon}{acc.Username}",
                    Tag = acc
                });
            }

            if (settings.Accounts.Count > 0 && settings.SelectedAccountIndex < settings.Accounts.Count)
                AccountSelector.SelectedIndex = settings.SelectedAccountIndex;
            else if (settings.Accounts.Count > 0)
                AccountSelector.SelectedIndex = 0;
            else
                AddDefaultAccount();

            AccountSelector.SelectionChanged += AccountSelector_SelectionChanged;
        }

        private void AddDefaultAccount()
        {
            if (!settings.Accounts.Any())
            {
                settings.Accounts.Add(new AccountData { Username = "PlayerName", Type = "Offline" });
            }
            UpdateAccountList();
        }

        private void AccountSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (AccountSelector.SelectedItem is ComboBoxItem item && item.Tag is AccountData acc)
            {
                settings.SelectedAccountIndex = AccountSelector.SelectedIndex;
                UpdateAvatar(acc.Username);
                SaveSettings();
            }
        }

        private void LoadAvatar()
        {
            if (AccountSelector.SelectedItem is ComboBoxItem item && item.Tag is AccountData acc)
                UpdateAvatar(acc.Username);
        }

        private async void UpdateAvatar(string username)
        {
            if (!settings.ShowAccountAvatar)
            {
                ApplyAvatarVisibility();
                return;
            }

            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    var uri = new Uri("avares://RaytolfasLauncher/Assets/no_internet.png");
                    if (AssetLoader.Exists(uri))
                        PlayerAvatar.Source = new Bitmap(AssetLoader.Open(uri));
                    return;
                }

                byte[] bytes = await apiClient.GetByteArrayAsync($"https://minotar.net/avatar/{username}/32");
                using var ms = new MemoryStream(bytes);
                PlayerAvatar.Source = new Bitmap(ms);
            }
            catch
            {
                try
                {
                    byte[] bytes = await apiClient.GetByteArrayAsync("https://minotar.net/avatar/char/32");
                    using var ms = new MemoryStream(bytes);
                    PlayerAvatar.Source = new Bitmap(ms);
                }
                catch { }
            }
        }

        private async void LaunchBtn_Click(object? sender, RoutedEventArgs e)
        {
            if (VersionBox.SelectedItem is not ComboBoxItem selectedVer || launcher == null)
                return;

            if (selectedVer.Tag is not string versionId || string.IsNullOrWhiteSpace(versionId))
                return;

            LogWindow? logWindow = null;
            if (settings.OpenLogWindowOnLaunch)
            {
                logWindow = new LogWindow(currentLanguage);
                logWindow.Show();
                WriteLog(logWindow, T("log.beta_runtime_warning"));
            }

            var sessionResult = await TryCreateSessionAsync(logWindow);
            if (!sessionResult.Success || sessionResult.Session == null)
            {
                logWindow?.Hide();
                return;
            }

            bool isOfflineSession = sessionResult.IsOfflineSession;
            bool requiresElyByInjector = sessionResult.RequiresElyByInjector;
            MSession session = sessionResult.Session;
            bool isNetworkAvailable = NetworkInterface.GetIsNetworkAvailable();

            DownloadPanel.IsVisible = true;
            LaunchBtn.IsEnabled = false;
            DownloadProgress.Value = 0;
            StatusLabel.Text = T("main.status.preparing");
            DownloadProgress.Maximum = 1;

            EventHandler<InstallerProgressChangedEventArgs>? fileProgressHandler = null;
            EventHandler<ByteProgress>? byteProgressHandler = null;
            try
            {
                WriteLog(logWindow, T("launch.log.preparing", versionId, session.Username ?? "Player"));
                if (isOfflineSession)
                {
                    WriteLog(logWindow, T("launch.log.offline_session"));
                }

                if (!isNetworkAvailable)
                {
                    WriteLog(logWindow, T("launch.log.offline_network"));
                }

                if (CheckBoxForce?.IsChecked == true)
                {
                    if (!isNetworkAvailable)
                    {
                        WriteLog(logWindow, T("launch.log.force_unavailable"));
                    }
                    else
                    {
                        WriteLog(logWindow, T("launch.log.force_delete"));

                        string versionPath = Path.Combine(settings.MinecraftPath, "versions", versionId);
                        string jarPath = Path.Combine(versionPath, $"{versionId}.jar");

                        if (File.Exists(jarPath))
                        {
                            File.Delete(jarPath);
                            WriteLog(logWindow, T("launch.log.force_deleted"));
                        }
                    }
                }

                string targetVersionDir = Path.Combine(settings.MinecraftPath, "versions", versionId);
                if (!Directory.Exists(targetVersionDir))
                {
                    Directory.CreateDirectory(targetVersionDir);
                }

                string? lastInstallMessage = null;
                DateTime lastFileUiUpdate = DateTime.MinValue;
                DateTime lastByteUpdate = DateTime.MinValue;

                fileProgressHandler = (s, args) =>
                {
                    if ((DateTime.UtcNow - lastFileUiUpdate).TotalMilliseconds < 120 &&
                        args.EventType != InstallerEventType.Done &&
                        args.ProgressedTasks < args.TotalTasks)
                    {
                        return;
                    }

                    lastFileUiUpdate = DateTime.UtcNow;
                    Dispatcher.UIThread.Post(() =>
                    {
                        DownloadProgress.Maximum = Math.Max(args.TotalTasks, 1);
                        DownloadProgress.Value = args.ProgressedTasks;
                        StatusLabel.Text = $"{FormatInstallerEvent(args.EventType)}: {args.ProgressedTasks}/{Math.Max(args.TotalTasks, 1)}";

                        string message = $"{FormatInstallerEvent(args.EventType)}: {args.Name}";
                        if (!string.Equals(lastInstallMessage, message, StringComparison.Ordinal))
                        {
                            lastInstallMessage = message;
                            WriteLog(logWindow, "[Launcher] " + message);
                        }
                    }, DispatcherPriority.Background);
                };

                byteProgressHandler = (s, args) =>
                {
                    if ((DateTime.UtcNow - lastByteUpdate).TotalMilliseconds < 250)
                        return;

                    lastByteUpdate = DateTime.UtcNow;
                    Dispatcher.UIThread.Post(() =>
                    {
                        int percent = (int)Math.Round(args.ToRatio() * 100);
                        StatusLabel.Text = T("main.status.downloading_files", percent);
                    }, DispatcherPriority.Background);
                };

                launcher.FileProgressChanged += fileProgressHandler;
                launcher.ByteProgressChanged += byteProgressHandler;

                if (isNetworkAvailable)
                {
                    await launcher.InstallAsync(versionId);
                }
                else
                {
                    string localVersionPath = Path.Combine(settings.MinecraftPath, "versions", versionId);
                    if (!Directory.Exists(localVersionPath))
                    {
                        await RayMessageBox.ShowAsync(this, T("launch.message.version_missing"), T("launch.message.error_title"));
                        DownloadPanel.IsVisible = false;
                        LaunchBtn.IsEnabled = true;
                        return;
                    }
                }

                string? resolvedJavaPath = await GetResolvedJavaPathForLaunchAsync(versionId);
                int launchRamMb = GetEffectiveLaunchRamMb(resolvedJavaPath, (int)RamSlider.Value);

                var launchOption = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = launchRamMb,
                    FullScreen = CbFullScreen.IsChecked ?? false,
                    ScreenWidth = int.TryParse(WinWidthBox.Text, out int w) ? w : 854,
                    ScreenHeight = int.TryParse(WinHeightBox.Text, out int h) ? h : 480,
                    JavaPath = resolvedJavaPath,
                };

                string modpackDir = Path.Combine(settings.MinecraftPath, "modpacks", versionId);
                if (Directory.Exists(modpackDir))
                {
                    launchOption.Path = new MinecraftPath(settings.MinecraftPath, modpackDir);
                }

                if (!string.IsNullOrWhiteSpace(JavaArgsBox.Text))
                {
                    var customArgs = new List<CmlLib.Core.ProcessBuilder.MArgument>();
                    foreach (var arg in JavaArgsBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        customArgs.Add(new CmlLib.Core.ProcessBuilder.MArgument(arg));
                    }
                    launchOption.ExtraJvmArguments = customArgs;
                }

                if (requiresElyByInjector)
                {
                    StatusLabel.Text = T("launch.status.preparing_elyby");
                    bool injectorReady = await EnsureElyByAuthlibInjectorAsync(logWindow, isOfflineSession);
                    if (injectorReady)
                    {
                        var extraJvmArguments = launchOption.ExtraJvmArguments?.ToList() ?? new List<CmlLib.Core.ProcessBuilder.MArgument>();
                        extraJvmArguments.Insert(0, new CmlLib.Core.ProcessBuilder.MArgument("-Dauthlibinjector.noShowServerName"));
                        extraJvmArguments.Insert(0, new CmlLib.Core.ProcessBuilder.MArgument("-Dauthlibinjector.noLogFile"));
                        extraJvmArguments.Insert(0, new CmlLib.Core.ProcessBuilder.MArgument($"-javaagent:{ElyByAuthlibInjectorPath}=ely.by"));
                        launchOption.ExtraJvmArguments = extraJvmArguments;
                    }
                }

                var process = await launcher.BuildProcessAsync(versionId, launchOption);

                DownloadPanel.IsVisible = false;
                StatusLabel.Text = T("main.status.launching");
                DownloadProgress.Value = 0;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.RedirectStandardError = false;

                if (settings.HideLauncherOnPlay) this.Hide();

                process.Start();
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    string? crashHint = process.ExitCode != 0 ? TryGetMinecraftCrashHint() : null;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (process.ExitCode != 0)
                        {
                            if (!string.IsNullOrWhiteSpace(crashHint))
                            {
                                WriteLog(logWindow, T("launch.log.crash_hint", crashHint));
                                RayMessageBox.Show(T("launch.message.crashed_with_hint", process.ExitCode, crashHint), T("launch.message.error_title"), this);
                            }
                            else
                            {
                                RayMessageBox.Show(T("launch.message.crashed", process.ExitCode), T("launch.message.error_title"), this);
                            }
                        }

                        if (settings.HideLauncherOnPlay || !this.IsVisible)
                            this.Show();

                        SetDiscordStatus(T("discord.state.launcher"), T("discord.details.choosing_version"));
                        WriteLog(logWindow, T("launch.log.game_closed"));
                        this.Activate();

                        DownloadPanel.IsVisible = false;
                        StatusLabel.Text = "";
                        LaunchBtn.IsEnabled = true;
                    }, DispatcherPriority.Background);
                };

                string? serverDisplay = !string.IsNullOrWhiteSpace(settings.LastServerName)
                    ? settings.LastServerName
                    : settings.LastServerIp;
                if (!string.IsNullOrWhiteSpace(serverDisplay))
                    SetDiscordStatus(T("discord.state.server", serverDisplay), T("discord.details.version", versionId));
                else
                    SetDiscordStatus(T("discord.state.in_game"), T("discord.details.version", versionId));
            }
            catch (Exception ex)
            {
                WriteLog(logWindow, T("launch.log.launch_error", ex.Message));
                await RayMessageBox.ShowAsync(this, T("launch.message.error", ex.Message), T("launch.message.error_title"));
                this.Show();
                DownloadPanel.IsVisible = false;
                StatusLabel.Text = "";
                LaunchBtn.IsEnabled = true;
            }
            finally
            {
                if (launcher != null && fileProgressHandler != null)
                    launcher.FileProgressChanged -= fileProgressHandler;

                if (launcher != null && byteProgressHandler != null)
                    launcher.ByteProgressChanged -= byteProgressHandler;
            }
        }

        private void SetCustomBackground(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                var uri = new Uri("avares://RaytolfasLauncher/Assets/background.png");
                if (AssetLoader.Exists(uri))
                    MainBgImage.Source = new Bitmap(AssetLoader.Open(uri));
                settings.CustomBackgroundPath = "";
            }
            else
            {
                try
                {
                    MainBgImage.Source = new Bitmap(path);
                    settings.CustomBackgroundPath = path;
                }
                catch
                {
                    var uri = new Uri("avares://RaytolfasLauncher/Assets/background.png");
                    if (AssetLoader.Exists(uri))
                        MainBgImage.Source = new Bitmap(AssetLoader.Open(uri));
                    settings.CustomBackgroundPath = "";
                }
            }
        }

        private void LoadSettingsFromFile()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    settings = JsonSerializer.Deserialize<LauncherSettings>(json, jsonOptions) ?? new LauncherSettings();
                }
                else
                {
                    settings = new LauncherSettings();
                }
            }
            catch
            {
                settings = new LauncherSettings();
            }

            Settings = settings;

            if (string.IsNullOrWhiteSpace(settings.MinecraftPath))
                settings.MinecraftPath = PlatformHelper.GetDefaultMinecraftFolder();

            CbDiscordRPC.IsChecked = settings.ShowDiscordStatus;
            CbHideLauncher.IsChecked = settings.HideLauncherOnPlay;
            CbShowAvatar.IsChecked = settings.ShowAccountAvatar;
            CbOpenLogWindow.IsChecked = settings.OpenLogWindowOnLaunch;
            CbElyBySkins.IsChecked = settings.EnableElyBySkins;
            currentLanguage = LocalizationManager.NormalizeLanguage(settings.Language);

            CbReleases.IsChecked = settings.ShowReleases;
            CbSnapshots.IsChecked = settings.ShowSnapshots;
            CbModded.IsChecked = settings.ShowModded;

            RamSlider.Value = NormalizeConfiguredRamMb(settings.SelectedRam);
            RamValueText.Text = $"{(int)RamSlider.Value} MB";
            WinWidthBox.Text = settings.WindowWidth.ToString();
            WinHeightBox.Text = settings.WindowHeight.ToString();
            CbFullScreen.IsChecked = settings.IsFullScreen;

            BgPathBox.Text = settings.CustomBackgroundPath;
            SetCustomBackground(settings.CustomBackgroundPath);

            PathBox.Text = settings.MinecraftPath;
            JavaArgsBox.Text = settings.JvmArgs;

            if (launcher != null)
                launcher = new MinecraftLauncher(new MinecraftPath(settings.MinecraftPath));

            ApplyAvatarVisibility();
            InitializeLanguageSelector();
            InitializeJavaProfiles();
        }

        private void MigrateLegacySettingsFile()
        {
            try
            {
                if (File.Exists(settingsPath))
                    return;

                if (!File.Exists(legacySettingsPath))
                    return;

                Directory.CreateDirectory(settingsFolder);
                File.Copy(legacySettingsPath, settingsPath, overwrite: false);
            }
            catch
            {
            }
        }

        private void MigrateLegacyMicrosoftAccountsFile()
        {
            try
            {
                if (File.Exists(microsoftAccountsPath))
                    return;

                if (!File.Exists(legacyMicrosoftAccountsPath))
                    return;

                Directory.CreateDirectory(settingsFolder);
                File.Copy(legacyMicrosoftAccountsPath, microsoftAccountsPath, overwrite: false);
            }
            catch
            {
            }
        }

        private void InitializeModCenterDefaults()
        {
            LoaderTypeBox.Items.Clear();
            LoaderTypeBox.Items.Add(new ComboBoxItem { Content = "Fabric", Tag = "fabric" });
            LoaderTypeBox.Items.Add(new ComboBoxItem { Content = "Forge", Tag = "forge" });
            LoaderTypeBox.SelectedIndex = 0;
        }

        private void InitializeJavaProfiles()
        {
            javaProfiles.Clear();
            javaProfiles.Add(new JavaProfile { Id = JavaProfileAutoId, Name = T("settings.java.auto"), JavaPath = null });
            javaProfiles.Add(new JavaProfile { Id = JavaProfileCustomId, Name = T("settings.java.custom"), JavaPath = settings.JavaPath });

            foreach (var path in PlatformHelper.DiscoverJavaPaths(settings.JavaPath))
            {
                AddJavaProfileFromPath(path);
            }

            JavaProfileBox.SelectionChanged -= JavaProfileBox_SelectionChanged;
            JavaProfileBox.Items.Clear();

            foreach (var profile in javaProfiles)
            {
                JavaProfileBox.Items.Add(new ComboBoxItem { Content = profile.Name, Tag = profile });
            }

            JavaProfile? selectedProfile = null;
            if (!string.IsNullOrWhiteSpace(settings.SelectedJavaProfileId))
            {
                selectedProfile = javaProfiles.FirstOrDefault(p => p.Id == settings.SelectedJavaProfileId);
            }

            if (selectedProfile == null)
            {
                selectedProfile = string.IsNullOrWhiteSpace(settings.JavaPath)
                    ? javaProfiles.FirstOrDefault(p => p.Id == JavaProfileAutoId)
                    : javaProfiles.FirstOrDefault(p => p.Id == JavaProfileCustomId);
            }

            if (selectedProfile != null)
            {
                JavaProfileBox.SelectedItem = JavaProfileBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Tag == selectedProfile);
                ApplyJavaProfileSelection(selectedProfile);
            }

            JavaProfileBox.SelectionChanged += JavaProfileBox_SelectionChanged;
        }

        private void LanguageBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (LanguageBox.SelectedItem is not ComboBoxItem item)
                return;

            currentLanguage = LocalizationManager.NormalizeLanguage(item.Tag?.ToString());
            ApplyLocalization();
        }

        private async Task<string?> GetResolvedJavaPathForLaunchAsync(string versionId)
        {
            string explicitPath = (JavaPathBox.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(explicitPath))
                return explicitPath;

            if (launcher != null)
            {
                try
                {
                    var version = await launcher.GetVersionAsync(versionId);
                    string? launcherJavaPath = launcher.GetJavaPath(version);
                    if (!string.IsNullOrWhiteSpace(launcherJavaPath) && File.Exists(launcherJavaPath))
                        return launcherJavaPath;
                }
                catch
                {
                }

                try
                {
                    string? defaultJavaPath = launcher.GetDefaultJavaPath();
                    if (!string.IsNullOrWhiteSpace(defaultJavaPath) && File.Exists(defaultJavaPath))
                        return defaultJavaPath;
                }
                catch
                {
                }
            }

            var discovered = PlatformHelper.DiscoverJavaPaths(settings.JavaPath)
                .OrderBy(path => IsLikely32BitJava(path))
                .ThenByDescending(path => TryReadJavaMajorVersion(path) ?? 0)
                .ThenByDescending(path => string.Equals(path, settings.JavaPath, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            return discovered;
        }

        private static bool IsLikely32BitJava(string? javawPath)
        {
            if (string.IsNullOrWhiteSpace(javawPath))
                return false;

            string? osArch = TryReadJavaReleaseValue(javawPath, "OS_ARCH");
            if (!string.IsNullOrWhiteSpace(osArch))
            {
                string normalizedArch = osArch.Trim();
                if (normalizedArch.Contains("64", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (normalizedArch.Contains("86", StringComparison.OrdinalIgnoreCase) ||
                    normalizedArch.Contains("32", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return javawPath.IndexOf("(x86)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   javawPath.IndexOf("\\x86\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddJavaProfileFromPath(string javawPath)
        {
            if (javaProfiles.Any(p => !string.IsNullOrWhiteSpace(p.JavaPath) && string.Equals(p.JavaPath, javawPath, StringComparison.OrdinalIgnoreCase)))
                return;

            string id = "path:" + javawPath.ToLowerInvariant();
            string name = BuildJavaProfileName(javawPath);
            javaProfiles.Add(new JavaProfile { Id = id, Name = name, JavaPath = javawPath });
        }

        private string BuildJavaProfileName(string javawPath)
        {
            string? version = TryReadJavaVersion(javawPath);
            string folderName = "";

            var rootDir = Directory.GetParent(javawPath)?.Parent?.FullName;
            if (!string.IsNullOrWhiteSpace(rootDir))
            {
                folderName = new DirectoryInfo(rootDir).Name;
            }

            if (!string.IsNullOrWhiteSpace(version))
            {
                return string.IsNullOrWhiteSpace(folderName) ? $"Java {version}" : $"Java {version} ({folderName})";
            }

            return string.IsNullOrWhiteSpace(folderName) ? "Java" : $"Java ({folderName})";
        }

        private string? TryReadJavaVersion(string javawPath)
        {
            return TryReadJavaReleaseValue(javawPath, "JAVA_VERSION");
        }

        private static string? TryReadJavaReleaseValue(string javawPath, string key)
        {
            var rootDir = Directory.GetParent(javawPath)?.Parent?.FullName;
            if (string.IsNullOrWhiteSpace(rootDir))
                return null;

            string releasePath = Path.Combine(rootDir, "release");
            if (!File.Exists(releasePath))
                return null;

            string prefix = key + "=";
            foreach (var line in File.ReadLines(releasePath))
            {
                if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = line.Split('=', 2);
                if (parts.Length < 2)
                    return null;

                return parts[1].Trim().Trim('"');
            }

            return null;
        }

        private static int? TryReadJavaMajorVersion(string javawPath)
        {
            string? version = TryReadJavaReleaseValue(javawPath, "JAVA_VERSION");
            if (string.IsNullOrWhiteSpace(version))
                return null;

            string normalizedVersion = version.Trim().Trim('"');
            string[] parts = normalizedVersion.Split(new[] { '.', '_', '-', '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            if (parts[0] == "1" && parts.Length > 1 && int.TryParse(parts[1], out int legacyMajor))
                return legacyMajor;

            return int.TryParse(parts[0], out int major) ? major : null;
        }

        private long GetTotalPhysicalMemoryMb()
        {
            try
            {
                return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024;
            }
            catch
            {
                return 8192;
            }
        }

        private int NormalizeConfiguredRamMb(int requestedRamMb)
        {
            int minimumRamMb = 1024;
            int maximumRamMb = 16384;
            int normalizedRamMb = requestedRamMb <= 0 ? Math.Min(2048, maximumRamMb) : requestedRamMb;
            normalizedRamMb = Math.Clamp(normalizedRamMb, minimumRamMb, maximumRamMb);

            long totalMemoryMb = GetTotalPhysicalMemoryMb();
            if (totalMemoryMb <= 0)
                return normalizedRamMb;

            long reservedForSystemMb = totalMemoryMb <= 4096 ? 1024 : Math.Max(1024, totalMemoryMb / 4);
            long safeCapMb = Math.Max(minimumRamMb, totalMemoryMb - reservedForSystemMb);
            return Math.Min(normalizedRamMb, (int)Math.Min(safeCapMb, maximumRamMb));
        }

        private int GetEffectiveLaunchRamMb(string? javaPath, int requestedRamMb)
        {
            int normalizedRamMb = NormalizeConfiguredRamMb(requestedRamMb);
            if (IsLikely32BitJava(javaPath))
                normalizedRamMb = Math.Min(normalizedRamMb, 1024);

            return normalizedRamMb;
        }

        private void ApplyJavaProfileSelection(JavaProfile profile)
        {
            settings.SelectedJavaProfileId = profile.Id;

            if (profile.Id == JavaProfileAutoId)
            {
                JavaPathBox.Text = "";
                JavaPathBox.IsReadOnly = true;
                return;
            }

            if (profile.Id == JavaProfileCustomId)
            {
                JavaPathBox.IsReadOnly = false;
                JavaPathBox.Text = settings.JavaPath ?? "";
                return;
            }

            JavaPathBox.IsReadOnly = true;
            JavaPathBox.Text = profile.JavaPath ?? "";
        }

        private void JavaProfileBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (JavaProfileBox.SelectedItem is ComboBoxItem item && item.Tag is JavaProfile profile)
            {
                ApplyJavaProfileSelection(profile);
            }
        }

        private void SaveSettings()
        {
            try
            {
                if (!Directory.Exists(settingsFolder))
                    Directory.CreateDirectory(settingsFolder);

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                RayMessageBox.Show(T("settings.file_save_error", ex.Message), T("settings.title"), this);
            }
        }

        private async void SaveSettings_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(PathBox.Text))
                {
                    PathBox.Text = PlatformHelper.GetDefaultMinecraftFolder();
                }

                if (JavaProfileBox.SelectedItem is ComboBoxItem profileItem && profileItem.Tag is JavaProfile profile)
                {
                    settings.SelectedJavaProfileId = profile.Id;
                    if (profile.Id == JavaProfileAutoId)
                    {
                        settings.JavaPath = "";
                    }
                    else if (profile.Id == JavaProfileCustomId)
                    {
                        settings.JavaPath = (JavaPathBox.Text ?? "").Trim();
                        profile.JavaPath = settings.JavaPath;
                    }
                    else
                    {
                        settings.JavaPath = profile.JavaPath ?? "";
                    }
                }
                else
                {
                    settings.JavaPath = (JavaPathBox.Text ?? "").Trim();
                }

                settings.WindowWidth = int.TryParse(WinWidthBox.Text, out int ww) ? ww : 854;
                settings.WindowHeight = int.TryParse(WinHeightBox.Text, out int wh) ? wh : 480;
                settings.IsFullScreen = CbFullScreen.IsChecked ?? false;
                settings.JvmArgs = JavaArgsBox.Text ?? "-XX:+UseG1GC";
                settings.SelectedRam = NormalizeConfiguredRamMb((int)RamSlider.Value);
                settings.MinecraftPath = PathBox.Text ?? "";
                settings.ShowReleases = CbReleases.IsChecked ?? true;
                settings.ShowSnapshots = CbSnapshots.IsChecked ?? false;
                settings.ShowModded = CbModded.IsChecked ?? true;
                settings.ShowDiscordStatus = CbDiscordRPC.IsChecked ?? true;
                settings.HideLauncherOnPlay = CbHideLauncher.IsChecked ?? true;
                settings.ShowAccountAvatar = CbShowAvatar.IsChecked ?? true;
                settings.OpenLogWindowOnLaunch = CbOpenLogWindow.IsChecked ?? false;
                settings.EnableElyBySkins = CbElyBySkins.IsChecked ?? false;
                settings.Language = GetSelectedComboTag(LanguageBox, currentLanguage);
                currentLanguage = LocalizationManager.NormalizeLanguage(settings.Language);
                settings.CustomBackgroundPath = BgPathBox.Text ?? "";

                SaveSettings();

                var mcPath = new MinecraftPath(settings.MinecraftPath);
                launcher = new MinecraftLauncher(mcPath);

                RamSlider.Value = settings.SelectedRam;
                RamValueText.Text = $"{settings.SelectedRam} MB";

                long totalMemory = GetTotalPhysicalMemoryMb();
                if ((long)RamSlider.Value > totalMemory)
                {
                    await RayMessageBox.ShowAsync(this, T("settings.ram_warning"), T("settings.title"));
                }

                LoadVersions();
                SettingsModal.IsVisible = false;

                SetCustomBackground(settings.CustomBackgroundPath);
                ApplyLocalization();
                await RayMessageBox.ShowAsync(this, T("settings.saved.message"), T("settings.saved.title"));
            }
            catch (Exception ex)
            {
                await RayMessageBox.ShowAsync(this, T("settings.save_error", ex.Message), T("settings.title"));
            }
        }

        private void CbOpenLogWindow_Checked(object? sender, RoutedEventArgs e)
        {
            if (suppressOptionEvents)
                return;
        }

        private string FormatInstallerEvent(InstallerEventType eventType)
        {
            return eventType switch
            {
                InstallerEventType.Queued => currentLanguage == "en" ? "Queued" : currentLanguage == "uk" ? "У черзі" : "В очереди",
                InstallerEventType.Done => currentLanguage == "en" ? "Done" : currentLanguage == "uk" ? "Готово" : "Готово",
                _ => currentLanguage == "en" ? "Installing" : currentLanguage == "uk" ? "Встановлення" : "Установка"
            };
        }

        private static MSession CreateMicrosoftSession(string username, string accessToken, string uuid)
        {
            var session = new MSession(username, accessToken, uuid)
            {
                UserType = "msa"
            };
            return session;
        }

        private static MSession CreateElyBySession(string username, string accessToken, string uuid, string? clientToken = null)
        {
            var session = new MSession(username, accessToken, uuid)
            {
                UserType = "Mojang"
            };
            if (!string.IsNullOrWhiteSpace(clientToken))
                session.ClientToken = clientToken;
            return session;
        }

        private static MSession CreateOfflineSession(string username, string? preferredUuid = null)
        {
            var session = MSession.CreateOfflineSession(username);
            session.UserType = "legacy";
            session.AccessToken = "0";
            session.ClientToken = "0";

            if (!string.IsNullOrWhiteSpace(preferredUuid))
                session.UUID = preferredUuid;

            return session;
        }

        private async Task<(bool Success, MSession? Session, bool IsOfflineSession, bool RequiresElyByInjector)> TryCreateSessionAsync(LogWindow? logWindow)
        {
            if (AccountSelector.SelectedItem is ComboBoxItem item && item.Tag is AccountData acc)
            {
                if (acc.Type == "Microsoft")
                {
                    var microsoftSession = await TryRefreshMicrosoftSessionAsync(acc, logWindow);
                    if (microsoftSession != null)
                    {
                        return (true, microsoftSession, false, false);
                    }

                    if (!string.IsNullOrWhiteSpace(acc.AccessToken) && !string.IsNullOrWhiteSpace(acc.UUID))
                    {
                        WriteLog(logWindow, T("launch.log.ms_saved_token"));
                        return (true, CreateMicrosoftSession(acc.Username, acc.AccessToken, acc.UUID), false, false);
                    }

                    if (!NetworkInterface.GetIsNetworkAvailable())
                    {
                        WriteLog(logWindow, T("launch.log.ms_offline"));
                        return (true, CreateOfflineSession(acc.Username, acc.UUID), true, false);
                    }

                    await RayMessageBox.ShowAsync(this, T("launch.message.session_expired"), T("launch.message.error_title"));
                    return (false, null, false, false);
                }

                if (acc.Type == "ElyBy")
                {
                    var elySession = await TryRefreshElyBySessionAsync(acc, logWindow);
                    if (elySession != null)
                        return (true, elySession, false, true);

                    if (!string.IsNullOrWhiteSpace(acc.AccessToken) && !string.IsNullOrWhiteSpace(acc.UUID))
                        return (true, CreateElyBySession(acc.Username, acc.AccessToken, acc.UUID, acc.ClientToken), false, true);

                    return (false, null, false, true);
                }

                return (true, await CreateOfflineSessionAsync(acc.Username, logWindow), true, settings.EnableElyBySkins);
            }

            string manualNick = NormalizeNickname((AccountSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "");
            if (string.IsNullOrEmpty(manualNick))
            {
                await RayMessageBox.ShowAsync(this, T("launch.message.enter_nick"), T("launch.message.error_title"));
                return (false, null, false, false);
            }

            return (true, await CreateOfflineSessionAsync(manualNick, logWindow), true, settings.EnableElyBySkins);
        }

        private async Task<MSession?> TryRefreshMicrosoftSessionAsync(AccountData account, LogWindow? logWindow)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return null;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(microsoftAccountsPath)!);

                var loginHandler = new JELoginHandlerBuilder()
                    .WithAccountManager(microsoftAccountsPath)
                    .Build();

                string identifier = !string.IsNullOrWhiteSpace(account.MicrosoftAccountIdentifier)
                    ? account.MicrosoftAccountIdentifier
                    : account.UUID;

                if (string.IsNullOrWhiteSpace(identifier))
                    return null;

                XboxGameAccountCollection accounts = loginHandler.AccountManager.GetAccounts();
                if (!accounts.TryGetAccount(identifier, out IXboxGameAccount? storedAccount) || storedAccount == null)
                {
                    WriteLog(logWindow, T("launch.log.ms_refresh_missing"));
                    return null;
                }

                MSession refreshed = await loginHandler.AuthenticateSilently(storedAccount);
                loginHandler.AccountManager.SaveAccounts();
                refreshed.UserType = "msa";

                account.Username = refreshed.Username ?? account.Username;
                account.UUID = refreshed.UUID ?? account.UUID;
                account.AccessToken = refreshed.AccessToken ?? account.AccessToken;
                account.MicrosoftAccountIdentifier = account.UUID;

                SaveSettings();
                WriteLog(logWindow, T("launch.log.ms_refreshed"));
                return refreshed;
            }
            catch (Exception ex)
            {
                WriteLog(logWindow, T("launch.log.ms_refresh_failed", ex.Message));
                return null;
            }
        }

        private async Task<MSession> CreateOfflineSessionAsync(string username, LogWindow? logWindow)
        {
            string normalizedUsername = NormalizeNickname(username);
            if (!settings.EnableElyBySkins || string.IsNullOrWhiteSpace(normalizedUsername) || !NetworkInterface.GetIsNetworkAvailable())
                return CreateOfflineSession(normalizedUsername);

            try
            {
                WriteLog(logWindow, T("launch.log.elyby_profile", normalizedUsername));

                using var response = await apiClient.GetAsync($"{ElyByProfileApiBaseUrl}/api/users/profiles/minecraft/{Uri.EscapeDataString(normalizedUsername)}");
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    WriteLog(logWindow, T("launch.log.elyby_profile_missing"));
                    return CreateOfflineSession(normalizedUsername);
                }

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var profile = JsonSerializer.Deserialize<ElyByProfileResponse>(json, jsonOptions);
                if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
                {
                    WriteLog(logWindow, T("launch.log.elyby_profile_missing"));
                    return CreateOfflineSession(normalizedUsername);
                }

                string profileName = string.IsNullOrWhiteSpace(profile.Name) ? normalizedUsername : profile.Name;
                WriteLog(logWindow, T("launch.log.elyby_profile_found", profileName, profile.Id));
                return CreateOfflineSession(profileName, profile.Id);
            }
            catch (Exception ex)
            {
                WriteLog(logWindow, T("launch.log.elyby_profile_failed", ex.Message));
                return CreateOfflineSession(normalizedUsername);
            }
        }

        private async Task<MSession?> TryRefreshElyBySessionAsync(AccountData account, LogWindow? logWindow)
        {
            if (!NetworkInterface.GetIsNetworkAvailable() || string.IsNullOrWhiteSpace(account.AccessToken))
                return null;

            try
            {
                string clientToken = string.IsNullOrWhiteSpace(account.ClientToken)
                    ? Guid.NewGuid().ToString("N")
                    : account.ClientToken;

                string payload = JsonSerializer.Serialize(new
                {
                    accessToken = account.AccessToken,
                    clientToken,
                    requestUser = true
                });

                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var response = await apiClient.PostAsync($"{ElyByProfileApiBaseUrl}/auth/refresh", content);
                string json = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();

                using JsonDocument document = JsonDocument.Parse(json);
                string accessToken = document.RootElement.GetProperty("accessToken").GetString() ?? "";
                JsonElement selectedProfile = document.RootElement.GetProperty("selectedProfile");
                string uuid = selectedProfile.GetProperty("id").GetString() ?? "";
                string username = selectedProfile.GetProperty("name").GetString() ?? "";

                if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(username))
                    throw new InvalidOperationException(T("elyby.invalid_response"));

                account.AccessToken = accessToken;
                account.ClientToken = clientToken;
                account.UUID = uuid;
                account.Username = username;
                SaveSettings();

                return CreateElyBySession(username, accessToken, uuid, clientToken);
            }
            catch (Exception ex)
            {
                WriteLog(logWindow, T("elyby.refresh_failed", ex.Message));
                return null;
            }
        }

        private async Task<bool> EnsureElyByAuthlibInjectorAsync(LogWindow? logWindow, bool respectSkinToggle)
        {
            if (respectSkinToggle && !settings.EnableElyBySkins)
                return false;

            try
            {
                if (File.Exists(ElyByAuthlibInjectorPath))
                {
                    WriteLog(logWindow, T("launch.log.elyby_injector_ready"));
                    return true;
                }

                if (!NetworkInterface.GetIsNetworkAvailable())
                    return false;

                Directory.CreateDirectory(ElyByToolsFolder);
                WriteLog(logWindow, T("launch.log.elyby_injector_downloading"));

                string releaseJson = await apiClient.GetStringAsync(AuthlibInjectorLatestReleaseApiUrl);
                var release = JsonSerializer.Deserialize<GitHubReleaseInfo>(releaseJson, jsonOptions) ?? new GitHubReleaseInfo();
                var asset = release.Assets.FirstOrDefault(a =>
                    a.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) &&
                    a.Name.Contains("authlib-injector", StringComparison.OrdinalIgnoreCase) &&
                    !a.Name.Contains("sources", StringComparison.OrdinalIgnoreCase) &&
                    !a.Name.Contains("javadoc", StringComparison.OrdinalIgnoreCase));

                if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
                    throw new InvalidOperationException("Не найден .jar файл authlib-injector в последнем релизе.");

                using var stream = await apiClient.GetStreamAsync(asset.BrowserDownloadUrl);
                using var file = File.Create(ElyByAuthlibInjectorPath);
                await stream.CopyToAsync(file);

                WriteLog(logWindow, T("launch.log.elyby_injector_ready"));
                return true;
            }
            catch (Exception ex)
            {
                WriteLog(logWindow, T("launch.log.elyby_injector_failed", ex.Message));
                return false;
            }
        }

        private static string NormalizeNickname(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            return raw.Replace("🔑", "").Replace("🦋", "").Replace("👤", "").Trim();
        }

        private async void LoadVersions()
        {
            if (launcher == null) return;

            LaunchBtn.IsEnabled = false;
            VersionBox.Items.Clear();

            string versionsDirPath = Path.Combine(settings.MinecraftPath, "versions");
            var localVersions = new List<string>();

            if (Directory.Exists(versionsDirPath))
            {
                localVersions = Directory.GetDirectories(versionsDirPath)
                                        .Select(Path.GetFileName)
                                        .Where(x => x != null)
                                        .ToList()!;
            }

            try
            {
                var allMetadata = await launcher.GetAllVersionsAsync();

                if (settings.ShowModded)
                {
                    foreach (var localName in localVersions)
                    {
                        if (!allMetadata.Any(v => v.Name == localName))
                        {
                            AddVersionToBox(localName, "custom", versionsDirPath);
                        }
                    }
                }

                foreach (var v in allMetadata)
                {
                    if (v.Type == "release" && !settings.ShowReleases) continue;
                    if (v.Type == "snapshot" && !settings.ShowSnapshots) continue;

                    AddVersionToBox(v.Name, v.Type, versionsDirPath);
                }
            }
            catch
            {
                if (Directory.Exists(versionsDirPath))
                {
                    foreach (var localName in localVersions)
                    {
                        AddVersionToBox(localName, "local", versionsDirPath);
                    }
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(settings.LastVersion))
                {
                    foreach (var rawItem in VersionBox.Items)
                    {
                        if (rawItem is ComboBoxItem item && string.Equals(item.Tag?.ToString(), settings.LastVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            VersionBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                if (VersionBox.SelectedIndex == -1 && VersionBox.Items.Count > 0)
                    VersionBox.SelectedIndex = 0;

                LaunchBtn.IsEnabled = true;
            }
        }

        private void VersionBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (VersionBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                settings.LastVersion = tag;
                SaveSettings();
            }
        }

        private void AddVersionToBox(string name, string? type, string dir)
        {
            if (VersionBox.Items.Cast<ComboBoxItem>().Any(i => i.Content?.ToString() == name)) return;

            var item = new ComboBoxItem
            {
                Content = name,
                Tag = name
            };

            string currentVersionPath = Path.Combine(dir, name);

            if (Directory.Exists(currentVersionPath))
            {
                item.Foreground = Brush.Parse("#BB86FC");
                item.FontWeight = FontWeight.Bold;
            }
            else
            {
                item.Foreground = Brushes.Gray;
            }

            VersionBox.Items.Add(item);
        }

        #region Discord RPC
        private void InitDiscordRPC()
        {
            try
            {
                if (!settings.ShowDiscordStatus) return;

                discordClient = discordClientID;
                discordClient.Initialize();
                SetDiscordStatus(T("discord.state.launcher"), T("discord.details.choosing_version"));
            }
            catch { }
        }

        public void SetDiscordStatus(string state, string? details = null)
        {
            if (settings?.ShowDiscordStatus == false || discordClient == null) return;

            string imageKey = "logo";
            string finalState = state;

            try
            {
                discordClient.SetPresence(new RichPresence()
                {
                    Details = details,
                    State = finalState,
                    Assets = new DiscordRPC.Assets()
                    {
                        LargeImageKey = imageKey,
                        LargeImageText = T("discord.image_text"),
                        SmallImageKey = "icon_play"
                    },
                    Timestamps = Timestamps.Now
                });
            }
            catch
            {
            }
        }
        #endregion

        #region System Tray
        private void InitTrayIcon()
        {
            try
            {
                var menu = new NativeMenu();
                var openItem = new NativeMenuItem(T("tray.open"));
                openItem.Click += (s, e) => Dispatcher.UIThread.Post(ToggleWindow);
                var exitItem = new NativeMenuItem(T("tray.exit"));
                exitItem.Click += (s, e) => Dispatcher.UIThread.Post(ShutdownApp);

                menu.Add(openItem);
                menu.Add(new NativeMenuItemSeparator());
                menu.Add(exitItem);

                trayIcon = new TrayIcon
                {
                    ToolTipText = "Raytolfas Launcher",
                    Menu = menu,
                    IsVisible = true
                };

                var iconUri = new Uri("avares://RaytolfasLauncher/Assets/logo.ico");
                if (AssetLoader.Exists(iconUri))
                {
                    trayIcon.Icon = new WindowIcon(AssetLoader.Open(iconUri));
                }

                trayIcon.Clicked += (s, e) => Dispatcher.UIThread.Post(ToggleWindow);

                var icons = TrayIcon.GetIcons(Application.Current!) ?? new TrayIcons();
                icons.Add(trayIcon);
                TrayIcon.SetIcons(Application.Current!, icons);
            }
            catch
            {
            }
        }

        private void ToggleWindow()
        {
            if (isShuttingDown)
                return;

            if (this.IsVisible && this.WindowState != WindowState.Minimized)
            {
                this.Hide();
            }
            else
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            }
        }

        private void ShutdownApp()
        {
            if (isShuttingDown)
                return;

            isShuttingDown = true;
            discordClient?.Dispose();
            DisposeTrayIcon();

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Environment.Exit(0);
            }
        }
        #endregion

        private async void OpenAccountManager_Click(object? sender, RoutedEventArgs e)
        {
            var accWin = new AccountWindow(settings);
            bool? res = await accWin.ShowDialog<bool?>(this);
            if (res == true)
            {
                UpdateAccountList();
                SaveSettings();
            }
        }

        private async void LoadServers_Click(object? sender, RoutedEventArgs e)
        {
            ServerModal.IsVisible = true;
            SetModCenterStatus("");

            if (LoaderGameVersionBox.Items.Count <= 1)
                await RefreshLoaderDataAsync();
        }

        private async void OpenModpacksWindow_Click(object? sender, RoutedEventArgs e)
        {
            var win = new ModpacksWindow(settings.MinecraftPath, currentLanguage, launcher);
            var res = await win.ShowDialog<bool?>(this);
            if (res == true && win.SelectedModpack != null)
            {
                settings.LastVersion = win.SelectedModpack.Id;
                SaveSettings();
                LoadVersions();
                ServerModal.IsVisible = false;
            }
            else
            {
                LoadVersions();
            }
        }

        private void SetModCenterStatus(string text)
        {
            ModCenterStatusText.Text = text;
        }

        private void SetModCenterBusy(bool isBusy, string status)
        {
            ModCenterProgress.IsVisible = isBusy;
            ModCenterProgress.IsIndeterminate = isBusy;
            SetModCenterStatus(status);
        }

        private async Task RefreshLoaderDataAsync()
        {
            SetModCenterBusy(true, T("modcenter.status.loading_loaders"));

            try
            {
                string selectedLoader = GetSelectedComboTag(LoaderTypeBox, "fabric");
                if (selectedLoader == "forge")
                    await LoadForgeInstallOptionsAsync();
                else
                    await LoadFabricInstallOptionsAsync();
            }
            catch (Exception ex)
            {
                await RayMessageBox.ShowAsync(this, T("modcenter.message.loader_error", ex.Message), T("modcenter.title"));
                SetModCenterStatus(T("modcenter.status.loader_failed"));
            }
            finally
            {
                SetModCenterBusy(false, ModCenterStatusText.Text ?? "");
            }
        }

        private async Task LoadFabricInstallOptionsAsync()
        {
            if (fabricInstaller == null)
                return;

            LoaderGameVersionBox.Items.Clear();
            LoaderVersionBox.Items.Clear();
            availableLoaderOptions.Clear();

            var supportedVersions = await fabricInstaller.GetSupportedVersionNames();
            foreach (string version in supportedVersions.OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase))
            {
                LoaderGameVersionBox.Items.Add(new ComboBoxItem { Content = version, Tag = version });
            }

            if (LoaderGameVersionBox.Items.Count > 0)
                LoaderGameVersionBox.SelectedIndex = 0;

            SetModCenterStatus(T("modcenter.status.fabric_ready"));
            await LoadSelectedLoaderVersionsAsync();
        }

        private async Task LoadForgeInstallOptionsAsync()
        {
            if (launcher == null)
                return;

            LoaderGameVersionBox.Items.Clear();
            LoaderVersionBox.Items.Clear();
            availableLoaderOptions.Clear();

            var vanillaVersions = await launcher.GetAllVersionsAsync();
            foreach (var version in vanillaVersions.Where(v => v.Type == "release").Select(v => v.Name).Distinct().OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase))
            {
                LoaderGameVersionBox.Items.Add(new ComboBoxItem { Content = version, Tag = version });
            }

            if (LoaderGameVersionBox.Items.Count > 0)
                LoaderGameVersionBox.SelectedIndex = 0;

            SetModCenterStatus(T("modcenter.status.forge_ready"));
            await LoadSelectedLoaderVersionsAsync();
        }

        private async Task LoadSelectedLoaderVersionsAsync()
        {
            LoaderVersionBox.Items.Clear();
            availableLoaderOptions.Clear();

            string loaderType = GetSelectedComboTag(LoaderTypeBox, "fabric");
            string gameVersion = GetSelectedComboTag(LoaderGameVersionBox, "");
            if (string.IsNullOrWhiteSpace(gameVersion))
                return;

            SetModCenterBusy(true, T("modcenter.status.loading_mod_loader", loaderType, gameVersion));

            try
            {
                if (loaderType == "forge")
                {
                    var forgeInstaller = new ForgeInstaller(launcher!);
                    var forgeVersions = await forgeInstaller.GetForgeVersions(gameVersion);
                    foreach (var forgeVersion in forgeVersions)
                    {
                        availableLoaderOptions.Add(new LoaderInstallOption
                        {
                            LoaderType = "Forge",
                            GameVersion = gameVersion,
                            LoaderVersion = forgeVersion.ForgeVersionName,
                            VersionId = forgeVersion.ForgeVersionName,
                            IsLatest = forgeVersion.IsLatestVersion,
                            IsRecommended = forgeVersion.IsRecommendedVersion
                        });
                    }
                }
                else
                {
                    var loaders = await fabricInstaller!.GetLoaders(gameVersion);
                    foreach (var loader in loaders.OrderByDescending(l => l.Stable).ThenByDescending(l => l.Build))
                    {
                        if (string.IsNullOrWhiteSpace(loader.Version))
                            continue;

                        availableLoaderOptions.Add(new LoaderInstallOption
                        {
                            LoaderType = "Fabric",
                            GameVersion = gameVersion,
                            LoaderVersion = loader.Version,
                            VersionId = FabricInstaller.GetVersionName(gameVersion, loader.Version),
                            IsLatest = false,
                            IsRecommended = loader.Stable
                        });
                    }
                }

                foreach (var option in availableLoaderOptions)
                {
                    LoaderVersionBox.Items.Add(new ComboBoxItem { Content = option.DisplayName, Tag = option });
                }

                if (LoaderVersionBox.Items.Count > 0)
                    LoaderVersionBox.SelectedIndex = 0;

                SetModCenterStatus(LoaderVersionBox.Items.Count == 0
                    ? T("modcenter.status.loader_none")
                    : T("modcenter.status.loader_count", LoaderVersionBox.Items.Count));
            }
            finally
            {
                SetModCenterBusy(false, ModCenterStatusText.Text ?? "");
            }
        }

        private async void RefreshLoaderVersions_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                await RefreshLoaderDataAsync();
            }
            catch (Exception ex)
            {
                await RayMessageBox.ShowAsync(this, T("modcenter.message.refresh_error", ex.Message), T("modcenter.title"));
            }
        }

        private async void LoaderTypeBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            await RefreshLoaderDataAsync();
        }

        private async void LoaderGameVersionBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            await LoadSelectedLoaderVersionsAsync();
        }

        private async void InstallSelectedLoader_Click(object? sender, RoutedEventArgs e)
        {
            if (LoaderVersionBox.SelectedItem is not ComboBoxItem item || item.Tag is not LoaderInstallOption option || launcher == null)
            {
                await RayMessageBox.ShowAsync(this, T("modcenter.message.select_loader"), T("modcenter.title"));
                return;
            }

            SetModCenterBusy(true, T("modcenter.status.installing_loader", option.LoaderType, option.GameVersion));

            try
            {
                string installedVersion;
                if (option.LoaderType == "Forge")
                {
                    var forgeInstaller = new ForgeInstaller(launcher);
                    installedVersion = await forgeInstaller.Install(option.GameVersion, option.LoaderVersion, new ForgeInstallOptions());
                }
                else
                {
                    installedVersion = await fabricInstaller!.Install(option.GameVersion, option.LoaderVersion, new MinecraftPath(settings.MinecraftPath));
                }

                await launcher.InstallAsync(installedVersion);

                settings.LastVersion = installedVersion;
                SaveSettings();
                LoadVersions();
                SetModCenterStatus(T("modcenter.status.loader_installed", option.LoaderType, installedVersion));
                await RayMessageBox.ShowAsync(this, T("modcenter.message.loader_installed", installedVersion), T("settings.saved.title"));
            }
            catch (Exception ex)
            {
                SetModCenterStatus(T("modcenter.status.loader_install_failed"));
                await RayMessageBox.ShowAsync(this, T("modcenter.message.loader_install_failed", ex.Message), T("modcenter.title"));
            }
            finally
            {
                SetModCenterBusy(false, ModCenterStatusText.Text ?? "");
            }
        }

        private async void ImportMrPack_Click(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выберите .mrpack файл",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Modrinth Pack") { Patterns = new[] { "*.mrpack" } }
                }
            });

            if (files.Count > 0)
                await InstallMrPackAsync(files[0].Path.LocalPath);
        }

        private async Task InstallMrPackAsync(string filePath, string? displayNameOverride = null)
        {
            if (launcher == null)
                return;

            SetModCenterBusy(true, T("modcenter.status.installing_pack"));

            try
            {
                using var archive = ZipFile.OpenRead(filePath);
                var indexEntry = archive.GetEntry("modrinth.index.json") ?? throw new InvalidOperationException(T("modcenter.message.pack_missing_index"));
                using var indexStream = indexEntry.Open();
                var packIndex = await JsonSerializer.DeserializeAsync<ModrinthPackIndex>(indexStream, jsonOptions) ?? throw new InvalidOperationException(T("modcenter.message.pack_invalid_index"));

                string installedVersion = await InstallPackDependenciesAsync(packIndex);
                await InstallPackFilesAsync(archive, packIndex);

                settings.LastVersion = installedVersion;
                SaveSettings();
                LoadVersions();

                string packName = string.IsNullOrWhiteSpace(displayNameOverride) ? packIndex.Name : displayNameOverride;
                SetModCenterStatus(T("modcenter.status.pack_installed", packName, installedVersion));
                await RayMessageBox.ShowAsync(
                    this,
                    T("modcenter.message.pack_installed_body", installedVersion, settings.MinecraftPath),
                    T("modcenter.message.pack_installed_title"));
            }
            catch (Exception ex)
            {
                SetModCenterStatus(ex.Message);
                await RayMessageBox.ShowAsync(this, ex.Message, T("launch.message.error_title"));
            }
            finally
            {
                SetModCenterBusy(false, ModCenterStatusText.Text ?? "");
            }
        }

        private async Task<string> InstallPackDependenciesAsync(ModrinthPackIndex packIndex)
        {
            if (launcher == null)
                throw new InvalidOperationException(T("modcenter.message.launcher_missing"));

            string minecraftVersion = packIndex.Dependencies.TryGetValue("minecraft", out string? mcVersion) ? mcVersion : "";
            if (string.IsNullOrWhiteSpace(minecraftVersion))
                throw new InvalidOperationException(T("modcenter.message.dependency_minecraft_missing"));

            if (packIndex.Dependencies.TryGetValue("forge", out string? forgeVersion) && !string.IsNullOrWhiteSpace(forgeVersion))
            {
                var forgeInstaller = new ForgeInstaller(launcher);
                string installedVersion = await forgeInstaller.Install(minecraftVersion, forgeVersion, new ForgeInstallOptions());
                await launcher.InstallAsync(installedVersion);
                return installedVersion;
            }

            if (packIndex.Dependencies.TryGetValue("fabric-loader", out string? fabricVersion) && !string.IsNullOrWhiteSpace(fabricVersion))
            {
                string installedVersion = await fabricInstaller!.Install(minecraftVersion, fabricVersion, new MinecraftPath(settings.MinecraftPath));
                await launcher.InstallAsync(installedVersion);
                return installedVersion;
            }

            if (packIndex.Dependencies.ContainsKey("quilt-loader") || packIndex.Dependencies.ContainsKey("neoforge"))
                throw new InvalidOperationException(T("modcenter.message.unsupported_pack_loader"));

            await launcher.InstallAsync(minecraftVersion);
            return minecraftVersion;
        }

        private async Task InstallPackFilesAsync(ZipArchive archive, ModrinthPackIndex packIndex)
        {
            string rootPath = settings.MinecraftPath;
            Directory.CreateDirectory(rootPath);

            foreach (var folderName in new[] { "overrides/", "client-overrides/" })
            {
                foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith(folderName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(e.Name)))
                {
                    string relativePath = entry.FullName.Substring(folderName.Length);
                    if (!IsSafeRelativePath(relativePath))
                        continue;

                    string targetPath = Path.Combine(rootPath, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    using var source = entry.Open();
                    using var target = File.Create(targetPath);
                    await source.CopyToAsync(target);
                }
            }

            int completed = 0;
            var clientFiles = packIndex.Files.Where(file => file.Environment?.Client != "unsupported").ToList();
            int total = clientFiles.Count;
            foreach (var file in clientFiles)
            {
                if (!IsSafeRelativePath(file.Path))
                    throw new InvalidOperationException($"Pack содержит небезопасный путь: {file.Path}");

                string downloadUrl = file.Downloads.FirstOrDefault() ?? throw new InvalidOperationException($"У файла {file.Path} нет download URL.");
                string targetPath = Path.Combine(rootPath, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                SetModCenterStatus(T("modcenter.status.pack_file_download", file.Path));
                using var source = await apiClient.GetStreamAsync(downloadUrl);
                using var target = File.Create(targetPath);
                await source.CopyToAsync(target);

                completed++;
                ModCenterStatusText.Text = T("modcenter.status.pack_file_progress", completed, total);
            }
        }

        private static bool IsSafeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (path.Contains("..", StringComparison.Ordinal))
                return false;

            if (Path.IsPathRooted(path))
                return false;

            return true;
        }

        private string? TryGetMinecraftCrashHint()
        {
            try
            {
                string crashReportsPath = Path.Combine(settings.MinecraftPath, "crash-reports");
                if (Directory.Exists(crashReportsPath))
                {
                    string? latestCrashReport = Directory.GetFiles(crashReportsPath, "*.txt")
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(latestCrashReport))
                    {
                        DateTime reportTime = File.GetLastWriteTimeUtc(latestCrashReport);
                        if ((DateTime.UtcNow - reportTime).TotalMinutes <= 10)
                        {
                            foreach (string line in File.ReadLines(latestCrashReport))
                            {
                                string trimmed = line.Trim();
                                if (trimmed.StartsWith("Description:", StringComparison.OrdinalIgnoreCase))
                                    return trimmed;

                                if (trimmed.StartsWith("Caused by:", StringComparison.OrdinalIgnoreCase))
                                    return trimmed;
                            }
                        }
                    }
                }

                string latestLogPath = Path.Combine(settings.MinecraftPath, "logs", "latest.log");
                if (!File.Exists(latestLogPath))
                    return null;

                string[] tail = File.ReadLines(latestLogPath).Reverse().Take(80).ToArray();
                foreach (string line in tail)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        continue;

                    if (trimmed.Contains("Caused by:", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.Contains("failed", StringComparison.OrdinalIgnoreCase))
                    {
                        return trimmed.Length > 320 ? trimmed.Substring(0, 320) + "..." : trimmed;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string GetSelectedComboTag(ComboBox comboBox, string fallback)
        {
            if (comboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                return tag;

            return fallback;
        }

        private void OpenDonateWeb_Click(object? sender, RoutedEventArgs e)
        {
            PlatformHelper.OpenBrowser("https://ko-fi.com/mixitosik");
        }

        private async void SelectBackground_Click(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = T("settings.background"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp" }
                    }
                }
            });

            if (files.Count > 0)
            {
                string localPath = files[0].Path.LocalPath;
                BgPathBox.Text = localPath;
                SetCustomBackground(localPath);
            }
        }

        private void FolderButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Avalonia.Controls.Button btn)
            {
                btn.Flyout?.ShowAt(btn);
            }
        }

        private void OpenRootFolder_Click(object? sender, RoutedEventArgs e) => OpenDir("");
        private void OpenModsFolder_Click(object? sender, RoutedEventArgs e) => OpenDir("mods");
        private void OpenSavesFolder_Click(object? sender, RoutedEventArgs e) => OpenDir("saves");
        private void OpenScreenshotsFolder_Click(object? sender, RoutedEventArgs e) => OpenDir("screenshots");

        private void OpenDir(string subDir)
        {
            string minecraftPath = string.IsNullOrWhiteSpace(settings.MinecraftPath)
                ? PlatformHelper.GetDefaultMinecraftFolder()
                : settings.MinecraftPath;

            string fullPath = string.IsNullOrWhiteSpace(subDir)
                ? minecraftPath
                : Path.Combine(minecraftPath, subDir);

            if (Directory.Exists(fullPath))
            {
                PlatformHelper.OpenFolder(fullPath);
            }
            else
            {
                string folderName = string.IsNullOrWhiteSpace(subDir) ? T("folders.root") : T("folders.named", subDir);
                RayMessageBox.Show(T("folders.not_created", folderName), T("window.title"), this);
            }
        }

        private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            isShuttingDown = true;
            discordClient?.Dispose();
            DisposeTrayIcon();
        }

        private void DisposeTrayIcon()
        {
            if (trayIcon == null)
                return;

            try
            {
                trayIcon.IsVisible = false;
                var icons = TrayIcon.GetIcons(Application.Current!);
                icons?.Remove(trayIcon);
            }
            catch
            {
            }
        }

        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            if (e.Source is Visual source && PlatformHelper.IsInteractiveElement(source))
                return;

            BeginMoveDrag(e);
        }

        private void CloseBtn_Click(object? sender, RoutedEventArgs e) => ShutdownApp();
        private void SettingsBtn_Click(object? sender, RoutedEventArgs e) => SettingsModal.IsVisible = true;
        private void Refresh_Click(object? sender, RoutedEventArgs e) => LoadVersions();
        private void CloseServerModal_Click(object? sender, RoutedEventArgs e) => ServerModal.IsVisible = false;
        private void CloseSettings_Click(object? sender, RoutedEventArgs e) => SettingsModal.IsVisible = false;

        private async void SelectPath_Click(object? sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = T("settings.game_folder"),
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                PathBox.Text = folders[0].Path.LocalPath;
            }
        }
    }
}
