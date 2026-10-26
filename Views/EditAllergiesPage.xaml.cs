using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class EditAllergiesPage : ContentPage
{
    public EditAllergiesPage()
    {
        InitializeComponent();
        BindingContext = new EditAllergiesViewModel();
    }
}
