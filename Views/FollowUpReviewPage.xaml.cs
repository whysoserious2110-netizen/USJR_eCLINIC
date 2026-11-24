using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class FollowUpReviewPage : ContentPage
{
    private readonly FollowUpReviewViewModel _viewModel;

    public FollowUpReviewPage(int appointmentId)
    {
        InitializeComponent();
        _viewModel = new FollowUpReviewViewModel(appointmentId);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
