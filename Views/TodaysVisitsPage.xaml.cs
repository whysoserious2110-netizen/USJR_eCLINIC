using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class TodaysVisitsPage : ContentPage
{
    private readonly TodaysVisitsViewModel _viewModel;

    public TodaysVisitsPage()
    {
        InitializeComponent();
        _viewModel = new TodaysVisitsViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}