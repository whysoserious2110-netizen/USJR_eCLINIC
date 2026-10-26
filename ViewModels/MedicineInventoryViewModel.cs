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
    public int LowStockThreshold { get; set; }
    public bool IsLowStock { get; set; }

    public string QuantityDisplay => $"{Quantity} {Unit}";
    public string ThresholdDisplay => $"Restock level: {LowStockThreshold} {Unit}";
    public string StockStateText => IsLowStock ? "LOW STOCK" : "IN STOCK";
    public Color StockBadgeColor => IsLowStock ? Color.FromArgb("#C94E4E") : Color.FromArgb("#078F83");
}

public partial class MedicineInventoryViewModel : ObservableObject
{
    private List<MedicineStockItem> _allItems = new();

    public ObservableCollection<MedicineStockItem> Items { get; } = new();
    public ObservableCollection<string> FilterOptions { get; } = new() { "All items", "Low stock" };

    [ObservableProperty] private bool hasItems;
    [ObservableProperty] private bool hasLowStock;
    [ObservableProperty] private int lowStockCount;
    [ObservableProperty] private int totalItemCount;
    [ObservableProperty] private string stockSummaryText = "Loading inventory...";
    [ObservableProperty] private string emptyMessage = "No medicines are in inventory yet.";
    [ObservableProperty] private string resultCountText = "0 items";
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedFilter = "All items";

    [ObservableProperty] private bool isAddingStock;
    [ObservableProperty] private MedicineStockItem? selectedItem;
    [ObservableProperty] private string amountToAdd = string.Empty;
    [ObservableProperty] private bool isAddingNew;
    [ObservableProperty] private string newMedicineName = string.Empty;
    [ObservableProperty] private string newQuantity = string.Empty;
    [ObservableProperty] private string newUnit = string.Empty;
    [ObservableProperty] private string newLowStockThreshold = "10";

    public async Task RefreshAsync()
    {
        var stock = await Services.MedicineStockService.Instance.GetAllAsync();

        _allItems = stock.Select(item => new MedicineStockItem
        {
            Id = item.Id,
            MedicineName = item.MedicineName,
            Quantity = item.Quantity,
            Unit = item.Unit,
            LowStockThreshold = item.LowStockThreshold,
            IsLowStock = item.Quantity <= item.LowStockThreshold
        }).ToList();

        TotalItemCount = _allItems.Count;
        LowStockCount = _allItems.Count(item => item.IsLowStock);
        HasLowStock = LowStockCount > 0;
        StockSummaryText = HasLowStock
            ? $"{LowStockCount} item{(LowStockCount == 1 ? "" : "s")} need restocking"
            : "All stock levels are above their restock limits";

        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedFilterChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        IEnumerable<MedicineStockItem> filtered = _allItems;
        if (SelectedFilter == "Low stock")
            filtered = filtered.Where(item => item.IsLowStock);

        var query = SearchText.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(item =>
                item.MedicineName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var matches = filtered.ToList();
        Items.Clear();
        foreach (var item in matches)
            Items.Add(item);

        HasItems = Items.Count > 0;
        ResultCountText = Items.Count == 1 ? "1 item" : $"{Items.Count} items";
        EmptyMessage = _allItems.Count == 0
            ? "No medicines are in inventory yet. Use Add medicine to create the first item."
            : SelectedFilter == "Low stock" && string.IsNullOrWhiteSpace(query)
                ? "There are no low-stock medicines right now."
                : "No medicines match your search and filter.";
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
        if (SelectedItem == null || !int.TryParse(AmountToAdd, out var amount) || amount <= 0)
        {
            await Shell.Current.DisplayAlert(
                "Enter a valid quantity",
                "The amount to add must be a positive whole number.",
                "OK");
            return;
        }

        var updated = await Services.MedicineStockService.Instance.AddStockAsync(SelectedItem.Id, amount);
        if (!updated)
        {
            await Shell.Current.DisplayAlert(
                "Stock not updated",
                "This medicine could not be found. Refresh the inventory and try again.",
                "OK");
            return;
        }

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
        NewLowStockThreshold = "10";
        IsAddingNew = true;
    }

    [RelayCommand]
    private async Task ConfirmAddNew()
    {
        if (string.IsNullOrWhiteSpace(NewMedicineName) ||
            !int.TryParse(NewQuantity, out var quantity) || quantity < 0 ||
            !int.TryParse(NewLowStockThreshold, out var threshold) || threshold < 0)
        {
            await Shell.Current.DisplayAlert(
                "Check medicine details",
                "Enter a medicine name and non-negative whole numbers for quantity and restock level.",
                "OK");
            return;
        }

        var added = await Services.MedicineStockService.Instance.AddNewMedicineAsync(
            new Models.MedicineStock
            {
                MedicineName = NewMedicineName.Trim(),
                Quantity = quantity,
                Unit = string.IsNullOrWhiteSpace(NewUnit) ? "pcs" : NewUnit.Trim(),
                LowStockThreshold = threshold
            });

        if (!added)
        {
            await Shell.Current.DisplayAlert(
                "Medicine not added",
                "Check the medicine details. A medicine with that name may already exist.",
                "OK");
            return;
        }

        IsAddingNew = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private void CancelAddNew() => IsAddingNew = false;

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
