using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage()
    {
        InitializeComponent();
        _viewModel = new ProfileViewModel();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshAllergiesFromCurrentUser();
    }
}
