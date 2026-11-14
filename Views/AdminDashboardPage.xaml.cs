namespace USJR_eCLINIC.Views;

public partial class AdminDashboardPage : ContentPage
{
    public AdminDashboardPage()
    {
        InitializeComponent();
        BindingContext = new ViewModels.AdminDashboardViewModel();
    }
}
