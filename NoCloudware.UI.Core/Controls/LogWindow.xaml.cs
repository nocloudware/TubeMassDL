using System.Windows;
using NoCloudware.UI.Core.Diagnostics;

namespace NoCloudware.UI.Core.Controls;

public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try { System.Windows.Clipboard.SetText(SessionLog.ToText()); } catch { }
    }

    private void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = SessionLog.LogFilePath,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        SessionLog.Clear();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
            Close();
    }
}
