using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class RegisterPatientPage : ContentPage
{
    public RegisterPatientPage()
    {
        InitializeComponent();
        BindingContext = new RegisterPatientViewModel();
    }
}