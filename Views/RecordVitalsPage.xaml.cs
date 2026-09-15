using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class RecordVitalsPage : ContentPage
{
    public RecordVitalsPage(string patientEmail, string patientName)
    {
        InitializeComponent();
        BindingContext = new RecordVitalsViewModel(patientEmail, patientName);
    }
}