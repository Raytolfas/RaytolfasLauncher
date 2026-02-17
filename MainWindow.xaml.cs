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
using System.IO;
using System.Text.Json;
using System.Net.Http;
using System.Management;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Diagnostics;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;
using WpfClipboard = System.Windows.Clipboard;
using Forms = System.Windows.Forms; 
using WpfApplication = System.Windows.Application;
using DiscordRPC;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Version;

namespace RaytolfasLauncher
{
    public partial class MainWindow : Window
    {
        public LauncherSettings Settings { get; set; } = new LauncherSettings();
        private MinecraftLauncher? launcher;
        private LauncherSettings settings = new LauncherSettings();
        private readonly DiscordRpcClient discordClientID = new DiscordRpcClient("1472589510742118400");
        private readonly string currentVersion = "0.0.1";
        private readonly string updateUrl = "https://github.com/Raytolfas/RaytolfasLauncherAssets/raw/refs/heads/main/Updates/update_0_0_1.json";
        private readonly string serversUrl = "https://github.com/Raytolfas/RaytolfasLauncherAssets/raw/refs/heads/main/Servers/servers_0_0_1.json";
        private string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RaytolfasLauncher");
        private string settingsPath;
        private DiscordRpcClient? discordClient;
        private Forms.NotifyIcon? trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            settingsPath = Path.Combine(settingsFolder, "settings.json");
            VersionText.Text = $"v{currentVersion}";
            
            LoadSettingsFromFile();
            
            AccountSelector.IsEditable = true; 

            UpdateAccountList();

            launcher = new MinecraftLauncher(new MinecraftPath(settings.MinecraftPath));
            LoadVersions();
            this.Loaded += async (s, e) => {
                await CheckForUpdates();
            };
            InitDiscordRPC();
            InitTrayIcon();
            LoadAvatar();
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
                        var upWin = new UpdateWindow(currentVersion, info.Version, info.Changelog, info.DownloadUrl);
                        upWin.Owner = this;
                        upWin.ShowDialog();
                        return true;
                    }
                    else if (isManual)
                    {
                        System.Windows.MessageBox.Show("У вас установлена самая последняя версия!", "Обновление");
                    }
                }
            }
            catch (Exception ex)
            {
                if (isManual) System.Windows.MessageBox.Show("Ошибка проверки: " + ex.Message);
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
                string icon = acc.Type == "Microsoft" ? "🔑 " : "👤 ";
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
            try 
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri($"https://minotar.net/avatar/{username}/32", UriKind.Absolute);
                bitmap.EndInit();
                PlayerAvatar.Source = bitmap;
            }
            catch { }
        }

        private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (VersionBox.SelectedItem is not ComboBoxItem selectedVer || launcher == null) return;

            string versionId = selectedVer.Content.ToString() ?? "";
            MSession session;

            if (AccountSelector.SelectedItem is ComboBoxItem item && item.Tag is AccountData acc && AccountSelector.Text == item.Content.ToString())
            {
                if (acc.Type == "Microsoft")
                    session = new MSession(acc.Username, acc.AccessToken, acc.UUID);
                else
                    session = MSession.CreateOfflineSession(acc.Username);
            }
            else
            {
                string manualNick = AccountSelector.Text.Trim().Replace("🔑 ", "").Replace("👤 ", "");
                if (string.IsNullOrEmpty(manualNick)) { WpfMessageBox.Show("Введите никнейм!"); return; }
                session = MSession.CreateOfflineSession(manualNick);
            }

            if (settings.HideLauncherOnPlay) this.Hide(); 

            DownloadPanel.Visibility = Visibility.Visible; 
            LaunchBtn.IsEnabled = false;

            try
            {
                var logWindow = new LogWindow();
                logWindow.Show();
                logWindow.WriteLog($"[Launcher] Подготовка игры {versionId} для {session.Username}");

                launcher.FileProgressChanged += (s, args) =>
                {
                    Dispatcher.Invoke(() => {
                        DownloadProgress.Maximum = args.TotalTasks;
                        DownloadProgress.Value = args.ProgressedTasks;
                        StatusLabel.Text = $"Загрузка: {args.ProgressedTasks}/{args.TotalTasks}";
                    });
                };

                await launcher.InstallAsync(versionId);

                var launchOption = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = (int)RamSlider.Value,
                };

                var process = await launcher.BuildProcessAsync(versionId, launchOption);

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.OutputDataReceived += (s, args) => { if (args.Data != null) logWindow.WriteLog(args.Data); };
                process.ErrorDataReceived += (s, args) => { if (args.Data != null) logWindow.WriteLog("[JAVA] " + args.Data); };

                process.Start();
                process.EnableRaisingEvents = true;

                process.Exited += (s, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        SetDiscordStatus("В лаунчере", "Выбирает версию...");
                        logWindow.WriteLog("[Launcher] Игра закрыта. Статус Discord обновлен.");
                        
                        this.Show();
                        this.Focus();
                    });
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                SetDiscordStatus("В игре", $"Версия: {versionId}");
            }
            catch (Exception ex) 
            { 
                WpfMessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка");
                this.Show();
            }
            finally
            {
                DownloadPanel.Visibility = Visibility.Collapsed;
                LaunchBtn.IsEnabled = true;
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
            
            RamSlider.Value = settings.SelectedRam;
            PathBox.Text = settings.MinecraftPath;
            CbReleases.IsChecked = settings.ShowReleases;
            CbSnapshots.IsChecked = settings.ShowSnapshots;
            CbModded.IsChecked = settings.ShowModded;
        }

        private void SaveSettings()
        {
            try
            {
                if (!Directory.Exists(settingsFolder)) Directory.CreateDirectory(settingsFolder);
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
            }
            catch { }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            settings.SelectedRam = (int)RamSlider.Value;
            settings.MinecraftPath = PathBox.Text;
            settings.ShowReleases = CbReleases.IsChecked ?? true;
            settings.ShowSnapshots = CbSnapshots.IsChecked ?? false;
            settings.ShowModded = CbModded.IsChecked ?? true;
            
            settings.ShowDiscordStatus = CbDiscordRPC.IsChecked ?? true;
            settings.HideLauncherOnPlay = CbHideLauncher.IsChecked ?? true; 
            settings.CustomBackgroundPath = BgPathBox.Text;

            SaveSettings();
            launcher = new MinecraftLauncher(new MinecraftPath(settings.MinecraftPath));
            LoadVersions();
            SettingsModal.Visibility = Visibility.Collapsed;
            
            if (string.IsNullOrEmpty(settings.CustomBackgroundPath))
            {
                MainBgImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/background.png"));
            }
        }

        private async void LoadVersions()
        {
            if (launcher == null) return;
            try
            {
                var versions = await launcher.GetAllVersionsAsync(); 
                VersionBox.Items.Clear();
                
                string versionsDirPath = Path.Combine(settings.MinecraftPath, "versions");

                foreach (var v in versions)
                {
                    if (v.Type == "release" && !settings.ShowReleases) continue;
                    if (v.Type == "snapshot" && !settings.ShowSnapshots) continue;

                    var item = new ComboBoxItem { Content = v.Name, Tag = v.Type };
                    
                    string currentVersionPath = Path.Combine(versionsDirPath, v.Name);
                    
                    if (Directory.Exists(currentVersionPath))
                    {
                        item.Foreground = System.Windows.Media.Brushes.DeepSkyBlue;
                        item.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        item.Foreground = System.Windows.Media.Brushes.Gray; 
                    }

                    VersionBox.Items.Add(item);
                }
            }
            catch (Exception ex) { WpfMessageBox.Show("Ошибка загрузки версий: " + ex.Message); }
        }

        #region Discord RPC
        private void InitDiscordRPC()
        {
            try {
                if (!settings.ShowDiscordStatus) return;

                discordClient = discordClientID;
                discordClient.Initialize();
                SetDiscordStatus("В лаунчере", "Выбирает версию...");
            } catch { }
        }

        public void SetDiscordStatus(string state, string details)
        {
            if (!settings.ShowDiscordStatus || discordClient == null) return;
            
            discordClient.SetPresence(new RichPresence()
            {
                Details = details,
                State = state,
                Assets = new Assets() { LargeImageKey = "logo", LargeImageText = "Raytolfas Launcher" },
                Timestamps = Timestamps.Now
            });
        }
        #endregion

        #region System Tray
        private void InitTrayIcon()
        {
            trayIcon = new Forms.NotifyIcon();
            try 
            {
                var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/logo.ico")).Stream;
                trayIcon.Icon = new System.Drawing.Icon(stream); 
            } 
            catch 
            {
                trayIcon.Icon = System.Drawing.SystemIcons.Application; 
            }
            
            trayIcon.Visible = true;
            trayIcon.Text = "Raytolfas Launcher";

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Открыть/Скрыть", null, (s, e) => ToggleWindow());
            contextMenu.Items.Add("Выйти", null, (s, e) => ShutdownApp());

            trayIcon.ContextMenuStrip = contextMenu;
            trayIcon.DoubleClick += (s, e) => ToggleWindow();
        }

        private void ToggleWindow()
        {
            if (this.IsVisible) this.Hide();
            else { this.Show(); this.WindowState = WindowState.Normal; this.Activate(); }
        }

        private void ShutdownApp()
        {
            if (trayIcon != null) trayIcon.Visible = false; 
            discordClient?.Dispose(); 
            WpfApplication.Current.Shutdown(); 
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
            base.OnClosing(e);
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
            try
            {
                using var client = new HttpClient();
                string json = await client.GetStringAsync(serversUrl);
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var servers = JsonSerializer.Deserialize<List<ServerInfo>>(json, options);
                
                if (servers != null)
                {
                    ServersList.ItemsSource = servers;
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show("Не удалось загрузить список серверов: " + ex.Message);
            }
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
                WpfMessageBox.Show($"Не удалось открыть ссылку: {ex.Message}");
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

        private void OpenGameFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(settings.MinecraftPath))
                System.Diagnostics.Process.Start("explorer.exe", settings.MinecraftPath);
            else
                WpfMessageBox.Show("Путь к Minecraft не найден!");
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove(); }
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => WpfApplication.Current.Shutdown();
        private void SettingsBtn_Click(object sender, RoutedEventArgs e) => SettingsModal.Visibility = Visibility.Visible;
        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadVersions();
        private void CloseServerModal_Click(object sender, RoutedEventArgs e) => ServerModal.Visibility = Visibility.Collapsed;
        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsModal.Visibility = Visibility.Collapsed;
        }
        private void CopyIP_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string ip) 
            {
                WpfClipboard.SetText(ip);
                WpfMessageBox.Show($"IP {ip} скопирован!");
            }
        }
        private void SelectPath_Click(object sender, RoutedEventArgs e) { var dialog = new Microsoft.Win32.OpenFolderDialog(); if (dialog.ShowDialog() == true) PathBox.Text = dialog.FolderName; }
    }
}