using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class ConsultationViewModel : ObservableObject
{
    private readonly int _appointmentId;
    private readonly string _patientEmail;

    [ObservableProperty] private string patientName = string.Empty;
    [ObservableProperty] private string patientRole = string.Empty;
    [ObservableProperty] private string chiefComplaint = string.Empty;
    [ObservableProperty] private string diagnosis = string.Empty;
    [ObservableProperty] private string treatment = string.Empty;
    [ObservableProperty] private string followUpNotes = string.Empty;
    [ObservableProperty] private string bloodPressure = string.Empty;
    [ObservableProperty] private string temperature = string.Empty;
    [ObservableProperty] private string pulseRate = string.Empty;
    [ObservableProperty] private string respiratoryRate = string.Empty;
    [ObservableProperty] private string oxygenSaturation = string.Empty;
    [ObservableProperty] private string initialAssessment = string.Empty;
    [ObservableProperty] private string nursingNotes = string.Empty;
    [ObservableProperty] private string vitalsRecordedBy = string.Empty;
    [ObservableProperty] private string vitalsRecordedAt = string.Empty;
    [ObservableProperty] private bool hasVitals;

    public ConsultationViewModel(
        int appointmentId,
        string patientEmail,
        string patientName,
        string patientRole,
        string prefillComplaint)
    {
        _appointmentId = appointmentId;
        _patientEmail = patientEmail;
        PatientName = patientName;
        PatientRole = patientRole;
        ChiefComplaint = prefillComplaint;

        _ = LoadVitalsAsync();
    }

    private async Task LoadVitalsAsync()
    {
        var vitals = await Services.VitalSignsService.Instance.GetForAppointmentAsync(_appointmentId);
        if (vitals == null)
        {
            HasVitals = false;
            return;
        }

        BloodPressure = vitals.BloodPressure;
        Temperature = vitals.Temperature;
        PulseRate = vitals.HeartRate;
        RespiratoryRate = vitals.RespiratoryRate;
        OxygenSaturation = vitals.OxygenSaturation;
        InitialAssessment = vitals.InitialAssessment;
        NursingNotes = vitals.NursingNotes;
        VitalsRecordedBy = vitals.RecordedBy;
        VitalsRecordedAt = vitals.DateRecorded.ToString("MMM dd, yyyy · h:mm tt");
        HasVitals = true;
    }

    [RelayCommand]
    private async Task OpenPrescription()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user?.Role != "Doctor")
        {
            await Shell.Current.DisplayAlert("Access denied", "Only the doctor can create this prescription.", "OK");
            return;
        }

        await Shell.Current.Navigation.PushAsync(new Views.WriteRxPage(_patientEmail));
    }

    [RelayCommand]
    private async Task SaveConsultation()
    {
        if (string.IsNullOrWhiteSpace(ChiefComplaint) ||
            string.IsNullOrWhiteSpace(Diagnosis) ||
            string.IsNullOrWhiteSpace(Treatment))
        {
            await Shell.Current.DisplayAlert(
                "Missing information",
                "Complete the chief complaint, diagnosis, and treatment before finishing the visit.",
                "OK");
            return;
        }

        var doctor = Services.AuthService.Instance.CurrentUser;
        if (doctor?.Role != "Doctor")
        {
            await Shell.Current.DisplayAlert("Access denied", "Only a doctor can complete this consultation.", "OK");
            return;
        }

        var appointment = await Services.AppointmentService.Instance.GetByIdAsync(_appointmentId);
        if (appointment == null ||
            !appointment.PatientEmail.Equals(_patientEmail, StringComparison.OrdinalIgnoreCase) ||
            appointment.Status is not ("CheckedIn" or "VitalsRecorded"))
        {
            await Shell.Current.DisplayAlert(
                "Visit unavailable",
                "This visit is no longer checked in. Refresh the doctor dashboard and select an available patient.",
                "OK");
            return;
        }

        var record = new Models.ConsultationRecord
        {
            PatientEmail = _patientEmail,
            ConsultationDate = DateTime.Now,
            ChiefComplaint = ChiefComplaint.Trim(),
            Diagnosis = Diagnosis.Trim(),
            Treatment = Treatment.Trim(),
            FollowUpNotes = FollowUpNotes.Trim(),
            AttendingStaff = doctor.FullName,
            BloodPressure = BloodPressure,
            Temperature = Temperature,
            PulseRate = PulseRate,
            RespiratoryRate = RespiratoryRate
        };

        await Services.MedicalRecordService.Instance.AddAsync(record);

        var completed = await Services.AppointmentService.Instance.MarkCompletedAsync(_appointmentId);
        if (!completed)
        {
            await Shell.Current.DisplayAlert(
                "Visit status not updated",
                "The consultation note was saved, but the visit could not be marked complete. Refresh the schedule before trying again.",
                "OK");
            return;
        }

        await Services.NotificationService.Instance.AddAsync(
            _patientEmail,
            "Consultation Completed",
            $"Dr. {doctor.FullName} completed your consultation. Your medical record has been updated.");

        await Shell.Current.DisplayAlert("Visit completed", "The consultation and medical record have been saved.", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}