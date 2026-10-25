using CommunityToolkit.Mvvm.ComponentModel;

namespace GBV_Shield.Features.Common
{
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string title = string.Empty;
    }
}
