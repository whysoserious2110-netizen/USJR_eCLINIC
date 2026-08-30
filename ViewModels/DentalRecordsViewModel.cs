using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DentalRecordListItem : ObservableObject
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string ExamFindings { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string TreatmentRendered { get; set; } = string.Empty;
    public string CareInstructions { get; set; } = string.Empty;
}

public partial class DentalRecordsViewModel : ObservableObject
{
    public ObservableCollection<DentalRecordListItem> Records { get; } = new();

    [ObservableProperty]
    private bool hasRecords;

    public DentalRecordsViewModel()
    {
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        var records = await Services.DentalRecordService.Instance.GetForPatientAsync(user.Email);

        Records.Clear();
        foreach (var r in records)
        {
            Records.Add(new DentalRecordListItem
            {
                Id = r.Id,
                DateDisplay = r.VisitDate.ToString("MMM dd, yyyy"),
                ExamFindings = r.ExamFindings,
                Diagnosis = r.Diagnosis,
                TreatmentRendered = r.TreatmentRendered,
                CareInstructions = r.CareInstructions
            });
        }

        HasRecords = Records.Count > 0;
    }

    [RelayCommand]
    private async Task ViewDetails(DentalRecordListItem item)
        => await Shell.Current.DisplayAlert(item.DateDisplay,
            $"Exam Findings: {item.ExamFindings}\n\nDiagnosis: {item.Diagnosis}\n\nTreatment: {item.TreatmentRendered}\n\nCare Instructions: {item.CareInstructions}",
            "OK");

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}