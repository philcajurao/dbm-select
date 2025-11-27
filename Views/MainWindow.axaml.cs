using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using dbm_select.Models;
using dbm_select.ViewModels;
using System;
using System.Linq;

namespace dbm_select.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Browse Folder Button Handler
        private async void BrowseFolder_Click(object? sender, RoutedEventArgs e)
        {
            // ✅ FIX: Force start in Pictures folder to prevent lag from invalid paths
            var startLocation = await this.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Pictures);

            var folders = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Folder with Photos",
                AllowMultiple = false,
                SuggestedStartLocation = startLocation
            });

            if (folders.Count >= 1)
            {
                var folderPath = folders[0].Path.LocalPath;
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.LoadImages(folderPath);
                }
            }
        }

        // Set Output Folder Button Handler
        private async void SetOutputFolder_Click(object? sender, RoutedEventArgs e)
        {
            // ✅ FIX: Force start in Documents
            var startLocation = await this.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);

            var folders = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Where to Save Images",
                AllowMultiple = false,
                SuggestedStartLocation = startLocation
            });

            if (folders.Count >= 1)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.OutputFolderPath = folders[0].Path.LocalPath;
                }
            }
        }

        // Set Excel Folder Button Handler
        private async void SetExcelFolder_Click(object? sender, RoutedEventArgs e)
        {
            // ✅ FIX: Force start in Documents
            var startLocation = await this.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);

            var folders = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Where to Save Excel Log",
                AllowMultiple = false,
                SuggestedStartLocation = startLocation
            });

            if (folders.Count >= 1)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.ExcelFolderPath = folders[0].Path.LocalPath;
                }
            }
        }

        // --- Drag & Drop Logic ---
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private ImageItem? _draggedItem;

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed && sender is Control control && control.DataContext is ImageItem item)
            {
                _dragStartPoint = point.Position;
                _draggedItem = item;
                _isDragging = false;
                e.Pointer?.Capture(control);
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control) return;

            var point = e.GetCurrentPoint(this);

            if (!_isDragging && _draggedItem != null && point.Properties.IsLeftButtonPressed)
            {
                var distance = Math.Sqrt(Math.Pow(point.Position.X - _dragStartPoint.X, 2) +
                                         Math.Pow(point.Position.Y - _dragStartPoint.Y, 2));

                if (distance > 10)
                {
                    _isDragging = true;
                    GhostImage.Source = _draggedItem.Bitmap;
                    DragCanvas.IsVisible = true;

                    // Apply cursor directly to the control being dragged
                    control.Cursor = new Cursor(StandardCursorType.SizeAll);
                }
            }

            if (_isDragging && DragCanvas.IsVisible)
            {
                var relativePoint = e.GetPosition(MainGrid);
                double x = relativePoint.X - (GhostImage.Width / 2);
                double y = relativePoint.Y - (GhostImage.Height / 2);
                Canvas.SetLeft(GhostImage, x);
                Canvas.SetTop(GhostImage, y);
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDragging && _draggedItem != null)
            {
                var currentPosition = e.GetPosition(MainGrid);
                var visuals = MainGrid.GetVisualsAt(currentPosition);
                var targetBorder = visuals.OfType<Border>().FirstOrDefault(b => b.Tag != null);

                if (targetBorder != null)
                {
                    var category = targetBorder.Tag.ToString();
                    if (!string.IsNullOrEmpty(category) && DataContext is MainWindowViewModel vm)
                    {
                        vm.SetPackageImage(category, _draggedItem);
                        System.Diagnostics.Debug.WriteLine($"SUCCESS: Manual Drop into {category}");
                    }
                }
            }

            _isDragging = false;
            _draggedItem = null;
            DragCanvas.IsVisible = false;
            e.Pointer?.Capture(null);

            // Reset cursor back to Hand
            if (sender is Control control)
            {
                control.Cursor = Cursor.Parse("Hand");
            }
        }
    }
}