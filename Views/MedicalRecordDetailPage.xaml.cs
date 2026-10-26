using USJR_eCLINIC.ViewModels;

namespace USJR_eCLINIC.Views;

public partial class MedicalRecordDetailPage : ContentPage
{
    public MedicalRecordDetailPage(MedicalRecordListItem record)
    {
        InitializeComponent();
        BindingContext = record;
    }
}
