using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class WelcomePage : ContentPage
{
    private bool _checkedRememberedSession;

    public WelcomePage()
    {
        InitializeComponent();

        BindingContext = new WelcomeViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_checkedRememberedSession)
            return;

        _checkedRememberedSession = true;

        if (Services.AuthService.Instance.CurrentUser != null)
            return;

        var rememberedUser =
            await Services.SecureSessionService.Instance
                .GetRememberedUserAsync();

        if (rememberedUser == null)
            return;

        await Shell.Current.Navigation.PushAsync(
            new ReturningUserPage());
    }
}