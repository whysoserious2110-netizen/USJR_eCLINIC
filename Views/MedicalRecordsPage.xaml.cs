using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class MedicalRecordsPage : ContentPage
{
    public MedicalRecordsPage()
    {
        InitializeComponent();
        BindingContext = new MedicalRecordsViewModel();
    }
}