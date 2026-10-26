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
    public DateTime VisitDate { get; set; }
    public string TimeDisplay { get; set; } = string.Empty;
    public string AppointmentStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public bool CanReview { get; set; }
    public bool CanCollect { get; set; }
    public bool CanTakeAction => CanReview || CanCollect;
    public string ActionLabel => CanReview
        ? "Review paper request"
        : CanCollect
            ? "Mark paper copy collected"
            : "View request status";
}

public partial class CertificatesViewModel : ObservableObject
{
    private readonly List<CertificateListItem> _all = new();
    private string _currentRole = string.Empty;

    public ObservableCollection<CertificateListItem> Certificates { get; } = new();

    [ObservableProperty]
    private bool hasCertificates;

    [ObservableProperty]
    private string selectedTab = "Action";

    [ObservableProperty]
    private string headerTitle = "Paper Certificate Requests";

    [ObservableProperty]
    private string firstTabLabel = "Needs Review";

    [ObservableProperty]
    private string emptyMessage = "No paper certificate requests need action.";

    public bool IsPendingTab => SelectedTab == "Action";
    public bool IsIssuedTab => SelectedTab == "History";

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null)
            return;

        _currentRole = user.Role;
        var isReads = _currentRole == "R.E.A.D.S. Scholar";
        var isClinician = _currentRole is "Doctor" or "Dentist";

        if (!isReads && !isClinician)
        {
            await Shell.Current.DisplayAlert(
                "Access denied",
                "Paper certificate requests are available to R.E.A.D.S. staff and the responsible clinician.",
                "OK");
            await Shell.Current.Navigation.PopAsync();
            return;
        }

        HeaderTitle = _currentRole switch
        {
            "Doctor" => "Medical Certificate Requests",
            "Dentist" => "Dental Certificate Requests",
            _ => "Paper Certificate Requests"
        };
        FirstTabLabel = isReads ? "Ready for Pickup" : "Needs Review";
        EmptyMessage = isReads
            ? "No paper certificates are ready for pickup."
            : "No completed certificate appointments are waiting for your review.";

        var requests = await Services.AppointmentService.Instance.GetAllCertRequestsAsync();
        _all.Clear();

        foreach (var appointment in requests)
        {
            if (_currentRole == "Doctor" && appointment.SubService != "Medical Certificate")
                continue;
            if (_currentRole == "Dentist" && appointment.SubService != "Dental Certificate")
                continue;

            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appointment.PatientEmail);
            var canReview = isClinician &&
                            appointment.Status == "Completed" &&
                            appointment.CertificateStatus == "Awaiting Clinical Review";
            var canCollect = isReads && appointment.CertificateStatus == "Ready for Pickup";

            _all.Add(new CertificateListItem
            {
                AppointmentId = appointment.Id,
                PatientEmail = appointment.PatientEmail,
                PatientName = patient?.FullName ?? appointment.PatientEmail,
                CertificateType = appointment.SubService,
                Purpose = appointment.ReasonOrPurpose,
                DateDisplay = appointment.VisitDate.ToString("MMM dd, yyyy"),
                VisitDate = appointment.VisitDate,
                TimeDisplay = appointment.VisitTime,
                AppointmentStatus = appointment.Status,
                Status = string.IsNullOrWhiteSpace(appointment.CertificateStatus)
                    ? "Awaiting Appointment Approval"
                    : appointment.CertificateStatus,
                StatusColor = appointment.CertificateStatus switch
                {
                    "Ready for Pickup" => Color.FromArgb("#0F9B8E"),
                    "Collected" => Color.FromArgb("#4A90D9"),
                    "Declined" or "Cancelled" or "Expired" or "Missed Appointment" => Color.FromArgb("#E05B5B"),
                    "Awaiting Clinical Review" => Color.FromArgb("#D9A441"),
                    _ => Color.FromArgb("#7C8794")
                },
                CanReview = canReview,
                CanCollect = canCollect
            });
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var isReads = _currentRole == "R.E.A.D.S. Scholar";
        var filtered = SelectedTab == "Action"
            ? _all.Where(item => item.CanReview || item.CanCollect)
            : _all.Where(item => !item.CanReview && !item.CanCollect);

        Certificates.Clear();
        foreach (var item in filtered.OrderByDescending(item => item.VisitDate))
            Certificates.Add(item);

        HasCertificates = Certificates.Count > 0;
        OnPropertyChanged(nameof(IsPendingTab));
        OnPropertyChanged(nameof(IsIssuedTab));
        if (!HasCertificates)
            EmptyMessage = isReads && SelectedTab == "Action"
                ? "No paper certificates are ready for pickup."
                : isReads
                    ? "No certificate pickup history yet."
                    : SelectedTab == "Action"
                        ? "No completed certificate appointments are waiting for your review."
                        : "No certificate request history yet.";
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        SelectedTab = tab;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task OpenCertificate(CertificateListItem? item)
    {
        if (item == null)
            return;

        if (item.CanReview)
        {
            await Shell.Current.Navigation.PushAsync(new Views.IssueCertificatePage(
                item.AppointmentId, item.PatientEmail, item.PatientName, item.CertificateType, item.Purpose));
            return;
        }

        if (item.CanCollect)
        {
            var confirmed = await Shell.Current.DisplayAlert(
                "Confirm Paper Pickup",
                $"Confirm that {item.PatientName} collected the {item.CertificateType.ToLowerInvariant()}?",
                "Mark Collected",
                "Cancel");

            if (!confirmed)
                return;

            var success = await Services.AppointmentService.Instance
                .MarkCertificateCollectedAsync(item.AppointmentId);

            if (!success)
            {
                await Shell.Current.DisplayAlert("Update Failed", "The certificate pickup could not be recorded.", "OK");
                return;
            }

            await RefreshAsync();
            await Shell.Current.DisplayAlert("Pickup Recorded", "The paper certificate was marked as collected.", "OK");
            return;
        }

        await Shell.Current.DisplayAlert(
            item.CertificateType,
            $"Appointment status: {item.AppointmentStatus}\nCertificate status: {item.Status}\nPurpose: {item.Purpose}",
            "OK");
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
