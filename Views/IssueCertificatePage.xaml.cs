using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class IssueCertificatePage : ContentPage
{
    public IssueCertificatePage(int appointmentId, string patientEmail, string patientName, string certificateType, string purpose)
    {
        InitializeComponent();
        BindingContext = new IssueCertificateViewModel(appointmentId, patientEmail, patientName, certificateType, purpose);
    }
}