using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class NurseDashboardPage : ContentPage
{
    private readonly NurseDashboardViewModel _viewModel;

    public NurseDashboardPage()
    {
        InitializeComponent();
        _viewModel = new NurseDashboardViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}