using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class PrescriptionDispensingPage : ContentPage
{
    private readonly PrescriptionDispensingViewModel _viewModel;

    public PrescriptionDispensingPage()
    {
        InitializeComponent();
        _viewModel = new PrescriptionDispensingViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}