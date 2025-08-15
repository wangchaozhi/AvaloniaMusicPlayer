using System;

namespace AvaloniaMusicPlayer.Models
{
    public class LyricLine
    {
        public TimeSpan Time { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
        
        public string DisplayText => string.IsNullOrEmpty(Text) ? "暂无歌词" : Text;
    }
}
