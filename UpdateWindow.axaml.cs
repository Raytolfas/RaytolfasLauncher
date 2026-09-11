// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace RaytolfasLauncher
{
    public partial class UpdateWindow : Window
    {
        private readonly string downloadUrl;
        private readonly string tempFile;
        private readonly string language;

        public UpdateWindow() : this("0.0.1", "0.0.2", "Changelog", "", LocalizationManager.DefaultLanguage) { }

        public UpdateWindow(string currentVer, string newVer, string changelog, string url, string language)
        {
            InitializeComponent();
            this.language = LocalizationManager.NormalizeLanguage(language);
            CurrentVerText.Text = currentVer;
            NewVerText.Text = newVer;
            ChangelogText.Text = changelog;
            downloadUrl = url;
            tempFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PlatformHelper.IsWindows ? "update_temp.exe" : "update_temp");
            ApplyLocalization();
        }

        private string T(string key) => LocalizationManager.Get(key, language);

        private string T(string key, params object[] args) => string.Format(T(key), args);

        private void ApplyLocalization()
        {
            Title = T("update.window_title");
            UpdateTitleText.Text = T("update.available");
            CurrentVersionLabel.Text = T("update.current");
            NewVersionLabel.Text = T("update.new");
            DownloadBtn.Content = T("update.download");
            CancelBtn.Content = T("update.cancel");
            RestartBtn.Content = T("update.restart");
        }

        private void CloseUpdate_Click(object? sender, RoutedEventArgs e) => Close();
        private void CancelBtn_Click(object? sender, RoutedEventArgs e) => Close();

        private async void DownloadBtn_Click(object? sender, RoutedEventArgs e)
        {
            DownloadBtn.IsVisible = false;
            UpdateProgress.IsVisible = true;
            CloseButton.IsEnabled = false;

            try
            {
                using var client = new HttpClient();
                var response = await client.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(tempFile, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }

                UpdateProgress.Value = 100;
                RestartBtn.IsVisible = true;
                await RayMessageBox.ShowAsync(this, T("update.downloaded"), T("update.success"));
            }
            catch (Exception ex)
            {
                await RayMessageBox.ShowAsync(this, T("update.error", ex.Message), T("update.window_title"));
                DownloadBtn.IsVisible = true;
                UpdateProgress.IsVisible = false;
                CloseButton.IsEnabled = true;
            }
        }

        private void RestartBtn_Click(object? sender, RoutedEventArgs e)
        {
            string? currentExe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExe))
                return;

            PlatformHelper.LaunchUpdater(currentExe, tempFile, AppDomain.CurrentDomain.BaseDirectory);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Environment.Exit(0);
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
    }
}
