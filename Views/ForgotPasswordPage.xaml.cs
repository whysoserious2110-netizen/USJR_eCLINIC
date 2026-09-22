namespace USJR_eCLINIC.Views;

public partial class ForgotPasswordPage : ContentPage
{
    public ForgotPasswordPage()
    {
        InitializeComponent();

        BindingContext =
            new ViewModels.ForgotPasswordViewModel();
    }
}