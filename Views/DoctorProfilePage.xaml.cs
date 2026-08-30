using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DoctorProfilePage : ContentPage
{
    public DoctorProfilePage()
    {
        InitializeComponent();
        BindingContext = new DoctorProfileViewModel();
    }
}