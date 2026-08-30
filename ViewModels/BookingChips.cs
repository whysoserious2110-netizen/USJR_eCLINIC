using CommunityToolkit.Mvvm.ComponentModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DateChip : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayLabel { get; set; } = string.Empty;   // MON
    public string DayNumber { get; set; } = string.Empty;  // 24

    [ObservableProperty]
    private bool isSelected;
}

public partial class TimeChip : ObservableObject
{
    public string Time { get; set; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isBooked;
}