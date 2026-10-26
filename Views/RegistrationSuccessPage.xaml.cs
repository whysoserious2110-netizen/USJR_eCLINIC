using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Views;

public partial class RegistrationSuccessPage : ContentPage
{
    public RegistrationSuccessPage(UserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        InitializeComponent();

        FullNameValue.Text = DisplayValue(account.FullName);
        IdLabel.Text = string.Equals(
            account.Role?.Trim(),
            "Student",
            StringComparison.OrdinalIgnoreCase)
                ? "Student ID"
                : "Employee ID";
        IdValue.Text = DisplayValue(account.IdNumber);
        RoleValue.Text = DisplayValue(account.Role);
        ProgramValue.Text = DisplayValue(account.ProgramOrDepartment);
        EmailValue.Text = DisplayValue(account.Email);

        // UserAccount has no account activation field. EmploymentStatus
        // describes clinic staff employment and must not be treated as
        // the account's activation status.
        AccountStatusValue.Text = "Not tracked";
    }

    private static string DisplayValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Not provided" : value.Trim();

    private async void OnContinueClicked(object sender, EventArgs e)
    {
        if (Shell.Current?.Navigation.NavigationStack.Count > 1)
            await Shell.Current.Navigation.PopAsync();
    }
}
