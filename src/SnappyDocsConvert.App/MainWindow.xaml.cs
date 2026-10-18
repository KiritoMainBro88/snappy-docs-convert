using System.Windows;
using System.Windows.Input;
using SnappyDocsConvert.App.ViewModels;

namespace SnappyDocsConvert.App;

public partial class MainWindow : Window
{
    // Set in PreviewMouseLeftButtonDown on the zone and consumed by the matching up event:
    // a click on the zone requires the zone to have SEEN the button-down. The mouse-up that
    // releases an OLE drag is routed here without a matching down (the down belonged to the
    // drag source), which is what used to open the file picker ghost-click style. A real
    // click after a drop still works because its down arrives normally.
    private bool _zoneMouseDownSeen;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;

        UpdateDropZoneHighlight(e);
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        //(drags bubble Leave from children; re-evaluate bounds so in-zone feedback never flickers)
        UpdateDropZoneHighlight(e);
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        // Single drop router: marking the event handled means the DropZoneBorder/other
        // ancestors can no longer run a second Drop pass for the same payload.
        e.Handled = true;
        SetDropZoneHighlight(false);

        if (DataContext is not MainWindowViewModel viewModel ||
            !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            if (viewModel.SelectedPage == AppPage.PdfTools)
            {
                viewModel.AddPdfToolPaths(paths);
            }
            else
            {
                viewModel.AddPaths(paths);
            }
        }
    }

    private void DropZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only events whose down really happened on this window count as click candidates.
        _zoneMouseDownSeen = true;
    }

    private void DropZone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // The zone itself opens the file picker; clicks that land on real buttons inside
        // (Add files / Add folder) are handled by those buttons and must not double-open.
        if (e.OriginalSource is DependencyObject source && FindAncestorButton(source) is not null)
        {
            return;
        }

        // Event-lifecycle ghost-click guard: the zone must have seen the matching
        // button-down for this to be a click. The mouse-up that releases a drag is routed
        // to the zone right after the Drop event without its own down, so this filters it
        // regardless of how long the drag took.
        if (!_zoneMouseDownSeen)
        {
            return;
        }
        _zoneMouseDownSeen = false;

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.AddFilesCommand.Execute(null);
        }
    }

    private static System.Windows.Controls.Button? FindAncestorButton(DependencyObject start)
    {
        var current = start;
        while (current is not null)
        {
            if (current is System.Windows.Controls.Button button)
            {
                return button;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void UpdateDropZoneHighlight(DragEventArgs e)
    {
        if (DropZoneBorder is null)
        {
            return;
        }

        var position = e.GetPosition(DropZoneBorder);
        var inside = position.X >= 0 && position.Y >= 0 &&
                     position.X <= DropZoneBorder.ActualWidth &&
                     position.Y <= DropZoneBorder.ActualHeight;
        SetDropZoneHighlight(inside);
    }

    private void SetDropZoneHighlight(bool active)
    {
        if (DropZoneBorder is null)
        {
            return;
        }

        // Pure view-side feedback tokenized through theme resources: accent fill/border while a
        // drag hovers the zone, standard tokens when not.
        var background = FindBrush(active ? "AccentBrush" : "AccentSoftBrush");
        var border = FindBrush(active ? "FocusRingBrush" : "AccentBrush");
        if (background is not null)
        {
            DropZoneBorder.Background = background;
        }

        if (border is not null)
        {
            DropZoneBorder.BorderBrush = border;
        }

        DropZoneBorder.BorderThickness = new Thickness(active ? 2 : 1.5);
    }

    private static System.Windows.Media.Brush? FindBrush(string key)
        => Application.Current?.TryFindResource(key) as System.Windows.Media.Brush;
}
