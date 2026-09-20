using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ApeLabResultsPage : ContentPage
{
    private readonly ApeLabResultsViewModel _viewModel;

    public ApeLabResultsPage()
    {
        InitializeComponent();

        _viewModel = new ApeLabResultsViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.RefreshAsync();
    }
}