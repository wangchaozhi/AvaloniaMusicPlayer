using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaMusicPlayer.Models;

namespace AvaloniaMusicPlayer.Services
{
    public interface ILyricService
    {
        event EventHandler<LyricLine?>? CurrentLyricChanged;
        
        Task<List<LyricLine>> LoadLyricsAsync(string songFilePath);
        void UpdateCurrentLyric(TimeSpan currentTime);
        void ClearLyrics();
    }
}
