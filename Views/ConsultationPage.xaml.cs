using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ConsultationPage : ContentPage
{
    public ConsultationPage(int appointmentId, string patientEmail, string patientName, string patientRole, string prefillComplaint)
    {
        InitializeComponent();
        BindingContext = new ConsultationViewModel(appointmentId, patientEmail, patientName, patientRole, prefillComplaint);
    }
}