using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using AvaloniaMusicPlayer.ViewModels;
using AvaloniaMusicPlayer.Views;
using AvaloniaMusicPlayer.Services;
using AvaloniaMusicPlayer.Models;

namespace AvaloniaMusicPlayer;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            // 创建服务实例
            var audioPlayerService = new AudioPlayerService();
            var lyricService = new LyricService();
            var cacheService = new PlaylistCacheService();
            
            var viewModel = new MainWindowViewModel(audioPlayerService, lyricService, cacheService);
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            
            // 监听应用程序关闭事件
            desktop.ShutdownRequested += (sender, e) =>
            {
                Console.WriteLine("应用程序正在关闭，保存缓存...");
                try
                {
                    if (audioPlayerService.Playlist.Count > 0)
                    {
                        var playlist = audioPlayerService.Playlist.ToList();
                        cacheService.SavePlaylistAsync(playlist).Wait();
                        Console.WriteLine("应用程序关闭时已保存播放列表缓存");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"关闭时保存缓存失败: {ex.Message}");
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}