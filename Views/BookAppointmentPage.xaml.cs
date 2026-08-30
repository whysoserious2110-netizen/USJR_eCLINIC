using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class BookAppointmentPage : ContentPage
{
    public BookAppointmentPage()
    {
        InitializeComponent();
        BindingContext = new BookAppointmentViewModel();
    }
}