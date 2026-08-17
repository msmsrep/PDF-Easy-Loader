using System.ComponentModel;
using System.Windows;
using PDF_Easy_Loader.ViewModels;

namespace PDF_Easy_Loader.Views;

public partial class MainWindow : Window
{
    /// <summary>結果が無い間の大きさ。ヘッダーの操作列が収まれば足りる</summary>
    private const double CompactWidth = 640;
    private const double CompactHeight = 130;

    /// <summary>結果を表示するときの大きさ</summary>
    private const double ExpandedWidth = 820;
    private const double ExpandedHeight = 620;

    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Width = viewModel.HasResults ? ExpandedWidth : CompactWidth;
        Height = viewModel.HasResults ? ExpandedHeight : CompactHeight;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.HasResults)) return;

        if (_viewModel.HasResults)
        {
            ApplySize(ExpandedWidth, ExpandedHeight);
        }
        else
        {
            ApplySize(CompactWidth, CompactHeight);
        }
    }

    /// <summary>
    /// 結果の有無に合わせて大きさを伸縮する。
    /// 画面外へはみ出しにくいよう、ウィンドウの中心は動かさない。
    /// </summary>
    private void ApplySize(double width, double height)
    {
        // 最大化中はユーザーの状態を尊重する
        if (WindowState != WindowState.Normal) return;

        if (Math.Abs(Width - width) >= 0.5)
        {
            Left -= (width - Width) / 2;
            Width = width;
        }

        if (Math.Abs(Height - height) >= 0.5)
        {
            Top -= (height - Height) / 2;
            Height = height;
        }
    }
}
