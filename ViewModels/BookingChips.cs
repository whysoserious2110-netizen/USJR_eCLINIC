using CommunityToolkit.Mvvm.ComponentModel;

namespace USJR_eCLINIC.ViewModels;

public partial class CalendarDayCell : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayNumber { get; set; } = string.Empty;
    public bool IsCurrentMonth { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsSelectable { get; set; }
    public bool IsToday { get; set; }

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private Color textColor = Colors.Black;

    [ObservableProperty]
    private Color cellBackgroundColor = Colors.Transparent;

    [ObservableProperty]
    private Color cellBorderColor = Colors.Transparent;
}

public partial class TimeChip : ObservableObject
{
    public string Time { get; set; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isBooked;
}