using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ReadsDashboardPage : ContentPage
{
    private readonly ReadsDashboardViewModel _viewModel;

    public ReadsDashboardPage()
    {
        InitializeComponent();
        _viewModel = new ReadsDashboardViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}