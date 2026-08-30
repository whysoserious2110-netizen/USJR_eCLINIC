using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class PostAnnouncementPage : ContentPage
{
    public PostAnnouncementPage()
    {
        InitializeComponent();
        BindingContext = new PostAnnouncementViewModel();
    }
}