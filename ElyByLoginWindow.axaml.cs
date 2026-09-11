// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RaytolfasLauncher
{
    public partial class ElyByLoginWindow : Window
    {
        private readonly string language;

        public string LoginValue => LoginBox.Text?.Trim() ?? "";
        public string PasswordValue => PasswordBox.Text ?? "";
        public string TotpValue => TotpBox.Text?.Trim() ?? "";

        public ElyByLoginWindow() : this(LocalizationManager.DefaultLanguage) { }

        public ElyByLoginWindow(string language)
        {
            InitializeComponent();
            this.language = LocalizationManager.NormalizeLanguage(language);
            ApplyLocalization();
        }

        private string T(string key) => LocalizationManager.Get(key, language);

        private void ApplyLocalization()
        {
            Title = T("elyby.window_title");
            TitleText.Text = T("elyby.title");
            LoginLabel.Text = T("elyby.login");
            PasswordLabel.Text = T("elyby.password");
            TotpLabel.Text = T("elyby.totp");
            HintText.Text = T("elyby.hint");
            CancelButton.Content = T("account.cancel");
            LoginButton.Content = T("elyby.sign_in");
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private void Login_Click(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LoginValue) || string.IsNullOrWhiteSpace(PasswordValue))
            {
                RayMessageBox.Show(T("elyby.enter_credentials"), T("elyby.window_title"), this);
                return;
            }

            Close(true);
        }
    }
}
