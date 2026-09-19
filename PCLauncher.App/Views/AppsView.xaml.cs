using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using PCLauncher.Core.Models;
using PCLauncher.Core.Services;

namespace PCLauncher.App.Views;

public class AppItemDisplay
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string ExePath { get; set; }
    public required string Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsFavorite { get; set; }
    public int LaunchCount { get; set; }
    public BitmapImage? DisplayIcon { get; set; }
    public Visibility FavoriteVisibility => IsFavorite ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DescriptionVisibility => string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;
    public string LaunchStatsText => LaunchCount > 0 ? $"• {LaunchCount} kez açıldı" : string.Empty;
    public required AppItem OriginalItem { get; set; }
}

public partial class AppsView : UserControl
{
    private readonly IStorageService _storageService;
    private readonly IAppLauncherService _launcherService;
    private readonly IIconService _iconService;
    private readonly ILoggerService _loggerService;

    private List<AppItem> _allApps = new();
    private readonly List<AppItemDisplay> _displayItems = new();
    private string _currentSearch = string.Empty;
    private string _currentCategory = "Tümü";

    public event Action? AppsChanged;

    public AppsView(
        IStorageService storageService,
        IAppLauncherService launcherService,
        IIconService iconService,
        ILoggerService loggerService)
    {
        InitializeComponent();

        _storageService = storageService;
        _launcherService = launcherService;
        _iconService = iconService;
        _loggerService = loggerService;

        Loaded += async (s, e) => await LoadAppsAsync();
    }

    public async Task LoadAppsAsync()
    {
        _allApps = await _storageService.LoadAppsAsync();
        UpdateCategoryFilter();
        FilterAndRenderApps();
    }

    private void UpdateCategoryFilter()
    {
        var categories = new HashSet<string> { "Tümü", "⭐ Favoriler" };
        foreach (var app in _allApps)
        {
            if (!string.IsNullOrWhiteSpace(app.Category))
            {
                categories.Add(app.Category.Trim());
            }
        }

        var prevSelected = CategoryFilterCombo.SelectedItem as string ?? "Tümü";

        CategoryFilterCombo.Items.Clear();
        foreach (var cat in categories)
        {
            CategoryFilterCombo.Items.Add(cat);
        }

        if (CategoryFilterCombo.Items.Contains(prevSelected))
        {
            CategoryFilterCombo.SelectedItem = prevSelected;
        }
        else
        {
            CategoryFilterCombo.SelectedIndex = 0;
        }
    }

    private void FilterAndRenderApps()
    {
        _displayItems.Clear();

        var query = _allApps.AsEnumerable();

        // Category filter
        if (_currentCategory == "⭐ Favoriler")
        {
            query = query.Where(a => a.IsFavorite);
        }
        else if (!string.IsNullOrWhiteSpace(_currentCategory) && _currentCategory != "Tümü")
        {
            query = query.Where(a => string.Equals(a.Category, _currentCategory, StringComparison.OrdinalIgnoreCase));
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(_currentSearch))
        {
            query = query.Where(a =>
                a.Name.Contains(_currentSearch, StringComparison.OrdinalIgnoreCase) ||
                a.Category.Contains(_currentSearch, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(_currentSearch, StringComparison.OrdinalIgnoreCase));
        }

        // Sort
        var sorted = query.OrderBy(a => a.Order).ThenBy(a => a.Name).ToList();

        foreach (var app in sorted)
        {
            _displayItems.Add(new AppItemDisplay
            {
                Id = app.Id,
                Name = app.Name,
                ExePath = app.ExePath,
                Category = app.Category,
                Description = app.Description,
                Order = app.Order,
                IsFavorite = app.IsFavorite,
                LaunchCount = app.LaunchCount,
                DisplayIcon = LoadBitmapFromBase64(app.IconBase64),
                OriginalItem = app
            });
        }

        AppsListBox.ItemsSource = null;
        AppsListBox.ItemsSource = _displayItems;

        AppsSummaryText.Text = $"{_allApps.Count} kayıtlı program mevcut.";

        EmptyStateBorder.Visibility = _allApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AppsListBox.Visibility = _allApps.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private BitmapImage? LoadBitmapFromBase64(string? b64)
    {
        if (string.IsNullOrWhiteSpace(b64)) return null;
        try
        {
            var bytes = _iconService.GetIconBytes(b64);
            if (bytes == null || bytes.Length == 0) return null;

            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private async void AddApp_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        var dialog = new AddEditAppDialog(_iconService)
        {
            Owner = window
        };

        if (dialog.ShowDialog() == true)
        {
            var newApp = dialog.ResultApp;
            _allApps.Add(newApp);
            await _storageService.SaveAppsAsync(_allApps);
            _loggerService.LogInfo($"Yeni program eklendi: {newApp.Name}");

            await LoadAppsAsync();
            AppsChanged?.Invoke();
        }
    }

    private async void EditApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string appId)
        {
            var target = _allApps.FirstOrDefault(a => a.Id == appId);
            if (target == null) return;

            var window = Window.GetWindow(this);
            var dialog = new AddEditAppDialog(_iconService, target)
            {
                Owner = window
            };

            if (dialog.ShowDialog() == true)
            {
                await _storageService.SaveAppsAsync(_allApps);
                _loggerService.LogInfo($"Program güncellendi: {target.Name}");

                await LoadAppsAsync();
                AppsChanged?.Invoke();
            }
        }
    }

    private async void DeleteApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string appId)
        {
            var target = _allApps.FirstOrDefault(a => a.Id == appId);
            if (target == null) return;

            var result = MessageBox.Show(
                $"'{target.Name}' programını silmek istediğinizden emin misiniz?",
                "Programı Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _allApps.Remove(target);
                await _storageService.SaveAppsAsync(_allApps);
                _loggerService.LogInfo($"Program silindi: {target.Name}");

                await LoadAppsAsync();
                AppsChanged?.Invoke();
            }
        }
    }

    private async void TestLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string appId)
        {
            var target = _allApps.FirstOrDefault(a => a.Id == appId);
            if (target == null) return;

            btn.IsEnabled = false;
            try
            {
                var result = await _launcherService.TestLaunchAsync(target);
                if (result.IsSuccess)
                {
                    target.LaunchCount++;
                    target.LastLaunchedAt = DateTime.Now;
                    await _storageService.SaveAppsAsync(_allApps);
                    FilterAndRenderApps();

                    MessageBox.Show(result.Message, "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Başlatılamadı:\n{result.Message}\n{result.ErrorDetail}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _currentSearch = SearchBox.Text.Trim();
        FilterAndRenderApps();
    }

    private void CategoryFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentCategory = CategoryFilterCombo.SelectedItem as string ?? "Tümü";
        FilterAndRenderApps();
    }
}
