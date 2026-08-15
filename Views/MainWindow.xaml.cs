using System.ComponentModel;
using System.Windows;
using PDF_Easy_Loader.ViewModels;

namespace PDF_Easy_Loader.Views;

public partial class MainWindow : Window
{
    /// <summary>結果が無い間の高さ。ヘッダーとステータスだけが見えれば足りる</summary>
    private const double CompactHeight = 145;

    /// <summary>結果を表示するときの高さ</summary>
    private const double ExpandedHeight = 620;

    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Height = viewModel.HasResults ? ExpandedHeight : CompactHeight;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.HasResults)) return;

        ApplyHeight(_viewModel.HasResults ? ExpandedHeight : CompactHeight);
    }

    /// <summary>
    /// 結果の有無に合わせて高さを伸縮する。
    /// 画面外へはみ出しにくいよう、ウィンドウの中心は動かさない。
    /// </summary>
    private void ApplyHeight(double height)
    {
        // 最大化中はユーザーの状態を尊重する
        if (WindowState != WindowState.Normal) return;

        if (Math.Abs(Height - height) < 0.5) return;

        Top -= (height - Height) / 2;
        Height = height;
    }
}
