using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media; // ✅ For RotateTransform
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

        // Handle Arrow Key Navigation
        private void PhotosListBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not ListBox listBox || listBox.ItemCount == 0 || listBox.SelectedItem == null) return;

            if (e.Key != Key.Up && e.Key != Key.Down && e.Key != Key.Left && e.Key != Key.Right) return;

            double listWidth = listBox.Bounds.Width;
            double itemWidth = 132;

            int currentIndex = listBox.SelectedIndex;
            if (currentIndex < 0) currentIndex = 0;

            var container = listBox.ContainerFromIndex(currentIndex) as Control;
            if (container != null)
            {
                itemWidth = container.Bounds.Width;
            }

            if (itemWidth < 50) itemWidth = 132;

            int columns = (int)(listWidth / itemWidth);
            if (columns < 1) columns = 1;

            int newIndex = currentIndex;
            bool handled = false;

            switch (e.Key)
            {
                case Key.Left:
                    if (currentIndex > 0)
                    {
                        newIndex = currentIndex - 1;
                        handled = true;
                    }
                    break;

                case Key.Right:
                    if (currentIndex < listBox.ItemCount - 1)
                    {
                        newIndex = currentIndex + 1;
                        handled = true;
                    }
                    break;

                case Key.Up:
                    if (currentIndex - columns >= 0)
                    {
                        newIndex = currentIndex - columns;
                        handled = true;
                    }
                    break;

                case Key.Down:
                    if (currentIndex + columns < listBox.ItemCount)
                    {
                        newIndex = currentIndex + columns;
                        handled = true;
                    }
                    break;
            }

            if (handled)
            {
                e.Handled = true;
                if (newIndex != currentIndex)
                {
                    listBox.SelectedIndex = newIndex;
                    var itemToScroll = listBox.Items[newIndex];
                    if (itemToScroll != null)
                    {
                        listBox.ScrollIntoView(itemToScroll);
                    }
                }
            }
        }

        // Browse Folder Button Handler
        private async void BrowseFolder_Click(object? sender, RoutedEventArgs e)
        {
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
                    await vm.LoadImages(folderPath);
                    PhotosListBox.Focus();
                }
            }
        }

        // Set Output Folder Button Handler
        private async void SetOutputFolder_Click(object? sender, RoutedEventArgs e)
        {
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

                    // ✅ NEW: Apply rotation to the Ghost Image
                    if (GhostImage.RenderTransform is RotateTransform rotateTransform)
                    {
                        rotateTransform.Angle = _draggedItem.RotationAngle;
                    }
                    else
                    {
                        GhostImage.RenderTransform = new RotateTransform(_draggedItem.RotationAngle);
                    }

                    DragCanvas.IsVisible = true;
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

                // Find the target Border that has a Tag (The slot)
                var targetBorder = visuals.OfType<Border>().FirstOrDefault(b => b.Tag != null);

                if (targetBorder != null)
                {
                    if (targetBorder.Tag is string categoryTag && DataContext is MainWindowViewModel vm)
                    {
                        vm.SetPackageImage(categoryTag, _draggedItem);
                        System.Diagnostics.Debug.WriteLine($"SUCCESS: Manual Drop into {categoryTag}");
                    }
                }
            }

            _isDragging = false;
            _draggedItem = null;
            DragCanvas.IsVisible = false;
            e.Pointer?.Capture(null);

            if (sender is Control control)
            {
                control.Cursor = Cursor.Parse("Hand");
            }
        }
    }
}