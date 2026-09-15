using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class PatientDetailViewModel : ObservableObject
{
    private readonly string _patientEmail;

    // Patient Information
    [ObservableProperty] private string patientName = string.Empty;
    [ObservableProperty] private string patientRole = string.Empty;
    [ObservableProperty] private string patientIdNumber = string.Empty;
    [ObservableProperty] private string patientEmail2 = string.Empty;
    [ObservableProperty] private string patientMobile = string.Empty;
    [ObservableProperty] private string patientProgram = string.Empty;
    [ObservableProperty] private string profileImagePath = string.Empty;

    // Consultations
    public ObservableCollection<MedicalRecordListItem> Consultations { get; } = new();
    [ObservableProperty] private bool hasConsultations;

    // Medical History (from patient's own profile clinical info)
    [ObservableProperty] private string bloodType = string.Empty;
    [ObservableProperty] private string allergies = string.Empty;
    [ObservableProperty] private string medicalConditions = string.Empty;
    [ObservableProperty] private string currentMedications = string.Empty;

    // Prescriptions
    public ObservableCollection<PrescriptionListItem> Prescriptions { get; } = new();
    [ObservableProperty] private bool hasPrescriptions;

    public PatientDetailViewModel(string patientEmail, string patientName, string patientRole, string patientIdNumber)
    {
        _patientEmail = patientEmail;
        PatientName = patientName;
        PatientRole = patientRole;
        PatientIdNumber = patientIdNumber;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var account = await Services.AuthService.Instance.GetAccountByEmailAsync(_patientEmail);
        if (account != null)
        {
            PatientEmail2 = account.Email;
            PatientMobile = account.MobileNumber;
            PatientProgram = account.ProgramOrDepartment;
            ProfileImagePath = account.ProfileImagePath;
            BloodType = account.BloodType;
            Allergies = account.Allergies;
            MedicalConditions = account.MedicalConditions;
            CurrentMedications = account.CurrentMedications;
        }

        var records = await Services.MedicalRecordService.Instance.GetForPatientAsync(_patientEmail);
        Consultations.Clear();
        foreach (var r in records)
        {
            Consultations.Add(new MedicalRecordListItem
            {
                Id = r.Id,
                DateDisplay = r.ConsultationDate.ToString("MMM dd, yyyy"),
                ChiefComplaint = r.ChiefComplaint,
                Diagnosis = r.Diagnosis,
                Treatment = r.Treatment,
                FollowUpNotes = r.FollowUpNotes,
                AttendingStaff = r.AttendingStaff
            });
        }
        HasConsultations = Consultations.Count > 0;

        var prescriptions = await Services.PrescriptionService.Instance.GetForPatientAsync(_patientEmail);
        Prescriptions.Clear();
        foreach (var p in prescriptions)
        {
            Prescriptions.Add(new PrescriptionListItem
            {
                DateDisplay = p.DatePrescribed.ToString("MMM dd, yyyy"),
                MedicineName = p.MedicineName,
                Dosage = p.Dosage,
                Instructions = p.Instructions,
                LabelText = p.IsClinicGiven ? "Clinic-Given" : "Take-Home",
                LabelColor = p.IsClinicGiven ? Color.FromArgb("#0F9B8E") : Color.FromArgb("#D9A441")
            });
        }
        HasPrescriptions = Prescriptions.Count > 0;
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();


    [RelayCommand]
    private async Task GoToVitals()
    => await Shell.Current.Navigation.PushAsync(new Views.RecordVitalsPage(_patientEmail, PatientName));


}