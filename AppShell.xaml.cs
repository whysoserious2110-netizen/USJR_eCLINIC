namespace USJR_eCLINIC
{
    public partial class AppShell : Shell
    {
        private bool _sessionRestoreChecked;

        public AppShell()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_sessionRestoreChecked)
                return;

            _sessionRestoreChecked = true;
            try
            {
                var restored = await Services.SecureSessionService.Instance.TryRestoreSessionAsync();
                var user = Services.AuthService.Instance.CurrentUser;
                if (restored && user != null)
                    await Navigation.PushAsync(GetDashboard(user.Role));
            }
            catch
            {
                await Services.SecureSessionService.Instance.ClearSessionAsync();
                Services.AuthService.Instance.Logout();
            }
        }

        internal static Page GetDashboard(string role) => role switch
        {
            "Doctor" => new Views.DoctorDashboardPage(),
            "R.E.A.D.S. Scholar" => new Views.ReadsDashboardPage(),
            "Nurse" => new Views.NurseDashboardPage(),
            "Dentist" => new Views.DentistDashboardPage(),
            "Admin Personnel" => new Views.AdminDashboardPage(),
            _ => new Views.HomePage()
        };
    }
}
