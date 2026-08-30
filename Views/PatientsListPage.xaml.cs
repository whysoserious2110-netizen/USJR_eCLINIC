using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class PatientsListPage : ContentPage
{
    private readonly PatientsListViewModel _viewModel;

    public PatientsListPage()
    {
        InitializeComponent();
        _viewModel = new PatientsListViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}