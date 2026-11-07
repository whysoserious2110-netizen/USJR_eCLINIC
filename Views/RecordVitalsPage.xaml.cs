using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class RecordVitalsPage : ContentPage
{
    public RecordVitalsPage(
        int appointmentId,
        string patientEmail,
        string patientName,
        string patientIdNumber = "")
    {
        InitializeComponent();

        BindingContext = new RecordVitalsViewModel(
            appointmentId,
            patientEmail,
            patientName,
            patientIdNumber);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RecordVitalsViewModel viewModel)
            await viewModel.InitializeAsync();
    }
}