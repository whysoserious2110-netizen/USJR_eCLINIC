using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class MedicalRecordListItem : ObservableObject
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string ChiefComplaint { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public string FollowUpNotes { get; set; } = string.Empty;
    public string AttendingStaff { get; set; } = string.Empty;
}

public partial class MedicalRecordsViewModel : ObservableObject
{
    public ObservableCollection<MedicalRecordListItem> Records { get; } = new();

    [ObservableProperty]
    private bool hasRecords;

    public MedicalRecordsViewModel()
    {
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        var records = await Services.MedicalRecordService.Instance.GetForPatientAsync(user.Email);

        Records.Clear();
        foreach (var r in records)
        {
            Records.Add(new MedicalRecordListItem
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

        HasRecords = Records.Count > 0;
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}