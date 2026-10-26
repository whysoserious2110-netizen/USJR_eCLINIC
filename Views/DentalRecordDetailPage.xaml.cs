using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class DentalRecordDetailPage : ContentPage
{
    public DentalRecordDetailPage(DentalRecordListItem record)
    {
        InitializeComponent();
        BindingContext = record;
    }
}
