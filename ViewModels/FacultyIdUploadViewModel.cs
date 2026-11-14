using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class FacultyIdUploadViewModel : ObservableObject
{
    private readonly int _userId;
    private readonly string _role;
    [ObservableProperty] private string imagePath = string.Empty;
    [ObservableProperty] private bool isBusy;
    public bool HasPhoto => !string.IsNullOrWhiteSpace(ImagePath);
    public bool IsPhotoMissing => !HasPhoto;
    public string ImageName => HasPhoto ? Path.GetFileName(ImagePath) : string.Empty;
    public string ReviewHeading => $"Upload your {_role} Employee ID";
    public string ReviewInstructions => $"Your email is verified, but an authorized clinic administrator must compare your {_role} Employee ID with an official USJ-R record before activating your account.";

    public FacultyIdUploadViewModel(int userId, string role)
    {
        _userId = userId;
        _role = role == "Non-Teaching" ? "Non-Teaching" : "Faculty";
    }
    partial void OnImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasPhoto));
        OnPropertyChanged(nameof(IsPhotoMissing));
        OnPropertyChanged(nameof(ImageName));
    }

    [RelayCommand]
    private async Task ChooseImage()
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose your Employee ID photo",
                FileTypes = FilePickerFileType.Images
            });
            if (file == null) return;
            var destination = Path.Combine(FileSystem.AppDataDirectory, $"faculty_id_{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}");
            await using var source = await file.OpenReadAsync();
            await using var output = File.Create(destination);
            await source.CopyToAsync(output);
            ImagePath = destination;
        }
        catch { await Shell.Current.DisplayAlert("Photo unavailable", "The selected photo could not be saved.", "OK"); }
    }

    [RelayCommand]
    private async Task Submit()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            await Shell.Current.DisplayAlert("Photo required", "Select a Faculty ID photo first.", "OK");
            return;
        }
        IsBusy = true;
        try
        {
            var saved = await Services.AuthService.Instance.UpdatePendingStaffIdImageAsync(_userId, ImagePath);
            if (!saved)
            {
                await Shell.Current.DisplayAlert("Submission failed", "The image could not be attached to this pending Faculty account.", "OK");
                return;
            }
            await Shell.Current.DisplayAlert("Submitted for review", "An authorized clinic administrator must compare your Employee ID before the account can be activated.", "OK");
            await Shell.Current.Navigation.PopAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.Navigation.PopAsync();
}
