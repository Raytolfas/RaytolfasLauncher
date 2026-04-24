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
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Diagnostics;
using System.Net.NetworkInformation;
using WpfMessageBox = System.Windows.MessageBox;
using WpfApplication = System.Windows.Application;
using DiscordRPC;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Installers;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installer.Forge;
using XboxAuthNet.Game.Accounts;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace RaytolfasLauncher
{
    public partial class MainWindow : Window
    {
        public LauncherSettings Settings { get; set; } = new LauncherSettings();
        private MinecraftLauncher? launcher;
        private LauncherSettings settings = new LauncherSettings();
        private readonly DiscordRpcClient discordClientID = new DiscordRpcClient("1472589510742118400");
        private readonly string currentVersion = "0.0.3";
        private readonly string updateUrl = "https://github.com/Raytolfas/RaytolfasLauncherAssets/raw/refs/heads/main/Updates/latest.json";
        private const string ElyByProfileApiBaseUrl = "https://authserver.ely.by";
        private const string AuthlibInjectorLatestReleaseApiUrl = "https://api.github.com/repos/yushijinhun/authlib-injector/releases/latest";
        private string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Raytolfas", "Raytolfas Launcher");
        private string legacySettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RaytolfasLauncher");
        private string settingsPath = string.Empty;
        private string legacySettingsPath = string.Empty;
        private string microsoftAccountsPath = string.Empty;
        private string legacyMicrosoftAccountsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "cml_accounts.json");
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
                    
                    AccountSelector.IsEditable = false; 

                    string mcPath = string.IsNullOrWhiteSpace(settings?.MinecraftPath) 
                        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft")
                        : settings.MinecraftPath;

                    launcher = new MinecraftLauncher(new MinecraftPath(mcPath));

                    UpdateAccountList();
                    LoadVersions();
                    LoadAvatar();
                    InitializeModCenterDefaults();

                    InitDiscordRPC();
                    this.Loaded += async (s, e) => {
                        await CheckForUpdates();
                    };
                }
                catch (Exception ex)
                {
                    string startupLanguage = LocalizationManager.NormalizeLanguage(settings.Language);
                    System.Windows.MessageBox.Show(LocalizationManager.Get("launch.startup_critical", startupLanguage)
                        .Replace("{0}", ex.Message)
                        .Replace("{1}", ex.StackTrace ?? ""));
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

            foreach (ComboBoxItem item in LanguageBox.Items)
            {
                if (string.Equals(item.Tag?.ToString(), selectedLanguage, StringComparison.OrdinalIgnoreCase))
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
            AccountSectionLabel.Content = T("main.account");
            VersionSectionLabel.Content = T("main.version");
            CheckBoxForce.Content = T("main.reinstall_files");
            LaunchBtn.Content = T("main.play");
            StatusLabel.Text = T("main.status.preparing");

            SettingsTitleText.Text = T("settings.title");
            BackgroundLabel.Content = T("settings.background");
            SelectBackgroundButton.Content = T("settings.select");
            GameFolderLabel.Content = T("settings.game_folder");
            BrowsePathButton.Content = T("settings.browse");
            OptionsLabel.Content = T("settings.options");
            CbDiscordRPC.Content = T("settings.discord");
            CbHideLauncher.Content = T("settings.hide_launcher");
            CbShowAvatar.Content = T("settings.show_avatar");
            CbOpenLogWindow.Content = T("settings.open_logs");
            CbElyBySkins.Content = T("settings.elyby_skins");
            LanguageLabel.Content = T("settings.language");
            JavaProfileLabel.Content = T("settings.java_profile");
            JavaPathLabel.Content = T("settings.java_path");
            ResolutionLabel.Content = T("settings.resolution");
            CbFullScreen.Content = T("settings.fullscreen");
            JavaArgsLabel.Content = T("settings.java_args");
            VersionFiltersLabel.Content = T("settings.show_versions");
            CbReleases.Content = T("settings.releases");
            CbSnapshots.Content = T("settings.snapshots");
            CbModded.Content = T("settings.modded");
            RamLabel.Content = T("settings.ram");
            SaveSettingsButton.Content = T("settings.save");

            ModCenterTitleText.Text = T("modcenter.title");
            ModCenterSubtitleText.Text = T("modcenter.subtitle");
            LoadersTabButton.Content = T("modcenter.tab.loaders");
            ModrinthTabButton.Content = T("modcenter.tab.pack");
            TrayOpenMenuItem.Header = T("tray.open");
            TrayExitMenuItem.Header = T("tray.exit");
            RootFolderMenuItem.Header = T("folders.menu.root");
            ModsFolderMenuItem.Header = T("folders.menu.mods");
            SavesFolderMenuItem.Header = T("folders.menu.saves");
            ScreenshotsFolderMenuItem.Header = T("folders.menu.screenshots");
            LoaderTypeLabel.Text = T("modcenter.loader");
            LoaderGameVersionLabel.Text = T("modcenter.minecraft");
            LoaderVersionLabel.Text = T("modcenter.loader_build");
            RefreshLoaderButton.Content = T("modcenter.refresh");
            InstallLoaderButton.Content = T("modcenter.install_version");
            LoadersInfoTitleText.Text = T("modcenter.loaders.info_title");
            LoadersInfoLine1Text.Text = T("modcenter.loaders.info_line1");
            LoadersInfoLine2Text.Text = T("modcenter.loaders.info_line2");
            MrPackTitleText.Text = T("modcenter.pack.title");
            MrPackSubtitleText.Text = T("modcenter.pack.subtitle");

            ApplyAvatarVisibility();
            InitializeJavaProfiles();
            SetModCenterStatus(T("modcenter.status.ready"));
            if (settings.ShowDiscordStatus)
                SetDiscordStatus(T("discord.state.launcher"), T("discord.details.choosing_version"));
        }

        private void ApplyAvatarVisibility()
        {
            if (PlayerAvatarContainer == null)
                return;

            bool showAvatar = settings.ShowAccountAvatar;
            PlayerAvatarContainer.Visibility = showAvatar ? Visibility.Visible : Visibility.Collapsed;
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

        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            bool found = await CheckForUpdates(isManual: true);
        }

        private async Task<bool> CheckForUpdates(bool isManual = false)
        {
            string versionUrl = updateUrl;

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "RaytolfasLauncher");
                var json = await client.GetStringAsync(versionUrl);
                var info = System.Text.Json.JsonSerializer.Deserialize<UpdateInfo>(json);

                if (info != null)
                {
                    Version latestVersion = new Version(info.Version);
                    Version localVersion = new Version(currentVersion);

                    if (latestVersion > localVersion)
                    {
                        var upWin = new UpdateWindow(currentVersion, info.Version, info.Changelog, info.DownloadUrl, currentLanguage);
                        upWin.Owner = this;
                        upWin.ShowDialog();
                        return true;
                    }
                    else if (isManual)
                    {
                        System.Windows.MessageBox.Show(T("update.latest"), T("update.latest_title"));
                    }
                }
            }
            catch (Exception ex)
            {
                if (isManual)
                    System.Windows.MessageBox.Show(T("update.check_error", ex.Message), T("launch.message.error_title"));
            }
            return false;
        }

        public class UpdateInfo
        {
            public string Version { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public string Changelog { get; set; } = "";
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
                AccountSelector.Items.Add(new ComboBoxItem { 
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

        private void AccountSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

        private void UpdateAvatar(string username)
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
                    PlayerAvatar.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/no_internet.png"));
                    return;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri($"https://minotar.net/avatar/{username}/32", UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                PlayerAvatar.Source = bitmap;
            }
            catch
            {
                PlayerAvatar.Source = new BitmapImage(new Uri("https://minotar.net/avatar/char/32", UriKind.Absolute));
            }
        }

        private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
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

            DownloadPanel.Visibility = Visibility.Visible;
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
                    
                        string versionPath = System.IO.Path.Combine(settings.MinecraftPath, "versions", versionId);
                        string jarPath = System.IO.Path.Combine(versionPath, $"{versionId}.jar");

                        if (System.IO.File.Exists(jarPath))
                        {
                            System.IO.File.Delete(jarPath);
                            WriteLog(logWindow, T("launch.log.force_deleted"));
                        }
                    }
                }

                if (!System.IO.Directory.Exists(System.IO.Path.Combine(settings.MinecraftPath, "versions", versionId)))
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(settings.MinecraftPath, "versions", versionId));
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
                    Dispatcher.BeginInvoke(new Action(() =>
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
                    }), DispatcherPriority.Background);
                };

                byteProgressHandler = (s, args) =>
                {
                    if ((DateTime.UtcNow - lastByteUpdate).TotalMilliseconds < 250)
                        return;

                    lastByteUpdate = DateTime.UtcNow;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        int percent = (int)Math.Round(args.ToRatio() * 100);
                        StatusLabel.Text = T("main.status.downloading_files", percent);
                    }), DispatcherPriority.Background);
                };

                launcher.FileProgressChanged += fileProgressHandler;
                launcher.ByteProgressChanged += byteProgressHandler;

                if (isNetworkAvailable)
                {
                    await launcher.InstallAsync(versionId);
                }
                else
                {
                    string localVersionPath = System.IO.Path.Combine(settings.MinecraftPath, "versions", versionId);
                    if (!System.IO.Directory.Exists(localVersionPath))
                    {
                        WpfMessageBox.Show(T("launch.message.version_missing"));
                        DownloadPanel.Visibility = Visibility.Collapsed;
                        LaunchBtn.IsEnabled = true;
                        return;
                    }
                }

                var launchOption = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = (int)RamSlider.Value,

                    FullScreen = CbFullScreen.IsChecked ?? false,
                    ScreenWidth = int.TryParse(WinWidthBox.Text, out int w) ? w : 854,
                    ScreenHeight = int.TryParse(WinHeightBox.Text, out int h) ? h : 480,
                    
                    JavaPath = GetResolvedJavaPathForLaunch(),
                };

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

                DownloadPanel.Visibility = Visibility.Collapsed;
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
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (process.ExitCode != 0)
                        {
                            if (!string.IsNullOrWhiteSpace(crashHint))
                            {
                                WriteLog(logWindow, T("launch.log.crash_hint", crashHint));
                                WpfMessageBox.Show(T("launch.message.crashed_with_hint", process.ExitCode, crashHint));
                            }
                            else
                            {
                                WpfMessageBox.Show(T("launch.message.crashed", process.ExitCode));
                            }
                        }

                        if (settings.HideLauncherOnPlay || !this.IsVisible)
                            this.Show();

                        SetDiscordStatus(T("discord.state.launcher"), T("discord.details.choosing_version"));
                        WriteLog(logWindow, T("launch.log.game_closed"));
                        this.Focus();

                        DownloadPanel.Visibility = Visibility.Collapsed;
                        StatusLabel.Text = "";
                        LaunchBtn.IsEnabled = true;
                    }), DispatcherPriority.Background);
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
                WpfMessageBox.Show(T("launch.message.error", ex.Message), T("launch.message.error_title"));
                this.Show();
                DownloadPanel.Visibility = Visibility.Collapsed;
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
                MainBgImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/background.png"));
                Settings.CustomBackgroundPath = "";
            }
            else
            {
                MainBgImage.Source = new BitmapImage(new Uri(path));
                Settings.CustomBackgroundPath = path;
            }
        }

        private void LoadSettingsFromFile()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    settings = JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
                }
            }
            catch { settings = new LauncherSettings(); }

            settings.Language = LocalizationManager.NormalizeLanguage(settings.Language);
            currentLanguage = settings.Language;

            if (string.IsNullOrWhiteSpace(settings.MinecraftPath))
            {
                settings.MinecraftPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
            }

            if (string.IsNullOrEmpty(settings.CustomBackgroundPath) || !File.Exists(settings.CustomBackgroundPath))
            {
                MainBgImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/background.png"));
                BgPathBox.Text = ""; 
            }
            else
            {
                MainBgImage.Source = new BitmapImage(new Uri(settings.CustomBackgroundPath));
                BgPathBox.Text = settings.CustomBackgroundPath;
            }

            CbDiscordRPC.IsChecked = settings.ShowDiscordStatus;
            CbHideLauncher.IsChecked = settings.HideLauncherOnPlay;
            CbShowAvatar.IsChecked = settings.ShowAccountAvatar;
            CbOpenLogWindow.IsChecked = settings.OpenLogWindowOnLaunch;
            CbElyBySkins.IsChecked = settings.EnableElyBySkins;
            
            RamSlider.Value = settings.SelectedRam;
            PathBox.Text = settings.MinecraftPath;
            JavaArgsBox.Text = settings.JvmArgs;
            WinWidthBox.Text = settings.WindowWidth.ToString();
            WinHeightBox.Text = settings.WindowHeight.ToString();
            CbFullScreen.IsChecked = settings.IsFullScreen;
            CbReleases.IsChecked = settings.ShowReleases;
            CbSnapshots.IsChecked = settings.ShowSnapshots;
            CbModded.IsChecked = settings.ShowModded;

            InitializeJavaProfiles();
            ApplyAvatarVisibility();
        }

        private void MigrateLegacySettingsFile()
        {
            try
            {
                if (File.Exists(settingsPath) || !File.Exists(legacySettingsPath))
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
                if (File.Exists(microsoftAccountsPath) || !File.Exists(legacyMicrosoftAccountsPath))
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
            if (LoaderTypeBox.Items.Count == 0)
            {
                LoaderTypeBox.Items.Add(new ComboBoxItem { Content = "Fabric", Tag = "fabric" });
                LoaderTypeBox.Items.Add(new ComboBoxItem { Content = "Forge", Tag = "forge" });
            }

            LoaderTypeBox.SelectedIndex = 0;
            SetModCenterTab(showLoaders: true);
            SetModCenterStatus(T("modcenter.status.ready"));
        }

        private void InitializeJavaProfiles()
        {
            javaProfiles.Clear();
            javaProfiles.Add(new JavaProfile { Id = JavaProfileAutoId, Name = T("settings.java.auto"), JavaPath = null });
            javaProfiles.Add(new JavaProfile { Id = JavaProfileCustomId, Name = T("settings.java.custom"), JavaPath = settings.JavaPath });

            foreach (var path in DiscoverJavaPaths())
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

        private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageBox.SelectedItem is not ComboBoxItem item)
                return;

            currentLanguage = LocalizationManager.NormalizeLanguage(item.Tag?.ToString());
            ApplyLocalization();
        }

        private IEnumerable<string> DiscoverJavaPaths()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddPath(string? path)
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                string normalized = path.Trim('"');
                if (File.Exists(normalized)) paths.Add(normalized);
            }

            AddPath(settings.JavaPath);

            string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                AddPath(Path.Combine(javaHome, "bin", "javaw.exe"));
            }

            string[] roots =
            {
                @"C:\Program Files\Java",
                @"C:\Program Files (x86)\Java",
                @"C:\Program Files\Eclipse Adoptium",
                @"C:\Program Files\AdoptOpenJDK",
                @"C:\Program Files\Amazon Corretto",
                @"C:\Program Files\Microsoft",
                @"C:\Program Files\BellSoft\LibericaJDK",
                @"C:\Program Files\Zulu"
            };

            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;

                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    AddPath(Path.Combine(dir, "bin", "javaw.exe"));
                }
            }

            return paths;
        }

        private string? GetResolvedJavaPathForLaunch()
        {
            string explicitPath = (JavaPathBox.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(explicitPath))
                return explicitPath;

            var discovered = DiscoverJavaPaths()
                .OrderBy(path => IsLikely32BitJava(path))
                .ThenByDescending(path => string.Equals(path, settings.JavaPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return discovered.FirstOrDefault();
        }

        private static bool IsLikely32BitJava(string? javawPath)
        {
            if (string.IsNullOrWhiteSpace(javawPath))
                return false;

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
            var rootDir = Directory.GetParent(javawPath)?.Parent?.FullName;
            if (string.IsNullOrWhiteSpace(rootDir)) return null;

            string releasePath = Path.Combine(rootDir, "release");
            if (!File.Exists(releasePath)) return null;

            foreach (var line in File.ReadLines(releasePath))
            {
                if (line.StartsWith("JAVA_VERSION=", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length < 2) return null;
                    return parts[1].Trim().Trim('"');
                }
            }

            return null;
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

        private void JavaProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
                System.Windows.MessageBox.Show(T("settings.file_save_error", ex.Message));
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                if (string.IsNullOrWhiteSpace(PathBox.Text))
                {
                    PathBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
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
                        settings.JavaPath = JavaPathBox.Text.Trim();
                        profile.JavaPath = settings.JavaPath;
                    }
                    else
                    {
                        settings.JavaPath = profile.JavaPath ?? "";
                    }
                }
                else
                {
                    settings.JavaPath = JavaPathBox.Text.Trim();
                }

                settings.WindowWidth = int.Parse(WinWidthBox.Text);
                settings.WindowHeight = int.Parse(WinHeightBox.Text);
                settings.IsFullScreen = CbFullScreen.IsChecked ?? false;
                settings.JvmArgs = JavaArgsBox.Text;
                settings.SelectedRam = (int)RamSlider.Value;
                settings.MinecraftPath = PathBox.Text;
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
                settings.CustomBackgroundPath = BgPathBox.Text;

                SaveSettings();

                var mcPath = new MinecraftPath(settings.MinecraftPath);
                launcher = new MinecraftLauncher(mcPath);

                long totalMemory = (long)(new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory / 1024 / 1024);
                
                if ((long)RamSlider.Value > totalMemory) {
                    System.Windows.MessageBox.Show(T("settings.ram_warning"));
                }
                
                LoadVersions();
                SettingsModal.Visibility = Visibility.Collapsed;
                
                if (string.IsNullOrEmpty(settings.CustomBackgroundPath))
                {
                    MainBgImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/background.png"));
                }
                else 
                {
                    try {
                        MainBgImage.Source = new BitmapImage(new Uri(settings.CustomBackgroundPath));
                    } catch {}
                }

                ApplyLocalization();
                System.Windows.MessageBox.Show(T("settings.saved.message"), T("settings.saved.title"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(T("settings.save_error", ex.Message));
            }
        }

        private void CbOpenLogWindow_Checked(object sender, RoutedEventArgs e)
        {
            if (suppressOptionEvents)
                return;

            var result = WpfMessageBox.Show(
                T("settings.open_logs_confirm_message"),
                T("settings.open_logs_confirm_title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
                return;

            suppressOptionEvents = true;
            CbOpenLogWindow.IsChecked = false;
            suppressOptionEvents = false;
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

        private async Task<(bool Success, MSession? Session, bool IsOfflineSession, bool RequiresElyByInjector)> TryCreateSessionAsync(LogWindow? logWindow)
        {
            if (AccountSelector.SelectedItem is ComboBoxItem item && item.Tag is AccountData acc && AccountSelector.Text == item.Content.ToString())
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
                        return (true, new MSession(acc.Username, acc.AccessToken, acc.UUID), false, false);
                    }

                    if (!NetworkInterface.GetIsNetworkAvailable())
                    {
                        WriteLog(logWindow, T("launch.log.ms_offline"));
                        return (true, MSession.CreateOfflineSession(acc.Username), true, false);
                    }

                    WpfMessageBox.Show(T("launch.message.session_expired"));
                    return (false, null, false, false);
                }

                if (acc.Type == "ElyBy")
                {
                    var elySession = await TryRefreshElyBySessionAsync(acc, logWindow);
                    if (elySession != null)
                        return (true, elySession, false, true);

                    if (!string.IsNullOrWhiteSpace(acc.AccessToken) && !string.IsNullOrWhiteSpace(acc.UUID))
                        return (true, new MSession(acc.Username, acc.AccessToken, acc.UUID), false, true);

                    return (false, null, false, true);
                }

                return (true, await CreateOfflineSessionAsync(acc.Username, logWindow), true, settings.EnableElyBySkins);
            }

            string manualNick = NormalizeNickname(AccountSelector.Text);
            if (string.IsNullOrEmpty(manualNick))
            {
                WpfMessageBox.Show(T("launch.message.enter_nick"));
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
                return MSession.CreateOfflineSession(normalizedUsername);

            try
            {
                WriteLog(logWindow, T("launch.log.elyby_profile", normalizedUsername));

                using var response = await apiClient.GetAsync($"{ElyByProfileApiBaseUrl}/api/users/profiles/minecraft/{Uri.EscapeDataString(normalizedUsername)}");
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    WriteLog(logWindow, T("launch.log.elyby_profile_missing"));
                    return MSession.CreateOfflineSession(normalizedUsername);
                }

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var profile = JsonSerializer.Deserialize<ElyByProfileResponse>(json, jsonOptions);
                if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
                {
                    WriteLog(logWindow, T("launch.log.elyby_profile_missing"));
                    return MSession.CreateOfflineSession(normalizedUsername);
                }

                string profileName = string.IsNullOrWhiteSpace(profile.Name) ? normalizedUsername : profile.Name;
                WriteLog(logWindow, T("launch.log.elyby_profile_found", profileName, profile.Id));
                return new MSession(profileName, "0", profile.Id);
            }
            catch (Exception ex)
            {
                WriteLog(logWindow, T("launch.log.elyby_profile_failed", ex.Message));
                return MSession.CreateOfflineSession(normalizedUsername);
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

                return new MSession(username, accessToken, uuid);
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
                    foreach (ComboBoxItem item in VersionBox.Items)
                    {
                        if (item.Tag?.ToString() == settings.LastVersion)
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

        private void VersionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VersionBox.SelectedItem is ComboBoxItem item)
            {
                settings.LastVersion = item.Tag.ToString();
                SaveSettings();
            }
        }

        private void AddVersionToBox(string name, string? type, string dir)
        {
            if (VersionBox.Items.Cast<ComboBoxItem>().Any(i => i.Content.ToString() == name)) return;

            var item = new ComboBoxItem 
            { 
                Content = name, 
                Tag = name 
            };

            string currentVersionPath = Path.Combine(dir, name);

            if (Directory.Exists(currentVersionPath))
            {
                item.Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#BB86FC"));
                item.FontWeight = FontWeights.Bold;
            }
            else
            {
                item.Foreground = System.Windows.Media.Brushes.Gray;
            }

            VersionBox.Items.Add(item);
        }

        #region Discord RPC
        private void InitDiscordRPC()
        {
            try {
                if (!settings.ShowDiscordStatus) return;

                discordClient = discordClientID;
                discordClient.Initialize();
                SetDiscordStatus(T("discord.state.launcher"), T("discord.details.choosing_version"));
            } catch { }
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
                    Assets = new Assets() 
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
        private void ToggleWindow()
        {
            if (isShuttingDown || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
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
            System.Windows.Application.Current.Shutdown();
        }

        private void ToggleWindow_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindow();
        }

        private void ShutdownApp_Click(object sender, RoutedEventArgs e)
        {
            ShutdownApp();
        }
        #endregion

        private void OpenAccountManager_Click(object sender, RoutedEventArgs e)
        {
            var accWin = new AccountWindow(settings);
            accWin.Owner = this;
            if (accWin.ShowDialog() == true) { UpdateAccountList(); SaveSettings(); }
        }

        private async void LoadServers_Click(object sender, RoutedEventArgs e)
        {
            ServerModal.Visibility = Visibility.Visible;
            SetModCenterStatus(T("modcenter.status.ready"));

            if (LoaderGameVersionBox.Items.Count <= 1)
                await RefreshLoaderDataAsync();
        }

        private void ShowLoadersTab_Click(object sender, RoutedEventArgs e) => SetModCenterTab(showLoaders: true);
        private void ShowModrinthTab_Click(object sender, RoutedEventArgs e) => SetModCenterTab(showLoaders: false);

        private void SetModCenterTab(bool showLoaders)
        {
            if (LoadersTabButton == null || ModrinthTabButton == null)
                return;

            LoadersTabButton.Background = showLoaders ? new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#BB86FC")) : new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#2D2D2D"));
            LoadersTabButton.Foreground = showLoaders ? MediaBrushes.Black : MediaBrushes.White;
            ModrinthTabButton.Background = showLoaders ? new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#2D2D2D")) : new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#BB86FC"));
            ModrinthTabButton.Foreground = showLoaders ? MediaBrushes.White : MediaBrushes.Black;

            LoadersTabPanel.Visibility = showLoaders ? Visibility.Visible : Visibility.Collapsed;
            ModrinthTabPanel.Visibility = showLoaders ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SetModCenterStatus(string text)
        {
            ModCenterStatusText.Text = text;
        }

        private void SetModCenterBusy(bool isBusy, string status)
        {
            ModCenterProgress.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
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
                WpfMessageBox.Show(T("modcenter.message.loader_error", ex.Message));
                SetModCenterStatus(T("modcenter.status.loader_failed"));
            }
            finally
            {
                SetModCenterBusy(false, ModCenterStatusText.Text);
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
                SetModCenterBusy(false, ModCenterStatusText.Text);
            }
        }

        private async void RefreshLoaderVersions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await RefreshLoaderDataAsync();
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(T("modcenter.message.refresh_error", ex.Message));
            }
        }

        private async void LoaderTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            await RefreshLoaderDataAsync();
        }

        private async void LoaderGameVersionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            await LoadSelectedLoaderVersionsAsync();
        }

        private async void InstallSelectedLoader_Click(object sender, RoutedEventArgs e)
        {
            if (LoaderVersionBox.SelectedItem is not ComboBoxItem item || item.Tag is not LoaderInstallOption option || launcher == null)
            {
                WpfMessageBox.Show(T("modcenter.message.select_loader"));
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
                WpfMessageBox.Show(T("modcenter.message.loader_installed", installedVersion), T("settings.saved.title"));
            }
            catch (Exception ex)
            {
                SetModCenterStatus(T("modcenter.status.loader_install_failed"));
                WpfMessageBox.Show(T("modcenter.message.loader_install_failed", ex.Message));
            }
            finally
            {
                SetModCenterBusy(false, ModCenterStatusText.Text);
            }
        }

        private async void ImportMrPack_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Modrinth Pack (*.mrpack)|*.mrpack"
            };

            if (dialog.ShowDialog() == true)
                await InstallMrPackAsync(dialog.FileName);
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
                WpfMessageBox.Show(
                    T("modcenter.message.pack_installed_body", installedVersion, settings.MinecraftPath),
                    T("modcenter.message.pack_installed_title"));
            }
            catch (Exception ex)
            {
                SetModCenterStatus(ex.Message);
                WpfMessageBox.Show(ex.Message, T("launch.message.error_title"));
            }
            finally
            {
                SetModCenterBusy(false, ModCenterStatusText.Text);
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

        private static string GetSelectedComboTag(System.Windows.Controls.ComboBox comboBox, string fallback)
        {
            if (comboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                return tag;

            return fallback;
        }

        private void OpenDonateWeb_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://ko-fi.com/mixitosik";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(T("common.open_link_error", ex.Message));
            }
        }

        private void SelectBackground_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Изображения|*.jpg;*.png;*.jpeg;*.bmp";
            if (dialog.ShowDialog() == true)
            {
                BgPathBox.Text = dialog.FileName;
                MainBgImage.Source = new BitmapImage(new Uri(dialog.FileName));
            }
        }

        private void OpenFolderMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.ContextMenu is not ContextMenu menu)
                return;

            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.HorizontalOffset = 0;
            menu.VerticalOffset = 6;
            menu.DataContext = button.DataContext;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void OpenRootFolder_Click(object sender, RoutedEventArgs e) => OpenDir("");
        private void OpenModsFolder_Click(object sender, RoutedEventArgs e) => OpenDir("mods");
        private void OpenSavesFolder_Click(object sender, RoutedEventArgs e) => OpenDir("saves");
        private void OpenScreenshotsFolder_Click(object sender, RoutedEventArgs e) => OpenDir("screenshots");

        private void OpenDir(string subDir)
        {
            string minecraftPath = string.IsNullOrWhiteSpace(settings.MinecraftPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft")
                : settings.MinecraftPath;

            string fullPath = string.IsNullOrWhiteSpace(subDir)
                ? minecraftPath
                : Path.Combine(minecraftPath, subDir);

            if (Directory.Exists(fullPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = fullPath,
                    UseShellExecute = true
                });
            }
            else
            {
                string folderName = string.IsNullOrWhiteSpace(subDir) ? T("folders.root") : T("folders.named", subDir);
                System.Windows.MessageBox.Show(T("folders.not_created", folderName));
            }
        }

        private void TrayIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e)
        {
            if (isShuttingDown || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                e.Handled = true;
                return;
            }

            Dispatcher.BeginInvoke(new Action(ToggleWindow));
            e.Handled = true;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            isShuttingDown = true;
            discordClient?.Dispose();
            DisposeTrayIcon();
        }

        private void DisposeTrayIcon()
        {
            if (MyNotifyIcon == null)
                return;

            try
            {
                if (MyNotifyIcon.ContextMenu != null)
                    MyNotifyIcon.ContextMenu.IsOpen = false;

                MyNotifyIcon.Visibility = Visibility.Collapsed;
                MyNotifyIcon.Dispose();
            }
            catch
            {
            }
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove(); }
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => ShutdownApp();
        private void SettingsBtn_Click(object sender, RoutedEventArgs e) => SettingsModal.Visibility = Visibility.Visible;
        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadVersions();
        private void CloseServerModal_Click(object sender, RoutedEventArgs e) => ServerModal.Visibility = Visibility.Collapsed;
        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsModal.Visibility = Visibility.Collapsed;
        }
        private void SelectPath_Click(object sender, RoutedEventArgs e) { var dialog = new Microsoft.Win32.OpenFolderDialog(); if (dialog.ShowDialog() == true) PathBox.Text = dialog.FolderName; }
    }
}
