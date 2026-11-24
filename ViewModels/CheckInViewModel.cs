using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using USJR_eCLINIC.Services;

namespace USJR_eCLINIC.ViewModels;

public partial class CheckInViewModel : ObservableObject
{
    [ObservableProperty] private string scannedId = string.Empty;

    // Result state
    [ObservableProperty] private bool showNotFound;
    [ObservableProperty] private bool showNoApproved;
    [ObservableProperty] private bool showAlreadyCheckedIn;
    [ObservableProperty] private bool showApprovedFound;
    [ObservableProperty] private bool showCheckInSuccess;

    // Patient/appointment display
    [ObservableProperty] private string patientName = string.Empty;
    [ObservableProperty] private string patientIdNumber = string.Empty;
    [ObservableProperty] private string patientIdDisplay = string.Empty;
    [ObservableProperty] private string patientProgram = string.Empty;
    [ObservableProperty] private bool isLateArrival;
    [ObservableProperty] private bool canCheckIn;
    [ObservableProperty] private bool requiresReschedule;
    [ObservableProperty] private string arrivalGuidance = string.Empty;
    [ObservableProperty] private string assignedShiftDisplay = string.Empty;
    [ObservableProperty] private string appointmentService = string.Empty;
    [ObservableProperty] private string appointmentDate = string.Empty;
    [ObservableProperty] private string appointmentTime = string.Empty;

    public bool HasArrivalGuidance => !string.IsNullOrWhiteSpace(ArrivalGuidance);
    public string CheckInStatusDisplay => IsLateArrival
        ? "Status: WAITING · LATE CHECK-IN"
        : "Status: WAITING · ON TIME";

    partial void OnArrivalGuidanceChanged(string value) =>
        OnPropertyChanged(nameof(HasArrivalGuidance));

    partial void OnIsLateArrivalChanged(bool value) =>
        OnPropertyChanged(nameof(CheckInStatusDisplay));

    // Already-checked-in / success display
    [ObservableProperty] private int queueNumber;
    [ObservableProperty] private string checkInTimeDisplay = string.Empty;

    private int _currentAppointmentId;

    [RelayCommand]
    private async Task SearchPatient()
    {
        ResetResultState();

        if (Services.AuthService.Instance.CurrentUser?.Role != "R.E.A.D.S. Scholar")
        {
            await Shell.Current.DisplayAlert(
                "Access denied",
                "Only R.E.A.D.S. Scholars can check in patients.",
                "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(ScannedId))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please enter or scan a Student/Employee ID.", "OK");
            return;
        }

        ScannedId = ScannedId.Trim();
        var account = await Services.AuthService.Instance.GetAccountByIdNumberAsync(ScannedId);
        if (account == null ||
            !Services.AuthService.CentralPatientRoles.Contains(
                account.Role,
                StringComparer.OrdinalIgnoreCase))
        {
            ShowNotFound = true;
            return;
        }

        // Already checked in today?
        var checkedIn = await Services.AppointmentService.Instance.GetCheckedInTodayAsync(account.Email);
        if (checkedIn != null)
        {
            PatientName = account.FullName;
            PatientIdNumber = account.IdNumber;
            PatientIdDisplay = $"{(account.Role == "Student" ? "Student ID" : "Employee ID")}: {account.IdNumber}";
            QueueNumber = checkedIn.QueueNumber ?? 0;
            CheckInTimeDisplay = checkedIn.CheckInTime?.ToString("h:mm tt") ?? string.Empty;
            ShowAlreadyCheckedIn = true;
            return;
        }

        // Has an approved appointment today?
        var approved = await Services.AppointmentService.Instance.GetApprovedAppointmentTodayAsync(account.Email);
        if (approved == null)
        {
            PatientName = account.FullName;
            PatientIdNumber = account.IdNumber;
            PatientIdDisplay = $"{(account.Role == "Student" ? "Student ID" : "Employee ID")}: {account.IdNumber}";
            ShowNoApproved = true;
            return;
        }

        _currentAppointmentId = approved.Id;
        PatientName = account.FullName;
        PatientIdNumber = account.IdNumber;
        PatientIdDisplay = $"{(account.Role == "Student" ? "Student ID" : "Employee ID")}: {account.IdNumber}";
        PatientProgram = account.ProgramOrDepartment;
        AppointmentService = string.IsNullOrWhiteSpace(approved.ReasonOrPurpose) ? approved.SubService : approved.ReasonOrPurpose;
        AppointmentDate = approved.VisitDate.ToString("MMMM dd, yyyy");
        AppointmentTime = approved.VisitTime;
        AssignedShiftDisplay = string.IsNullOrWhiteSpace(approved.AssignedShift)
            ? "Shift not assigned"
            : $"{approved.AssignedShift} Shift";

        var eligibility = await Services.AppointmentService.Instance
            .GetCheckInEligibilityAsync(approved.Id);
        CanCheckIn = eligibility.Success;
        RequiresReschedule = eligibility.RequiresReschedule;
        IsLateArrival = eligibility.IsLate;
        ArrivalGuidance = eligibility.Message;
        ShowApprovedFound = true;
    }

    [RelayCommand]
    private async Task ConfirmCheckIn()
    {
        var result = await Services.AppointmentService.Instance
            .CheckInWithPolicyAsync(_currentAppointmentId);
        if (!result.Success || result.Appointment == null)
        {
            CanCheckIn = false;
            RequiresReschedule = result.RequiresReschedule;
            ArrivalGuidance = result.Message;
            IsLateArrival = result.IsLate;
            await Shell.Current.DisplayAlert(
                "Check-In Unavailable",
                result.Message,
                "OK");
            return;
        }

        QueueNumber = result.Appointment.QueueNumber ?? 0;
        CheckInTimeDisplay = result.Appointment.CheckInTime?.ToString("h:mm tt") ?? string.Empty;
        IsLateArrival = result.IsLate;
        ArrivalGuidance = result.Message;

        ShowApprovedFound = false;
        ShowCheckInSuccess = true;
    }

    [RelayCommand]
    private void ScanAnother()
    {
        ScannedId = string.Empty;
        ResetResultState();
    }

    private void ResetResultState()
    {
        IsLateArrival = false;
        CanCheckIn = false;
        RequiresReschedule = false;
        ArrivalGuidance = string.Empty;
        AssignedShiftDisplay = string.Empty;
        ShowNotFound = false;
        ShowNoApproved = false;
        ShowAlreadyCheckedIn = false;
        ShowApprovedFound = false;
        ShowCheckInSuccess = false;
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
