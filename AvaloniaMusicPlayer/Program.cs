using Avalonia;
using System;
using System.Runtime.InteropServices;
#if DEBUG
using AvaloniaMusicPlayer;
#endif

namespace AvaloniaMusicPlayer;

sealed class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool FreeConsole();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
#if DEBUG
        // 在调试模式下分配控制台窗口
        AllocConsole();
        Console.WriteLine("🎵 音乐播放器调试控制台");
        Console.WriteLine("=" + new string('=', 40));
#endif
        
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        
#if DEBUG
        // 程序结束时释放控制台
        FreeConsole();
#endif
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
