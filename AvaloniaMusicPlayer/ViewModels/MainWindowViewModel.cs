using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AvaloniaMusicPlayer.Models;
using AvaloniaMusicPlayer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;

namespace AvaloniaMusicPlayer.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly IAudioPlayerService _audioPlayerService;
        private readonly ILyricService _lyricService;
        private readonly IPlaylistCacheService _cacheService;
        private bool _isUpdatingSelection = false;
        private System.Timers.Timer? _uiUpdateTimer;

        [ObservableProperty]
        private Song? _currentSong;

        [ObservableProperty]
        private TimeSpan _currentPosition;

        [ObservableProperty]
        private TimeSpan _duration;

        // 移除本地的IsPlaying字段，直接使用AudioPlayerService的状态
        public bool IsPlaying => _audioPlayerService.IsPlaying;

        [ObservableProperty]
        private double _volume = 1.0;

        [ObservableProperty]
        private double _sliderValue;

        [ObservableProperty]
        private bool _isSliderDragging;

        [ObservableProperty]
        private bool _isSliderPressed;

        [ObservableProperty]
        private LyricLine? _currentLyric;

        [ObservableProperty]
        private Song? _selectedSong;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public ObservableCollection<Song> Playlist { get; } = new();
        public ObservableCollection<Song> FilteredPlaylist { get; } = new();
        
        // 搜索结果显示
        public string SearchResultText => string.IsNullOrWhiteSpace(SearchText) 
            ? $"共 {Playlist.Count} 首歌曲" 
            : $"找到 {FilteredPlaylist.Count} 首歌曲 (共 {Playlist.Count} 首)";

        // 播放控制相关属性
        public string PlayPauseIcon => IsPlaying ? "M6 19h4V5H6v14zm8-14v14h4V5h-4z" : "M8 5v14l11-7z";
        public string PlayPauseTooltip => IsPlaying ? "暂停" : "播放";
        public bool HasPlaylist => Playlist.Count > 0;
        public bool HasCurrentSong => CurrentSong != null;

        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand RemoveFromPlaylistCommand { get; }
        public ICommand ClearPlaylistCommand { get; }
        public ICommand ClearSearchCommand { get; }


        public MainWindowViewModel(IAudioPlayerService audioPlayerService, ILyricService lyricService, IPlaylistCacheService cacheService)
        {
            _audioPlayerService = audioPlayerService;
            _lyricService = lyricService;
            _cacheService = cacheService;
            
            // 初始化命令
            PlayCommand = new AsyncRelayCommand(PlayAsync, CanPlay);
            PauseCommand = new AsyncRelayCommand(PauseAsync, CanPause);
            StopCommand = new AsyncRelayCommand(StopAsync);
            NextCommand = new AsyncRelayCommand(NextAsync, CanNavigate);
            PreviousCommand = new AsyncRelayCommand(PreviousAsync, CanNavigate);
            PlayPauseCommand = new AsyncRelayCommand(PlayPauseAsync, CanPlayPause);
            OpenFileCommand = new AsyncRelayCommand(OpenFileAsync);
            OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync);
            RemoveFromPlaylistCommand = new RelayCommand<object?>(RemoveFromPlaylist);
            ClearPlaylistCommand = new RelayCommand(ClearPlaylist);
            ClearSearchCommand = new RelayCommand(ClearSearch);


            // 订阅音频播放器事件
            _audioPlayerService.PositionChanged += OnPositionChanged;
            _audioPlayerService.DurationChanged += OnDurationChanged;
            _audioPlayerService.IsPlayingChanged += OnIsPlayingChanged;
            _audioPlayerService.CurrentSongChanged += OnCurrentSongChanged;
            
            // 订阅歌词服务事件
            _lyricService.CurrentLyricChanged += OnCurrentLyricChanged;

            // 同步播放列表
            SyncPlaylist();
            
            // 启动UI状态更新定时器
            StartUIUpdateTimer();
            
            // 加载缓存的播放列表
            _ = LoadCachedPlaylistAsync();
        }
        
        private void StartUIUpdateTimer()
        {
            _uiUpdateTimer = new System.Timers.Timer(200); // 每200ms更新一次，降低频率
            _uiUpdateTimer.Elapsed += (sender, e) =>
            {
                // 在UI线程中更新状态
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // 只在播放状态可能发生变化时更新UI
                    // 这样可以确保UI始终反映真实的播放状态
                    OnPropertyChanged(nameof(IsPlaying));
                    OnPropertyChanged(nameof(PlayPauseIcon));
                    OnPropertyChanged(nameof(PlayPauseTooltip));
                    
                    // 同时更新命令的可执行状态
                    (PlayCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                    (PauseCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                    (PlayPauseCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                });
            };
            _uiUpdateTimer.Start();
            Console.WriteLine("UI状态更新定时器已启动，每200ms同步一次播放状态");
        }
        
        // 添加Dispose方法来清理资源
        public void Dispose()
        {
            _uiUpdateTimer?.Stop();
            _uiUpdateTimer?.Dispose();
            
            // 在应用程序关闭时保存播放列表缓存
            if (_audioPlayerService.Playlist.Count > 0)
            {
                try
                {
                    var playlist = _audioPlayerService.Playlist.ToList();
                    _cacheService.SavePlaylistAsync(playlist).Wait();
                    Console.WriteLine("应用程序关闭时已保存播放列表缓存");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"关闭时保存缓存失败: {ex.Message}");
                }
            }
        }

        // 搜索文本变化时的处理
        partial void OnSearchTextChanged(string value)
        {
            FilterPlaylist();
        }

        private void FilterPlaylist()
        {
            FilteredPlaylist.Clear();
            
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // 如果搜索文本为空，显示所有歌曲
                foreach (var song in Playlist)
                {
                    FilteredPlaylist.Add(song);
                }
            }
            else
            {
                // 根据搜索文本过滤歌曲
                var searchLower = SearchText.ToLower();
                foreach (var song in Playlist)
                {
                    if (song.Title.ToLower().Contains(searchLower) ||
                        song.Artist.ToLower().Contains(searchLower) ||
                        song.Album.ToLower().Contains(searchLower))
                    {
                        FilteredPlaylist.Add(song);
                    }
                }
            }
            
            Console.WriteLine($"搜索 '{SearchText}': 找到 {FilteredPlaylist.Count} 首歌曲");
            
            // 更新搜索结果显示
            OnPropertyChanged(nameof(SearchResultText));
        }

        private async Task PlayAsync()
        {
            // 如果没有当前歌曲但有播放列表，选择第一首
            if (CurrentSong == null && Playlist.Count > 0)
            {
                await _audioPlayerService.LoadSongAsync(Playlist[0]);
            }
            await _audioPlayerService.PlayAsync();
        }

        private bool CanPlay() => !IsPlaying && Playlist.Count > 0;

        private async Task PauseAsync()
        {
            await _audioPlayerService.PauseAsync();
        }

        private bool CanPause() => IsPlaying;

        private async Task StopAsync()
        {
            await _audioPlayerService.StopAsync();
        }

        private async Task NextAsync()
        {
            await _audioPlayerService.NextAsync();
        }

        private async Task PreviousAsync()
        {
            await _audioPlayerService.PreviousAsync();
        }

        private bool CanNavigate() => Playlist.Count > 1;

        private async Task PlayPauseAsync()
        {
            Console.WriteLine($"PlayPauseAsync 被调用: IsPlaying={IsPlaying}, Playlist.Count={Playlist.Count}, CurrentSong={CurrentSong?.Title ?? "null"}");
            
            if (IsPlaying)
            {
                await PauseAsync();
            }
            else
            {
                await PlayAsync();
            }
        }

        private bool CanPlayPause() => Playlist.Count > 0;

        private async Task OpenFileAsync()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(GetMainWindow());
                if (topLevel?.StorageProvider != null)
                {
                    var options = new FilePickerOpenOptions
                    {
                        Title = "选择音频文件",
                        AllowMultiple = true,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("音频文件")
                            {
                                Patterns = new[] { "*.mp3", "*.wav", "*.flac", "*.m4a", "*.aac", "*.ogg" }
                            }
                        }
                    };

                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                    
                    var addedSongs = new List<Song>();
                    foreach (var file in files)
                    {
                        if (file.Path.LocalPath != null)
                        {
                            var song = await LoadSongFromFileAsync(file.Path.LocalPath);
                            if (song != null)
                            {
                                // 检查是否已存在相同文件路径的歌曲
                                if (!_audioPlayerService.Playlist.Any(s => s.FilePath == song.FilePath))
                                {
                                    _audioPlayerService.AddToPlaylist(song);
                                    addedSongs.Add(song);
                                    Console.WriteLine($"添加歌曲: {song.Title}");
                                }
                                else
                                {
                                    Console.WriteLine($"跳过重复歌曲: {song.Title}");
                                }
                            }
                        }
                    }
                    SyncPlaylist();
                    
                    // 如果添加了歌曲且当前没有播放歌曲，自动选中第一首
                    if (addedSongs.Count > 0 && CurrentSong == null)
                    {
                        var firstSong = _audioPlayerService.Playlist.FirstOrDefault();
                        if (firstSong != null)
                        {
                            Console.WriteLine($"自动选中第一首歌曲: {firstSong.Title}");
                            _ = _audioPlayerService.LoadSongAsync(firstSong);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开文件失败: {ex.Message}");
            }
        }

        private async Task OpenFolderAsync()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(GetMainWindow());
                if (topLevel?.StorageProvider != null)
                {
                    var options = new FolderPickerOpenOptions
                    {
                        Title = "选择音乐文件夹"
                    };

                    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                    var addedSongs = new List<Song>();
                    
                    foreach (var folder in folders)
                    {
                        if (folder.Path.LocalPath != null)
                        {
                            var files = Directory.GetFiles(folder.Path.LocalPath, "*.*", SearchOption.AllDirectories)
                                .Where(file => IsAudioFile(file))
                                .ToArray();

                            Console.WriteLine($"找到 {files.Length} 个音频文件，开始处理...");

                            // 使用Task.Run将文件处理移到后台线程，避免阻塞UI
                            await Task.Run(async () =>
                            {
                                var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount); // 限制并发数
                                var tasks = files.Select(async file =>
                                {
                                    await semaphore.WaitAsync();
                                    try
                                    {
                                        var song = await LoadSongFromFileAsync(file);
                                        if (song != null)
                                        {
                                            // 在UI线程中更新播放列表
                                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                            {
                                                // 检查是否已存在相同文件路径的歌曲
                                                if (!_audioPlayerService.Playlist.Any(s => s.FilePath == song.FilePath))
                                                {
                                                    _audioPlayerService.AddToPlaylist(song);
                                                    addedSongs.Add(song);
                                                    Console.WriteLine($"添加歌曲: {song.Title}");
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"跳过重复歌曲: {song.Title}");
                                                }
                                            });
                                        }
                                    }
                                    finally
                                    {
                                        semaphore.Release();
                                    }
                                }).ToArray();

                                await Task.WhenAll(tasks);
                            });
                        }
                    }
                    
                    // 在UI线程中同步播放列表
                    SyncPlaylist();
                    
                    // 保存到缓存（立即保存，不等到关闭时）
                    if (addedSongs.Count > 0)
                    {
                        Console.WriteLine($"添加了 {addedSongs.Count} 首歌曲，立即保存到缓存");
                        try
                        {
                            var playlist = _audioPlayerService.Playlist.ToList();
                            await _cacheService.SavePlaylistAsync(playlist);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"保存播放列表缓存失败: {ex.Message}");
                        }
                    }
                    
                    // 如果添加了歌曲且当前没有播放歌曲，自动选中第一首
                    if (addedSongs.Count > 0 && CurrentSong == null)
                    {
                        var firstSong = _audioPlayerService.Playlist.FirstOrDefault();
                        if (firstSong != null)
                        {
                            Console.WriteLine($"自动选中第一首歌曲: {firstSong.Title}");
                            _ = _audioPlayerService.LoadSongAsync(firstSong);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开文件夹失败: {ex.Message}");
            }
        }

        private void RemoveFromPlaylist(object? parameter)
        {
            if (parameter is Song song)
            {
                var index = _audioPlayerService.Playlist.IndexOf(song);
                if (index >= 0)
                {
                    _audioPlayerService.RemoveFromPlaylist(index);
                    SyncPlaylist();
                }
            }
        }

        private void ClearPlaylist()
        {
            _audioPlayerService.ClearPlaylist();
            SyncPlaylist();
            
            // 确保当前歌曲也被清除
            CurrentSong = null;
            CurrentLyric = null;
            
            // 清除缓存
            _ = _cacheService.ClearCacheAsync();
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        private async Task LoadCachedPlaylistAsync()
        {
            try
            {
                if (!_cacheService.HasCache())
                {
                    Console.WriteLine("没有找到播放列表缓存文件");
                    return;
                }

                Console.WriteLine("正在加载缓存的播放列表...");
                var cachedSongs = await _cacheService.LoadPlaylistAsync();
                
                if (cachedSongs.Count > 0)
                {
                    // 在UI线程中添加歌曲到播放列表
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var song in cachedSongs)
                        {
                            _audioPlayerService.AddToPlaylist(song);
                        }
                        SyncPlaylist();
                        Console.WriteLine($"从缓存加载了 {cachedSongs.Count} 首歌曲");
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载缓存播放列表失败: {ex.Message}");
            }
        }





        private void OnPositionChanged(object? sender, TimeSpan position)
        {
            // 确保在UI线程中更新
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsSliderDragging && !IsSliderPressed)
                {
                    CurrentPosition = position;
                    SliderValue = position.TotalSeconds;
                }
                
                // 更新歌词
                _lyricService.UpdateCurrentLyric(position);
            });
        }

        private void OnDurationChanged(object? sender, TimeSpan duration)
        {
            // 确保在UI线程中更新
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Duration = duration;
            });
        }

        private void OnIsPlayingChanged(object? sender, bool isPlaying)
        {
            // 确保在UI线程中更新
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Console.WriteLine($"OnIsPlayingChanged: 播放状态变为 {isPlaying}");
                
                // 立即更新一次UI状态，不等定时器
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(PlayPauseIcon));
                OnPropertyChanged(nameof(PlayPauseTooltip));
                
                // 刷新命令的可执行状态
                (PlayCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                (PauseCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                (PlayPauseCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                (NextCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                (PreviousCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
                
                Console.WriteLine($"UI状态已立即更新: IsPlaying={IsPlaying}");
            });
        }

        private void OnCurrentSongChanged(object? sender, Song? song)
        {
            // 确保在UI线程中更新
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Console.WriteLine($"OnCurrentSongChanged: {CurrentSong?.Title ?? "null"} -> {song?.Title ?? "null"}");
                CurrentSong = song;
                
                // 同步选中的歌曲，但避免触发切换逻辑
                if (song != null && SelectedSong != song)
                {
                    // 临时标记，避免在同步选择时触发切换逻辑
                    _isUpdatingSelection = true;
                    SelectedSong = song;
                    _isUpdatingSelection = false;
                    Console.WriteLine($"同步选中歌曲: {song.Title}");
                }
                
                // 重置进度条
                if (song != null)
                {
                    SliderValue = 0;
                    CurrentPosition = TimeSpan.Zero;
                    _ = LoadLyricsForSongAsync(song);
                }
                else
                {
                    SliderValue = 0;
                    CurrentPosition = TimeSpan.Zero;
                    Duration = TimeSpan.Zero;
                    _lyricService.ClearLyrics();
                    CurrentLyric = null;
                }
                
                // 通知UI更新相关属性
                OnPropertyChanged(nameof(HasCurrentSong));
                (PlayPauseCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            });
        }

        private void OnCurrentLyricChanged(object? sender, LyricLine? lyric)
        {
            // 确保在UI线程中更新
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentLyric = lyric;
            });
        }

        private Task LoadLyricsForSongAsync(Song song)
        {
            try
            {
                return _lyricService.LoadLyricsAsync(song.FilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载歌词失败: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        private void SyncPlaylist()
        {
            Playlist.Clear();
            foreach (var song in _audioPlayerService.Playlist)
            {
                Playlist.Add(song);
            }
            
            // 更新过滤后的播放列表
            FilterPlaylist();
            
            // 通知UI更新相关属性
            OnPropertyChanged(nameof(HasPlaylist));
            OnPropertyChanged(nameof(SearchResultText));
            (NextCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (PreviousCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (PlayPauseCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }

        private async Task<Song?> LoadSongFromFileAsync(string filePath)
        {
            try
            {
                // 在后台线程中执行文件I/O操作
                return await Task.Run(() =>
                {
                    try
                    {
                        var song = new Song { FilePath = filePath };
                        
                        // 使用TagLib读取音频文件信息
                        using var file = TagLib.File.Create(filePath);
                        
                        // 优先使用文件名作为标题，然后尝试修复元数据
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        song.Title = EncodingHelper.FixMusicTagEncoding(fileName) ?? "未知标题";
                        
                        // 修复元数据中的艺术家和专辑信息
                        song.Artist = EncodingHelper.FixMusicTagEncoding(file.Tag.FirstPerformer) ?? "未知艺术家";
                        song.Album = EncodingHelper.FixMusicTagEncoding(file.Tag.Album) ?? "未知专辑";
                        song.Duration = file.Properties.Duration;

                        // 记录调试信息（只在Debug模式下输出详细信息）
#if DEBUG
                        Console.WriteLine($"文件: {filePath}");
                        Console.WriteLine($"文件名: {fileName}");
                        Console.WriteLine($"元数据标题: {file.Tag.Title ?? "null"}");
                        Console.WriteLine($"显示标题: {song.Title}");
                        Console.WriteLine($"艺术家: {song.Artist}");
                        Console.WriteLine($"专辑: {song.Album}");
                        
                        // 输出字节信息用于调试
                        if (!string.IsNullOrEmpty(file.Tag.Title))
                        {
                            var titleBytes = System.Text.Encoding.Default.GetBytes(file.Tag.Title);
                            Console.WriteLine($"元数据标题字节: {BitConverter.ToString(titleBytes)}");
                        }
#endif

                        return song;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"读取音频文件失败: {ex.Message}");
                        return null;
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理音频文件异常: {ex.Message}");
                return null;
            }
        }



        private bool IsAudioFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".mp3" or ".wav" or ".flac" or ".m4a" or ".aac" or ".ogg" => true,
                _ => false
            };
        }

        private Window? GetMainWindow()
        {
            // 获取主窗口的简单实现
            return App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
        }

        partial void OnVolumeChanged(double value)
        {
            _ = _audioPlayerService.SetVolumeAsync(value);
        }



        partial void OnSliderValueChanged(double value)
        {
            // 当滑块值改变时，更新当前位置显示
            CurrentPosition = TimeSpan.FromSeconds(value);
            
            // 如果正在拖动，立即更新播放位置
            if (IsSliderDragging)
            {
                var position = TimeSpan.FromSeconds(value);
                _ = _audioPlayerService.SetPositionAsync(position);
            }
        }

        // 手动设置滑块拖动状态的方法
        public void SetSliderDragging(bool isDragging)
        {
            Console.WriteLine($"设置拖拽状态: {isDragging}, 滑块值: {SliderValue}");
            IsSliderDragging = isDragging;
            IsSliderPressed = isDragging;
            
            if (!isDragging)
            {
                // 拖拽结束时，确保位置正确设置
                var position = TimeSpan.FromSeconds(SliderValue);
                Console.WriteLine($"拖拽结束，设置位置: {position:mm\\:ss}");
                _ = _audioPlayerService.SetPositionAsync(position);
            }
        }

        // 处理选中歌曲变化
        partial void OnSelectedSongChanged(Song? value)
        {
            // 如果是程序内部同步选择，不处理
            if (_isUpdatingSelection)
            {
                Console.WriteLine("内部同步选择，跳过处理");
                return;
            }
            
            if (value != null)
            {
                Console.WriteLine($"用户选中歌曲: {value.Title}, 当前歌曲: {CurrentSong?.Title ?? "无"}");
                
                // 如果点击的是当前正在播放的歌曲，从头播放
                if (value == CurrentSong)
                {
                    Console.WriteLine("点击相同歌曲，从头播放");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _audioPlayerService.SetPositionAsync(TimeSpan.Zero);
                            if (!_audioPlayerService.IsPlaying)
                            {
                                await _audioPlayerService.PlayAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"重新播放歌曲失败: {ex.Message}");
                        }
                    });
                }
                else
                {
                    // 切换到选中的歌曲
                    Console.WriteLine($"切换到新歌曲: {value.Title}");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // 先停止当前播放
                            if (_audioPlayerService.IsPlaying)
                            {
                                await _audioPlayerService.PauseAsync();
                                Console.WriteLine("已暂停当前播放");
                            }
                            
                            // 切换歌曲
                            await _audioPlayerService.PlaySongAsync(value);
                            Console.WriteLine("歌曲切换完成");
                            
                            // 稍微延迟以确保状态同步
                            await Task.Delay(50);
                            
                            // 开始播放新歌曲
                            await _audioPlayerService.PlayAsync();
                            
                            Console.WriteLine($"成功切换并播放: {value.Title}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"播放选中歌曲失败: {ex.Message}");
                            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
                        }
                    });
                }
            }
        }
    }
}
