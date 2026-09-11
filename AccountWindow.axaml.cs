// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CmlLib.Core.Auth.Microsoft;

namespace RaytolfasLauncher
{
    public partial class AccountWindow : Window
    {
        private readonly LauncherSettings settings;
        private readonly string microsoftAccountsPath;
        private readonly string language;
        private readonly HttpClient httpClient = new HttpClient();

        public AccountWindow() : this(new LauncherSettings()) { }

        public AccountWindow(LauncherSettings settings)
        {
            InitializeComponent();
            this.settings = settings;
            language = LocalizationManager.NormalizeLanguage(settings.Language);
            microsoftAccountsPath = Path.Combine(
                PlatformHelper.GetDefaultSettingsFolder(),
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

            foreach (var acc in settings.Accounts)
            {
                if (acc.AvatarBitmap == null)
                {
                    _ = LoadAvatarAsync(acc);
                }
            }

            if (settings.SelectedAccountIndex >= 0 && settings.SelectedAccountIndex < settings.Accounts.Count)
            {
                AccountsListBox.SelectedIndex = settings.SelectedAccountIndex;
            }
        }

        private async Task LoadAvatarAsync(AccountData acc)
        {
            try
            {
                string url = $"https://minotar.net/avatar/{acc.Username}/24";
                var bytes = await httpClient.GetByteArrayAsync(url);
                using var stream = new MemoryStream(bytes);
                acc.AvatarBitmap = new Bitmap(stream);
                AccountsListBox.ItemsSource = null;
                AccountsListBox.ItemsSource = settings.Accounts;
            }
            catch
            {
                try
                {
                    var uri = new Uri("avares://RaytolfasLauncher/Assets/no_internet.png");
                    if (AssetLoader.Exists(uri))
                    {
                        acc.AvatarBitmap = new Bitmap(AssetLoader.Open(uri));
                    }
                }
                catch { }
            }
        }

        private async void AddOffline_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new AddAccountDialog(language);
            bool? result = await dialog.ShowDialog<bool?>(this);

            if (result != true || string.IsNullOrWhiteSpace(dialog.ResultUsername))
                return;

            string nick = dialog.ResultUsername;

            if (settings.Accounts.Any(a => a.Username.Equals(nick, StringComparison.OrdinalIgnoreCase)))
            {
                await RayMessageBox.ShowAsync(this, T("account.duplicate"), T("account.window_title"));
                return;
            }

            var newAcc = new AccountData { Username = nick, Type = "Offline" };
            settings.Accounts.Add(newAcc);
            RefreshList();
            AccountsListBox.SelectedItem = newAcc;
        }

        private async void AddMicrosoft_Click(object? sender, RoutedEventArgs e)
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
                    await RayMessageBox.ShowAsync(this, T("account.microsoft_duplicate"), T("account.window_title"));
                    return;
                }

                loginHandler.AccountManager.SaveAccounts();

                var newAcc = new AccountData
                {
                    Username = session.Username ?? "",
                    Type = "Microsoft",
                    AccessToken = session.AccessToken ?? "",
                    UUID = session.UUID ?? "",
                    MicrosoftAccountIdentifier = session.UUID ?? ""
                };
                settings.Accounts.Add(newAcc);

                RefreshList();
                AccountsListBox.SelectedItem = newAcc;
            }
            catch (Exception ex)
            {
                await RayMessageBox.ShowAsync(this, T("account.login_error", ex.Message), T("account.window_title"));
            }
        }

        private async void AddElyBy_Click(object? sender, RoutedEventArgs e)
        {
            var loginWindow = new ElyByLoginWindow(language);
            bool? result = await loginWindow.ShowDialog<bool?>(this);

            if (result != true)
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
                    await RayMessageBox.ShowAsync(this, T("elyby.duplicate"), T("account.window_title"));
                    return;
                }

                var newAcc = new AccountData
                {
                    Username = username,
                    Type = "ElyBy",
                    AccessToken = accessToken,
                    ClientToken = returnedClientToken,
                    UUID = uuid
                };
                settings.Accounts.Add(newAcc);

                RefreshList();
                AccountsListBox.SelectedItem = newAcc;
            }
            catch (Exception ex)
            {
                await RayMessageBox.ShowAsync(this, T("elyby.login_error", ex.Message), T("account.window_title"));
            }
        }

        private void DeleteAccount_Click(object? sender, RoutedEventArgs e)
        {
            if (AccountsListBox.SelectedItem is not AccountData acc)
                return;

            settings.Accounts.Remove(acc);
            RefreshList();
        }

        private void SelectAndClose_Click(object? sender, RoutedEventArgs e)
        {
            if (AccountsListBox.SelectedIndex == -1)
            {
                RayMessageBox.Show(T("account.select_required"), T("account.window_title"), this);
                return;
            }

            settings.SelectedAccountIndex = AccountsListBox.SelectedIndex;
            Close(true);
        }

        private void Close_Click(object? sender, RoutedEventArgs e) => Close(false);
    }
}
