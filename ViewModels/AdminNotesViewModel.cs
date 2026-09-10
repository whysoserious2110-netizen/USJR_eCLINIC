using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class AdminNoteListItem : ObservableObject
{
    public string Category { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string DateDisplay { get; set; } = string.Empty;
}

public partial class AdminNotesViewModel : ObservableObject
{
    private readonly string _patientEmail;

    public string PatientName { get; }

    public ObservableCollection<string> Categories { get; } = new()
    {
        "Late Arrival", "Reschedule Request", "ID / Records Issue", "Contact Log", "Other"
    };

    [ObservableProperty]
    private string selectedCategory = string.Empty;

    [ObservableProperty]
    private string noteText = string.Empty;

    public ObservableCollection<AdminNoteListItem> Notes { get; } = new();

    [ObservableProperty]
    private bool hasNotes;

    public AdminNotesViewModel(string patientEmail, string patientName)
    {
        _patientEmail = patientEmail;
        PatientName = patientName;
        _ = LoadNotesAsync();
    }

    private async Task LoadNotesAsync()
    {
        var notes = await Services.AdministrativeNoteService.Instance.GetForPatientAsync(_patientEmail);

        Notes.Clear();
        foreach (var n in notes)
        {
            Notes.Add(new AdminNoteListItem
            {
                Category = n.Category,
                NoteText = n.NoteText,
                AuthorName = n.AuthorName,
                DateDisplay = n.DateCreated.ToString("MMM dd, yyyy · h:mm tt")
            });
        }

        HasNotes = Notes.Count > 0;
    }

    [RelayCommand]
    private async Task SaveNote()
    {
        if (string.IsNullOrWhiteSpace(SelectedCategory))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please select a category.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(NoteText))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please enter a note.", "OK");
            return;
        }

        var staff = Services.AuthService.Instance.CurrentUser;

        var note = new Models.AdministrativeNote
        {
            PatientEmail = _patientEmail,
            Category = SelectedCategory,
            NoteText = NoteText,
            AuthorName = staff?.FullName ?? "Staff",
            DateCreated = DateTime.Now
        };

        await Services.AdministrativeNoteService.Instance.AddAsync(note);

        NoteText = string.Empty;
        SelectedCategory = string.Empty;

        await LoadNotesAsync();
        await Shell.Current.DisplayAlert("Saved", "Administrative note has been recorded.", "OK");
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}