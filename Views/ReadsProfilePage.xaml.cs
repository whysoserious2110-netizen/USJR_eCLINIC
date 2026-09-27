using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ReadsProfilePage : ContentPage
{
    public ReadsProfilePage()
    {
        InitializeComponent();
        BindingContext = new ReadsProfileViewModel();
    }
}