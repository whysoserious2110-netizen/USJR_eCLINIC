using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DentalRecordListItem : ObservableObject
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string DateHeading { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientIdNumber { get; set; } = string.Empty;
    public string PatientInitials => string.Join(string.Empty, (PatientName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => part[..1].ToUpperInvariant()));
    public string PatientIdLabel { get; set; } = "Student ID";
    public string PatientIdDisplay => string.IsNullOrWhiteSpace(PatientIdNumber)
        ? "ID unavailable"
        : $"{PatientIdLabel}: {PatientIdNumber}";
    public string CourseYearDisplay { get; set; } = string.Empty;
    public string CourseYearLabel { get; set; } = "Course";
    public bool HasCourseYear => !string.IsNullOrWhiteSpace(CourseYearDisplay);
    public string ProfileImagePath { get; set; } = string.Empty;
    public string Procedure { get; set; } = "Dental visit";
    public string StatusDisplay { get; set; } = "Completed";
    public string FollowUpDisplay { get; set; } = "No follow-up visit booked.";
    public string PageTitle { get; set; } = "Dental Record Details";
    public bool IsDentist { get; set; }
    public string SecondTabLabel => IsDentist ? "Patients" : "Appointments";
    public string ThirdTabLabel => IsDentist ? "Appointments" : "Notifications";
    public string ExamFindings { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string TreatmentRendered { get; set; } = string.Empty;
    public string CareInstructions { get; set; } = string.Empty;
    public string AttendingDentist { get; set; } = string.Empty;
    public string DentalNotes { get; set; } = string.Empty;
    public bool HasCareInstructions => !string.IsNullOrWhiteSpace(CareInstructions);
    public bool HasAttendingDentist => !string.IsNullOrWhiteSpace(AttendingDentist);
    public bool HasDentalNotes => !string.IsNullOrWhiteSpace(DentalNotes);

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();

    [RelayCommand]
    private async Task GoToHome()
    {
        if (IsDentist)
        {
            await Shell.Current.Navigation.PushAsync(new Views.DentistDashboardPage());
            return;
        }
        await Shell.Current.GoToAsync("//HomePage");
    }

    [RelayCommand]
    private async Task GoToSecondTab()
    {
        if (IsDentist)
        {
            await Shell.Current.Navigation.PushAsync(new Views.DentistMyPatientsPage());
            return;
        }
        await Shell.Current.GoToAsync("//AppointmentsPage");
    }

    [RelayCommand]
    private async Task GoToThirdTab()
    {
        if (IsDentist)
        {
            await Shell.Current.Navigation.PushAsync(new Views.DentistAppointmentsPage());
            return;
        }
        await Shell.Current.GoToAsync("//NotificationsPage");
    }

    [RelayCommand]
    private async Task GoToProfile()
    {
        if (IsDentist)
        {
            await Shell.Current.Navigation.PushAsync(new Views.DentistProfilePage());
            return;
        }
        await Shell.Current.GoToAsync("//ProfilePage");
    }
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
        if (user == null)
            return;

        var records = await Services.DentalRecordService.Instance.GetForPatientAsync(user.Email);
        var appointments = await Services.AppointmentService.Instance.GetAllForServiceAsync("Dental");
        var followUps = appointments
            .Where(appointment =>
                appointment.PatientEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(appointment.SubService, "Dental Follow-up", StringComparison.OrdinalIgnoreCase) &&
                appointment.VisitDate.Date >= DateTime.Today &&
                (appointment.Status is "Pending" or "Confirmed" or "CheckedIn" or "VitalsRecorded"))
            .OrderBy(appointment => appointment.VisitDate)
            .ToList();

        Records.Clear();
        foreach (var record in records)
        {
            var appointment = record.AppointmentId > 0
                ? await Services.AppointmentService.Instance.GetByIdAsync(record.AppointmentId)
                : null;
            Records.Add(CreateListItem(record, user, appointment, followUps.FirstOrDefault(), false));
        }

        HasRecords = Records.Count > 0;
    }

    internal static DentalRecordListItem CreateListItem(
        Models.DentalRecord record,
        Models.UserAccount patient,
        Models.Appointment? appointment,
        Models.Appointment? followUp,
        bool isDentist)
    {
        var courseYear = patient.Role == "Student"
            ? patient.StudentProgramAndYear
            : patient.ProgramOrDepartment;

        return new DentalRecordListItem
        {
            Id = record.Id,
            DateDisplay = record.VisitDate.ToString("MMM dd, yyyy"),
            DateHeading = record.VisitDate.ToString("dddd, MMMM d, yyyy"),
            PatientName = patient.FullName,
            PatientIdNumber = patient.IdNumber,
            PatientIdLabel = patient.Role == "Student" ? "Student ID" : "Employee ID",
            CourseYearDisplay = courseYear,
            CourseYearLabel = patient.Role == "Student" ? "Course" : "Department",
            ProfileImagePath = patient.ProfileImagePath,
            Procedure = string.IsNullOrWhiteSpace(appointment?.SubService) ? "Dental visit" : appointment.SubService,
            StatusDisplay = "Completed",
            FollowUpDisplay = followUp == null
                ? "No follow-up visit booked."
                : $"Booked follow-up on {followUp.VisitDate:MMMM d, yyyy} at {followUp.VisitTime}.",
            PageTitle = isDentist ? "View Dental Record" : "Dental Record Details",
            IsDentist = isDentist,
            ExamFindings = record.ExamFindings,
            Diagnosis = record.Diagnosis,
            TreatmentRendered = record.TreatmentRendered,
            CareInstructions = record.CareInstructions,
            AttendingDentist = record.AttendingDentist,
            DentalNotes = record.DentalNotes
        };
    }

    [RelayCommand]
    private async Task ViewDetails(DentalRecordListItem item)
    {
        await Shell.Current.Navigation.PushAsync(new Views.DentalRecordDetailPage(item));
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();

    [RelayCommand]
    private async Task GoToHome() => await Shell.Current.GoToAsync("//HomePage");

    [RelayCommand]
    private async Task GoToAppointments() => await Shell.Current.GoToAsync("//AppointmentsPage");

    [RelayCommand]
    private async Task GoToNotifications() => await Shell.Current.GoToAsync("//NotificationsPage");

    [RelayCommand]
    private async Task GoToProfile() => await Shell.Current.GoToAsync("//ProfilePage");
}

