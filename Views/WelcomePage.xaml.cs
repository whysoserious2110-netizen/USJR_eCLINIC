using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class WelcomePage : ContentPage
{
    public WelcomePage()
    {
        InitializeComponent();
        BindingContext = new WelcomeViewModel();
    }



}