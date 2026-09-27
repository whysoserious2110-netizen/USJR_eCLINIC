using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class AdminPatientsListPage : ContentPage
{
    private readonly AdminPatientsListViewModel _viewModel;

    public AdminPatientsListPage()
    {
        InitializeComponent();
        _viewModel = new AdminPatientsListViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }

    private async void SearchEntry_Focused(object sender, FocusEventArgs e)
    {
        SearchFrame.BorderColor = Color.FromArgb("#0F9B8E");
        await SearchFrame.ScaleTo(1.03, 120, Easing.CubicOut);
    }

    private async void SearchEntry_Unfocused(object sender, FocusEventArgs e)
    {
        SearchFrame.BorderColor = Color.FromArgb("#E2E6EA");
        await SearchFrame.ScaleTo(1.0, 120, Easing.CubicIn);
    }
}