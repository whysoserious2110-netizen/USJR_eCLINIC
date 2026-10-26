using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class WriteRxPage : ContentPage
{
    public WriteRxPage(string? patientEmail = null)
    {
        InitializeComponent();
        BindingContext = new WriteRxViewModel(patientEmail);
    }
}