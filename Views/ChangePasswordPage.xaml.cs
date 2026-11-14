namespace USJR_eCLINIC.Views;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage()
    {
        InitializeComponent();
        BindingContext = new ViewModels.ChangePasswordViewModel();
    }
}
