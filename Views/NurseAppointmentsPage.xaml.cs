using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class NurseAppointmentsPage : ContentPage
{
    private readonly NurseAppointmentsViewModel _viewModel;

    public NurseAppointmentsPage()
    {
        InitializeComponent();
        _viewModel = new NurseAppointmentsViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}