using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaMusicPlayer.ViewModels;
using AvaloniaMusicPlayer.Models;

namespace AvaloniaMusicPlayer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        // 在窗口关闭时清理资源
        if (DataContext is MainWindowViewModel viewModel && viewModel is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnClosed(e);
    }

    private void Slider_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetSliderDragging(true);
        }
    }

    private void Slider_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetSliderDragging(false);
        }
    }


}