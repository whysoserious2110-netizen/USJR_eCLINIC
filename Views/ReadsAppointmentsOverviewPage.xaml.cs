using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ReadsAppointmentsOverviewPage : ContentPage
{
    private readonly ReadsAppointmentsOverviewViewModel _viewModel;

    public ReadsAppointmentsOverviewPage()
    {
        InitializeComponent();
        _viewModel = new ReadsAppointmentsOverviewViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}