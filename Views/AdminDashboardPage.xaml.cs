namespace USJR_eCLINIC.Views;

public partial class AdminDashboardPage : ContentPage
{
    public AdminDashboardPage()
    {
        InitializeComponent();
        BindingContext = new ViewModels.AdminDashboardViewModel();
        SizeChanged += (_, _) => ApplyResponsiveSections();
    }

    private void ApplyResponsiveSections()
    {
        var isWide = Width >= 700;
        DashboardSections.ColumnDefinitions.Clear();
        DashboardSections.RowDefinitions.Clear();

        if (isWide)
        {
            DashboardSections.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            DashboardSections.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            DashboardSections.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(ApprovalsPanel, 0);
            Grid.SetColumn(ApprovalsPanel, 0);
            Grid.SetRow(StaffPanel, 0);
            Grid.SetColumn(StaffPanel, 1);
        }
        else
        {
            DashboardSections.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            DashboardSections.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            DashboardSections.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(ApprovalsPanel, 0);
            Grid.SetColumn(ApprovalsPanel, 0);
            Grid.SetRow(StaffPanel, 1);
            Grid.SetColumn(StaffPanel, 0);
        }
    }
}
