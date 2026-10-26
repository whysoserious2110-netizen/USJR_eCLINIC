using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ApeLabEncodingPage : ContentPage
{
    private readonly ApeLabEncodingViewModel _viewModel;

    public ApeLabEncodingPage()
    {
        InitializeComponent();
        _viewModel = new ApeLabEncodingViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadPatientsAsync();
    }
}
