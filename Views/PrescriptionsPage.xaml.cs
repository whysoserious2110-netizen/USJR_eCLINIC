using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class PrescriptionsPage : ContentPage
{
    public PrescriptionsPage()
    {
        InitializeComponent();
        BindingContext = new PrescriptionsViewModel();
    }
}