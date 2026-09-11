// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CmlLib.Core;

namespace RaytolfasLauncher
{
    public partial class ModpacksWindow : Window
    {
        private readonly string minecraftPath;
        private readonly string language;
        private readonly MinecraftLauncher? launcher;
        private ModpackInfo? currentPack;
        private string installedFilter = "all";
        private CancellationTokenSource? currentOperationCts;

        public ModpackInfo? SelectedModpack { get; private set; }

        public ModpacksWindow() : this(PlatformHelper.GetDefaultMinecraftFolder()) { }

        public ModpacksWindow(string minecraftPath, string language = "en", MinecraftLauncher? launcher = null)
        {
            InitializeComponent();
            this.minecraftPath = minecraftPath;
            this.language = LocalizationManager.NormalizeLanguage(language);
            this.launcher = launcher;
            ModpackInfo.CurrentDisplayLanguage = this.language;

            ApplyLocalization();
            InitializeFormControls();
            LoadModpacks();
            _ = LoadAllVersionsAsync();
        }

        private string T(string key, params object[] args)
        {
            string template = LocalizationManager.Get(key, language);
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        private void ApplyLocalization()
        {
            Title = T("modpacks.window_title");
            HeaderTitleText.Text = T("modpacks.header_title");
            HeaderSubtitleText.Text = T("modpacks.header_subtitle");
            YourPacksLabel.Text = T("modpacks.your_packs");
            CreatePackButton.Content = T("modpacks.btn_create");
            ImportMrPackButton.Content = T("modpacks.btn_import");
            DeletePackButton.Content = T("modpacks.btn_delete");

            NoSelectionTitle.Text = T("modpacks.no_sel_title");
            NoSelectionSubtitle.Text = T("modpacks.no_sel_desc");
            NoSelectionCreateButton.Content = T("modpacks.no_sel_create");

            SelectForPlayBtn.Content = T("modpacks.select_for_play");
            OpenPackFolderBtn.Content = T("modpacks.folder_pack");

            TabInstalledBtn.Content = T("modpacks.tab_installed");
            TabModrinthBtn.Content = T("modpacks.tab_modrinth");

            FilterAllBtn.Content = T("modpacks.filter_all");
            FilterModsBtn.Content = T("modpacks.filter_mods");
            FilterPacksBtn.Content = T("modpacks.filter_resourcepacks");
            FilterShadersBtn.Content = T("modpacks.filter_shaderpacks");

            ModrinthSearchBox.Watermark = T("modpacks.search_watermark");
            SearchModrinthButton.Content = T("modpacks.btn_search");

            ModalTitleText.Text = T("modpacks.modal_title");
            ModalPackNameLabel.Text = T("modpacks.name_label");
            NewPackNameBox.Watermark = T("modpacks.name_watermark");
            ModalPackVersionLabel.Text = T("modpacks.version_label");
            ModalPackLoaderLabel.Text = T("modpacks.loader_label");
            CancelCreateButton.Content = T("modpacks.btn_cancel");
            ConfirmCreateButton.Content = T("modpacks.btn_confirm_create");
        }

        private void InitializeFormControls()
        {
            ModrinthTypeBox.Items.Clear();
            ModrinthTypeBox.Items.Add(new ComboBoxItem { Content = T("modpacks.type_mods"), Tag = "mod" });
            ModrinthTypeBox.Items.Add(new ComboBoxItem { Content = T("modpacks.type_resourcepacks"), Tag = "resourcepack" });
            ModrinthTypeBox.Items.Add(new ComboBoxItem { Content = T("modpacks.type_shaders"), Tag = "shader" });
            ModrinthTypeBox.SelectedIndex = 0;

            NewPackVersionBox.Text = "1.20.1";
            NewPackVersionSelect.Items.Clear();
            var popularVersions = new[]
            {
                "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21",
                "1.20.6", "1.20.4", "1.20.2", "1.20.1", "1.20",
                "1.19.4", "1.19.2", "1.19",
                "1.18.2", "1.17.1", "1.16.5", "1.12.2", "1.8.9", "1.7.10"
            };
            foreach (var v in popularVersions)
            {
                NewPackVersionSelect.Items.Add(new ComboBoxItem { Content = v, Tag = v });
            }
            var initialMatch = NewPackVersionSelect.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Tag?.ToString() == "1.20.1");
            if (initialMatch != null)
                NewPackVersionSelect.SelectedItem = initialMatch;

            NewPackLoaderBox.Items.Clear();
            NewPackLoaderBox.Items.Add(new ComboBoxItem { Content = "Fabric", Tag = "Fabric" });
            NewPackLoaderBox.Items.Add(new ComboBoxItem { Content = "Forge", Tag = "Forge" });
            NewPackLoaderBox.Items.Add(new ComboBoxItem { Content = "NeoForge", Tag = "NeoForge" });
            NewPackLoaderBox.Items.Add(new ComboBoxItem { Content = "Quilt", Tag = "Quilt" });
            NewPackLoaderBox.Items.Add(new ComboBoxItem { Content = "Vanilla", Tag = "Vanilla" });
            NewPackLoaderBox.SelectedIndex = 0;
        }

        private void NewPackVersionSelect_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (NewPackVersionSelect.SelectedItem is ComboBoxItem item && item.Tag is string ver)
            {
                NewPackVersionBox.Text = ver;
            }
        }

        private async Task LoadAllVersionsAsync()
        {
            var versionList = new List<string>();

            if (launcher != null)
            {
                try
                {
                    var allVersions = await launcher.GetAllVersionsAsync();
                    foreach (var v in allVersions.Where(x => x.Type == "release" || x.Type == "custom" || x.Type == "modified"))
                    {
                        if (!string.IsNullOrWhiteSpace(v.Name) && !versionList.Contains(v.Name))
                            versionList.Add(v.Name);
                    }
                }
                catch
                {
                }
            }

            if (versionList.Count == 0)
            {
                versionList.AddRange(new[]
                {
                    "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21",
                    "1.20.6", "1.20.5", "1.20.4", "1.20.3", "1.20.2", "1.20.1", "1.20",
                    "1.19.4", "1.19.2", "1.19",
                    "1.18.2", "1.17.1", "1.16.5", "1.12.2", "1.8.9", "1.7.10"
                });
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                string current = NewPackVersionBox.Text?.Trim() ?? "1.20.1";

                NewPackVersionSelect.SelectionChanged -= NewPackVersionSelect_SelectionChanged;
                NewPackVersionSelect.Items.Clear();
                foreach (var v in versionList)
                {
                    NewPackVersionSelect.Items.Add(new ComboBoxItem { Content = v, Tag = v });
                }

                var match = NewPackVersionSelect.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Tag?.ToString() == current);
                if (match != null)
                    NewPackVersionSelect.SelectedItem = match;

                NewPackVersionSelect.SelectionChanged += NewPackVersionSelect_SelectionChanged;
            });
        }

        public void LoadModpacks()
        {
            var packs = ModpackManager.GetModpacks(minecraftPath);
            ModpacksListBox.ItemsSource = packs;

            if (currentPack != null)
            {
                var match = packs.FirstOrDefault(p => p.Id == currentPack.Id);
                ModpacksListBox.SelectedItem = match ?? packs.FirstOrDefault();
            }
            else if (packs.Count > 0)
            {
                ModpacksListBox.SelectedIndex = 0;
            }
            else
            {
                ShowNoSelection();
            }
        }

        private void ModpacksListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ModpacksListBox.SelectedItem is ModpackInfo pack)
            {
                SelectModpack(pack);
            }
            else
            {
                ShowNoSelection();
            }
        }

        private void SelectModpack(ModpackInfo pack)
        {
            currentPack = pack;
            NoSelectionPanel.IsVisible = false;
            PackDetailsPanel.IsVisible = true;

            SelectedPackTitle.Text = pack.DisplayName;
            SelectedPackBadgeText.Text = $"{pack.Loader} {pack.GameVersion}";

            RefreshInstalledFiles();
            TriggerModrinthSearch();
        }

        private void ShowNoSelection()
        {
            currentPack = null;
            NoSelectionPanel.IsVisible = true;
            PackDetailsPanel.IsVisible = false;
        }

        private void RefreshInstalledFiles()
        {
            if (currentPack == null) return;
            var files = ModpackManager.GetInstalledFiles(currentPack, installedFilter);
            InstalledFilesListBox.ItemsSource = files;
        }

        private void ShowInstalledTab_Click(object? sender, RoutedEventArgs e)
        {
            TabInstalledBtn.Background = Brush.Parse("#BB86FC");
            TabInstalledBtn.Foreground = Brushes.Black;
            TabModrinthBtn.Background = Brush.Parse("#252525");
            TabModrinthBtn.Foreground = Brushes.White;

            InstalledTabPanel.IsVisible = true;
            ModrinthTabPanel.IsVisible = false;
            RefreshInstalledFiles();
        }

        private void ShowModrinthTab_Click(object? sender, RoutedEventArgs e)
        {
            TabInstalledBtn.Background = Brush.Parse("#252525");
            TabInstalledBtn.Foreground = Brushes.White;
            TabModrinthBtn.Background = Brush.Parse("#BB86FC");
            TabModrinthBtn.Foreground = Brushes.Black;

            InstalledTabPanel.IsVisible = false;
            ModrinthTabPanel.IsVisible = true;

            if (ModrinthResultsListBox.ItemsSource == null)
            {
                TriggerModrinthSearch();
            }
        }

        private void OpenPackFolder_Click(object? sender, RoutedEventArgs e)
        {
            if (currentPack != null)
                PlatformHelper.OpenFolder(currentPack.RootPath);
        }

        private void OpenModsFolder_Click(object? sender, RoutedEventArgs e)
        {
            if (currentPack != null)
            {
                Directory.CreateDirectory(currentPack.ModsPath);
                PlatformHelper.OpenFolder(currentPack.ModsPath);
            }
        }

        private void OpenResourcepacksFolder_Click(object? sender, RoutedEventArgs e)
        {
            if (currentPack != null)
            {
                Directory.CreateDirectory(currentPack.ResourcepacksPath);
                PlatformHelper.OpenFolder(currentPack.ResourcepacksPath);
            }
        }

        private void OpenShaderpacksFolder_Click(object? sender, RoutedEventArgs e)
        {
            if (currentPack != null)
            {
                Directory.CreateDirectory(currentPack.ShaderpacksPath);
                PlatformHelper.OpenFolder(currentPack.ShaderpacksPath);
            }
        }

        private void SelectForPlay_Click(object? sender, RoutedEventArgs e)
        {
            if (currentPack == null) return;
            SelectedModpack = currentPack;
            Close(true);
        }

        private void FilterInstalled_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                installedFilter = tag;

                FilterAllBtn.Background = tag == "all" ? Brush.Parse("#BB86FC") : Brush.Parse("#252525");
                FilterAllBtn.Foreground = tag == "all" ? Brushes.Black : Brushes.White;
                FilterModsBtn.Background = tag == "mods" ? Brush.Parse("#BB86FC") : Brush.Parse("#252525");
                FilterModsBtn.Foreground = tag == "mods" ? Brushes.Black : Brushes.White;
                FilterPacksBtn.Background = tag == "resourcepacks" ? Brush.Parse("#BB86FC") : Brush.Parse("#252525");
                FilterPacksBtn.Foreground = tag == "resourcepacks" ? Brushes.Black : Brushes.White;
                FilterShadersBtn.Background = tag == "shaderpacks" ? Brush.Parse("#BB86FC") : Brush.Parse("#252525");
                FilterShadersBtn.Foreground = tag == "shaderpacks" ? Brushes.Black : Brushes.White;

                RefreshInstalledFiles();
            }
        }

        private async void DeleteFile_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && File.Exists(path))
            {
                string filename = Path.GetFileName(path);
                bool? confirm = await RayMessageBox.ShowConfirmAsync(this, T("modpacks.delete_file_confirm", filename), T("modpacks.delete_file_title"));
                if (confirm == true)
                {
                    ModpackManager.DeleteFile(path);
                    RefreshInstalledFiles();
                    SetStatus(T("modpacks.file_deleted", filename));
                }
            }
        }

        private void ModrinthSearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TriggerModrinthSearch();
            }
        }

        private void ModrinthTypeBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                TriggerModrinthSearch();
        }

        private void SearchModrinth_Click(object? sender, RoutedEventArgs e)
        {
            TriggerModrinthSearch();
        }

        private async void TriggerModrinthSearch()
        {
            if (currentPack == null) return;

            string query = ModrinthSearchBox?.Text?.Trim() ?? "";
            string projectType = (ModrinthTypeBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "mod";

            SetBusy(true, T("modpacks.searching"));

            try
            {
                var hits = await ModpackManager.SearchModrinthAsync(
                    query,
                    projectType,
                    currentPack.GameVersion,
                    currentPack.Loader);

                ModrinthResultsListBox.ItemsSource = hits;
                SetStatus(T("modpacks.found_results", hits.Count));
            }
            catch (Exception ex)
            {
                SetStatus(T("modpacks.search_error", ex.Message));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void InstallModrinthProject_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not ModrinthProjectHit hit || currentPack == null)
                return;

            string projectType = hit.ProjectType;
            string targetDir = projectType switch
            {
                "resourcepack" => currentPack.ResourcepacksPath,
                "shader" => currentPack.ShaderpacksPath,
                _ => currentPack.ModsPath
            };

            btn.IsEnabled = false;
            btn.Content = "⏳...";
            SetBusy(true, T("modpacks.downloading", hit.Title));

            currentOperationCts?.Cancel();
            currentOperationCts = new CancellationTokenSource();

            var progress = new Progress<double>(val =>
            {
                OperationProgress.Value = val;
                OperationProgress.IsIndeterminate = false;
            });

            try
            {
                string filename = await ModpackManager.DownloadModrinthProjectAsync(
                    hit.ProjectId,
                    currentPack.GameVersion,
                    currentPack.Loader,
                    targetDir,
                    progress,
                    currentOperationCts.Token);

                btn.Content = T("modpacks.btn_installed");
                btn.Background = Brush.Parse("#4CAF50");
                SetStatus(T("modpacks.installed_file", filename));

                RefreshInstalledFiles();
                LoadModpacks();
            }
            catch (Exception ex)
            {
                btn.IsEnabled = true;
                btn.Content = T("modpacks.btn_install");
                SetStatus(T("modpacks.install_error", ex.Message));
                await RayMessageBox.ShowAsync(this, ex.Message, T("modpacks.download_error_title"));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void OpenCreateModal_Click(object? sender, RoutedEventArgs e)
        {
            NewPackNameBox.Text = T("modpacks.default_name", DateTime.Now.ToString("dd.MM"));
            CreateModal.IsVisible = true;
        }

        private void CancelCreate_Click(object? sender, RoutedEventArgs e)
        {
            CreateModal.IsVisible = false;
        }

        private async void ConfirmCreate_Click(object? sender, RoutedEventArgs e)
        {
            string name = NewPackNameBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                await RayMessageBox.ShowAsync(this, T("modpacks.enter_name"), T("modpacks.error_title"));
                return;
            }

            string version = NewPackVersionBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(version))
            {
                if (NewPackVersionSelect.SelectedItem is ComboBoxItem item)
                    version = item.Tag?.ToString() ?? item.Content?.ToString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(version))
                version = "1.20.1";

            string loader = (NewPackLoaderBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Fabric";

            SetBusy(true, T("modpacks.creating"));

            try
            {
                var newPack = await ModpackManager.CreateModpackAsync(minecraftPath, name, version, loader);
                CreateModal.IsVisible = false;
                LoadModpacks();
                SelectModpack(newPack);
                SetStatus(T("modpacks.created", newPack.DisplayName));
            }
            catch (Exception ex)
            {
                await RayMessageBox.ShowAsync(this, ex.Message, T("modpacks.create_error_title"));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void DeleteModpack_Click(object? sender, RoutedEventArgs e)
        {
            if (currentPack == null) return;

            bool? confirm = await RayMessageBox.ShowConfirmAsync(
                this,
                T("modpacks.delete_confirm", currentPack.DisplayName),
                T("modpacks.delete_title"));

            if (confirm == true)
            {
                ModpackManager.DeleteModpack(minecraftPath, currentPack);
                LoadModpacks();
                SetStatus(T("modpacks.deleted"));
            }
        }

        private async void ImportMrPack_Click(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = T("modpacks.import_title"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Modrinth Pack") { Patterns = new[] { "*.mrpack" } }
                }
            });

            if (files.Count == 0) return;

            string filePath = files[0].Path.LocalPath;
            SetBusy(true, T("modpacks.importing"));

            try
            {
                using var archive = ZipFile.OpenRead(filePath);
                var indexEntry = archive.GetEntry("modrinth.index.json")
                    ?? throw new InvalidOperationException(T("modpacks.import_no_index"));

                using var indexStream = indexEntry.Open();
                using var doc = await JsonDocument.ParseAsync(indexStream);
                var root = doc.RootElement;

                string packName = root.TryGetProperty("name", out var n) ? n.GetString() ?? "Imported Pack" : "Imported Pack";
                string mcVersion = "1.20.1";
                string loader = "Fabric";

                if (root.TryGetProperty("dependencies", out var deps))
                {
                    if (deps.TryGetProperty("minecraft", out var mv))
                        mcVersion = mv.GetString() ?? mcVersion;

                    if (deps.TryGetProperty("forge", out _))
                        loader = "Forge";
                    else if (deps.TryGetProperty("neoforge", out _))
                        loader = "NeoForge";
                    else if (deps.TryGetProperty("quilt-loader", out _))
                        loader = "Quilt";
                    else if (deps.TryGetProperty("fabric-loader", out _))
                        loader = "Fabric";
                }

                var newPack = await ModpackManager.CreateModpackAsync(minecraftPath, packName, mcVersion, loader);

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase))
                    {
                        string relPath = entry.FullName.Substring("overrides/".Length);
                        if (string.IsNullOrWhiteSpace(relPath) || relPath.EndsWith("/")) continue;

                        string destPath = Path.Combine(newPack.RootPath, relPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                }

                LoadModpacks();
                SelectModpack(newPack);
                SetStatus(T("modpacks.imported", packName));
                await RayMessageBox.ShowAsync(this, T("modpacks.import_success_msg", packName, loader, mcVersion), T("modpacks.import_success_title"));
            }
            catch (Exception ex)
            {
                await RayMessageBox.ShowAsync(this, ex.Message, T("modpacks.import_error_title"));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetStatus(string text)
        {
            OperationStatusText.Text = text;
        }

        private void SetBusy(bool isBusy, string text = "")
        {
            OperationProgress.IsVisible = isBusy;
            OperationProgress.IsIndeterminate = isBusy;
            if (!string.IsNullOrEmpty(text))
                OperationStatusText.Text = text;
        }

        private void Close_Click(object? sender, RoutedEventArgs e) => Close(false);

        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            if (e.Source is Visual source && PlatformHelper.IsInteractiveElement(source))
                return;

            BeginMoveDrag(e);
        }
    }
}
