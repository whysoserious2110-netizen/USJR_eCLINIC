using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ApeLabEncodingPage : ContentPage
{
    public ApeLabEncodingPage()
    {
        InitializeComponent();
        BindingContext = new ApeLabEncodingViewModel();
    }
}