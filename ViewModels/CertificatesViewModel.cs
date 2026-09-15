using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class CertificateListItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string DateDisplay { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public bool IsPending { get; set; }
}

public partial class CertificatesViewModel : ObservableObject
{
    private List<CertificateListItem> _all = new();

    public ObservableCollection<CertificateListItem> Certificates { get; } = new();

    [ObservableProperty]
    private bool hasCertificates;

    [ObservableProperty]
    private string selectedTab = "Pending";

    public bool IsPendingTab => SelectedTab == "Pending";
    public bool IsIssuedTab => SelectedTab == "Issued";

    public async Task RefreshAsync()
    {
        var requests = await Services.AppointmentService.Instance.GetAllCertRequestsAsync();

        _all.Clear();
        foreach (var appt in requests)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appt.PatientEmail);

            _all.Add(new CertificateListItem
            {
                AppointmentId = appt.Id,
                PatientEmail = appt.PatientEmail,
                PatientName = patient?.FullName ?? appt.PatientEmail,
                CertificateType = appt.SubService,
                Purpose = appt.ReasonOrPurpose,
                DateDisplay = appt.VisitDate.ToString("MMM dd, yyyy"),
                Status = appt.Status,
                IsPending = appt.Status == "Pending" || appt.Status == "Confirmed",
                StatusColor = appt.Status switch
                {
                    "Completed" => Color.FromArgb("#4A90D9"),
                    "Cancelled" => Color.FromArgb("#E05B5B"),
                    _ => Color.FromArgb("#D9A441")
                }
            });
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = SelectedTab == "Pending"
            ? _all.Where(c => c.IsPending)
            : _all.Where(c => !c.IsPending);

        Certificates.Clear();
        foreach (var c in filtered)
            Certificates.Add(c);

        HasCertificates = Certificates.Count > 0;
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        SelectedTab = tab;
        OnPropertyChanged(nameof(IsPendingTab));
        OnPropertyChanged(nameof(IsIssuedTab));
        ApplyFilter();
    }

    [RelayCommand]
    private async Task OpenCertificate(CertificateListItem item)
    {
        if (!item.IsPending)
        {
            await Shell.Current.DisplayAlert("Already Issued", $"This certificate was already {item.Status.ToLower()}.", "OK");
            return;
        }

        await Shell.Current.Navigation.PushAsync(new Views.IssueCertificatePage(
            item.AppointmentId, item.PatientEmail, item.PatientName, item.CertificateType, item.Purpose));
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}