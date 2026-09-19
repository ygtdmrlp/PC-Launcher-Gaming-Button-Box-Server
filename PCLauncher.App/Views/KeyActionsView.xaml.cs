using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCLauncher.Core.Models;
using PCLauncher.Core.Services;

namespace PCLauncher.App.Views;

public class KeyActionItemDisplay
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string KeySequence { get; set; }
    public string KeyBadge => $"[{KeySequence.ToUpperInvariant()}]";
    public required string GameOrCategory { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";
    public string? PresetIcon { get; set; }
    public BitmapImage? CustomIcon { get; set; }
    public Visibility PresetIconVisibility => CustomIcon == null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CustomIconVisibility => CustomIcon != null ? Visibility.Visible : Visibility.Collapsed;
    public Brush CardBorderBrush
    {
        get
        {
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorHex));
            }
            catch
            {
                return new SolidColorBrush(Color.FromRgb(59, 130, 246));
            }
        }
    }
    public int PressCount { get; set; }
    public string StatsText => PressCount > 0 ? $"• {PressCount} kez basıldı" : string.Empty;
    public required KeyActionItem OriginalItem { get; set; }
}

public partial class KeyActionsView : UserControl
{
    private readonly IStorageService _storageService;
    private readonly IKeySimulatorService _keySimulatorService;
    private readonly IIconService _iconService;
    private readonly ILoggerService _loggerService;

    private List<KeyActionItem> _allKeys = new();
    private readonly List<KeyActionItemDisplay> _displayItems = new();
    private string _currentSearch = string.Empty;
    private string _currentCategory = "Tümü";

    public event Action? KeyActionsChanged;

    public KeyActionsView(
        IStorageService storageService,
        IKeySimulatorService keySimulatorService,
        IIconService iconService,
        ILoggerService loggerService)
    {
        InitializeComponent();

        _storageService = storageService;
        _keySimulatorService = keySimulatorService;
        _iconService = iconService;
        _loggerService = loggerService;

        Loaded += async (s, e) => await LoadKeysAsync();
    }

    public async Task LoadKeysAsync()
    {
        _allKeys = await _storageService.LoadKeyActionsAsync();
        UpdateCategoryFilter();
        FilterAndRenderKeys();
    }

    private void UpdateCategoryFilter()
    {
        var categories = new HashSet<string> { "Tümü" };
        foreach (var k in _allKeys)
        {
            if (!string.IsNullOrWhiteSpace(k.GameOrCategory))
            {
                categories.Add(k.GameOrCategory.Trim());
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

    private void FilterAndRenderKeys()
    {
        _displayItems.Clear();

        var query = _allKeys.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_currentCategory) && _currentCategory != "Tümü")
        {
            query = query.Where(k => string.Equals(k.GameOrCategory, _currentCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_currentSearch))
        {
            query = query.Where(k =>
                k.Title.Contains(_currentSearch, StringComparison.OrdinalIgnoreCase) ||
                k.KeySequence.Contains(_currentSearch, StringComparison.OrdinalIgnoreCase) ||
                k.GameOrCategory.Contains(_currentSearch, StringComparison.OrdinalIgnoreCase));
        }

        var sorted = query.OrderBy(k => k.Order).ThenBy(k => k.Title).ToList();

        foreach (var k in sorted)
        {
            _displayItems.Add(new KeyActionItemDisplay
            {
                Id = k.Id,
                Title = k.Title,
                KeySequence = k.KeySequence,
                GameOrCategory = k.GameOrCategory,
                ColorHex = k.ColorHex,
                PresetIcon = k.PresetIcon,
                CustomIcon = LoadBitmapFromBase64(k.IconBase64),
                PressCount = k.PressCount,
                OriginalItem = k
            });
        }

        KeysListBox.ItemsSource = null;
        KeysListBox.ItemsSource = _displayItems;

        SummaryText.Text = $"{_allKeys.Count} adet oyun tuşu / makro tanımlı.";
        EmptyStateBorder.Visibility = _allKeys.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        KeysListBox.Visibility = _allKeys.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
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

    private async void AddKey_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        var dialog = new AddEditKeyDialog(_iconService)
        {
            Owner = window
        };

        if (dialog.ShowDialog() == true)
        {
            var newItem = dialog.ResultItem;
            _allKeys.Add(newItem);
            await _storageService.SaveKeyActionsAsync(_allKeys);
            _loggerService.LogInfo($"Yeni tuş eklendi: {newItem.Title} [{newItem.KeySequence}]");

            await LoadKeysAsync();
            KeyActionsChanged?.Invoke();
        }
    }

    private async void EditKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string keyId)
        {
            var target = _allKeys.FirstOrDefault(k => k.Id == keyId);
            if (target == null) return;

            var window = Window.GetWindow(this);
            var dialog = new AddEditKeyDialog(_iconService, target)
            {
                Owner = window
            };

            if (dialog.ShowDialog() == true)
            {
                await _storageService.SaveKeyActionsAsync(_allKeys);
                _loggerService.LogInfo($"Tuş güncellendi: {target.Title} [{target.KeySequence}]");

                await LoadKeysAsync();
                KeyActionsChanged?.Invoke();
            }
        }
    }

    private async void DeleteKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string keyId)
        {
            var target = _allKeys.FirstOrDefault(k => k.Id == keyId);
            if (target == null) return;

            var result = MessageBox.Show(
                $"'{target.Title}' tuşunu silmek istediğinizden emin misiniz?",
                "Tuşu Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _allKeys.Remove(target);
                await _storageService.SaveKeyActionsAsync(_allKeys);
                _loggerService.LogInfo($"Tuş silindi: {target.Title}");

                await LoadKeysAsync();
                KeyActionsChanged?.Invoke();
            }
        }
    }

    private async void TestKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string keyId)
        {
            var target = _allKeys.FirstOrDefault(k => k.Id == keyId);
            if (target == null) return;

            btn.IsEnabled = false;
            try
            {
                var holdMs = target.HoldDurationMs > 0 ? target.HoldDurationMs : 100;
                var result = await _keySimulatorService.SimulateKeySequenceAsync(target.KeySequence, holdMs);
                if (result.Success)
                {
                    target.PressCount++;
                    target.LastPressedAt = DateTime.Now;
                    await _storageService.SaveKeyActionsAsync(_allKeys);
                    FilterAndRenderKeys();

                    _loggerService.LogInfo($"Tuş testi: {target.Title} [{target.KeySequence}]");
                }
                else
                {
                    MessageBox.Show($"Tuş gönderilemedi:\n{result.Message}\n{result.ErrorDetail}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
        FilterAndRenderKeys();
    }

    private void CategoryFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentCategory = CategoryFilterCombo.SelectedItem as string ?? "Tümü";
        FilterAndRenderKeys();
    }
}
