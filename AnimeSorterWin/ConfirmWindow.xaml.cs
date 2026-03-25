using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Data;
using Microsoft.Win32;
using AnimeSorterWin.Converters;
using AnimeSorterWin.Models;
using AnimeSorterWin.ViewModels;

namespace AnimeSorterWin;

public partial class ConfirmWindow : Window
{
    private readonly ConfirmWindowViewModel _vm;
    private CancellationTokenSource? _previewCts;

    // 预览绘制时使用的 box（归一化坐标）
    private bool _hasBox;
    private double _x1, _y1, _x2, _y2;

    public ConfirmWindow(ConfirmWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        RebuildListColumns();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConfirmWindowViewModel.FilterMode))
                Dispatcher.Invoke(() =>
                {
                    RebuildListColumns();
                    ItemsListView.Items.Refresh();
                    UpdatePreviewForFocusedItem();
                });
        };

        // 确保初始输入与 dropdown 与第一个 focused item 一致
        Loaded += async (_, _) =>
        {
            await _vm.LoadCandidatesForFocusedItemAsync(CancellationToken.None);
            UpdatePreviewForFocusedItem();
        };
    }

    private async void ItemsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedItem = ItemsListView.SelectedItem as PendingImageItem;
        _vm.FocusedItemChanged(selectedItem);

        // 异步加载 candidates（用于下拉框 options）
        // 由于这只是 UI 辅助信息，加载失败不影响“确认/跳过”本身。
        try
        {
            await _vm.LoadCandidatesForFocusedItemAsync(CancellationToken.None);
        }
        catch
        {
            // ignore
        }

        UpdatePreviewForFocusedItem();
    }

    private void UpdatePreviewForFocusedItem()
    {
        if (_vm.FocusedItem is null)
            return;

        var item = _vm.FocusedItem;
        _hasBox = true;
        _x1 = item.BoxX1;
        _y1 = item.BoxY1;
        _x2 = item.BoxX2;
        _y2 = item.BoxY2;

        // 异步加载图片（避免阻塞 UI）
        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                var bmp = await LoadBitmapAsync(item.FilePath, ct).ConfigureAwait(false);

                Dispatcher.Invoke(() =>
                {
                    PreviewImage.Source = bmp;
                    UpdateBoxOverlay();
                });
            }
            catch
            {
                // ignore preview errors
            }
        }, ct);
    }

    private async Task<BitmapImage> LoadBitmapAsync(string filePath, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.UriSource = new Uri(filePath);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }, ct).ConfigureAwait(false);
    }

    private void PreviewImage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBoxOverlay();
    }

    private void UpdateBoxOverlay()
    {
        if (!_hasBox)
            return;
        if (PreviewImage.Source is not BitmapImage bmp)
            return;
        if (PreviewImage.RenderSize.Width <= 0 || PreviewImage.RenderSize.Height <= 0)
            return;

        var containerW = PreviewImage.RenderSize.Width;
        var containerH = PreviewImage.RenderSize.Height;

        var imgW = bmp.PixelWidth;
        var imgH = bmp.PixelHeight;
        if (imgW <= 0 || imgH <= 0)
            return;

        var imgAspect = (double)imgW / imgH;
        var containerAspect = containerW / containerH;

        double renderedW, renderedH, offsetX, offsetY;

        if (imgAspect > containerAspect)
        {
            renderedW = containerW;
            renderedH = containerW / imgAspect;
            offsetX = 0;
            offsetY = (containerH - renderedH) / 2;
        }
        else
        {
            renderedH = containerH;
            renderedW = containerH * imgAspect;
            offsetX = (containerW - renderedW) / 2;
            offsetY = 0;
        }

        // box 坐标归一化在 [0,1]，做轻微保护
        var x1 = Clamp01(_x1);
        var y1 = Clamp01(_y1);
        var x2 = Clamp01(_x2);
        var y2 = Clamp01(_y2);

        var left = offsetX + Math.Min(x1, x2) * renderedW;
        var top = offsetY + Math.Min(y1, y2) * renderedH;
        var width = Math.Abs(x2 - x1) * renderedW;
        var height = Math.Abs(y2 - y1) * renderedH;

        // Stroke 在 WPF 里是以中心绘制的，直接用 width/height 容易出现“看起来偏小/偏一点”的体感。
        // 这里把 stroke 厚度纳入修正，并对齐到像素网格减少亚像素误差。
        var stroke = BoxRect.StrokeThickness;
        var halfStroke = stroke / 2.0;
        left -= halfStroke;
        top -= halfStroke;
        width += stroke;
        height += stroke;

        var leftPx = Math.Round(left);
        var topPx = Math.Round(top);
        var widthPx = Math.Max(1, Math.Round(width));
        var heightPx = Math.Max(1, Math.Round(height));

        BoxRect.SnapsToDevicePixels = true;
        BoxRect.Width = widthPx;
        BoxRect.Height = heightPx;
        Canvas.SetLeft(BoxRect, leftPx);
        Canvas.SetTop(BoxRect, topPx);
    }

    private static double Clamp01(double v)
    {
        if (v < 0) return 0;
        if (v > 1) return 1;
        return v;
    }

    private PendingImageItem[] GetSelectedPendingItems()
    {
        return ItemsListView.SelectedItems.OfType<PendingImageItem>().ToArray();
    }

    private void ConfirmSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedPendingItems();
        _vm.ConfirmSelectedItems(selected);
        ItemsListView.Items.Refresh();
    }

    private void SkipSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedPendingItems();
        _vm.SkipSelectedItems(selected);
        ItemsListView.Items.Refresh();
    }

    private void ConfirmAll_Click(object sender, RoutedEventArgs e)
    {
        _vm.ConfirmAll();
        ItemsListView.Items.Refresh();
    }

    private void StartOrganize_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void RebuildListColumns()
    {
        var fileNameConverter = (IValueConverter)Resources["FilePathToFileNameConverter"];
        var statusBrushConverter = (IValueConverter)Resources["PendingStatusToBrushConverter"];

        var gridView = new GridView();

        // 文件名列
        var fileCol = new GridViewColumn
        {
            Header = "文件名",
            Width = 300,
            CellTemplate = CreateTextCellTemplate(new System.Windows.Data.Binding("FilePath") { Converter = fileNameConverter })
        };
        gridView.Columns.Add(fileCol);

        switch (_vm.FilterMode)
        {
            case CandidateFilterMode.WorkOnly:
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "作品",
                    Width = 180,
                    CellTemplate = CreateTextCellTemplate(new System.Windows.Data.Binding("SelectedWork"))
                });
                break;
            case CandidateFilterMode.CharacterOnly:
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "角色",
                    Width = 220,
                    CellTemplate = CreateTextCellTemplate(new System.Windows.Data.Binding("SelectedCharacter"))
                });
                break;
            case CandidateFilterMode.WorkCharacter:
            default:
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "作品",
                    Width = 140,
                    CellTemplate = CreateTextCellTemplate(new System.Windows.Data.Binding("SelectedWork"))
                });
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "角色",
                    Width = 200,
                    CellTemplate = CreateTextCellTemplate(new System.Windows.Data.Binding("SelectedCharacter"))
                });
                break;
        }

        // 状态列（带颜色提示）
        gridView.Columns.Add(new GridViewColumn
        {
            Header = "状态",
            Width = 120,
            CellTemplate = CreateTextCellTemplate(
                textBinding: new System.Windows.Data.Binding("StatusDisplay"),
                backgroundBinding: new System.Windows.Data.Binding("Status") { Converter = statusBrushConverter })
        });

        ItemsListView.View = gridView;
    }

    private static DataTemplate CreateTextCellTemplate(System.Windows.Data.Binding textBinding)
    {
        return CreateTextCellTemplate(textBinding, backgroundBinding: null);
    }

    private static DataTemplate CreateTextCellTemplate(System.Windows.Data.Binding textBinding, System.Windows.Data.Binding? backgroundBinding)
    {
        var f = new FrameworkElementFactory(typeof(TextBlock));
        f.SetBinding(TextBlock.TextProperty, textBinding);
        f.SetValue(TextBlock.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        f.SetValue(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2));
        if (backgroundBinding is not null)
            f.SetBinding(TextBlock.BackgroundProperty, backgroundBinding);
        f.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Black);
        f.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        return new DataTemplate { VisualTree = f };
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出确认数据",
            Filter = "AnimeSorter确认数据 (*.animesortercache.json)|*.animesortercache.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                await _vm.ExportToFileAsync(dlg.FileName, CancellationToken.None);
                System.Windows.MessageBox.Show("导出成功。", "AnimeSorter", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入确认数据",
            Filter = "AnimeSorter确认数据 (*.animesortercache.json)|*.animesortercache.json|JSON (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                await _vm.ImportFromFileAsync(dlg.FileName, CancellationToken.None);

                if (_vm.Items.Count > 0)
                    ItemsListView.SelectedItem = _vm.Items[0];

                // 重建列并刷新
                RebuildListColumns();
                ItemsListView.Items.Refresh();

                await _vm.LoadCandidatesForFocusedItemAsync(CancellationToken.None);
                UpdatePreviewForFocusedItem();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

