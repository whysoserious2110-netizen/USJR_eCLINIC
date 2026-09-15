using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class MedicineStockItem : ObservableObject
{
    public int Id { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsLowStock { get; set; }
}

public partial class MedicineInventoryViewModel : ObservableObject
{
    public ObservableCollection<MedicineStockItem> Items { get; } = new();

    [ObservableProperty] private bool hasItems;

    // Add-stock dialog state
    [ObservableProperty] private bool isAddingStock;
    [ObservableProperty] private MedicineStockItem? selectedItem;
    [ObservableProperty] private string amountToAdd = string.Empty;

    // Add-new-medicine state
    [ObservableProperty] private bool isAddingNew;
    [ObservableProperty] private string newMedicineName = string.Empty;
    [ObservableProperty] private string newQuantity = string.Empty;
    [ObservableProperty] private string newUnit = string.Empty;

    public async Task RefreshAsync()
    {
        var stock = await Services.MedicineStockService.Instance.GetAllAsync();

        Items.Clear();
        foreach (var s in stock)
        {
            Items.Add(new MedicineStockItem
            {
                Id = s.Id,
                MedicineName = s.MedicineName,
                Quantity = s.Quantity,
                Unit = s.Unit,
                IsLowStock = s.Quantity <= s.LowStockThreshold
            });
        }

        HasItems = Items.Count > 0;
    }

    [RelayCommand]
    private void OpenAddStock(MedicineStockItem item)
    {
        SelectedItem = item;
        AmountToAdd = string.Empty;
        IsAddingStock = true;
    }

    [RelayCommand]
    private async Task ConfirmAddStock()
    {
        if (SelectedItem == null || !int.TryParse(AmountToAdd, out int amount) || amount <= 0)
        {
            await Shell.Current.DisplayAlert("Invalid amount", "Please enter a valid quantity to add.", "OK");
            return;
        }

        await Services.MedicineStockService.Instance.AddStockAsync(SelectedItem.Id, amount);
        IsAddingStock = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private void CancelAddStock() => IsAddingStock = false;

    [RelayCommand]
    private void OpenAddNew()
    {
        NewMedicineName = string.Empty;
        NewQuantity = string.Empty;
        NewUnit = string.Empty;
        IsAddingNew = true;
    }

    [RelayCommand]
    private async Task ConfirmAddNew()
    {
        if (string.IsNullOrWhiteSpace(NewMedicineName) || !int.TryParse(NewQuantity, out int qty))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please enter a medicine name and valid quantity.", "OK");
            return;
        }

        await Services.MedicineStockService.Instance.AddNewMedicineAsync(new Models.MedicineStock
        {
            MedicineName = NewMedicineName,
            Quantity = qty,
            Unit = string.IsNullOrWhiteSpace(NewUnit) ? "pcs" : NewUnit
        });

        IsAddingNew = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private void CancelAddNew() => IsAddingNew = false;

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}