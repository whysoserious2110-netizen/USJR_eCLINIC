using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class NotificationDetailPage : ContentPage
{
    public NotificationDetailPage(string title, string message, string dateDisplay)
    {
        InitializeComponent();
        BindingContext = new NotificationDetailViewModel(title, message, dateDisplay);
    }
}