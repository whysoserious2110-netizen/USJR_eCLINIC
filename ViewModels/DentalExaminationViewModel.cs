using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class DentalExaminationViewModel : ObservableObject
{
    private readonly int _appointmentId;
    private readonly string _patientEmail;

    [ObservableProperty] private string patientName = string.Empty;
    [ObservableProperty] private string patientRole = string.Empty;

    [ObservableProperty] private string examFindings = string.Empty;
    [ObservableProperty] private string diagnosis = string.Empty;
    [ObservableProperty] private string treatmentRendered = string.Empty;
    [ObservableProperty] private string dentalNotes = string.Empty;
    [ObservableProperty] private string careInstructions = string.Empty;

    public DentalExaminationViewModel(int appointmentId, string patientEmail, string patientName, string patientRole, string prefillFindings)
    {
        _appointmentId = appointmentId;
        _patientEmail = patientEmail;
        PatientName = patientName;
        PatientRole = patientRole;
        ExamFindings = prefillFindings;
    }

    [RelayCommand]
    private async Task SaveExamination()
    {
        if (string.IsNullOrWhiteSpace(ExamFindings) || string.IsNullOrWhiteSpace(Diagnosis) || string.IsNullOrWhiteSpace(TreatmentRendered))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please fill in exam findings, diagnosis, and treatment rendered.", "OK");
            return;
        }

        var dentist = Services.AuthService.Instance.CurrentUser;

        var record = new Models.DentalRecord
        {
            PatientEmail = _patientEmail,
            VisitDate = DateTime.Now,
            ExamFindings = ExamFindings,
            Diagnosis = Diagnosis,
            TreatmentRendered = TreatmentRendered,
            DentalNotes = DentalNotes,
            CareInstructions = CareInstructions,
            AttendingDentist = dentist?.FullName ?? "Dentist"
        };

        await Services.DentalRecordService.Instance.AddAsync(record);

        if (_appointmentId > 0)
            await Services.AppointmentService.Instance.MarkCompletedAsync(_appointmentId);

        await Services.NotificationService.Instance.AddAsync(
            _patientEmail,
            "Dental Examination Completed",
            $"Dr. {dentist?.FullName} has completed your dental examination. Your dental record has been updated.");

        await Shell.Current.DisplayAlert("Saved", "Dental examination recorded and appointment marked as completed.", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}