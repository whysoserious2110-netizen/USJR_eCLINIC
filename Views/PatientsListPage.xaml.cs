using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class PatientsListPage : ContentPage
{
    private readonly PatientsListViewModel _viewModel;

    public PatientsListPage(string? serviceTypeFilter = null, bool consultedOnly = false)
    {
        InitializeComponent();
        _viewModel = new PatientsListViewModel(serviceTypeFilter, consultedOnly);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}
