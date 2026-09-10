using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class AdminNotesPage : ContentPage
{
    public AdminNotesPage(string patientEmail, string patientName)
    {
        InitializeComponent();
        BindingContext = new AdminNotesViewModel(patientEmail, patientName);
    }
}