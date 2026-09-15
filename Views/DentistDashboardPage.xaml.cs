using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DentistDashboardPage : ContentPage
{
    private readonly DentistDashboardViewModel _viewModel;

    public DentistDashboardPage()
    {
        InitializeComponent();
        _viewModel = new DentistDashboardViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}