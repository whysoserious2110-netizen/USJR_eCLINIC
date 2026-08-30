using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class AppointmentsPage : ContentPage
{
    private readonly AppointmentsViewModel _viewModel;

    public AppointmentsPage()
    {
        InitializeComponent();
        _viewModel = new AppointmentsViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}