using System;

namespace AvaloniaMusicPlayer.Models
{
    public class Song
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string CoverArtPath { get; set; } = string.Empty;
        
        public string DisplayTitle => string.IsNullOrEmpty(Title) ? System.IO.Path.GetFileNameWithoutExtension(FilePath) : Title;
        public string DisplayArtist => string.IsNullOrEmpty(Artist) ? "未知艺术家" : Artist;
        public string DisplayAlbum => string.IsNullOrEmpty(Album) ? "未知专辑" : Album;
        public string DurationString => Duration.ToString(@"mm\:ss");
    }
}
