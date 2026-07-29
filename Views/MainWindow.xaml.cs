using System.Windows;
using PDF_Easy_Loader.ViewModels;

namespace PDF_Easy_Loader.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
