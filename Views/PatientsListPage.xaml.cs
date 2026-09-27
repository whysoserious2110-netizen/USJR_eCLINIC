using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class PatientsListPage : ContentPage
{
    private readonly PatientsListViewModel _viewModel;

    public PatientsListPage(string? serviceTypeFilter = null)
    {
        InitializeComponent();
        _viewModel = new PatientsListViewModel(serviceTypeFilter);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}