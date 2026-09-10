using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class TodaysOverviewPage : ContentPage
{
    private readonly TodaysOverviewViewModel _viewModel;

    public TodaysOverviewPage()
    {
        InitializeComponent();
        _viewModel = new TodaysOverviewViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}