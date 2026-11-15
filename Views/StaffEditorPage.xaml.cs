using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Views;

public partial class StaffEditorPage : ContentPage
{
    public StaffEditorPage(UserAccount? staff = null)
    {
        InitializeComponent();
        BindingContext = new ViewModels.StaffEditorViewModel(staff);
    }
}
