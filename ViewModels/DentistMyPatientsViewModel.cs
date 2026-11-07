using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DentistMyPatientsViewModel : ObservableObject
{
    private readonly List<MyPatientItem> _allPatients = new();
    public ObservableCollection<MyPatientItem> Patients { get; } = new();

    [ObservableProperty] private bool hasPatients;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string emptyMessage = "No dental examination records have been saved by you yet.";
    [ObservableProperty] private string resultsSummary = "0 patients";

    partial void OnSearchTextChanged(string value) => ApplySearch();

    public async Task RefreshAsync()
    {
        var dentist = Services.AuthService.Instance.CurrentUser;
        if (dentist == null)
            return;

        var emails = await Services.DentalRecordService.Instance.GetDistinctPatientEmailsForStaffAsync(dentist.FullName);
        _allPatients.Clear();

        foreach (var email in emails)
        {
            var account = await Services.AuthService.Instance.GetAccountByEmailAsync(email);
            var records = await Services.DentalRecordService.Instance.GetForPatientAsync(email);
            var latestRecord = records.FirstOrDefault(record => string.Equals(record.AttendingDentist, dentist.FullName, StringComparison.OrdinalIgnoreCase));
            if (account == null || latestRecord == null)
                continue;

            var appointment = latestRecord.AppointmentId > 0
                ? await Services.AppointmentService.Instance.GetByIdAsync(latestRecord.AppointmentId)
                : null;
            var courseYear = account.Role == "Student"
                ? account.StudentProgramAndYear
                : account.ProgramOrDepartment;
_allPatients.Add(new MyPatientItem
            {
                Email = email,
                FullName = account.FullName,
                Role = account.Role,
                IdNumber = account.IdNumber,
                ProfileImagePath = account.ProfileImagePath,
                CourseYearDisplay = courseYear,
                LastVisitDisplay = $"Dental Visit: {latestRecord.VisitDate:MMM d, yyyy} · {appointment?.SubService ?? "Dental visit"}",
            });
        }

        ApplySearch();
    }

    private static Models.Appointment? FindUpcomingFollowUp(
        IEnumerable<Models.Appointment> appointments,
        string patientEmail) => appointments
        .Where(appointment =>
            appointment.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(appointment.SubService, "Dental Follow-up", StringComparison.OrdinalIgnoreCase) &&
            appointment.VisitDate.Date >= DateTime.Today &&
            (appointment.Status is "Pending" or "Confirmed" or "CheckedIn" or "VitalsRecorded"))
        .OrderBy(appointment => appointment.VisitDate)
        .FirstOrDefault();

    private void ApplySearch()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allPatients
            : _allPatients.Where(patient =>
                patient.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                patient.IdNumber.Contains(query, StringComparison.OrdinalIgnoreCase));

        var results = filtered.OrderBy(patient => patient.FullName).ToList();
        Patients.Clear();
        foreach (var patient in results)
            Patients.Add(patient);

        HasPatients = Patients.Count > 0;
        ResultsSummary = Patients.Count == 1 ? "1 patient" : $"{Patients.Count} patients";
        EmptyMessage = _allPatients.Count == 0
            ? "No dental examination records have been saved by you yet."
            : "No treated patient matches that name or ID.";
    }

    [RelayCommand]
    private async Task OpenPatient(MyPatientItem? patient)
    {
        if (patient == null)
            return;

        var dentist = Services.AuthService.Instance.CurrentUser;
        var account = await Services.AuthService.Instance.GetAccountByEmailAsync(patient.Email);
        var records = await Services.DentalRecordService.Instance.GetForPatientAsync(patient.Email);
        var latestRecord = records.FirstOrDefault(record => dentist != null && string.Equals(record.AttendingDentist, dentist.FullName, StringComparison.OrdinalIgnoreCase));
        if (latestRecord == null || account == null)
        {
            await Shell.Current.DisplayAlert("Dental record unavailable", "This patient does not have a saved dental record to open.", "OK");
            await RefreshAsync();
            return;
        }

        var appointment = latestRecord.AppointmentId > 0
            ? await Services.AppointmentService.Instance.GetByIdAsync(latestRecord.AppointmentId)
            : null;
        var dentalAppointments = await Services.AppointmentService.Instance.GetAllForServiceAsync("Dental");
        var followUp = FindUpcomingFollowUp(dentalAppointments, patient.Email);
        var recordItem = DentalRecordsViewModel.CreateListItem(latestRecord, account, appointment, followUp, dentist?.Role == "Dentist");
        await Shell.Current.Navigation.PushAsync(new Views.DentalRecordDetailPage(recordItem));
    }

    [RelayCommand]
    private async Task GoToHome() => await Shell.Current.Navigation.PushAsync(new Views.DentistDashboardPage());

    [RelayCommand]
    private async Task GoToAppointments() => await Shell.Current.Navigation.PushAsync(new Views.DentistAppointmentsPage());

    [RelayCommand]
    private async Task GoToProfile() => await Shell.Current.Navigation.PushAsync(new Views.DentistProfilePage());

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}




