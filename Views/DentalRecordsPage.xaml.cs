using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DentalRecordsPage : ContentPage
{
    public DentalRecordsPage()
    {
        InitializeComponent();
        BindingContext = new DentalRecordsViewModel();
    }
}