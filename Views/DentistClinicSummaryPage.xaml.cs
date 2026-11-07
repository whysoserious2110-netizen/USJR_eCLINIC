using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DentistClinicSummaryPage : ContentPage
{
    private readonly DentistClinicSummaryViewModel _viewModel;

    public DentistClinicSummaryPage()
    {
        InitializeComponent();
        _viewModel = new DentistClinicSummaryViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}
