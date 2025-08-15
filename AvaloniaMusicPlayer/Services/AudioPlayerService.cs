using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AvaloniaMusicPlayer.Models;
using NAudio.Wave;
using NAudio.MediaFoundation;
using TagLib;

namespace AvaloniaMusicPlayer.Services
{
    public class AudioPlayerService : IAudioPlayerService, IDisposable
    {
        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioFile;
        private readonly List<Song> _playlist = new();
        private int _currentIndex = -1;
        private double _volume = 1.0;
        private bool _disposed = false;
        private bool _isManualStop = false;

        public event EventHandler<TimeSpan>? PositionChanged;
        public event EventHandler<TimeSpan>? DurationChanged;
        public event EventHandler<bool>? IsPlayingChanged;
        public event EventHandler<Song?>? CurrentSongChanged;

        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
        public TimeSpan CurrentPosition => _audioFile?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan Duration => _audioFile?.TotalTime ?? TimeSpan.Zero;
        public Song? CurrentSong => _currentIndex >= 0 && _currentIndex < _playlist.Count ? _playlist[_currentIndex] : null;
        public List<Song> Playlist => _playlist;
        public int CurrentIndex => _currentIndex;

        public AudioPlayerService()
        {
            // 尝试初始化MediaFoundation
            try
            {
                MediaFoundationApi.Startup();
                Console.WriteLine("MediaFoundation初始化成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MediaFoundation初始化失败: {ex.Message}");
            }
            
            _waveOut = new WaveOutEvent();
            _waveOut.PlaybackStopped += OnPlaybackStopped;
        }

        public Task LoadSongAsync(Song song)
        {
            try
            {
                Console.WriteLine($"LoadSongAsync: 开始加载歌曲 {song.Title}");
                
                // 设置手动停止标志，防止自动播放下一首
                _isManualStop = true;
                
                // 先停止当前播放
                if (_waveOut != null)
                {
                    try
                    {
                        if (_waveOut.PlaybackState != PlaybackState.Stopped)
                        {
                            _waveOut.Stop();
                        }
                    }
                    catch (Exception stopEx)
                    {
                        Console.WriteLine($"停止播放时发生异常: {stopEx.Message}");
                        // 如果停止失败，尝试重新创建WaveOut
                        try
                        {
                            _waveOut.Dispose();
                        }
                        catch { }
                        _waveOut = null;
                    }
                }
                
                // 释放旧的音频文件
                if (_audioFile != null)
                {
                    _audioFile.Dispose();
                    _audioFile = null;
                }

                if (System.IO.File.Exists(song.FilePath))
                {
                    _audioFile = new AudioFileReader(song.FilePath);
                    _audioFile.Volume = (float)_volume;
                    
                    // 确保音频文件从头开始
                    _audioFile.Position = 0;
                    
                    Console.WriteLine($"音频文件信息: 采样率={_audioFile.WaveFormat.SampleRate}Hz, 声道={_audioFile.WaveFormat.Channels}, 位数={_audioFile.WaveFormat.BitsPerSample}bit");
                    
                    // 安全地释放当前的WaveOut
                    SafeDisposeWaveOut();
                    
                    // 尝试多种方式创建和初始化WaveOut
                    bool initSuccess = false;
                    Exception lastException = null;
                    
                    // 方法1: 使用默认设备
                    try
                    {
                        _waveOut = CreateNewWaveOut();
                        if (_waveOut != null)
                        {
                            _waveOut.Init(_audioFile);
                            initSuccess = true;
                            Console.WriteLine("使用默认设备初始化成功");
                        }
                        else
                        {
                            throw new InvalidOperationException("无法创建WaveOutEvent");
                        }
                    }
                    catch (Exception ex1)
                    {
                        Console.WriteLine($"默认设备初始化失败: {ex1.Message}");
                        lastException = ex1;
                        SafeDisposeWaveOut();
                    }
                    
                    // 方法2: 如果默认设备失败，尝试指定设备ID
                    if (!initSuccess)
                    {
                        try
                        {
                            _waveOut = new WaveOutEvent() { DeviceNumber = 0 };
                            _waveOut.PlaybackStopped += OnPlaybackStopped;
                            _waveOut.Init(_audioFile);
                            initSuccess = true;
                            Console.WriteLine("使用设备0初始化成功");
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine($"设备0初始化失败: {ex2.Message}");
                            lastException = ex2;
                            SafeDisposeWaveOut();
                        }
                    }
                    
                    // 方法3: 如果还是失败，尝试创建重采样器
                    if (!initSuccess)
                    {
                        try
                        {
                            // 首先尝试初始化MediaFoundation
                            MediaFoundationApi.Startup();
                            
                            // 创建一个标准格式的重采样器
                            var resampler = new MediaFoundationResampler(_audioFile, new WaveFormat(44100, 16, 2));
                            resampler.ResamplerQuality = 60;
                            
                            _waveOut = CreateNewWaveOut();
                            if (_waveOut != null)
                            {
                                _waveOut.Init(resampler);
                                initSuccess = true;
                                Console.WriteLine("使用重采样器初始化成功");
                            }
                            else
                            {
                                throw new InvalidOperationException("无法创建WaveOutEvent用于重采样器");
                            }
                        }
                        catch (Exception ex3)
                        {
                            Console.WriteLine($"重采样器初始化失败: {ex3.Message}");
                            lastException = ex3;
                            SafeDisposeWaveOut();
                        }
                    }
                    
                    // 方法4: 如果重采样器也失败，尝试使用WaveChannel32
                    if (!initSuccess)
                    {
                        try
                        {
                            // 重新读取音频文件
                            _audioFile.Position = 0;
                            var waveChannel = new WaveChannel32(_audioFile);
                            
                            _waveOut = CreateNewWaveOut();
                            if (_waveOut != null)
                            {
                                _waveOut.Init(waveChannel);
                                initSuccess = true;
                                Console.WriteLine("使用WaveChannel32初始化成功");
                            }
                            else
                            {
                                throw new InvalidOperationException("无法创建WaveOutEvent用于WaveChannel32");
                            }
                        }
                        catch (Exception ex4)
                        {
                            Console.WriteLine($"WaveChannel32初始化失败: {ex4.Message}");
                            lastException = ex4;
                            SafeDisposeWaveOut();
                        }
                    }
                    
                    // 方法5: 最后的备用方案 - 使用DirectSound
                    if (!initSuccess)
                    {
                        try
                        {
                            _audioFile.Position = 0;
                            _waveOut = new WaveOutEvent() 
                            { 
                                DeviceNumber = -1, // 使用默认设备
                                DesiredLatency = 200 // 增加延迟以提高兼容性
                            };
                            _waveOut.PlaybackStopped += OnPlaybackStopped;
                            _waveOut.Init(_audioFile);
                            initSuccess = true;
                            Console.WriteLine("使用备用方案初始化成功");
                        }
                        catch (Exception ex5)
                        {
                            Console.WriteLine($"备用方案初始化失败: {ex5.Message}");
                            lastException = ex5;
                            SafeDisposeWaveOut();
                        }
                    }
                    
                    if (!initSuccess)
                    {
                        Console.WriteLine($"所有初始化方法都失败，将跳过此文件");
                        // 不抛出异常，而是创建一个空的WaveOut以避免后续空引用
                        _waveOut = CreateNewWaveOut();
                        Console.WriteLine($"最后一个异常: {lastException?.Message}");
                        
                        // 清理音频文件
                        _audioFile?.Dispose();
                        _audioFile = null;
                        return Task.CompletedTask;
                    }
                    
                    // 通知UI更新
                    DurationChanged?.Invoke(this, Duration);
                    CurrentSongChanged?.Invoke(this, song);
                    PositionChanged?.Invoke(this, TimeSpan.Zero);
                    IsPlayingChanged?.Invoke(this, false);
                    
                    Console.WriteLine($"LoadSongAsync: 成功加载 {song.Title}, 时长: {Duration}, 位置: {_audioFile.CurrentTime}");
                    
                    // 启动位置更新定时器
                    StartPositionTimer();
                }
                else
                {
                    Console.WriteLine($"LoadSongAsync: 文件不存在 {song.FilePath}");
                    // 确保清理状态
                    _audioFile = null;
                    SafeDisposeWaveOut();
                    _waveOut = CreateNewWaveOut();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载歌曲失败: {ex.Message}");
                Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
                
                // 确保在异常情况下清理状态
                try
                {
                    _audioFile?.Dispose();
                    _audioFile = null;
                    SafeDisposeWaveOut();
                    _waveOut = CreateNewWaveOut();
                }
                catch (Exception cleanupEx)
                {
                    Console.WriteLine($"清理状态时发生异常: {cleanupEx.Message}");
                }
            }
            return Task.CompletedTask;
        }

        public async Task PlayAsync()
        {
            try
            {
                // 确保有当前歌曲且已加载
                if (CurrentSong == null && _playlist.Count > 0)
                {
                    Console.WriteLine("没有当前歌曲，加载第一首");
                    await LoadSongAsync(_playlist[0]);
                }

                if (_waveOut != null && _audioFile != null)
                {
                    _isManualStop = false; // 开始播放时重置手动停止标志
                    Console.WriteLine($"▶️ [开始播放] {CurrentSong?.Title ?? "未知歌曲"}");
                    Console.WriteLine($"   音频状态: WaveOut={_waveOut.PlaybackState}, 文件长度={_audioFile.TotalTime}");
                    
                    try
                    {
                        _waveOut.Play();
                        IsPlayingChanged?.Invoke(this, true);
                    }
                    catch (Exception playEx)
                    {
                        Console.WriteLine($"调用WaveOut.Play()时发生异常: {playEx.Message}");
                        
                        // 尝试重新初始化WaveOut
                        try
                        {
                            Console.WriteLine("尝试重新初始化WaveOut...");
                            
                            // 安全地释放当前的WaveOut
                            SafeDisposeWaveOut();
                            
                            // 重新创建WaveOut
                            _waveOut = CreateNewWaveOut();
                            if (_waveOut != null && _audioFile != null)
                            {
                                _waveOut.Init(_audioFile);
                                _waveOut.Play();
                                IsPlayingChanged?.Invoke(this, true);
                                Console.WriteLine("重新初始化WaveOut后播放成功");
                            }
                            else
                            {
                                Console.WriteLine("重新创建WaveOut失败");
                                throw new InvalidOperationException("无法重新创建WaveOut");
                            }
                        }
                        catch (Exception retryEx)
                        {
                            Console.WriteLine($"重新初始化WaveOut失败: {retryEx.Message}");
                            throw;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"❌ [播放失败] WaveOut={_waveOut != null}, AudioFile={_audioFile != null}, CurrentSong={CurrentSong?.Title ?? "null"}");
                    
                    // 尝试重新初始化
                    if (CurrentSong != null)
                    {
                        Console.WriteLine("尝试重新加载当前歌曲");
                        await LoadSongAsync(CurrentSong);
                        
                        // 重新尝试播放
                        if (_waveOut != null && _audioFile != null)
                        {
                            try
                            {
                                _isManualStop = false;
                                _waveOut.Play();
                                IsPlayingChanged?.Invoke(this, true);
                            }
                            catch (Exception retryPlayEx)
                            {
                                Console.WriteLine($"重新尝试播放失败: {retryPlayEx.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [播放异常] {ex.Message}");
                Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            }
            
            await Task.CompletedTask;
        }

        public async Task PauseAsync()
        {
            _isManualStop = true;
            Console.WriteLine($"⏸️ [暂停播放] {CurrentSong?.Title ?? "未知歌曲"}");
            
            try
            {
                if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
                {
                    _waveOut.Pause();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"暂停播放时发生异常: {ex.Message}");
            }
            
            IsPlayingChanged?.Invoke(this, false);
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            _isManualStop = true;
            
            try
            {
                if (_waveOut != null && _waveOut.PlaybackState != PlaybackState.Stopped)
                {
                    _waveOut.Stop();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"停止播放时发生异常: {ex.Message}");
            }
            
            try
            {
                if (_audioFile != null)
                {
                    _audioFile.Position = 0;
                    PositionChanged?.Invoke(this, TimeSpan.Zero);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"重置音频位置时发生异常: {ex.Message}");
            }
            
            IsPlayingChanged?.Invoke(this, false);
            await Task.CompletedTask;
        }

        public async Task NextAsync()
        {
            if (_playlist.Count > 0)
            {
                var wasPlaying = IsPlaying;
                var oldIndex = _currentIndex;
                
                Console.WriteLine($"🔽 [用户点击下一首] 当前索引: {oldIndex}, 播放状态: {(wasPlaying ? "播放中" : "暂停")}");
                
                // 切换到下一首
                _currentIndex = (_currentIndex + 1) % _playlist.Count;
                
                Console.WriteLine($"   NextAsync: 从索引 {oldIndex} 切换到 {_currentIndex}");
                Console.WriteLine($"   歌曲: {_playlist[oldIndex].Title} → {_playlist[_currentIndex].Title}");
                
                // 直接加载新歌曲（这会自动停止当前播放）
                await LoadSongAsync(_playlist[_currentIndex]);
                
                // 如果之前在播放，立即开始播放新歌曲
                if (wasPlaying)
                {
                    Console.WriteLine($"   继续播放新歌曲: {_playlist[_currentIndex].Title}");
                    await PlayAsync();
                }
                else
                {
                    Console.WriteLine($"   歌曲已切换，保持暂停状态");
                }
            }
            else
            {
                Console.WriteLine($"🔽 [用户点击下一首] 播放列表为空，无法切换");
            }
        }

        public async Task PreviousAsync()
        {
            if (_playlist.Count > 0)
            {
                var wasPlaying = IsPlaying;
                var oldIndex = _currentIndex;
                
                Console.WriteLine($"🔼 [用户点击上一首] 当前索引: {oldIndex}, 播放状态: {(wasPlaying ? "播放中" : "暂停")}");
                
                // 切换到上一首
                _currentIndex = _currentIndex <= 0 ? _playlist.Count - 1 : _currentIndex - 1;
                
                Console.WriteLine($"   PreviousAsync: 从索引 {oldIndex} 切换到 {_currentIndex}");
                Console.WriteLine($"   歌曲: {_playlist[oldIndex].Title} → {_playlist[_currentIndex].Title}");
                
                // 直接加载新歌曲（这会自动停止当前播放）
                await LoadSongAsync(_playlist[_currentIndex]);
                
                // 如果之前在播放，立即开始播放新歌曲
                if (wasPlaying)
                {
                    Console.WriteLine($"   继续播放新歌曲: {_playlist[_currentIndex].Title}");
                    await PlayAsync();
                }
                else
                {
                    Console.WriteLine($"   歌曲已切换，保持暂停状态");
                }
            }
            else
            {
                Console.WriteLine($"🔼 [用户点击上一首] 播放列表为空，无法切换");
            }
        }

        public async Task SetPositionAsync(TimeSpan position)
        {
            if (_audioFile != null && position >= TimeSpan.Zero && position <= _audioFile.TotalTime)
            {
                _audioFile.CurrentTime = position;
                // 立即通知位置变化
                PositionChanged?.Invoke(this, position);
                Console.WriteLine($"位置设置为: {position:mm\\:ss}");
            }
            await Task.CompletedTask;
        }

        public async Task SetVolumeAsync(double volume)
        {
            _volume = Math.Max(0, Math.Min(1, volume));
            if (_audioFile != null)
            {
                _audioFile.Volume = (float)_volume;
            }
            await Task.CompletedTask;
        }

        public void AddToPlaylist(Song song)
        {
            _playlist.Add(song);
            if (_currentIndex == -1)
            {
                _currentIndex = 0;
                // 立即设置当前歌曲并通知UI
                CurrentSongChanged?.Invoke(this, song);
                // 异步加载歌曲
                _ = Task.Run(async () =>
                {
                    await LoadSongAsync(song);
                });
            }
        }

        public async Task PlaySongAsync(Song song)
        {
            // 找到歌曲在播放列表中的索引
            var index = _playlist.IndexOf(song);
            if (index >= 0)
            {
                Console.WriteLine($"PlaySongAsync: 切换到索引 {index}, 歌曲: {song.Title}");
                _currentIndex = index;
                await LoadSongAsync(song);
                
                // 确保当前歌曲被正确设置
                CurrentSongChanged?.Invoke(this, song);
                
                // 确保播放状态被正确设置为停止状态
                IsPlayingChanged?.Invoke(this, false);
                Console.WriteLine($"PlaySongAsync: 歌曲切换完成，播放状态重置为false");
            }
            else
            {
                Console.WriteLine($"PlaySongAsync: 歌曲 {song.Title} 不在播放列表中");
            }
        }

        public void RemoveFromPlaylist(int index)
        {
            if (index >= 0 && index < _playlist.Count)
            {
                _playlist.RemoveAt(index);
                if (_currentIndex >= index && _currentIndex > 0)
                {
                    _currentIndex--;
                }
                if (_playlist.Count == 0)
                {
                    _currentIndex = -1;
                }
            }
        }

        public void ClearPlaylist()
        {
            _playlist.Clear();
            _currentIndex = -1;
            _ = StopAsync();
            
            // 触发事件通知UI更新
            CurrentSongChanged?.Invoke(this, null);
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            var currentPos = _audioFile?.CurrentTime ?? TimeSpan.Zero;
            var duration = _audioFile?.TotalTime ?? TimeSpan.Zero;
            var playbackState = _waveOut?.PlaybackState.ToString() ?? "Unknown";
            
            Console.WriteLine($"🛑 [播放停止事件] 歌曲: {CurrentSong?.Title ?? "未知"}");
            Console.WriteLine($"   位置: {currentPos:mm\\:ss} / {duration:mm\\:ss}, 状态: {playbackState}");
            Console.WriteLine($"   异常: {e.Exception?.Message ?? "无"}, 手动停止: {_isManualStop}");
            
            IsPlayingChanged?.Invoke(this, false);
            
            // 如果是手动停止，重置标志并返回
            if (_isManualStop)
            {
                _isManualStop = false;
                Console.WriteLine("   → 手动停止播放，不自动切换下一首");
                return;
            }
            
            // 检查是否真的播放完毕（位置接近结尾）
            var isReallyFinished = duration > TimeSpan.Zero && 
                                   Math.Abs((duration - currentPos).TotalSeconds) < 1.0;
            
            // 如果播放完成且不是用户主动停止，自动播放下一首
            if (e.Exception == null && _playlist.Count > 0 && isReallyFinished)
            {
                Console.WriteLine($"   → 歌曲真正播放完毕，自动切换到下一首");
                _ = NextAsync();
            }
            else if (!isReallyFinished)
            {
                Console.WriteLine($"   → 歌曲未播放完毕就停止，可能是加载问题，不自动切换");
            }
        }

        private void SafeDisposeWaveOut()
        {
            if (_waveOut != null)
            {
                try
                {
                    // 先尝试停止播放
                    if (_waveOut.PlaybackState != PlaybackState.Stopped)
                    {
                        try
                        {
                            _waveOut.Stop();
                        }
                        catch (Exception stopEx)
                        {
                            Console.WriteLine($"停止WaveOut时异常: {stopEx.Message}");
                        }
                    }
                }
                catch (Exception stateEx)
                {
                    Console.WriteLine($"检查WaveOut状态时异常: {stateEx.Message}");
                }

                try
                {
                    _waveOut.Dispose();
                }
                catch (Exception disposeEx)
                {
                    Console.WriteLine($"释放WaveOut时异常: {disposeEx.Message}");
                }
                finally
                {
                    _waveOut = null;
                }
            }
        }

        private WaveOutEvent? CreateNewWaveOut()
        {
            try
            {
                var waveOut = new WaveOutEvent();
                waveOut.PlaybackStopped += OnPlaybackStopped;
                Console.WriteLine("成功创建新的WaveOutEvent");
                return waveOut;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建WaveOutEvent失败: {ex.Message}");
                return null;
            }
        }

        private void StartPositionTimer()
        {
            // 启动一个持续的定时器来更新位置
            _ = Task.Run(async () =>
            {
                while (!_disposed)
                {
                    if (_waveOut?.PlaybackState == PlaybackState.Playing && _audioFile != null)
                    {
                        PositionChanged?.Invoke(this, CurrentPosition);
                    }
                    await Task.Delay(100);
                }
            });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                SafeDisposeWaveOut();
                _audioFile?.Dispose();
                _audioFile = null;
                
                // 清理MediaFoundation
                try
                {
                    MediaFoundationApi.Shutdown();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MediaFoundation清理失败: {ex.Message}");
                }
            }
        }
    }
}
