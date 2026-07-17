using CommunityToolkit.Mvvm.ComponentModel;

namespace NoCloudware.UI.Core.ViewModels;

public partial class BaseFileItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private long _fileSize;

    [ObservableProperty]
    private FileStatus _status = FileStatus.Queued;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _resultMessage = string.Empty;

    [ObservableProperty]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private bool _canRetry;

    [ObservableProperty]
    private bool _canOpen;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _sourceText = string.Empty;

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private bool _progressBarVisible;
}
