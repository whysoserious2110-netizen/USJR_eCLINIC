using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class MedicineInventoryPage : ContentPage
{
    private readonly MedicineInventoryViewModel _viewModel;

    public MedicineInventoryPage()
    {
        InitializeComponent();
        _viewModel = new MedicineInventoryViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.RefreshAsync();
    }
}