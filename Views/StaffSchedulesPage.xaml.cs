namespace USJR_eCLINIC.Views;

public partial class StaffSchedulesPage : ContentPage
{
    public StaffSchedulesPage()
    {
        InitializeComponent();
        BindingContext = new ViewModels.StaffSchedulesViewModel();
    }
}
