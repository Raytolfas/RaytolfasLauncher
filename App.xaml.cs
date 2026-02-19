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
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => 
            ReportError((Exception)e.ExceptionObject);
        
        this.DispatcherUnhandledException += (s, e) => 
        {
            ReportError(e.Exception);
            e.Handled = true;
        };
    }

    private void ReportError(Exception ex)
    {
        string errorText = $"Тип ошибки: {ex.GetType().Name}\n" +
                           $"Сообщение: {ex.Message}\n\n" +
                           $"Стек вызовов:\n{ex.StackTrace}";

        System.Windows.MessageBox.Show(errorText, "Критическая ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
        
        try {
            File.WriteAllText("crash_report.txt", errorText);
        } catch { }

        System.Environment.Exit(1);
    }
}