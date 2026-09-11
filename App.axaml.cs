// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace RaytolfasLauncher
{
    public partial class App : Application
    {
        private bool isReportingError;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            InputElement.PointerPressedEvent.AddClassHandler<ComboBox>((combo, e) =>
            {
                if (e.GetCurrentPoint(combo).Properties.IsRightButtonPressed)
                {
                    e.Handled = true;
                }
            }, RoutingStrategies.Tunnel);

            InputElement.PointerReleasedEvent.AddClassHandler<ComboBox>((combo, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Right)
                {
                    e.Handled = true;
                }
            }, RoutingStrategies.Tunnel);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                ReportError(e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception"));
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ReportError(Exception ex)
        {
            if (isReportingError)
                return;

            isReportingError = true;

            string errorText = $"Тип ошибки: {ex.GetType().Name}\n" +
                               $"Сообщение: {ex.Message}\n\n" +
                               $"Стек вызовов:\n{ex.StackTrace}";

            try
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_report.txt"), errorText);
            }
            catch { }

            try
            {
                RayMessageBox.Show(errorText, "Критическая ошибка запуска");
            }
            catch { }
        }
    }
}
