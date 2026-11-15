namespace USJR_eCLINIC.Views;

public partial class StaffManagementPage : ContentPage
{
    public StaffManagementPage()
    {
        InitializeComponent();
        BindingContext = new ViewModels.StaffManagementViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.StaffManagementViewModel viewModel)
            await viewModel.RefreshAsync();
    }
}
