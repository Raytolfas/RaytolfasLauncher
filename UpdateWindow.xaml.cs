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
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;

namespace RaytolfasLauncher
{
    public partial class UpdateWindow : Window
    {
        private string downloadUrl;
        private string tempFile;

        public UpdateWindow(string currentVer, string newVer, string changelog, string url)
        {
            InitializeComponent();
            CurrentVerText.Text = currentVer;
            NewVerText.Text = newVer;
            ChangelogText.Text = changelog;
            downloadUrl = url;
            tempFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_temp.exe");
        }

        private void CloseUpdate_Click(object sender, RoutedEventArgs e) => this.Close();
        private void CancelBtn_Click(object sender, RoutedEventArgs e) => this.Close();

        private async void DownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            DownloadBtn.Visibility = Visibility.Collapsed;
            UpdateProgress.Visibility = Visibility.Visible;
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
                RestartBtn.Visibility = Visibility.Visible;
                
                System.Windows.MessageBox.Show("Обновление скачано!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Ошибка: " + ex.Message);
                DownloadBtn.Visibility = Visibility.Visible;
                UpdateProgress.Visibility = Visibility.Collapsed;
                CloseButton.IsEnabled = true;
            }
        }

        private void RestartBtn_Click(object sender, RoutedEventArgs e)
        {
            string? currentExe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExe)) return;
            
            string finalExeName = "RaytolfasLauncher.exe";
            string finalExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, finalExeName);

            string batchScript = $@"
        @echo off
        timeout /t 2 /nobreak > nul
        :loop
        del /f /q ""{currentExe}""
        if exist ""{currentExe}"" goto loop
        move /y ""{tempFile}"" ""{finalExePath}""
        start """" ""{finalExePath}""
        del ""%~f0""";

            string batchPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.bat");
            
            File.WriteAllText(batchPath, batchScript, System.Text.Encoding.Default);

            Process.Start(new ProcessStartInfo 
            { 
                FileName = "cmd.exe", 
                Arguments = $"/c \"{batchPath}\"", 
                CreateNoWindow = true, 
                UseShellExecute = false 
            });
            
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) 
        { 
            if (e.LeftButton == MouseButtonState.Pressed) DragMove(); 
        }
    }
}