using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DentistTodaysVisitsPage : ContentPage
{
    private readonly DentistTodaysVisitsViewModel _viewModel;

    public DentistTodaysVisitsPage()
    {
        InitializeComponent();
        _viewModel = new DentistTodaysVisitsViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}