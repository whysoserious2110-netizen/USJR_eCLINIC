using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DentistMyPatientsPage : ContentPage
{
    private readonly DentistMyPatientsViewModel _viewModel;

    public DentistMyPatientsPage()
    {
        InitializeComponent();
        _viewModel = new DentistMyPatientsViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}