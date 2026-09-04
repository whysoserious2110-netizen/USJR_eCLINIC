using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class MyPatientsPage : ContentPage
{
    private readonly MyPatientsViewModel _viewModel;

    public MyPatientsPage()
    {
        InitializeComponent();
        _viewModel = new MyPatientsViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}