using System.Windows.Input;

namespace NoCloudware.UI.Core.ViewModels;

public partial class BaseFileItem
{
    public ICommand? DeleteCommand { get; set; }

    public ICommand? TogglePlaylistCommand { get; set; }
}
