using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class NurseProfilePage : ContentPage
{
    public NurseProfilePage()
    {
        InitializeComponent();
        BindingContext = new NurseProfileViewModel();
    }
}