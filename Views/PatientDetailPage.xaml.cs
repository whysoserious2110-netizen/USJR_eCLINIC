using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class PatientDetailPage : ContentPage
{
    public PatientDetailPage(string patientEmail, string patientName, string patientRole, string patientIdNumber)
    {
        InitializeComponent();
        BindingContext = new PatientDetailViewModel(patientEmail, patientName, patientRole, patientIdNumber);
    }
}