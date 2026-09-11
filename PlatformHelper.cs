// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.VisualTree;
using CmlLib.Core;

namespace RaytolfasLauncher
{
    public static class PlatformHelper
    {
        public static bool IsWindows => OperatingSystem.IsWindows();
        public static bool IsLinux => OperatingSystem.IsLinux();
        public static bool IsMacOS => OperatingSystem.IsMacOS();

        public static string GetDefaultMinecraftFolder()
        {
            try
            {
                return MinecraftPath.GetOSDefaultPath();
            }
            catch
            {
                if (IsWindows)
                {
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
                }
                else if (IsMacOS)
                {
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "minecraft");
                }
                else
                {
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".minecraft");
                }
            }
        }

        public static string GetDefaultSettingsFolder()
        {
            if (IsWindows)
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Raytolfas",
                    "Raytolfas Launcher");
            }
            else if (IsMacOS)
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library",
                    "Application Support",
                    "RaytolfasLauncher");
            }
            else
            {
                string configDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? "";
                if (string.IsNullOrWhiteSpace(configDir))
                {
                    configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                }
                return Path.Combine(configDir, "RaytolfasLauncher");
            }
        }

        public static void OpenFolder(string path)
        {
            if (!Directory.Exists(path))
                return;

            try
            {
                if (IsWindows)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    });
                }
                else if (IsMacOS)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = false
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = false
                    });
                }
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                catch
                {
                }
            }
        }

        public static void OpenBrowser(string url)
        {
            try
            {
                if (IsWindows)
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (IsMacOS)
                {
                    Process.Start("open", url);
                }
                else
                {
                    Process.Start("xdg-open", url);
                }
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch
                {
                }
            }
        }

        public static IEnumerable<string> DiscoverJavaPaths(string? userConfiguredPath)
        {
            var paths = new HashSet<string>(IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

            void AddPath(string? path)
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                string normalized = path.Trim().Trim('"');
                if (File.Exists(normalized)) paths.Add(normalized);
            }

            AddPath(userConfiguredPath);

            string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                if (IsWindows)
                {
                    AddPath(Path.Combine(javaHome, "bin", "javaw.exe"));
                    AddPath(Path.Combine(javaHome, "bin", "java.exe"));
                }
                else
                {
                    AddPath(Path.Combine(javaHome, "bin", "java"));
                }
            }

            if (IsWindows)
            {
                string[] roots =
                {
                    @"C:\Program Files\Java",
                    @"C:\Program Files (x86)\Java",
                    @"C:\Program Files\Eclipse Adoptium",
                    @"C:\Program Files\AdoptOpenJDK",
                    @"C:\Program Files\Amazon Corretto",
                    @"C:\Program Files\Microsoft",
                    @"C:\Program Files\BellSoft\LibericaJDK",
                    @"C:\Program Files\Zulu"
                };

                foreach (var root in roots)
                {
                    if (!Directory.Exists(root)) continue;

                    try
                    {
                        foreach (var dir in Directory.EnumerateDirectories(root))
                        {
                            AddPath(Path.Combine(dir, "bin", "javaw.exe"));
                            AddPath(Path.Combine(dir, "bin", "java.exe"));
                        }
                    }
                    catch { }
                }
            }
            else
            {
                AddPath("/usr/bin/java");
                AddPath("/usr/local/bin/java");
                AddPath("/etc/alternatives/java");

                string[] linuxJvmRoots =
                {
                    "/usr/lib/jvm",
                    "/usr/lib64/jvm",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sdkman/candidates/java")
                };

                foreach (var root in linuxJvmRoots)
                {
                    if (!Directory.Exists(root)) continue;

                    try
                    {
                        foreach (var dir in Directory.EnumerateDirectories(root))
                        {
                            AddPath(Path.Combine(dir, "bin", "java"));
                        }
                    }
                    catch { }
                }

                string? envPath = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(envPath))
                {
                    foreach (var p in envPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                    {
                        AddPath(Path.Combine(p, "java"));
                    }
                }
            }

            return paths;
        }

        public static void LaunchUpdater(string currentExe, string tempFile, string baseDirectory)
        {
            if (IsWindows)
            {
                string finalExeName = "RaytolfasLauncher.exe";
                string finalExePath = Path.Combine(baseDirectory, finalExeName);

                string batchScript = $@"
@echo off
timeout /t 2 /nobreak > nul
:loop
del /f /q ""{currentExe}""
if exist ""{currentExe}"" goto loop
move /y ""{tempFile}"" ""{finalExePath}""
start """" ""{finalExePath}""
del ""%~f0""";

                string batchPath = Path.Combine(baseDirectory, "update.bat");
                File.WriteAllText(batchPath, batchScript, System.Text.Encoding.Default);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            else
            {
                string finalExePath = Path.Combine(baseDirectory, Path.GetFileName(currentExe));
                string shScript = $@"#!/bin/sh
sleep 2
rm -f ""{currentExe}""
mv -f ""{tempFile}"" ""{finalExePath}""
chmod +x ""{finalExePath}""
nohup ""{finalExePath}"" >/dev/null 2>&1 &
rm -f ""$0""
";
                string shPath = Path.Combine(baseDirectory, "update.sh");
                File.WriteAllText(shPath, shScript);

                try
                {
                    Process.Start("chmod", $"+x \"{shPath}\"")?.WaitForExit();
                }
                catch { }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = $"\"{shPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }

        public static bool IsInteractiveElement(Visual? visual)
        {
            while (visual != null)
            {
                if (visual is Avalonia.Controls.Button
                    or Avalonia.Controls.ComboBox
                    or Avalonia.Controls.ComboBoxItem
                    or Avalonia.Controls.TextBox
                    or Avalonia.Controls.Slider
                    or Avalonia.Controls.CheckBox
                    or Avalonia.Controls.RadioButton
                    or Avalonia.Controls.ListBox
                    or Avalonia.Controls.ListBoxItem
                    or Avalonia.Controls.Primitives.ScrollBar
                    or Avalonia.Controls.ScrollViewer
                    or Avalonia.Controls.Primitives.SelectingItemsControl
                    or Avalonia.Controls.Primitives.ToggleButton
                    or Avalonia.Controls.Primitives.Popup)
                {
                    return true;
                }

                visual = visual.GetVisualParent();
            }

            return false;
        }
    }
}
