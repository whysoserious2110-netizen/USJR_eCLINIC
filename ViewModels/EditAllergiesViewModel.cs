using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class AllergyEntry : ObservableObject
{
    [ObservableProperty]
    private string allergyName = string.Empty;

    [ObservableProperty]
    private string allergyType = "Medication";

    [ObservableProperty]
    private string reaction = string.Empty;
}

public sealed class AllergyEntryData
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Medication";
    public string Reaction { get; set; } = string.Empty;
}

public partial class EditAllergiesViewModel : ObservableObject
{
    private readonly Models.UserAccount? _user;

    public ObservableCollection<AllergyEntry> AllergyEntries { get; } = new();

    public ObservableCollection<string> AllergyTypes { get; } = new()
    {
        "Medication", "Food", "Environmental", "Insect", "Other"
    };

    [ObservableProperty]
    private string fullName = string.Empty;

    [ObservableProperty]
    private bool hasNoKnownAllergies;

    public bool ShowAllergyEntries => !HasNoKnownAllergies;

    public EditAllergiesViewModel()
    {
        _user = Services.AuthService.Instance.CurrentUser;
        if (_user == null)
        {
            HasNoKnownAllergies = true;
            return;
        }

        FullName = _user.FullName;
        LoadExistingAllergies(_user);
    }

    partial void OnHasNoKnownAllergiesChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowAllergyEntries));
        if (value)
            AllergyEntries.Clear();
    }

    private void LoadExistingAllergies(Models.UserAccount user)
    {
        if (!string.IsNullOrWhiteSpace(user.AllergyDetailsJson))
        {
            try
            {
                var savedEntries = JsonSerializer.Deserialize<List<AllergyEntryData>>(
                    user.AllergyDetailsJson);

                if (savedEntries != null)
                {
                    foreach (var saved in savedEntries)
                    {
                        if (string.IsNullOrWhiteSpace(saved.Name))
                            continue;

                        AllergyEntries.Add(new AllergyEntry
                        {
                            AllergyName = saved.Name,
                            AllergyType = AllergyTypes.Contains(saved.Type) ? saved.Type : "Other",
                            Reaction = saved.Reaction
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to the legacy single-text allergy value below.
            }
        }

        if (AllergyEntries.Count == 0)
            LoadLegacyAllergySummary(user.Allergies);

        HasNoKnownAllergies = AllergyEntries.Count == 0;
    }

    private void LoadLegacyAllergySummary(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary) ||
            string.Equals(summary.Trim(), "No known allergies", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var part in summary.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith("Details:", StringComparison.OrdinalIgnoreCase))
            {
                AllergyEntries.Add(new AllergyEntry
                {
                    AllergyName = "Other",
                    AllergyType = "Other",
                    Reaction = part["Details:".Length..].Trim()
                });
            }
            else
            {
                // Existing sign-up data has allergy names only, so the type is left for the user to confirm.
                AllergyEntries.Add(new AllergyEntry
                {
                    AllergyName = part,
                    AllergyType = "Other"
                });
            }
        }
    }

    [RelayCommand]
    private void AddAllergy()
    {
        HasNoKnownAllergies = false;
        AllergyEntries.Add(new AllergyEntry());
    }

    [RelayCommand]
    private void RemoveAllergy(AllergyEntry entry)
    {
        if (entry != null)
            AllergyEntries.Remove(entry);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_user == null)
        {
            await Shell.Current.DisplayAlert("Unable to save", "Please sign in again and retry.", "OK");
            return;
        }

        var entries = HasNoKnownAllergies
            ? new List<AllergyEntryData>()
            : AllergyEntries.Select(entry => new AllergyEntryData
            {
                Name = entry.AllergyName.Trim(),
                Type = entry.AllergyType.Trim(),
                Reaction = entry.Reaction.Trim()
            }).ToList();

        if (!HasNoKnownAllergies && entries.Count == 0)
        {
            await Shell.Current.DisplayAlert(
                "Add allergy details",
                "Add an allergy or select ‘No known allergies’ before saving.",
                "OK");
            return;
        }

        if (entries.Any(entry => string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Type)))
        {
            await Shell.Current.DisplayAlert(
                "Missing information",
                "Enter an allergy name and choose its type for each allergy.",
                "OK");
            return;
        }

        var oldSummary = _user.Allergies;
        var oldJson = _user.AllergyDetailsJson;
        _user.Allergies = HasNoKnownAllergies
            ? "No known allergies"
            : string.Join("; ", entries.Select(entry =>
                string.IsNullOrWhiteSpace(entry.Reaction)
                    ? $"{entry.Name} ({entry.Type})"
                    : $"{entry.Name} ({entry.Type}) — Reaction: {entry.Reaction}"));
        _user.AllergyDetailsJson = JsonSerializer.Serialize(entries);

        try
        {
            var saved = await Services.AuthService.Instance.UpdateProfileAsync(_user);
            if (!saved)
            {
                _user.Allergies = oldSummary;
                _user.AllergyDetailsJson = oldJson;
                await Shell.Current.DisplayAlert("Save failed", "Your allergy details could not be saved. Please try again.", "OK");
                return;
            }

            await Shell.Current.DisplayAlert("Allergies Updated", "Your allergy information has been saved.", "OK");
            await Shell.Current.Navigation.PopAsync();
        }
        catch
        {
            _user.Allergies = oldSummary;
            _user.AllergyDetailsJson = oldJson;
            await Shell.Current.DisplayAlert("Save failed", "Your allergy details could not be saved. Please try again.", "OK");
        }
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
    private async Task GoToProfile() => await Shell.Current.Navigation.PopAsync();
}
