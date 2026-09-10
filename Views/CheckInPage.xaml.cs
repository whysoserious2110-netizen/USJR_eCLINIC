using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class CheckInPage : ContentPage
{
    public CheckInPage()
    {
        InitializeComponent();
        BindingContext = new CheckInViewModel();
    }
}