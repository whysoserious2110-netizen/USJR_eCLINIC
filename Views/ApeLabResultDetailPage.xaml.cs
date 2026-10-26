using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class ApeLabResultDetailPage : ContentPage
{
    public ApeLabResultDetailPage(ApeLabResultItem result)
    {
        InitializeComponent();
        BindingContext = result;
    }
}
