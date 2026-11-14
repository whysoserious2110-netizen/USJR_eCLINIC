namespace USJR_eCLINIC.Views;

public partial class FacultyIdUploadPage : ContentPage
{
    public FacultyIdUploadPage(int userId, string role)
    {
        InitializeComponent();
        BindingContext = new ViewModels.FacultyIdUploadViewModel(userId, role);
    }
}
