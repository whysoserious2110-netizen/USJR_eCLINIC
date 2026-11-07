using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DentalExaminationPage : ContentPage
{
    private readonly DentalExaminationViewModel _viewModel;

    public DentalExaminationPage(int appointmentId, string patientEmail, string patientName, string patientRole, string appointmentContext)
    {
        InitializeComponent();
        _viewModel = new DentalExaminationViewModel(appointmentId, patientEmail, patientName, patientRole, appointmentContext);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
