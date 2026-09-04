
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class IssueCertificateViewModel : ObservableObject
{
    private readonly int _appointmentId;
    private readonly string _patientEmail;

    [ObservableProperty] private string patientName = string.Empty;
    [ObservableProperty] private string certificateType = string.Empty;
    [ObservableProperty] private string purpose = string.Empty;
    [ObservableProperty] private string certificateContent = string.Empty;

    public IssueCertificateViewModel(int appointmentId, string patientEmail, string patientName, string certificateType, string purpose)
    {
        _appointmentId = appointmentId;
        _patientEmail = patientEmail;
        PatientName = patientName;
        CertificateType = certificateType;
        Purpose = purpose;
    }

    [RelayCommand]
    private async Task IssueCertificate()
    {
        if (string.IsNullOrWhiteSpace(CertificateContent))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please enter the certificate content.", "OK");
            return;
        }

        var success = await Services.AppointmentService.Instance.IssueCertificateAsync(_appointmentId, CertificateContent);

        if (success)
        {
            var doctor = Services.AuthService.Instance.CurrentUser;
            await Services.NotificationService.Instance.AddAsync(
                _patientEmail,
                "Certificate Issued",
                $"Your {CertificateType} has been issued by Dr. {doctor?.FullName}. Check your Appointments for details.");

            await Shell.Current.DisplayAlert("Issued", "Certificate has been issued.", "OK");
            await Shell.Current.Navigation.PopAsync();
        }
        else
        {
            await Shell.Current.DisplayAlert("Error", "Could not issue certificate. Please try again.", "OK");
        }
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}