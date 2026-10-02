using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Threading;
using dbm_select.Models;
using dbm_select.ViewModels;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace dbm_select.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Bring window to front when opened
            Opened += (s, e) =>
            {
                Activate();
                Topmost = true;
                Topmost = false;
            };
        }

        // Handle Arrow Key Navigation for Grid Layout (Windows Explorer Style)
        // MainWindow.axaml.cs

private void PhotosListBox_KeyDown(object? sender, KeyEventArgs e)
{
    // 1. Safety Checks
    if (sender is not ListBox listBox || listBox.ItemCount == 0) return;
    
    // Only handle arrow keys
    if (e.Key != Key.Up && e.Key != Key.Down && e.Key != Key.Left && e.Key != Key.Right) return;

    int currentIndex = listBox.SelectedIndex;
    if (currentIndex < 0) currentIndex = 0;

    // 2. Dynamic Column Detection
    int columns = 1;
    var container0 = listBox.ContainerFromIndex(0) as Control;
    
    if (container0 != null)
    {
        double firstRowTop = container0.Bounds.Top;
        // Scan up to 20 items to find the row break
        for (int i = 1; i < Math.Min(listBox.ItemCount, 20); i++)
        {
            var nextContainer = listBox.ContainerFromIndex(i) as Control;
            if (nextContainer != null && nextContainer.Bounds.Top > firstRowTop + 5)
            {
                columns = i;
                break;
            }
        }
    }
    else
    {
        // Fallback calculation
        double availableWidth = listBox.Bounds.Width - 20; 
        double itemWidth = 120; 
        columns = (int)(availableWidth / itemWidth);
        if (columns < 1) columns = 1;
    }

    // 3. Calculate New Index
    int newIndex = currentIndex;
    bool handled = false;

    switch (e.Key)
    {
        case Key.Left:
            if (currentIndex > 0) { newIndex = currentIndex - 1; handled = true; }
            break;
        case Key.Right:
            if (currentIndex < listBox.ItemCount - 1) { newIndex = currentIndex + 1; handled = true; }
            break;
        case Key.Up:
            if (currentIndex - columns >= 0) { newIndex = currentIndex - columns; handled = true; }
            break;
        case Key.Down:
            if (currentIndex + columns < listBox.ItemCount) { newIndex = currentIndex + columns; handled = true; }
            break;
    }

    // 4. Apply Changes
    if (handled)
    {
        e.Handled = true; // Stop default behavior

        if (newIndex != currentIndex)
        {
            listBox.SelectedIndex = newIndex;

            // FIX: Null check to satisfy the compiler
            var item = listBox.Items[newIndex];
            if (item != null)
            {
                listBox.ScrollIntoView(item);
            }

            // FIX: Force focus update on the UI Thread
            Dispatcher.UIThread.Post(() =>
            {
                var container = listBox.ContainerFromIndex(newIndex) as Control;
                container?.Focus();
            }, DispatcherPriority.Input);
        }
    }
}

        private void ClientCourse_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is not AutoCompleteBox courseBox || DataContext is not MainWindowViewModel viewModel)
                return;

            var input = courseBox.Text?.Trim() ?? string.Empty;
            var normalized = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower(CultureInfo.CurrentCulture));
            var titleWords = Regex.Matches(normalized, @"[\p{L}\p{M}]+");
            var wordIndex = 0;
            normalized = Regex.Replace(normalized, @"[\p{L}\p{M}]+", match =>
            {
                var precedingText = normalized[..match.Index].TrimEnd();
                var isTitleBoundary = wordIndex == 0 || wordIndex == titleWords.Count - 1 || precedingText.EndsWith(':');
                var isMinorWord = match.Value is "A" or "An" or "The" or "And" or "But" or "For" or "Or" or "Nor"
                    or "As" or "At" or "By" or "In" or "Of" or "On" or "Per" or "To" or "Via" or "Vs";
                wordIndex++;
                return isMinorWord && !isTitleBoundary
                    ? match.Value.ToLower(CultureInfo.CurrentCulture)
                    : match.Value;
            });
            normalized = Regex.Replace(normalized, @"\bBS\b", "Bachelor of Science", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"\bBA\b", "Bachelor of Arts", RegexOptions.IgnoreCase);

            viewModel.ClientCourse = normalized;
            courseBox.Text = normalized;
        }

        private void ClientInput_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || sender is not Control control)
                return;

            e.Handled = true;
            TopLevel.GetTopLevel(control)?.FocusManager?.ClearFocus();
        }

        private void ClientEmail_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is not TextBox emailBox || DataContext is not MainWindowViewModel viewModel)
                return;

            var email = emailBox.Text?.Trim() ?? string.Empty;
            if (email.Length > 0)
            {
                email = email.Contains('@')
                    ? Regex.Replace(email, @"@gmai(?:l)?(?:\.(?:c|co|com|con|cmo|comm))?$", "@gmail.com", RegexOptions.IgnoreCase)
                    : $"{email}@gmail.com";
            }

            emailBox.Text = email;
            viewModel.ClientEmail = email;
        }

        private void ContactNumber_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox phoneBox)
                return;

            var normalized = NormalizeContactNumber(phoneBox.Text, allowPartialCountryPrefix: true);
            if (normalized == phoneBox.Text)
                return;

            phoneBox.Text = normalized;
            phoneBox.CaretIndex = normalized.Length;
            if (DataContext is MainWindowViewModel viewModel)
                viewModel.ClientContactNumber = normalized;
        }

        private void ContactNumber_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is not TextBox phoneBox || DataContext is not MainWindowViewModel viewModel)
                return;

            var normalized = NormalizeContactNumber(phoneBox.Text, allowPartialCountryPrefix: false);
            phoneBox.Text = normalized;
            viewModel.ClientContactNumber = normalized;
        }

        private static string NormalizeContactNumber(string? input, bool allowPartialCountryPrefix)
        {
            var value = input?.Trim() ?? string.Empty;
            if (value.StartsWith('+'))
            {
                var internationalDigits = Regex.Replace(value[1..], @"\D", string.Empty);
                if (allowPartialCountryPrefix && "63".StartsWith(internationalDigits, StringComparison.Ordinal) && internationalDigits.Length < 2)
                    return $"+{internationalDigits}";

                if (internationalDigits.StartsWith("63", StringComparison.Ordinal))
                    return $"+63{internationalDigits[2..Math.Min(internationalDigits.Length, 12)]}";

                if (allowPartialCountryPrefix && internationalDigits.Length == 0)
                    return "+";

                return internationalDigits.Length == 0
                    ? string.Empty
                    : $"0{internationalDigits[..Math.Min(internationalDigits.Length, 10)]}";
            }

            var digits = Regex.Replace(value, @"\D", string.Empty);
            if (digits.StartsWith('0'))
                return $"0{digits[1..Math.Min(digits.Length, 11)]}";

            if (digits.StartsWith("63", StringComparison.Ordinal))
                return $"+63{digits[2..Math.Min(digits.Length, 12)]}";

            return digits.Length == 0
                ? string.Empty
                : $"0{digits[..Math.Min(digits.Length, 10)]}";
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

                    // FIX: Use FindControl to safely access the ListBox
                    var photosBox = this.FindControl<ListBox>("PhotosListBox");
                    photosBox?.Focus();
                }
            }
        }

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
                    // Safely check for Tag string
                    if (targetBorder.Tag is string category && DataContext is MainWindowViewModel vm)
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