using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using NoCloudware.UI.Core.Controls;
using NoCloudware.UI.Core.Services;
using NoCloudware.UI.Core.ViewModels;
using TubeMassDL.Models;
using TubeMassDL.Panels;
using TubeMassDL.Services;

namespace TubeMassDL;

public partial class App : System.Windows.Application
{
    internal static string DefaultOutputPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "TubeMassDL");

    private ClipboardMonitor? _clipboardMonitor;
    private DownloadManager? _downloadManager;
    private LinkCollector? _linkCollector;
    private YtdlpUpdater? _updater;
    private ShellWindow? _window;
    private ThemeService _themeService = new();
    private LanguageService _languageService = new();
    private readonly string _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            System.Windows.Application.Current.DispatcherUnhandledException += (_, args) =>
            {
                LogMessage($"Error: {args.Exception.Message}");
                args.Handled = true;
            };

            var (savedTheme, savedLang, savedOutputPath) = LoadPreferences();
            InitTheme(savedTheme);
            InitLogging();
            InitUpdater();
            InitWindow();
            if (!string.IsNullOrEmpty(savedOutputPath))
            {
                _window!.OutputFolderText = savedOutputPath;
                _window!.MainControl.OutputFolderText = savedOutputPath;
            }
            InitLanguageSelector(savedLang);
            InitServices();
            WireShellEvents();
            WireWindowClose();

            var optionsPanel = new OptionsPanel(_linkCollector!, _downloadManager!, _updater!);
            _window!.MainControl.OptionsContent.Content = optionsPanel;

            LogMessage("TubeMassDL v1.0 iniciado.");
            _window!.Show();
        }
        catch (Exception ex)
        {
            File.WriteAllText("crash.log", $"[{DateTime.Now:HH:mm:ss}] Fatal: {ex}\n{ex.StackTrace}");
            System.Windows.MessageBox.Show($"Error: {ex.Message}", "Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private (bool isDark, string lang, string? outputPath) LoadPreferences()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return (true, "auto", null);
            var json = File.ReadAllText(_settingsPath);
            using var doc = JsonDocument.Parse(json);
            var s = doc.RootElement.GetProperty("Settings");
            bool dark = s.TryGetProperty("DarkTheme", out var t) ? t.GetBoolean() : true;
            string lang = s.TryGetProperty("Language", out var l) ? l.GetString() ?? "auto" : "auto";
            string? outputPath = s.TryGetProperty("DefaultOutputPath", out var o) ? o.GetString() : null;
            return (dark, lang, outputPath);
        }
        catch { return (true, "auto", null); }
    }

    private void SavePreferences()
    {
        try
        {
            string lang = _languageService.CurrentCulture.TwoLetterISOLanguageName;
            string outputPath = _window?.MainControl.OutputFolderText ?? "";
            var prefs = new { Settings = new { DarkTheme = _themeService.IsDarkTheme, Language = lang, MaxConcurrentDownloads = 3, DefaultOutputPath = outputPath, AntiBlockMode = true } };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void InitTheme(bool isDark)
    {
        _themeService = new ThemeService();
        _themeService.ThemeChanged += (_, dark) => SavePreferences();
        _themeService.ApplyTheme(isDark);
    }

    private void InitLogging()
    {
        LogMessage("Iniciando TubeMassDL v2.0...");
    }

    private void InitUpdater()
    {
        _updater = new YtdlpUpdater();
        try
        {
            var task = Task.Run(() => InitializeUpdaterAsync());
            task.Wait(TimeSpan.FromSeconds(30));
        }
        catch { }
    }

    private void InitWindow()
    {
        _window = new ShellWindow
        {
            Title = "TubeMassDL - Cazador de Enlaces",
            Width = 1100, Height = 750,
            WindowMinWidth = 900, WindowMinHeight = 600,
            DropText = "Los enlaces se capturan automáticamente del portapapeles",
            AcceptedFormatsText = "",
            ActionButtonText = "Descargar todos",
            AboutButtonText = "Acerca de",
            DonateButtonText = "Donar",
            ExitButtonText = "Salir",
            OutputFolderText = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads", "TubeMassDL")
        };
        _window.MainControl.ShowSelectFilesButton = Visibility.Collapsed;
        _window.MainControl.OptionsPanelMinWidth = 280;
        _window.MainControl.AppTitle = "TubeMassDL";
        _window.MainControl.AppTagline = "Cazador de Enlaces y Descargador Masivo";
        _window.MainControl.FileListHeader = "Cola de Descargas";
        _window.MainControl.OutputFolderText = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads", "TubeMassDL");
        _window.MainControl.GlobalProgressVisible = Visibility.Visible;
    }

    private void InitLanguageSelector(string savedLang)
    {
        if (_window == null) return;
        var flagBase = "pack://application:,,,/NoCloudware.UI.Core;component/Assets/Flags/";
        _window.LanguageSelector.Languages = new ObservableCollection<LanguageItem>
        {
            new("English",   $"{flagBase}flag-uk.png", "en"),
            new("Español",   $"{flagBase}flag-es.png", "es"),
            new("Français",  $"{flagBase}flag-fr.png", "fr"),
            new("Deutsch",   $"{flagBase}flag-de.png", "de"),
            new("Português", $"{flagBase}flag-br.png", "pt"),
            new("中文",       $"{flagBase}flag-cn.png", "zh"),
            new("日本語",     $"{flagBase}flag-jp.png", "ja")
        };

        _window.LanguageSelector.ComboMaxWidth = 36;

        string initialLang = savedLang == "auto"
            ? _languageService.DetectSystemLanguage().TwoLetterISOLanguageName
            : savedLang;

        _window.LanguageSelector.SetLanguage(initialLang);
        ApplyLanguage(new CultureInfo(initialLang));

        _window.LanguageSelector.LanguageChanged += (_, args) =>
        {
            if (args is LanguageChangedEventArgs a)
            {
                var ci = new CultureInfo(a.CultureCode);
                ApplyLanguage(ci);
                _languageService.SetLanguage(ci);
                SavePreferences();
            }
        };
    }

    private void ApplyLanguage(CultureInfo ci)
    {
        Thread.CurrentThread.CurrentUICulture = ci;
        if (_window == null) return;

        _window.Title = Translations.Get("AppTitle", ci);
        _window.DropText = Translations.Get("DropText", ci);
        _window.ActionButtonText = Translations.Get("ActionButton", ci);
        _window.AboutButtonText = Translations.Get("AboutButton", ci);
        _window.DonateButtonText = Translations.Get("DonateButton", ci);
        _window.ExitButtonText = Translations.Get("ExitButton", ci);

        _window.MainControl.FileListHeader = Translations.Get("FileListHeader", ci);
        _window.MainControl.AppTagline = Translations.Get("AppTagline", ci);

        _window.MainControl.StatusBar.TotalLabel = Translations.Get("StatusTotal", ci);
        _window.MainControl.StatusBar.ProcessedLabel = Translations.Get("StatusProcessed", ci);
        _window.MainControl.StatusBar.PendingLabel = Translations.Get("StatusPending", ci);
        _window.MainControl.StatusBar.ErrorsLabel = Translations.Get("StatusErrors", ci);

        if (_window.MainControl.OptionsContent.Content is OptionsPanel panel)
            panel.ApplyLanguage(ci);

        _window.MainControl.ChangeButtonText = Translations.Get("ChangeBtn", ci);
        _window.FileListBox.RemoveMenuItemText = Translations.Get("RemoveBtn", ci);
        _window.FileListBox.ClearAllMenuItemText = Translations.Get("ClearAllBtn", ci);

        _window.MainControl.UpdateCounters();
    }

    private void InitServices()
    {
        if (_window == null) return;

        _linkCollector = new LinkCollector();
        _downloadManager = new DownloadManager();
        _clipboardMonitor = new ClipboardMonitor();

        _linkCollector.QueueUpdated += () => SyncQueueToWindow();

        _clipboardMonitor.LinkCaptured += link =>
        {
            _linkCollector?.AddOrUpdate(link);
            LogMessage($"Capturado: {link.Url}");
            TaskbarFlashService.Flash(_window!);
        };
        _clipboardMonitor.Start();
        LogMessage("Listener de portapapeles activado.");

        _downloadManager.ItemProgress += (item, progress) =>
        {
            _window?.Dispatcher.Invoke(() =>
            {
                item.Progress = progress;
                item.StatusText = $"{progress}%";
                _linkCollector?.UpdatePlaylistProgressFromChild(item);
                _window.MainControl.UpdateCounters();
            });
        };

        _downloadManager.ItemCompleted += (item, success) =>
        {
            _window?.Dispatcher.Invoke(() =>
            {
                item.Status = success ? FileStatus.Processed : FileStatus.Error;
                item.StatusText = success ? "Completado" : "Error";
                item.ProgressBarVisible = false;
                _linkCollector?.UpdatePlaylistProgressFromChild(item);
                _window.MainControl.UpdateCounters();
            });

            LogMessage(success ? $"Completado: {item.FileName}" : $"Error: {item.FileName}");
            if (_window != null) TaskbarFlashService.Flash(_window);
        };

        _downloadManager.AllCompleted += () =>
        {
            _window?.Dispatcher.Invoke(() =>
            {
                _window.MainControl.UpdateCounters();
            });

            LogMessage("Todas las descargas completadas.");
            if (_window != null) TaskbarFlashService.Flash(_window);
        };
    }

    private void WireShellEvents()
    {
        if (_window == null) return;

        _window.AboutClick += (_, _) =>
        {
            var ci = _languageService.CurrentCulture;
            var assemblyVer = System.Reflection.Assembly.GetEntryAssembly()
                ?.GetName().Version;
            var appVersion = assemblyVer != null
                ? $"{assemblyVer.Major}.{assemblyVer.Minor}.{assemblyVer.Build}"
                : "1.0.0";

            var logo = new BitmapImage();
            logo.BeginInit();
            logo.UriSource = new Uri("pack://application:,,,/TubeMassDL;component/Resources/tubemassdl-logo.png");
            logo.CacheOption = BitmapCacheOption.OnLoad;
            logo.EndInit();

            var about = new AboutDialog
            {
                AppName = "TubeMassDL",
                AppVersion = $"{Translations.Get("AboutVersion", ci)} v{appVersion}",
                AppLogo = logo,
                DialogTitle = Translations.Get("AboutTitle", ci),
                CreditsHeader = Translations.Get("AboutCredits", ci),
                DevelopedByText = Translations.Get("AboutDevelopedBy", ci),
                DeveloperName = Translations.Get("AboutDeveloperName", ci),
                DeveloperUrl = "https://www.nocloudware.com",
                ThirdPartyLibrariesText = Translations.Get("AboutThirdPartyLibraries", ci),
                YtDlpDesc = Translations.Get("AboutYtDlpDesc", ci),
                WpfUiDesc = Translations.Get("AboutWpfUiDesc", ci),
                MvvmDesc = Translations.Get("AboutMvvmDesc", ci),
                SpecialThanksText = Translations.Get("AboutSpecialThanks", ci),
                SpecialThanksMessage = Translations.Get("AboutSpecialThanksMessage", ci),
                TechnologiesUsedText = Translations.Get("AboutTechnologiesUsed", ci),
                TechList = Translations.Get("AboutTechList", ci),
                LicenseText = Translations.Get("AboutLicense", ci),
                LicenseInfo = Translations.Get("AboutLicenseInfo", ci),
                CheckUpdatesText = Translations.Get("CheckUpdatesBtn", ci),
                CloseButtonText = Translations.Get("CloseBtn", ci),
                Owner = _window
            };

            about.CheckUpdatesClick += async (s, e) =>
            {
                about.CheckUpdatesText = Translations.Get("CheckingUpdates", ci);
                about.IsCheckUpdatesEnabled = false;
                try
                {
                    var updateService = new AppUpdateService();
                    var updateInfo = await updateService.CheckForUpdatesAsync();

                    if (updateInfo == null)
                    {
                        about.CheckUpdatesText = Translations.Get("UpdateCheckFailed", ci);
                        System.Windows.MessageBox.Show(
                            Translations.Get("UpdateCheckFailed", ci),
                            Translations.Get("AboutTitle", ci),
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else if (updateInfo.IsNewerVersion)
                    {
                        string msg = string.Format(Translations.Get("UpdateAvailable", ci), updateInfo.Version);
                        about.CheckUpdatesText = msg;
                        var result = System.Windows.MessageBox.Show(
                            msg + "\n\n" + Translations.Get("DownloadPrompt", ci),
                            Translations.Get("AboutTitle", ci),
                            MessageBoxButton.YesNo, MessageBoxImage.Information);
                        if (result == MessageBoxResult.Yes)
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = updateInfo.DownloadUrl,
                                UseShellExecute = true
                            });
                    }
                    else
                    {
                        string msg = string.Format(Translations.Get("UpdateUpToDate", ci), $"v{appVersion}");
                        about.CheckUpdatesText = msg;
                        System.Windows.MessageBox.Show(
                            msg,
                            Translations.Get("AboutTitle", ci),
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch
                {
                    about.CheckUpdatesText = Translations.Get("UpdateCheckFailed", ci);
                }
                finally
                {
                    about.IsCheckUpdatesEnabled = true;
                }
            };

            about.ShowDialog();
        };

        _window.DonateClick += (_, _) =>
            new DonationService("https://nocloudware.com/donate.html").OpenDonationPage();

        _window.ThemeToggle.ThemeToggled += (_, args) =>
        {
            if (args is ThemeToggledEventArgs a)
            {
                _themeService.ApplyTheme(a.IsDarkTheme);
                SavePreferences();
            }
        };

        _window.ActionClick += (_, _) =>
        {
            // "Descargar todos": descarga TODOS los elementos sin importar selección
            var allItems = _linkCollector?.Items
                .Where(i => i.SourceText != "📁 Playlist" && i.Status is FileStatus.Queued or FileStatus.Error)
                .ToList() ?? new List<BaseFileItem>();
            if (allItems.Count == 0)
            {
                LogMessage("No hay elementos para descargar.");
                return;
            }

            if (_window.MainControl.OptionsContent.Content is not OptionsPanel panel) return;
            string outputPath = _window.MainControl.OutputFolderText;
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads", "TubeMassDL");
            Directory.CreateDirectory(outputPath);

            string format = panel.GetSelectedFormat();
            bool antiBlock = panel.AntiBlockEnabled;
            bool extractAudio = panel.ExtractAudio;

            var tasks = allItems.Select(item => new DownloadTask
            {
                Item = item,
                OutputPath = outputPath,
                Format = format,
                AntiBlock = antiBlock,
                ExtractAudio = extractAudio
            });

            LogMessage($"Añadiendo {allItems.Count} elemento(s) a la cola...");
            _downloadManager!.Enqueue(tasks);
            panel.SetDownloadingState(true);
        };

        _window.ExitClick += (_, _) => _window.Close();

        _window.GotFocus += (_, _) => TaskbarFlashService.StopFlashing(_window);

        _window.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Delete)
            {
                var first = _window.Files.FirstOrDefault(i => i.IsSelected);
                if (first != null)
                    _linkCollector?.RemoveItem(first);
                args.Handled = true;
            }
        };

        _window.FileListBox.ItemDoubleClick += (_, args) =>
        {
            var item = args.Item;

            if (item.SourceText == "📁 Playlist")
            {
                _linkCollector?.TogglePlaylist(item);
                SyncQueueToWindow();
            }
            else if (item.Status == FileStatus.Processing)
            {
                // Pause and requeue: move to end of queue
                bool paused = _downloadManager?.PauseAndRequeue(item) ?? false;
                if (paused)
                {
                    LogMessage($"Pausado y movido al final: {item.FileName}");
                    SyncQueueToWindow();
                }
            }
            else if (item.Status is FileStatus.Queued or FileStatus.Error)
            {
                DownloadSingleItem(item);
            }
        };
    }

    private void DownloadSingleItem(BaseFileItem item)
    {
        if (_window?.MainControl.OptionsContent.Content is not OptionsPanel panel) return;
        if (_downloadManager == null) return;

        string outputPath = _window.MainControl.OutputFolderText;
        string format = panel.GetSelectedFormat();
        bool antiBlock = panel.AntiBlockEnabled;
        bool extractAudio = panel.ExtractAudio;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads", "TubeMassDL");
            _window.MainControl.OutputFolderText = outputPath;
        }
        Directory.CreateDirectory(outputPath);

        LogMessage($"Añadiendo a cola: {item.FileName}");
        _downloadManager.Enqueue(new DownloadTask
        {
            Item = item,
            OutputPath = outputPath,
            Format = format,
            AntiBlock = antiBlock,
            ExtractAudio = extractAudio
        });
    }

    private void WireWindowClose()
    {
        _window!.Closing += (_, _) =>
        {
            SavePreferences();
            _clipboardMonitor?.Stop();
            _downloadManager?.Stop();
            LogMessage("Aplicación cerrada.");
        };
    }

    private void LogMessage(string msg)
    {
        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
    }

    private async Task InitializeUpdaterAsync()
    {
        try
        {
            var result = await _updater!.CheckAndUpdateAsync();
            LogMessage(result.success ? $"yt-dlp {result.version}" : "No se pudo verificar yt-dlp.");
        }
        catch (Exception ex) { LogMessage($"Error en actualizador: {ex.Message}"); }
    }

    private void SyncQueueToWindow()
    {
        _window?.Dispatcher.Invoke(() =>
        {
            var items = _linkCollector?.Items;
            if (items == null) return;
            _window!.Files.Clear();
            foreach (var item in items)
                _window.Files.Add(item);
            _window.MainControl.UpdateCounters();
        });
    }
}
