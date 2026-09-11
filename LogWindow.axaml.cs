// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace RaytolfasLauncher
{
    public partial class LogWindow : Window
    {
        private readonly StringBuilder pendingLog4jEvent = new StringBuilder();
        private readonly ConcurrentQueue<string> pendingLines = new ConcurrentQueue<string>();
        private readonly DispatcherTimer flushTimer;
        private readonly string language;
        private const int MaxVisibleEntries = 700;
        private int flushRequested;
        private bool isCollectingLog4jEvent;

        public ObservableCollection<LogEntry> Entries { get; } = new ObservableCollection<LogEntry>();

        public LogWindow() : this(LocalizationManager.DefaultLanguage) { }

        public LogWindow(string language = LocalizationManager.DefaultLanguage)
        {
            InitializeComponent();
            this.language = LocalizationManager.NormalizeLanguage(language);
            LogListBox.ItemsSource = Entries;

            flushTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(180)
            };
            flushTimer.Tick += FlushTimer_Tick;
            ApplyLocalization();
        }

        private string T(string key) => LocalizationManager.Get(key, language);

        private void ApplyLocalization()
        {
            Title = T("log.window_title");
            LogTitleText.Text = T("log.title");
            BetaBadgeText.Text = T("log.beta_badge");
            LogDescriptionText.Text = T("log.description");
            ClearButton.Content = T("log.clear");
            CopyButton.Content = T("log.copy");
            HideButton.Content = T("log.hide");
        }

        public void WriteLog(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            foreach (var rawLine in text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                pendingLines.Enqueue(rawLine.TrimEnd());
            }

            RequestFlush();
        }

        private void RequestFlush()
        {
            if (Interlocked.Exchange(ref flushRequested, 1) != 0)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (!flushTimer.IsEnabled)
                    flushTimer.Start();
            }, DispatcherPriority.Background);
        }

        private void FlushTimer_Tick(object? sender, EventArgs e)
        {
            int processed = 0;
            bool hasUiChanges = false;
            while (processed < 120 && pendingLines.TryDequeue(out string? line))
            {
                hasUiChanges |= ProcessIncomingLine(line);
                processed++;
            }

            if (hasUiChanges && Entries.Count > 0)
            {
                LogListBox.ScrollIntoView(Entries[^1]);
            }

            if (pendingLines.IsEmpty)
            {
                flushTimer.Stop();
                Interlocked.Exchange(ref flushRequested, 0);

                if (!pendingLines.IsEmpty)
                    RequestFlush();
            }
        }

        private bool ProcessIncomingLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            if (isCollectingLog4jEvent || line.Contains("<log4j:Event", StringComparison.OrdinalIgnoreCase))
            {
                pendingLog4jEvent.AppendLine(line);
                isCollectingLog4jEvent = true;

                if (line.Contains("</log4j:Event>", StringComparison.OrdinalIgnoreCase))
                    return FlushPendingLog4jEvent();

                return false;
            }

            if (line.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("<log4j:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("</log4j:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            AppendEntry(FormatRegularLine(line));
            return true;
        }

        private bool FlushPendingLog4jEvent()
        {
            string xml = pendingLog4jEvent.ToString();
            pendingLog4jEvent.Clear();
            isCollectingLog4jEvent = false;

            string level = GetAttribute(xml, "level") ?? "INFO";
            string? timestampText = GetAttribute(xml, "timestamp");
            string message = Regex.Match(xml, @"<log4j:Message><!\[CDATA\[(.*?)\]\]></log4j:Message>", RegexOptions.Singleline).Groups[1].Value.Trim();

            if (string.IsNullOrWhiteSpace(message))
                return false;

            string timeText = DateTime.Now.ToString("HH:mm:ss");
            if (long.TryParse(timestampText, out long unixMs))
            {
                timeText = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime().ToString("HH:mm:ss");
            }

            AppendEntry(new LogEntry
            {
                Prefix = $"[{timeText}] [Minecraft/{level}]",
                Message = message,
                Brush = GetBrushForLevel(level)
            });
            return true;
        }

        private static string? GetAttribute(string source, string attributeName)
        {
            var match = Regex.Match(source, attributeName + "=\"(.*?)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private LogEntry FormatRegularLine(string line)
        {
            if (line.StartsWith("[JAVA ERROR]", StringComparison.OrdinalIgnoreCase))
            {
                return new LogEntry
                {
                    Prefix = $"[{DateTime.Now:HH:mm:ss}] [Java/ERROR]",
                    Message = line.Replace("[JAVA ERROR]", "").Trim(),
                    Brush = Brushes.IndianRed
                };
            }

            if (line.StartsWith("[ERROR]", StringComparison.OrdinalIgnoreCase))
            {
                return new LogEntry
                {
                    Prefix = $"[{DateTime.Now:HH:mm:ss}] [Launcher/ERROR]",
                    Message = line.Replace("[ERROR]", "").Trim(),
                    Brush = Brushes.IndianRed
                };
            }

            if (line.StartsWith("[Launcher]", StringComparison.OrdinalIgnoreCase))
            {
                return new LogEntry
                {
                    Prefix = $"[{DateTime.Now:HH:mm:ss}] [Launcher]",
                    Message = line.Replace("[Launcher]", "").Trim(),
                    Brush = Brushes.WhiteSmoke
                };
            }

            var modernGameLog = Regex.Match(line, @"^\[(?<time>[^\]]+)\]\s+\[(?<thread>[^/\]]+)\/(?<level>[A-Z]+)\]:\s+(?<message>.*)$");
            if (modernGameLog.Success)
            {
                string level = modernGameLog.Groups["level"].Value;
                return new LogEntry
                {
                    Prefix = $"[{modernGameLog.Groups["time"].Value}] [Minecraft/{level}]",
                    Message = modernGameLog.Groups["message"].Value,
                    Brush = GetBrushForLevel(level)
                };
            }

            string upperLine = line.ToUpperInvariant();
            string guessedLevel = upperLine.Contains("ERROR") ? "ERROR" : upperLine.Contains("WARN") ? "WARN" : "INFO";
            return new LogEntry
            {
                Prefix = $"[{DateTime.Now:HH:mm:ss}] [Game/{guessedLevel}]",
                Message = line,
                Brush = GetBrushForLevel(guessedLevel)
            };
        }

        private static IBrush GetBrushForLevel(string level)
        {
            return level.ToUpperInvariant() switch
            {
                "ERROR" => Brushes.IndianRed,
                "WARN" => Brushes.Goldenrod,
                "DEBUG" => Brushes.LightSteelBlue,
                _ => Brushes.WhiteSmoke
            };
        }

        private void AppendEntry(LogEntry entry)
        {
            Entries.Add(entry);
            TrimOldEntries();
        }

        private void TrimOldEntries()
        {
            while (Entries.Count > MaxVisibleEntries)
            {
                Entries.RemoveAt(0);
            }
        }

        private void Clear_Click(object? sender, RoutedEventArgs e)
        {
            Entries.Clear();
        }

        private async void Copy_Click(object? sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            foreach (var entry in Entries)
            {
                sb.AppendLine($"{entry.PrefixText}{entry.Message}");
            }

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(sb.ToString());
            }
        }

        private void Close_Click(object? sender, RoutedEventArgs e) => Hide();

        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            if (e.Source is Visual source && PlatformHelper.IsInteractiveElement(source))
                return;

            BeginMoveDrag(e);
        }

        public sealed class LogEntry
        {
            public string Prefix { get; set; } = "";
            public string PrefixText => string.IsNullOrEmpty(Prefix) ? "" : Prefix + " ";
            public string Message { get; set; } = "";
            public IBrush Brush { get; set; } = Brushes.WhiteSmoke;
        }
    }
}
