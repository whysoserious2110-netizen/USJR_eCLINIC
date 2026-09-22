using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ReturningUserPage : ContentPage
{
    private readonly ReturningUserViewModel _viewModel;

    public ReturningUserPage()
    {
        InitializeComponent();

        _viewModel = new ReturningUserViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.InitializeAsync();
    }
}