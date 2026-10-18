using System.Windows;
using System.Windows.Input;
using SnappyDocsConvert.App.ViewModels;
using SnappyDocsConvert.App.Views;

namespace SnappyDocsConvert.App;

public partial class MainWindow : Window
{
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

        // The drop zone lives in ConvertPageView since the S1 page split; forward the drag
        // position so only in-zone hovers light up.
        ConvertPage?.UpdateDragHighlight(e);
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        //(drags bubble Leave from children; re-evaluate bounds so in-zone feedback never flickers)
        ConvertPage?.UpdateDragHighlight(e);
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        // Single drop router: marking the event handled means the DropZoneBorder/other
        // ancestors can no longer run a second Drop pass for the same payload.
        e.Handled = true;
        ConvertPage?.SetHighlight(false);

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
                viewModel.Convert.AddPaths(paths);
            }
        }
    }
}
