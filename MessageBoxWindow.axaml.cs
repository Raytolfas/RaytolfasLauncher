// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace RaytolfasLauncher
{
    public partial class MessageBoxWindow : Window
    {
        public MessageBoxWindow() : this("", "Raytolfas Launcher") { }

        public MessageBoxWindow(string message, string title = "Raytolfas Launcher", bool isConfirm = false)
        {
            InitializeComponent();
            Title = title;
            TitleText.Text = title.ToUpperInvariant();
            MessageText.Text = message;
            if (isConfirm)
            {
                CancelButton.IsVisible = true;
                OkButton.Content = "ДА";
            }
        }

        private void Ok_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }

    public static class RayMessageBox
    {
        public static Task ShowAsync(Window? owner, string message, string title = "Raytolfas Launcher")
        {
            var tcs = new TaskCompletionSource<bool>();

            void Action()
            {
                var box = new MessageBoxWindow(message, title);
                Window? effectiveOwner = owner;
                if (effectiveOwner == null && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    effectiveOwner = desktop.MainWindow;
                }

                if (effectiveOwner != null && effectiveOwner.IsVisible)
                {
                    box.ShowDialog(effectiveOwner).ContinueWith(_ => tcs.TrySetResult(true));
                }
                else
                {
                    box.Closed += (s, e) => tcs.TrySetResult(true);
                    box.Show();
                }
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Action();
            }
            else
            {
                Dispatcher.UIThread.Post(Action);
            }

            return tcs.Task;
        }

        public static Task<bool> ShowConfirmAsync(Window? owner, string message, string title = "Raytolfas Launcher")
        {
            var tcs = new TaskCompletionSource<bool>();

            void Action()
            {
                var box = new MessageBoxWindow(message, title, isConfirm: true);
                Window? effectiveOwner = owner;
                if (effectiveOwner == null && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    effectiveOwner = desktop.MainWindow;
                }

                if (effectiveOwner != null && effectiveOwner.IsVisible)
                {
                    box.ShowDialog<bool>(effectiveOwner).ContinueWith(t => tcs.TrySetResult(t.Result));
                }
                else
                {
                    box.Closed += (s, e) => tcs.TrySetResult(false);
                    box.Show();
                }
            }

            if (Dispatcher.UIThread.CheckAccess())
                Action();
            else
                Dispatcher.UIThread.Post(Action);

            return tcs.Task;
        }

        public static void Show(string message, string title = "Raytolfas Launcher", Window? owner = null)
        {
            _ = ShowAsync(owner, message, title);
        }
    }
}
