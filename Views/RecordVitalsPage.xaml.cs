using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class RecordVitalsPage : ContentPage
{
    public RecordVitalsPage(
        int appointmentId,
        string patientEmail,
        string patientName)
    {
        InitializeComponent();

        BindingContext = new RecordVitalsViewModel(
            appointmentId,
            patientEmail,
            patientName);
    }
}