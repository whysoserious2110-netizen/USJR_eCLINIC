using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DoctorDashboardPage : ContentPage
{
    private readonly DoctorDashboardViewModel _viewModel;

    public DoctorDashboardPage()
    {
        InitializeComponent();
        _viewModel = new DoctorDashboardViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}