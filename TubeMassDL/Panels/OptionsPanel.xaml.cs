using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using NoCloudware.UI.Core.Controls;
using TubeMassDL.Models;
using TubeMassDL.Services;

namespace TubeMassDL.Panels;

public partial class OptionsPanel : System.Windows.Controls.UserControl
{
    private readonly LinkCollector _collector;
    private readonly DownloadManager _downloadManager;
    private bool _initialized;

    private static readonly string[] VideoFormats = { "mp4", "avi", "webm", "mkv" };
    private static readonly string[] AudioFormats = { "mp3", "m4a", "opus", "wav" };

    public OptionsPanel(LinkCollector collector, DownloadManager downloadManager, YtdlpUpdater updater)
    {
        InitializeComponent();
        _collector = collector;
        _downloadManager = downloadManager;
        _initialized = true;
        PopulateFormats(true);
    }

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        bool isVideo = TypeComboBox.SelectedIndex == 0;
        PopulateFormats(isVideo);
    }

    private void PopulateFormats(bool isVideo)
    {
        if (FormatComboBox?.Items == null) return;
        FormatComboBox.Items.Clear();
        var formats = isVideo ? VideoFormats : AudioFormats;
        foreach (var fmt in formats)
            FormatComboBox.Items.Add(new ComboBoxItem { Content = fmt });
        if (FormatComboBox.Items.Count > 0)
            FormatComboBox.SelectedIndex = 0;
        QualityLabel.Visibility = isVideo ? Visibility.Visible : Visibility.Collapsed;
        QualityComboBox.Visibility = isVideo ? Visibility.Visible : Visibility.Collapsed;
        if (!isVideo)
            QualityComboBox.SelectedIndex = -1;
    }

    private void OnAddUrlClick(object sender, RoutedEventArgs e)
    {
        string url = UrlTextBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out _)) return;

        _collector.AddOrUpdate(new CapturedLink { Url = url, Timestamp = DateTime.Now });
        UrlTextBox.Clear();
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        var win = Window.GetWindow(this) as ShellWindow;
        if (win == null) return;
        win.RaiseEvent(new RoutedEventArgs(BaseMainControl.ActionClickEvent));
    }

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        if (_downloadManager.IsPaused)
        {
            _downloadManager.Resume();
            PauseButton.Content = "PAUSAR";
        }
        else
        {
            // Try to pause the currently processing item
            _downloadManager.Pause();
            PauseButton.Content = "REANUDAR";
        }
        StopButton.IsEnabled = true;
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        _downloadManager.Stop();
        PauseButton.Content = "PAUSAR";
        SetDownloadingState(false);
    }

    private void OnChangePathClick(object sender, RoutedEventArgs e)
    {
        var shell = Window.GetWindow(this) as ShellWindow;
        if (shell == null) return;
        var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            shell.MainControl.OutputFolderText = dialog.SelectedPath;
    }

    public void ApplyLanguage(CultureInfo ci)
    {
        CaptureLabel.Text = "📋 " + Translations.Get("CaptureLabel", ci);
        UrlLabel.Text = Translations.Get("UrlLabel", ci);
        TypeLabel.Text = Translations.Get("TypeLabel", ci);
        FormatLabel.Text = Translations.Get("FormatLabel", ci);
        if (TypeComboBox.Items.Count > 0 && TypeComboBox.Items[0] is ComboBoxItem type0)
            type0.Content = "🎬 " + Translations.Get("TypeVideo", ci);
        if (TypeComboBox.Items.Count > 1 && TypeComboBox.Items[1] is ComboBoxItem type1)
            type1.Content = "🎵 " + Translations.Get("TypeAudio", ci);
        AntiBlockCheckBox.Content = "🛡️ " + Translations.Get("AntiBlock", ci);
        DownloadButton.Content = Translations.Get("DownloadBtn", ci);
    }

    public void SetDownloadingState(bool downloading)
    {
        if (DownloadButton != null) DownloadButton.IsEnabled = !downloading;
        if (PauseButton != null) PauseButton.IsEnabled = downloading;
        if (StopButton != null) StopButton.IsEnabled = downloading;
    }

    public string GetSelectedFormat()
    {
        bool isVideo = TypeComboBox.SelectedIndex == 0;
        string sf = FormatComboBox.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString()?.ToLowerInvariant() ?? "mp4"
            : "mp4";

        if (!isVideo)
        {
            // Audio: request native format directly when possible
            return sf switch
            {
                "m4a" => "bestaudio[ext=m4a]/bestaudio/best",
                "opus" => "bestaudio[ext=opus]/bestaudio/best",
                "mp3" or "wav" => "bestaudio/best",
                _ => "bestaudio[ext=m4a]/bestaudio/best"
            };
        }

        string quality = QualityComboBox.SelectedItem is ComboBoxItem qItem
            ? qItem.Content?.ToString() ?? ""
            : "";

        string codec = sf switch
        {
            "mp4" => "mp4",
            "webm" => "webm",
            "mkv" => "mkv",
            "avi" => "avi",
            _ => "mp4"
        };

        if (quality == "4K")
            return $"bestvideo[height<=2160][ext={codec}]+bestaudio[ext=m4a]/best[height<=2160][ext={codec}]/bestvideo+bestaudio/best";
        if (quality == "1080p")
            return $"bestvideo[height<=1080][ext={codec}]+bestaudio[ext=m4a]/best[height<=1080][ext={codec}]/bestvideo+bestaudio/best";
        if (quality == "720p")
            return $"bestvideo[height<=720][ext={codec}]+bestaudio[ext=m4a]/best[height<=720][ext={codec}]/bestvideo+bestaudio/best";
        if (quality == "480p")
            return $"bestvideo[height<=480][ext={codec}]+bestaudio[ext=m4a]/best[height<=480][ext={codec}]/bestvideo+bestaudio/best";
        if (quality == "360p")
            return $"bestvideo[height<=360][ext={codec}]+bestaudio[ext=m4a]/best[height<=360][ext={codec}]/bestvideo+bestaudio/best";

        return "bestvideo+bestaudio/best";
    }

    public bool CaptureEnabled => CaptureToggle?.IsChecked == true;
    public bool AntiBlockEnabled => AntiBlockCheckBox?.IsChecked == true;

    public bool ExtractAudio => TypeComboBox?.SelectedIndex == 1;
}
