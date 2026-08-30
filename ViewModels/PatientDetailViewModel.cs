using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class PatientDetailViewModel : ObservableObject
{
    private readonly string _patientEmail;

    [ObservableProperty]
    private string patientName = string.Empty;

    [ObservableProperty]
    private string patientRole = string.Empty;

    [ObservableProperty]
    private string patientIdNumber = string.Empty;

    public ObservableCollection<MedicalRecordListItem> MedicalRecords { get; } = new();

    [ObservableProperty]
    private bool hasMedicalRecords;

    public ObservableCollection<PrescriptionListItem> Prescriptions { get; } = new();

    [ObservableProperty]
    private bool hasPrescriptions;

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
        var records = await Services.MedicalRecordService.Instance.GetForPatientAsync(_patientEmail);

        MedicalRecords.Clear();
        foreach (var r in records)
        {
            MedicalRecords.Add(new MedicalRecordListItem
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
        HasMedicalRecords = MedicalRecords.Count > 0;

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
}