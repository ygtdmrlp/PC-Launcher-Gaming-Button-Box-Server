using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PCLauncher.Core.Models;
using PCLauncher.Core.Services;

namespace PCLauncher.App.Views;

public partial class AddEditKeyDialog : Window
{
    private readonly IIconService _iconService;
    private readonly KeyActionItem? _existingItem;

    private string _selectedColorHex = "#3B82F6";
    private string _selectedPresetIcon = "💡";
    private string? _selectedCustomIconBase64;

    public KeyActionItem ResultItem { get; private set; }

    private static readonly string[] PresetIcons = new[]
    {
        "💡", "⚡", "🛑", "📻", "🗺️", "📸", "🛡️", "🎯", "⚙️", "🚀", "🔊", "🔇", "🎮", "🏎️", "⚔️", "🚗", "✈️", "🔄", "💥", "📦"
    };

    private static readonly (string Name, string Hex)[] PresetColors = new[]
    {
        ("Mavi", "#3B82F6"),
        ("Yeşil", "#10B981"),
        ("Kırmızı", "#EF4444"),
        ("Turuncu", "#F59E0B"),
        ("Mor", "#8B5CF6"),
        ("Pembe", "#EC4899"),
        ("Camgöbeği", "#06B6D4"),
        ("Gri", "#64748B")
    };

    public AddEditKeyDialog(IIconService iconService, KeyActionItem? existingItem = null)
    {
        InitializeComponent();

        _iconService = iconService;
        _existingItem = existingItem;

        PopulateCategories();
        PopulateQuickKeys();
        BuildPresetIcons();
        BuildColorPills();

        if (_existingItem != null)
        {
            DialogTitleText.Text = "Oyun Tuşunu Düzenle";
            TitleTextBox.Text = _existingItem.Title;
            KeyTextBox.Text = _existingItem.KeySequence;
            CategoryComboBox.Text = _existingItem.GameOrCategory;
            _selectedColorHex = _existingItem.ColorHex ?? "#3B82F6";
            _selectedPresetIcon = _existingItem.PresetIcon ?? "🎮";
            _selectedCustomIconBase64 = _existingItem.IconBase64;
            SelectHoldDuration(_existingItem.HoldDurationMs);

            ResultItem = _existingItem;
        }
        else
        {
            DialogTitleText.Text = "Yeni Oyun Tuşu Ekle (Button Box)";
            CategoryComboBox.Text = "Simülasyon";
            KeyTextBox.Text = "L";
            TitleTextBox.Text = "Farlar";
            SelectHoldDuration(100);
            ResultItem = new KeyActionItem();
        }

        UpdatePreview();
    }

    private void PopulateCategories()
    {
        CategoryComboBox.Items.Clear();
        CategoryComboBox.Items.Add("Simülasyon");
        CategoryComboBox.Items.Add("ETS 2 / ATS");
        CategoryComboBox.Items.Add("Flight Simulator");
        CategoryComboBox.Items.Add("Yarış / Sürüş");
        CategoryComboBox.Items.Add("FPS / Aksiyon");
        CategoryComboBox.Items.Add("MMO / RPG");
        CategoryComboBox.Items.Add("Genel / Medya");
    }

    private void PopulateQuickKeys()
    {
        QuickKeysCombo.Items.Clear();
        QuickKeysCombo.Items.Add("Hızlı Seç...");
        QuickKeysCombo.Items.Add("Space");
        QuickKeysCombo.Items.Add("Enter");
        QuickKeysCombo.Items.Add("Esc");
        QuickKeysCombo.Items.Add("Tab");
        QuickKeysCombo.Items.Add("L");
        QuickKeysCombo.Items.Add("E");
        QuickKeysCombo.Items.Add("M");
        QuickKeysCombo.Items.Add("X");
        QuickKeysCombo.Items.Add("F");
        QuickKeysCombo.Items.Add("R");
        QuickKeysCombo.Items.Add("C");
        QuickKeysCombo.Items.Add("F1");
        QuickKeysCombo.Items.Add("F2");
        QuickKeysCombo.Items.Add("F5");
        QuickKeysCombo.Items.Add("F10");
        QuickKeysCombo.Items.Add("F12");
        QuickKeysCombo.Items.Add("Ctrl+Shift+Esc");
        QuickKeysCombo.Items.Add("Alt+F4");
        QuickKeysCombo.Items.Add("Mute");
        QuickKeysCombo.Items.Add("PlayPause");
        QuickKeysCombo.Items.Add("VolumeUp");
        QuickKeysCombo.Items.Add("VolumeDown");
        QuickKeysCombo.SelectedIndex = 0;
    }

    private void BuildPresetIcons()
    {
        PresetsPanel.Children.Clear();
        foreach (var icon in PresetIcons)
        {
            var btn = new Button
            {
                Content = icon,
                FontSize = 18,
                Width = 34,
                Height = 34,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(0),
                Style = (Style)FindResource("ModernButton"),
                Tag = icon
            };
            btn.Click += (s, e) =>
            {
                _selectedPresetIcon = icon;
                _selectedCustomIconBase64 = null;
                UpdatePreview();
            };
            PresetsPanel.Children.Add(btn);
        }
    }

    private void BuildColorPills()
    {
        ColorsPanel.Children.Clear();
        foreach (var (name, hex) in PresetColors)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var border = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 8, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = name
            };

            border.MouseLeftButtonDown += (s, e) =>
            {
                _selectedColorHex = hex;
                UpdatePreview();
            };

            ColorsPanel.Children.Add(border);
        }
    }

    private void QuickKeysCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QuickKeysCombo.SelectedIndex > 0 && QuickKeysCombo.SelectedItem is string key)
        {
            KeyTextBox.Text = key;
        }
    }

    private void Form_Changed(object sender, TextChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void BrowseCustomIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Tuş İçin Resim / İkon Seçin",
            Filter = "Resim Dosyaları (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico|Tüm Dosyalar (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                var bytes = File.ReadAllBytes(dialog.FileName);
                _selectedCustomIconBase64 = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
                UpdatePreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Resim yüklenemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void UpdatePreview()
    {
        var title = string.IsNullOrWhiteSpace(TitleTextBox.Text) ? "Tuş" : TitleTextBox.Text.Trim();
        var key = string.IsNullOrWhiteSpace(KeyTextBox.Text) ? "?" : KeyTextBox.Text.Trim();

        PreviewTitleText.Text = title;
        PreviewKeyBadgeText.Text = $"[{key.ToUpperInvariant()}]";

        try
        {
            var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_selectedColorHex));
            PreviewBtnBorder.BorderBrush = brush;
            PreviewKeyBadgeText.Foreground = brush;
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(_selectedCustomIconBase64))
        {
            try
            {
                var bytes = _iconService.GetIconBytes(_selectedCustomIconBase64);
                if (bytes != null && bytes.Length > 0)
                {
                    using var ms = new MemoryStream(bytes);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();

                    PreviewCustomIconImage.Source = bmp;
                    PreviewCustomIconImage.Visibility = Visibility.Visible;
                    PreviewPresetIconText.Visibility = Visibility.Collapsed;
                    return;
                }
            }
            catch { }
        }

        PreviewCustomIconImage.Visibility = Visibility.Collapsed;
        PreviewPresetIconText.Visibility = Visibility.Visible;
        PreviewPresetIconText.Text = _selectedPresetIcon;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text.Trim();
        var key = KeyTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show("Lütfen buton başlığını girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            TitleTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show("Lütfen atanacak tuş veya kısayolu girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            KeyTextBox.Focus();
            return;
        }

        ResultItem.Title = title;
        ResultItem.KeySequence = key;
        ResultItem.GameOrCategory = string.IsNullOrWhiteSpace(CategoryComboBox.Text) ? "Genel" : CategoryComboBox.Text.Trim();
        ResultItem.ColorHex = _selectedColorHex;
        ResultItem.PresetIcon = _selectedPresetIcon;
        ResultItem.IconBase64 = _selectedCustomIconBase64;
        ResultItem.HoldDurationMs = GetSelectedHoldDuration();

        DialogResult = true;
        Close();
    }

    private void SelectHoldDuration(int durationMs)
    {
        foreach (ComboBoxItem item in HoldDurationComboBox.Items)
        {
            if (item.Tag is string s && int.TryParse(s, out var val) && val == durationMs)
            {
                HoldDurationComboBox.SelectedItem = item;
                return;
            }
        }
        HoldDurationComboBox.SelectedIndex = 0;
    }

    private int GetSelectedHoldDuration()
    {
        if (HoldDurationComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string s &&
            int.TryParse(s, out var val))
        {
            return val;
        }
        return 100;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
