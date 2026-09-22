namespace USJR_eCLINIC.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();

        BindingContext =
            new ViewModels.LoginViewModel();
    }
}