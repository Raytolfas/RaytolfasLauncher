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
using System.Linq;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;
using CmlLib.Core.Auth.Microsoft;

namespace RaytolfasLauncher
{
    public partial class AccountWindow : Window
    {
        private LauncherSettings _settings;

        public AccountWindow(LauncherSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            RefreshList();
        }

        private void RefreshList()
        {
            AccountsListBox.ItemsSource = null;
            AccountsListBox.ItemsSource = _settings.Accounts;
        }

        private void AddOffline_Click(object sender, RoutedEventArgs e)
        {
            string nick = Microsoft.VisualBasic.Interaction.InputBox("Введите ваш никнейм для пиратской игры:", "Новый аккаунт", "RaytolfasPlayer");

            if (!string.IsNullOrWhiteSpace(nick))
            {
                if (_settings.Accounts.Any(a => a.Username.Equals(nick, StringComparison.OrdinalIgnoreCase)))
                {
                    WpfMessageBox.Show("Такой аккаунт уже есть в списке!");
                    return;
                }

                _settings.Accounts.Add(new AccountData { Username = nick, Type = "Offline" });
                RefreshList();
                AccountsListBox.SelectedIndex = _settings.Accounts.Count - 1;
            }
        }

        private async void AddMicrosoft_Click(object sender, RoutedEventArgs e)
        {
            try {
                var loginHandler = JELoginHandlerBuilder.BuildDefault();
                var session = await loginHandler.AuthenticateInteractively();
                
                if (_settings.Accounts.Any(a => a.Username == session.Username))
                {
                    WpfMessageBox.Show("Этот Microsoft-аккаунт уже добавлен!");
                    return;
                }

                _settings.Accounts.Add(new AccountData { 
                    Username = session.Username!, 
                    Type = "Microsoft",
                    AccessToken = session.AccessToken!,
                    UUID = session.UUID!
                });
                RefreshList();
            } catch (Exception ex) {
                WpfMessageBox.Show("Ошибка входа: " + ex.Message); 
            }
        }

        private void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            if (AccountsListBox.SelectedItem is AccountData acc)
            {
                _settings.Accounts.Remove(acc);
                RefreshList();
            }
        }

        private void SelectAndClose_Click(object sender, RoutedEventArgs e)
        {
            if (AccountsListBox.SelectedIndex != -1)
            {
                _settings.SelectedAccountIndex = AccountsListBox.SelectedIndex;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                WpfMessageBox.Show("Выберите аккаунт из списка!");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}