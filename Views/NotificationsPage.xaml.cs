using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class NotificationsPage : ContentPage
{
    private readonly NotificationsViewModel _viewModel;

    public NotificationsPage()
    {
        InitializeComponent();
        _viewModel = new NotificationsViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}