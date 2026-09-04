using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DoctorAppointmentsPage : ContentPage
{
    private readonly DoctorAppointmentsViewModel _viewModel;

    public DoctorAppointmentsPage()
    {
        InitializeComponent();
        _viewModel = new DoctorAppointmentsViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}