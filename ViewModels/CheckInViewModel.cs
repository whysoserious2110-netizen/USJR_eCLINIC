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
    [ObservableProperty] private string patientProgram = string.Empty;
    [ObservableProperty] private string appointmentService = string.Empty;
    [ObservableProperty] private string appointmentDate = string.Empty;
    [ObservableProperty] private string appointmentTime = string.Empty;

    // Already-checked-in / success display
    [ObservableProperty] private int queueNumber;
    [ObservableProperty] private string checkInTimeDisplay = string.Empty;

    private int _currentAppointmentId;

    [RelayCommand]
    private async Task SearchPatient()
    {
        ResetResultState();

        if (string.IsNullOrWhiteSpace(ScannedId))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please enter or scan a Student/Employee ID.", "OK");
            return;
        }

        var account = await Services.AuthService.Instance.GetAccountByIdNumberAsync(ScannedId);
        if (account == null)
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
            ShowNoApproved = true;
            return;
        }

        _currentAppointmentId = approved.Id;
        PatientName = account.FullName;
        PatientIdNumber = account.IdNumber;
        PatientProgram = account.ProgramOrDepartment;
        AppointmentService = string.IsNullOrWhiteSpace(approved.ReasonOrPurpose) ? approved.SubService : approved.ReasonOrPurpose;
        AppointmentDate = approved.VisitDate.ToString("MMMM dd, yyyy");
        AppointmentTime = approved.VisitTime;
        ShowApprovedFound = true;
    }

    [RelayCommand]
    private async Task ConfirmCheckIn()
    {
        var appt = await Services.AppointmentService.Instance.CheckInAsync(_currentAppointmentId);

        QueueNumber = appt.QueueNumber ?? 0;
        CheckInTimeDisplay = appt.CheckInTime?.ToString("h:mm tt") ?? string.Empty;

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
        ShowNotFound = false;
        ShowNoApproved = false;
        ShowAlreadyCheckedIn = false;
        ShowApprovedFound = false;
        ShowCheckInSuccess = false;
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}