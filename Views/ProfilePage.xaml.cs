using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ProfilePage : ContentPage
{
    public ProfilePage()
    {
        InitializeComponent();
        BindingContext = new ProfileViewModel();
    }
}