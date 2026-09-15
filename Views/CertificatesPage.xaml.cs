using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class CertificatesPage : ContentPage
{
    private readonly CertificatesViewModel _viewModel;

    public CertificatesPage()
    {
        InitializeComponent();
        _viewModel = new CertificatesViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}