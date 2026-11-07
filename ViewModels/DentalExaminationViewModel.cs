using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class DentalExaminationViewModel : ObservableObject
{
    private readonly int _appointmentId;
    private readonly string _patientEmail;

    [ObservableProperty] private string patientName = string.Empty;
    [ObservableProperty] private string patientRole = string.Empty;
    [ObservableProperty] private string patientIdDisplay = string.Empty;
    [ObservableProperty] private string patientAcademicDisplay = string.Empty;
    [ObservableProperty] private string profileImagePath = string.Empty;
    [ObservableProperty] private string appointmentContext = string.Empty;

    [ObservableProperty] private string examFindings = string.Empty;
    [ObservableProperty] private string diagnosis = string.Empty;
    [ObservableProperty] private string treatmentRendered = string.Empty;
    [ObservableProperty] private string dentalNotes = string.Empty;
    [ObservableProperty] private string careInstructions = string.Empty;

    public string PatientInitials => string.Join(string.Empty,
        (PatientName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(part => part[..1].ToUpperInvariant()));

    public DentalExaminationViewModel(
        int appointmentId,
        string patientEmail,
        string patientName,
        string patientRole,
        string appointmentContext)
    {
        _appointmentId = appointmentId;
        _patientEmail = patientEmail;
        PatientName = patientName;
        PatientRole = patientRole;
        AppointmentContext = appointmentContext;
    }

    public async Task InitializeAsync()
    {
        var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(_patientEmail);
        if (patient == null)
        {
            PatientIdDisplay = "ID unavailable";
            PatientAcademicDisplay = PatientRole;
            return;
        }

        PatientName = patient.FullName;
        PatientRole = patient.Role;
        PatientIdDisplay = $"{(patient.Role == "Student" ? "Student ID" : "Employee ID")}: {patient.IdNumber}";
        var academic = patient.Role == "Student"
            ? patient.StudentProgramAndYear
            : patient.ProgramOrDepartment;
        PatientAcademicDisplay = string.IsNullOrWhiteSpace(academic)
            ? patient.Role
            : $"{patient.Role} · {academic}";
        ProfileImagePath = patient.ProfileImagePath;
    }

    [RelayCommand]
    private async Task SaveExamination()
    {
        if (string.IsNullOrWhiteSpace(ExamFindings) ||
            string.IsNullOrWhiteSpace(Diagnosis) ||
            string.IsNullOrWhiteSpace(TreatmentRendered))
        {
            await Shell.Current.DisplayAlert(
                "Missing info",
                "Please fill in exam findings, diagnosis, and treatment rendered.",
                "OK");
            return;
        }

        var dentist = Services.AuthService.Instance.CurrentUser;
        if (dentist?.Role != "Dentist")
        {
            await Shell.Current.DisplayAlert("Access denied", "Only a Dentist can save a dental examination.", "OK");
            return;
        }

        var record = new Models.DentalRecord
        {
            AppointmentId = _appointmentId,
            PatientEmail = _patientEmail,
            VisitDate = DateTime.Now,
            ExamFindings = ExamFindings.Trim(),
            Diagnosis = Diagnosis.Trim(),
            TreatmentRendered = TreatmentRendered.Trim(),
            DentalNotes = DentalNotes.Trim(),
            CareInstructions = CareInstructions.Trim(),
            AttendingDentist = dentist.FullName
        };

        var saved = await Services.DentalRecordService.Instance.AddForAppointmentAndCompleteAsync(record);
        if (!saved)
        {
            await Shell.Current.DisplayAlert(
                "Visit not ready",
                "The examination was not saved. The patient must have a dental appointment checked in and vitals recorded by the Nurse today.",
                "OK");
            return;
        }

        try
        {
            await Services.NotificationService.Instance.AddAsync(
                _patientEmail,
                "Dental Examination Completed",
                $"Dr. {dentist.FullName} completed your dental examination. Your dental record is linked to the appointment.");

            var appointment = await Services.AppointmentService.Instance.GetByIdAsync(_appointmentId);
            if (appointment != null)
            {
                await Services.AppointmentService.Instance.NotifyAssignedClinicalStaffAsync(
                    appointment,
                    "Dental Examination Completed",
                    $"The dental examination for {PatientName} was saved and linked to appointment #{_appointmentId}.");
            }
        }
        catch
        {
            // The saved dental record and completed appointment remain valid if a notice cannot be created.
        }

        await Shell.Current.DisplayAlert(
            "Saved",
            "Dental examination recorded, linked to the appointment, and marked completed.",
            "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();

    [RelayCommand]
    private async Task GoToHome() => await Shell.Current.Navigation.PushAsync(new Views.DentistDashboardPage());

    [RelayCommand]
    private async Task GoToPatients() => await Shell.Current.Navigation.PushAsync(new Views.DentistMyPatientsPage());

    [RelayCommand]
    private async Task GoToAppointments() => await Shell.Current.Navigation.PushAsync(new Views.DentistAppointmentsPage());

    [RelayCommand]
    private async Task GoToProfile() => await Shell.Current.Navigation.PushAsync(new Views.DentistProfilePage());
}
