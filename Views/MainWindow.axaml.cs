using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using dbm_select.Models;
using System;

namespace dbm_select.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Stores the point where the user started clicking
        private Point _dragStartPoint;

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Record the position where the mouse button was pressed
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed)
            {
                _dragStartPoint = point.Position;
            }
        }

        private async void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            // Check if the left button is held down
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed) return;

            // Calculate how far the mouse has moved
            var distance = Math.Sqrt(Math.Pow(point.Position.X - _dragStartPoint.X, 2) +
                                     Math.Pow(point.Position.Y - _dragStartPoint.Y, 2));

            // Only start dragging if moved more than 10 pixels (prevents accidental drags on clicks)
            if (distance > 10)
            {
                if (sender is Border border && border.DataContext is ImageItem imageItem)
                {
                    // Create the drag data payload
                    var dragData = new DataObject();
                    dragData.Set("ImageItem", imageItem); // We store the ImageItem object

                    // Initiate the Drag and Drop operation
                    // This awaits until the drop is complete
                    await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy);
                }
            }
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            // 1. Validate the data and the target
            if (e.Data.Contains("ImageItem") && sender is Border targetBorder)
            {
                // 2. Get the data
                var imageItem = e.Data.Get("ImageItem") as ImageItem;

                // 3. Get the category from the Tag we set in XAML (e.g., "Barong", "8x16")
                var category = targetBorder.Tag?.ToString();

                if (imageItem != null && category != null)
                {
                    // For now, we just print to the debug console.
                    // In the next step, we will add logic to save this to the ViewModel.
                    System.Diagnostics.Debug.WriteLine($"SUCCESS: Dropped {imageItem.FileName} into {category}");
                }
            }
        }
    }
}