using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class AdminPatientPage : ContentPage
{
    public AdminPatientPage(string patientEmail)
    {
        InitializeComponent();
        BindingContext = new AdminPatientViewModel(patientEmail);
    }
}