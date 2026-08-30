using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class SignUpPage : ContentPage
{
    public SignUpPage()
    {
        InitializeComponent();
        BindingContext = new SignUpViewModel();
    }
}