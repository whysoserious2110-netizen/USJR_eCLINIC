using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ManageAppointmentsPage : ContentPage
{
    private readonly ManageAppointmentsViewModel _viewModel;

    public ManageAppointmentsPage()
    {
        InitializeComponent();
        _viewModel = new ManageAppointmentsViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}