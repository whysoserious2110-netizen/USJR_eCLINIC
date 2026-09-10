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
}