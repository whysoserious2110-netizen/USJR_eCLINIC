using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Views;

public partial class StaffSchedulePage : ContentPage
{
    public StaffSchedulePage(UserAccount staff)
    {
        InitializeComponent();
        BindingContext = new ViewModels.StaffScheduleViewModel(staff);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ViewModels.StaffScheduleViewModel viewModel)
            await viewModel.RefreshAsync();
    }
}
