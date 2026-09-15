using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DentistProfilePage : ContentPage
{
    public DentistProfilePage()
    {
        InitializeComponent();
        BindingContext = new DentistProfileViewModel();
    }
}