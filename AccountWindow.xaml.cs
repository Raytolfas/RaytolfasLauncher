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
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using CmlLib.Core.Auth.Microsoft;
using WpfMessageBox = System.Windows.MessageBox;

namespace RaytolfasLauncher
{
    public partial class AccountWindow : Window
    {
        private readonly LauncherSettings settings;
        private readonly string microsoftAccountsPath;
        private readonly string language;
        private readonly HttpClient httpClient = new HttpClient();

        public AccountWindow(LauncherSettings settings)
        {
            InitializeComponent();
            this.settings = settings;
            language = LocalizationManager.NormalizeLanguage(settings.Language);
            microsoftAccountsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Raytolfas",
                "Raytolfas Launcher",
                "microsoft_accounts.json");
            ApplyLocalization();
            RefreshList();
        }

        private string T(string key) => LocalizationManager.Get(key, language);

        private string T(string key, params object[] args) => string.Format(T(key), args);

        private void ApplyLocalization()
        {
            Title = T("account.window_title");
            AccountsTitleText.Text = T("account.title");
            DeleteAccountButton.Content = T("account.delete");
            AddOfflineButton.Content = T("account.add_offline");
            AddMicrosoftButton.Content = T("account.add_microsoft");
            AddElyByButton.Content = T("account.add_elyby");
            UseAccountButton.Content = T("account.use");
            CancelButton.Content = T("account.cancel");
        }

        private void RefreshList()
        {
            AccountsListBox.ItemsSource = null;
            AccountsListBox.ItemsSource = settings.Accounts;
        }

        private void AddOffline_Click(object sender, RoutedEventArgs e)
        {
            string nick = Microsoft.VisualBasic.Interaction.InputBox(
                T("account.input_prompt"),
                T("account.input_title"),
                "RaytolfasPlayer");

            if (string.IsNullOrWhiteSpace(nick))
                return;

            if (settings.Accounts.Any(a => a.Username.Equals(nick, StringComparison.OrdinalIgnoreCase)))
            {
                WpfMessageBox.Show(T("account.duplicate"));
                return;
            }

            settings.Accounts.Add(new AccountData { Username = nick, Type = "Offline" });
            RefreshList();
            AccountsListBox.SelectedIndex = settings.Accounts.Count - 1;
        }

        private async void AddMicrosoft_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(microsoftAccountsPath)!);

                var loginHandler = new JELoginHandlerBuilder()
                    .WithAccountManager(microsoftAccountsPath)
                    .Build();

                var session = await loginHandler.AuthenticateInteractively();

                if (settings.Accounts.Any(a => a.Type == "Microsoft" && a.UUID == session.UUID))
                {
                    WpfMessageBox.Show(T("account.microsoft_duplicate"));
                    return;
                }

                loginHandler.AccountManager.SaveAccounts();

                settings.Accounts.Add(new AccountData
                {
                    Username = session.Username ?? "",
                    Type = "Microsoft",
                    AccessToken = session.AccessToken ?? "",
                    UUID = session.UUID ?? "",
                    MicrosoftAccountIdentifier = session.UUID ?? ""
                });

                RefreshList();
                AccountsListBox.SelectedIndex = settings.Accounts.Count - 1;
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(T("account.login_error", ex.Message));
            }
        }

        private async void AddElyBy_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new ElyByLoginWindow(language)
            {
                Owner = this
            };

            if (loginWindow.ShowDialog() != true)
                return;

            try
            {
                string password = string.IsNullOrWhiteSpace(loginWindow.TotpValue)
                    ? loginWindow.PasswordValue
                    : $"{loginWindow.PasswordValue}:{loginWindow.TotpValue}";

                string clientToken = Guid.NewGuid().ToString("N");
                string payload = JsonSerializer.Serialize(new
                {
                    username = loginWindow.LoginValue,
                    password,
                    clientToken,
                    requestUser = true
                });

                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var response = await httpClient.PostAsync("https://authserver.ely.by/auth/authenticate", content);
                string json = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();

                using JsonDocument document = JsonDocument.Parse(json);
                string accessToken = document.RootElement.GetProperty("accessToken").GetString() ?? "";
                string returnedClientToken = document.RootElement.GetProperty("clientToken").GetString() ?? clientToken;
                JsonElement selectedProfile = document.RootElement.GetProperty("selectedProfile");
                string uuid = selectedProfile.GetProperty("id").GetString() ?? "";
                string username = selectedProfile.GetProperty("name").GetString() ?? "";

                if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(username))
                    throw new InvalidOperationException(T("elyby.invalid_response"));

                if (settings.Accounts.Any(a => a.Type == "ElyBy" && a.UUID == uuid))
                {
                    WpfMessageBox.Show(T("elyby.duplicate"));
                    return;
                }

                settings.Accounts.Add(new AccountData
                {
                    Username = username,
                    Type = "ElyBy",
                    AccessToken = accessToken,
                    ClientToken = returnedClientToken,
                    UUID = uuid
                });

                RefreshList();
                AccountsListBox.SelectedIndex = settings.Accounts.Count - 1;
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(T("elyby.login_error", ex.Message));
            }
        }

        private void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            if (AccountsListBox.SelectedItem is not AccountData acc)
                return;

            settings.Accounts.Remove(acc);
            RefreshList();
        }

        private void SelectAndClose_Click(object sender, RoutedEventArgs e)
        {
            if (AccountsListBox.SelectedIndex == -1)
            {
                WpfMessageBox.Show(T("account.select_required"));
                return;
            }

            settings.SelectedAccountIndex = AccountsListBox.SelectedIndex;
            DialogResult = true;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
