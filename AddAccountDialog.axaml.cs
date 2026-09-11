// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RaytolfasLauncher
{
    public partial class AddAccountDialog : Window
    {
        public string? ResultUsername { get; private set; }

        public AddAccountDialog() : this(LocalizationManager.DefaultLanguage) { }

        public AddAccountDialog(string language = LocalizationManager.DefaultLanguage)
        {
            InitializeComponent();
            string lang = LocalizationManager.NormalizeLanguage(language);
            Title = LocalizationManager.Get("account.input_title", lang);
            TitleText.Text = LocalizationManager.Get("account.input_title", lang).ToUpperInvariant();
            PromptText.Text = LocalizationManager.Get("account.input_prompt", lang);
            UsernameBox.Text = "RaytolfasPlayer";
            CancelButton.Content = LocalizationManager.Get("account.cancel", lang);
            ConfirmButton.Content = LocalizationManager.Get("account.add_offline", lang).Replace("+ ", "");

            UsernameBox.AttachedToVisualTree += (s, e) =>
            {
                UsernameBox.Focus();
                UsernameBox.SelectAll();
            };
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            ResultUsername = null;
            Close(false);
        }

        private void Confirm_Click(object? sender, RoutedEventArgs e)
        {
            string text = UsernameBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text))
                return;

            ResultUsername = text;
            Close(true);
        }
    }
}
