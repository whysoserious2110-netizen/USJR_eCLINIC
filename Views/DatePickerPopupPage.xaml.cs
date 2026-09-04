using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DatePickerPopupPage : ContentPage
{
    public DatePickerPopupPage(BookAppointmentViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}