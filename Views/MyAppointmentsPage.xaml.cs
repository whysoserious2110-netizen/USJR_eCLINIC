using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class AppointmentsPage : ContentPage
{
    private readonly AppointmentsViewModel _viewModel;

    public AppointmentsPage() : this(null)
    {
    }

    public AppointmentsPage(int? appointmentIdToOpen)
    {
        InitializeComponent();
        _viewModel = new AppointmentsViewModel(appointmentIdToOpen);
        BindingContext = _viewModel;
    }







    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}
