using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class ConsultationViewModel : ObservableObject
{
    private readonly int _appointmentId;
    private readonly string _patientEmail;

    [ObservableProperty]
    private string patientName = string.Empty;

    [ObservableProperty]
    private string patientRole = string.Empty;

    [ObservableProperty]
    private string chiefComplaint = string.Empty;

    [ObservableProperty]
    private string diagnosis = string.Empty;

    [ObservableProperty]
    private string treatment = string.Empty;

    [ObservableProperty]
    private string followUpNotes = string.Empty;

    public ConsultationViewModel(int appointmentId, string patientEmail, string patientName, string patientRole, string prefillComplaint)
    {
        _appointmentId = appointmentId;
        _patientEmail = patientEmail;
        PatientName = patientName;
        PatientRole = patientRole;
        ChiefComplaint = prefillComplaint;
    }

    [RelayCommand]
    private async Task SaveConsultation()
    {
        if (string.IsNullOrWhiteSpace(ChiefComplaint) || string.IsNullOrWhiteSpace(Diagnosis) || string.IsNullOrWhiteSpace(Treatment))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please fill in chief complaint, diagnosis, and treatment.", "OK");
            return;
        }

        var doctor = Services.AuthService.Instance.CurrentUser;

        var record = new Models.ConsultationRecord
        {
            PatientEmail = _patientEmail,
            ConsultationDate = DateTime.Now,
            ChiefComplaint = ChiefComplaint,
            Diagnosis = Diagnosis,
            Treatment = Treatment,
            FollowUpNotes = FollowUpNotes,
            AttendingStaff = doctor?.FullName ?? "Doctor"
        };

        await Services.MedicalRecordService.Instance.AddAsync(record);
        await Services.AppointmentService.Instance.MarkCompletedAsync(_appointmentId);

        await Services.NotificationService.Instance.AddAsync(
            _patientEmail,
            "Consultation Completed",
            $"Dr. {doctor?.FullName} has completed your consultation. Your medical record has been updated.");

        await Shell.Current.DisplayAlert("Saved", "Consultation recorded and appointment marked as completed.", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}