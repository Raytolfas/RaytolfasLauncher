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
using System.Windows;

namespace RaytolfasLauncher;

public partial class App : System.Windows.Application
{
    private bool isReportingError;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => 
            ReportError(e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception"));
        
        this.DispatcherUnhandledException += (s, e) => 
        {
            if (ShouldIgnoreException(e.Exception))
            {
                e.Handled = true;
                return;
            }

            ReportError(e.Exception);
            e.Handled = true;
        };
    }

    private bool ShouldIgnoreException(Exception ex)
    {
        return ex is InvalidOperationException &&
               (Current?.Dispatcher?.HasShutdownStarted == true || Current?.Dispatcher?.HasShutdownFinished == true);
    }

    private void ReportError(Exception ex)
    {
        if (isReportingError || ShouldIgnoreException(ex))
            return;

        isReportingError = true;

        string errorText = $"Тип ошибки: {ex.GetType().Name}\n" +
                           $"Сообщение: {ex.Message}\n\n" +
                           $"Стек вызовов:\n{ex.StackTrace}";
        
        try {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_report.txt"), errorText);
        } catch { }

        try
        {
            if (Current?.Dispatcher?.HasShutdownStarted != true && Current?.Dispatcher?.HasShutdownFinished != true)
            {
                System.Windows.MessageBox.Show(errorText, "Критическая ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch { }

        try
        {
            if (Current != null && !Current.Dispatcher.HasShutdownStarted && !Current.Dispatcher.HasShutdownFinished)
            {
                Current.Shutdown(-1);
                return;
            }
        }
        catch { }

        System.Environment.Exit(1);
    }
}
