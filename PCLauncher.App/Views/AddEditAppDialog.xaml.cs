using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PCLauncher.Core.Models;
using PCLauncher.Core.Services;

namespace PCLauncher.App.Views;

public partial class AddEditAppDialog : Window
{
    private readonly IIconService _iconService;
    private readonly AppItem? _existingApp;
    private string? _currentIconBase64;

    public AppItem ResultApp { get; private set; }

    public AddEditAppDialog(IIconService iconService, AppItem? existingApp = null)
    {
        InitializeComponent();

        _iconService = iconService;
        _existingApp = existingApp;

        PopulateCategories();

        if (_existingApp != null)
        {
            DialogTitleText.Text = "Programı Düzenle";
            NameTextBox.Text = _existingApp.Name;
            ExePathTextBox.Text = _existingApp.ExePath;
            ArgumentsTextBox.Text = _existingApp.Arguments;
            DescriptionTextBox.Text = _existingApp.Description;
            CategoryComboBox.Text = _existingApp.Category;
            OrderTextBox.Text = _existingApp.Order.ToString();
            IsFavoriteCheckBox.IsChecked = _existingApp.IsFavorite;
            _currentIconBase64 = _existingApp.IconBase64;
            UpdateIconPreview();

            ResultApp = _existingApp;
        }
        else
        {
            DialogTitleText.Text = "Program Ekle";
            CategoryComboBox.Text = "Genel";
            ResultApp = new AppItem();
        }
    }

    private void PopulateCategories()
    {
        CategoryComboBox.Items.Clear();
        CategoryComboBox.Items.Add("Genel");
        CategoryComboBox.Items.Add("Oyunlar");
        CategoryComboBox.Items.Add("Medya");
        CategoryComboBox.Items.Add("Araçlar");
        CategoryComboBox.Items.Add("Çalışma");
        CategoryComboBox.Items.Add("İletişim");
        CategoryComboBox.Items.Add("Tarayıcılar");
    }

    private void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Çalıştırılacak Programı Seçin",
            Filter = "Çalıştırılabilir Dosyalar (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|Tüm Dosyalar (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            ExePathTextBox.Text = dialog.FileName;

            // Auto-fill Name if empty
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                NameTextBox.Text = CleanAppName(fileName);
            }

            // Extract Icon
            ExtractAndSetIcon(dialog.FileName);
        }
    }

    private static string CleanAppName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        if (name.Equals("code", StringComparison.OrdinalIgnoreCase)) return "VS Code";
        if (name.Equals("chrome", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
        if (name.Equals("msedge", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
        if (name.Equals("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        if (name.Equals("discord", StringComparison.OrdinalIgnoreCase)) return "Discord";
        if (name.Equals("steam", StringComparison.OrdinalIgnoreCase)) return "Steam";
        if (name.Equals("vlc", StringComparison.OrdinalIgnoreCase)) return "VLC Media Player";
        return char.ToUpper(name[0]) + name.Substring(1);
    }

    private void ExePathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentIconBase64) && File.Exists(ExePathTextBox.Text))
        {
            ExtractAndSetIcon(ExePathTextBox.Text);
        }
    }

    private void ExtractAndSetIcon(string path)
    {
        try
        {
            var b64 = _iconService.ExtractIconAsBase64(path);
            if (!string.IsNullOrEmpty(b64))
            {
                _currentIconBase64 = b64;
                UpdateIconPreview();
            }
        }
        catch { }
    }

    private void CustomIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Özel İkon Resmi Seçin",
            Filter = "Resim Dosyaları (*.png;*.ico;*.jpg;*.jpeg)|*.png;*.ico;*.jpg;*.jpeg|Tüm Dosyalar (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                var bytes = File.ReadAllBytes(dialog.FileName);
                _currentIconBase64 = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
                UpdateIconPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İkon yüklenemedi: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ResetIcon_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(ExePathTextBox.Text))
        {
            ExtractAndSetIcon(ExePathTextBox.Text);
        }
        else
        {
            _currentIconBase64 = null;
            IconPreviewImage.Source = null;
        }
    }

    private void UpdateIconPreview()
    {
        if (string.IsNullOrWhiteSpace(_currentIconBase64))
        {
            IconPreviewImage.Source = null;
            return;
        }

        try
        {
            var bytes = _iconService.GetIconBytes(_currentIconBase64);
            if (bytes == null || bytes.Length == 0) return;

            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            IconPreviewImage.Source = bitmap;
        }
        catch
        {
            IconPreviewImage.Source = null;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        var exePath = ExePathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Lütfen program adını girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(exePath))
        {
            MessageBox.Show("Lütfen program dosya yolunu (EXE) seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            ExePathTextBox.Focus();
            return;
        }

        if (!File.Exists(exePath))
        {
            var confirm = MessageBox.Show(
                $"Belirtilen dosya diskinizde bulunamadı:\n{exePath}\n\nYine de kaydetmek istiyor musunuz?",
                "Dosya Bulunamadı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                ExePathTextBox.Focus();
                return;
            }
        }

        int.TryParse(OrderTextBox.Text, out var order);

        // Fallback icon if none extracted
        if (string.IsNullOrWhiteSpace(_currentIconBase64))
        {
            var cat = string.IsNullOrWhiteSpace(CategoryComboBox.Text) ? "Genel" : CategoryComboBox.Text;
            _currentIconBase64 = _iconService.CreateDefaultIcon(name, cat);
        }

        ResultApp.Name = name;
        ResultApp.ExePath = exePath;
        ResultApp.Arguments = ArgumentsTextBox.Text.Trim();
        ResultApp.WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
        ResultApp.Description = DescriptionTextBox.Text.Trim();
        ResultApp.Category = string.IsNullOrWhiteSpace(CategoryComboBox.Text) ? "Genel" : CategoryComboBox.Text.Trim();
        ResultApp.Order = order;
        ResultApp.IconBase64 = _currentIconBase64;
        ResultApp.IsFavorite = IsFavoriteCheckBox.IsChecked ?? false;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
