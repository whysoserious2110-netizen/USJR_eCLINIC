using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class BookAppointmentPage : ContentPage
{
    private readonly BookAppointmentViewModel _viewModel;

    public BookAppointmentPage()
    {
        InitializeComponent();
        _viewModel = new BookAppointmentViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadFollowUpRecommendationsAsync();
    }
}
