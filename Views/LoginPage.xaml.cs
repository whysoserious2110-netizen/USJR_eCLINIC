using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        BindingContext = new LoginViewModel();
    }
}