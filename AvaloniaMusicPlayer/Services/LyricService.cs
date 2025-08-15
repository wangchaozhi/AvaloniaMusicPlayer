using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvaloniaMusicPlayer.Models;

namespace AvaloniaMusicPlayer.Services
{
    public class LyricService : ILyricService
    {
        private List<LyricLine> _lyrics = new();
        private int _currentIndex = -1;

        public event EventHandler<LyricLine?>? CurrentLyricChanged;

        public async Task<List<LyricLine>> LoadLyricsAsync(string songFilePath)
        {
            _lyrics.Clear();
            _currentIndex = -1;

            try
            {
                // 尝试查找同名的.lrc文件
                var lrcPath = Path.ChangeExtension(songFilePath, ".lrc");
                if (File.Exists(lrcPath))
                {
                    var lrcContent = await File.ReadAllTextAsync(lrcPath);
                    _lyrics = ParseLrcContent(lrcContent);
                }
                else
                {
                    // 如果没有找到.lrc文件，尝试从音频文件中提取歌词
                    _lyrics = await ExtractLyricsFromAudioAsync(songFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载歌词失败: {ex.Message}");
            }

            return _lyrics;
        }

        public void UpdateCurrentLyric(TimeSpan currentTime)
        {
            if (_lyrics.Count == 0)
            {
                if (_currentIndex != -1)
                {
                    _currentIndex = -1;
                    CurrentLyricChanged?.Invoke(this, null);
                }
                return;
            }

            // 找到当前时间对应的歌词行
            var newIndex = -1;
            for (int i = 0; i < _lyrics.Count; i++)
            {
                if (currentTime >= _lyrics[i].Time)
                {
                    newIndex = i;
                }
                else
                {
                    break;
                }
            }

            // 如果当前歌词行发生变化，触发事件
            if (newIndex != _currentIndex)
            {
                if (_currentIndex >= 0 && _currentIndex < _lyrics.Count)
                {
                    _lyrics[_currentIndex].IsCurrent = false;
                }

                _currentIndex = newIndex;

                if (_currentIndex >= 0 && _currentIndex < _lyrics.Count)
                {
                    _lyrics[_currentIndex].IsCurrent = true;
                    CurrentLyricChanged?.Invoke(this, _lyrics[_currentIndex]);
                }
                else
                {
                    CurrentLyricChanged?.Invoke(this, null);
                }
            }
        }

        public void ClearLyrics()
        {
            _lyrics.Clear();
            _currentIndex = -1;
            CurrentLyricChanged?.Invoke(this, null);
        }

        private List<LyricLine> ParseLrcContent(string content)
        {
            var lyrics = new List<LyricLine>();
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                    continue;

                // 匹配时间标签 [mm:ss.xx] 或 [mm:ss:xx]
                var timeMatches = Regex.Matches(trimmedLine, @"\[(\d{2}):(\d{2})[\.:](\d{2})\]");
                
                if (timeMatches.Count > 0)
                {
                    // 提取歌词文本（在最后一个时间标签之后）
                    var lastTimeMatch = timeMatches[timeMatches.Count - 1];
                    var lyricText = trimmedLine.Substring(lastTimeMatch.Index + lastTimeMatch.Length).Trim();
                    
                    // 为每个时间标签创建歌词行
                    foreach (Match match in timeMatches)
                    {
                        if (int.TryParse(match.Groups[1].Value, out var minutes) &&
                            int.TryParse(match.Groups[2].Value, out var seconds) &&
                            int.TryParse(match.Groups[3].Value, out var centiseconds))
                        {
                            var time = TimeSpan.FromMinutes(minutes) + 
                                     TimeSpan.FromSeconds(seconds) + 
                                     TimeSpan.FromMilliseconds(centiseconds * 10);
                            
                            lyrics.Add(new LyricLine
                            {
                                Time = time,
                                Text = lyricText
                            });
                        }
                    }
                }
            }

            // 按时间排序
            return lyrics.OrderBy(l => l.Time).ToList();
        }

        private Task<List<LyricLine>> ExtractLyricsFromAudioAsync(string audioFilePath)
        {
            var lyrics = new List<LyricLine>();
            
            try
            {
                // 使用TagLib尝试从音频文件中提取歌词
                using var file = TagLib.File.Create(audioFilePath);
                
                // 检查是否有内嵌歌词
                if (file.Tag.Lyrics != null && !string.IsNullOrEmpty(file.Tag.Lyrics))
                {
                    lyrics.Add(new LyricLine
                    {
                        Time = TimeSpan.Zero,
                        Text = file.Tag.Lyrics
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"从音频文件提取歌词失败: {ex.Message}");
            }

            return Task.FromResult(lyrics);
        }
    }
}
