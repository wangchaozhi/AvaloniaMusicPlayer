using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaMusicPlayer.Models;
using AvaloniaMusicPlayer.ViewModels;

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

    // 删除拖拽相关事件处理，不再需要

    // 删除拖拽相关事件处理，不再需要

    // 删除拖拽相关事件处理，不再需要

    private void Slider_ValueChanged(
        object? sender,
        Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e
    )
    {
        Console.WriteLine($"Slider_ValueChanged: {e.NewValue}");

        if (DataContext is MainWindowViewModel viewModel)
        {
            // 手动更新ViewModel中的SliderValue
            viewModel.SliderValue = e.NewValue;
        }
    }
}
