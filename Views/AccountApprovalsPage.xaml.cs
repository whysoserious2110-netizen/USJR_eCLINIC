namespace USJR_eCLINIC.Views;

public partial class AccountApprovalsPage : ContentPage
{
    public AccountApprovalsPage()
    {
        InitializeComponent();
        BindingContext = new ViewModels.AdminDashboardViewModel();
    }
}
