using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DentalExaminationPage : ContentPage
{
    public DentalExaminationPage(int appointmentId, string patientEmail, string patientName, string patientRole, string prefillFindings)
    {
        InitializeComponent();
        BindingContext = new DentalExaminationViewModel(appointmentId, patientEmail, patientName, patientRole, prefillFindings);
    }
}