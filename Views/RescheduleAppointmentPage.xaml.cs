using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class RescheduleAppointmentPage : ContentPage
{
    private readonly RescheduleAppointmentViewModel _viewModel;

    public RescheduleAppointmentPage(int appointmentId)
    {
        InitializeComponent();
        _viewModel = new RescheduleAppointmentViewModel(appointmentId);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private async void OnDateSelected(object? sender, DateChangedEventArgs e)
    {
        await _viewModel.RefreshAvailableSlotsAsync();
    }
}
