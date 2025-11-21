using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree; // ✅ Required to find what is under the mouse
using dbm_select.Models;
using dbm_select.ViewModels;
using System;
using System.Linq; // ✅ Required for finding the specific Border

namespace dbm_select.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // ✅ NEW: Browse Folder Button Handler
        private async void BrowseFolder_Click(object? sender, RoutedEventArgs e)
        {
            // 1. Open the Folder Picker
            var folders = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Folder with Photos",
                AllowMultiple = false
            });

            // 2. If user selected a folder
            if (folders.Count >= 1)
            {
                // Get the local path (e.g., "C:\Users\Name\Pictures")
                var folderPath = folders[0].Path.LocalPath;

                // 3. Call ViewModel to load images
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.LoadImages(folderPath);
                }
            }
        }

        // State variables to track the custom drag operation
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private ImageItem? _draggedItem;

        // 1. Start the Drag
        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);

            // Check if left click AND if we clicked on an item with ImageItem context
            if (point.Properties.IsLeftButtonPressed && sender is Control control && control.DataContext is ImageItem item)
            {
                _dragStartPoint = point.Position;
                _draggedItem = item;
                _isDragging = false; // We don't start dragging immediately, we wait for movement

                // ✅ CRITICAL: Capture the pointer. 
                // This ensures we keep receiving events even if the mouse leaves the original tile.
                e.Pointer?.Capture(control);
            }
        }

        // 2. Move the Ghost Image
        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            // If we aren't holding an item, do nothing
            if (sender is not Control control) return;

            var point = e.GetCurrentPoint(this);

            // Logic to detecting the START of a drag (moved > 10px)
            if (!_isDragging && _draggedItem != null && point.Properties.IsLeftButtonPressed)
            {
                var distance = Math.Sqrt(Math.Pow(point.Position.X - _dragStartPoint.X, 2) +
                                         Math.Pow(point.Position.Y - _dragStartPoint.Y, 2));

                if (distance > 10)
                {
                    _isDragging = true;

                    // ✅ Show the Ghost Image
                    // Note: 'GhostImage' and 'DragCanvas' are defined in your XAML
                    GhostImage.Source = _draggedItem.Bitmap;
                    DragCanvas.IsVisible = true;
                }
            }

            // Logic for WHILE dragging
            if (_isDragging && DragCanvas.IsVisible)
            {
                // Get position relative to the whole window (MainGrid)
                // Note: 'MainGrid' is the name we gave the root Grid in XAML
                var relativePoint = e.GetPosition(MainGrid);

                // Center the ghost image on the mouse cursor
                double x = relativePoint.X - (GhostImage.Width / 2);
                double y = relativePoint.Y - (GhostImage.Height / 2);

                // Move the image
                Canvas.SetLeft(GhostImage, x);
                Canvas.SetTop(GhostImage, y);
            }
        }

        // 3. Drop (Release)
        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            // If we were actually dragging something...
            if (_isDragging && _draggedItem != null)
            {
                // ✅ Manual Hit Testing
                // Ask the MainGrid: "What visual elements are under the mouse right now?"
                var currentPosition = e.GetPosition(MainGrid);
                var visuals = MainGrid.GetVisualsAt(currentPosition);

                // Find the first Border in that stack that has a Tag (our Drop Boxes)
                var targetBorder = visuals.OfType<Border>().FirstOrDefault(b => b.Tag != null);

                if (targetBorder != null)
                {
                    var category = targetBorder.Tag.ToString();

                    // Update the ViewModel
                    if (!string.IsNullOrEmpty(category) && DataContext is MainWindowViewModel vm)
                    {
                        vm.SetPackageImage(category, _draggedItem);
                        System.Diagnostics.Debug.WriteLine($"SUCCESS: Manual Drop into {category}");
                    }
                }
            }

            // ✅ Cleanup / Reset
            _isDragging = false;
            _draggedItem = null;
            DragCanvas.IsVisible = false; // Hide the ghost image
            e.Pointer?.Capture(null);     // Stop tracking the mouse
        }
    }
}