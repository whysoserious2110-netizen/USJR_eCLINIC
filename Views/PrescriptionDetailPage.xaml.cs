using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class PrescriptionDetailPage : ContentPage
{
    public PrescriptionDetailPage(PrescriptionListItem prescription)
    {
        InitializeComponent();
        BindingContext = prescription;
    }
}
