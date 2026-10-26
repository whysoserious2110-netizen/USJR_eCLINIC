using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class IssueCertificateViewModel : ObservableObject
{
    private readonly int _appointmentId;

    [ObservableProperty] private string patientName = string.Empty;
    [ObservableProperty] private string certificateType = string.Empty;
    [ObservableProperty] private string purpose = string.Empty;
    [ObservableProperty] private string decisionNote = string.Empty;
    [ObservableProperty] private string clinicianRole = string.Empty;

    public IssueCertificateViewModel(int appointmentId, string patientEmail, string patientName, string certificateType, string purpose)
    {
        _appointmentId = appointmentId;
        PatientName = patientName;
        CertificateType = certificateType;
        Purpose = purpose;
        ClinicianRole = Services.AuthService.Instance.CurrentUser?.Role ?? "Clinic clinician";
    }

    [RelayCommand]
    private async Task ApproveAndMarkReady()
    {
        var confirmed = await Shell.Current.DisplayAlert(
            "Mark Paper Certificate Ready?",
            $"Confirm that the paper {CertificateType.ToLowerInvariant()} has been reviewed, signed, and prepared for pickup.",
            "Mark Ready",
            "Cancel");

        if (!confirmed)
            return;

        var success = await Services.AppointmentService.Instance
            .ReviewCertificateRequestAsync(
                _appointmentId,
                approve: true,
                decisionNote: DecisionNote);

        if (!success)
        {
            await Shell.Current.DisplayAlert(
                "Request Unavailable",
                "This request may no longer be awaiting clinical review, or your role cannot review this certificate type.",
                "OK");
            return;
        }

        await Shell.Current.DisplayAlert(
            "Ready for Pickup",
            "The patient has been notified that the paper certificate is ready.",
            "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task DeclineRequest()
    {
        if (string.IsNullOrWhiteSpace(DecisionNote))
        {
            await Shell.Current.DisplayAlert(
                "Reason Required",
                "Enter a brief reason before declining the certificate request.",
                "OK");
            return;
        }

        var confirmed = await Shell.Current.DisplayAlert(
            "Decline Certificate Request?",
            "The patient will be notified with the reason entered below.",
            "Decline Request",
            "Cancel");

        if (!confirmed)
            return;

        var success = await Services.AppointmentService.Instance
            .ReviewCertificateRequestAsync(
                _appointmentId,
                approve: false,
                decisionNote: DecisionNote);

        if (!success)
        {
            await Shell.Current.DisplayAlert(
                "Request Unavailable",
                "This request may no longer be awaiting clinical review, or your role cannot review this certificate type.",
                "OK");
            return;
        }

        await Shell.Current.DisplayAlert("Request Declined", "The patient has been notified.", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
