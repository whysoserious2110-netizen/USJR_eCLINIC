namespace USJR_eCLINIC.Views;

public partial class EmailVerificationPage : ContentPage
{
    public EmailVerificationPage(int userId, string initialMessage)
    {
        InitializeComponent();
        BindingContext = new ViewModels.EmailVerificationViewModel(userId, initialMessage);
    }
}
