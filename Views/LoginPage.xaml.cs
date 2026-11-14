namespace USJR_eCLINIC.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();

        BindingContext =
            new ViewModels.LoginViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.LoginViewModel viewModel)
            await viewModel.LoadRememberedAccountAsync();
    }
}
