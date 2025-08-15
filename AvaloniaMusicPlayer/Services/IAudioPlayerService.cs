using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaMusicPlayer.Models;

namespace AvaloniaMusicPlayer.Services
{
    public interface IAudioPlayerService
    {
        event EventHandler<TimeSpan>? PositionChanged;
        event EventHandler<TimeSpan>? DurationChanged;
        event EventHandler<bool>? IsPlayingChanged;
        event EventHandler<Song?>? CurrentSongChanged;

        bool IsPlaying { get; }
        TimeSpan CurrentPosition { get; }
        TimeSpan Duration { get; }
        Song? CurrentSong { get; }
        List<Song> Playlist { get; }
        int CurrentIndex { get; }

        Task LoadSongAsync(Song song);
        Task PlaySongAsync(Song song);
        Task PlayAsync();
        Task PauseAsync();
        Task StopAsync();
        Task NextAsync();
        Task PreviousAsync();
        Task SetPositionAsync(TimeSpan position);
        Task SetVolumeAsync(double volume);

        // 删除SetUserDragging方法，不再需要
        void AddToPlaylist(Song song);
        void RemoveFromPlaylist(int index);
        void ClearPlaylist();
    }
}
