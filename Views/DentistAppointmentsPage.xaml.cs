using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DentistAppointmentsPage : ContentPage
{
    private readonly DentistAppointmentsViewModel _viewModel;

    public DentistAppointmentsPage(bool followUpsOnly = false)
    {
        InitializeComponent();
        _viewModel = new DentistAppointmentsViewModel(followUpsOnly);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}
