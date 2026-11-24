namespace USJR_eCLINIC.Views;

public partial class StaffSchedulesPage : ContentPage
{
    public StaffSchedulesPage()
    {
        InitializeComponent();
        BindingContext = new ViewModels.StaffSchedulesViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.StaffSchedulesViewModel viewModel)
            await viewModel.RefreshAsync();
    }
}
