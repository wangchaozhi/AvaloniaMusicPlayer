using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AvaloniaMusicPlayer.Models;

namespace AvaloniaMusicPlayer.Services
{
    // JSON序列化上下文，支持AOT编译
    [JsonSerializable(typeof(PlaylistCache))]
    [JsonSerializable(typeof(Song))]
    [JsonSerializable(typeof(List<Song>))]
    [JsonSerializable(typeof(DateTime))]
    [JsonSerializable(typeof(TimeSpan))]
    public partial class PlaylistJsonContext : JsonSerializerContext
    {
    }

    public interface IPlaylistCacheService
    {
        Task SavePlaylistAsync(List<Song> playlist);
        Task<List<Song>> LoadPlaylistAsync();
        Task ClearCacheAsync();
        bool HasCache();
    }

    public class PlaylistCacheService : IPlaylistCacheService
    {
        private readonly string _cacheFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public PlaylistCacheService()
        {
            // 将缓存文件保存在用户文档目录或当前工作目录，更容易找到
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var cacheDirectory = Path.Combine(documentsPath, "AvaloniaMusicPlayer");
            
            // 如果文档目录不可用，使用当前工作目录
            if (string.IsNullOrEmpty(documentsPath))
            {
                cacheDirectory = Environment.CurrentDirectory;
            }
            
            // 确保目录存在
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
                Console.WriteLine($"创建缓存目录: {cacheDirectory}");
            }
            
            _cacheFilePath = Path.Combine(cacheDirectory, "playlist_cache.json");
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true, // 格式化JSON便于阅读
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 支持中文字符
                TypeInfoResolver = PlaylistJsonContext.Default // 使用源代码生成器
            };
            
            Console.WriteLine($"播放列表缓存文件路径: {_cacheFilePath}");
            Console.WriteLine($"缓存目录: {cacheDirectory}");
            Console.WriteLine($"当前工作目录: {Environment.CurrentDirectory}");
            Console.WriteLine($"应用程序目录: {AppDomain.CurrentDomain.BaseDirectory}");
        }

        public async Task SavePlaylistAsync(List<Song> playlist)
        {
            try
            {
                if (playlist == null || playlist.Count == 0)
                {
                    Console.WriteLine("播放列表为空，跳过保存缓存");
                    return;
                }

                var cacheData = new PlaylistCache
                {
                    SaveTime = DateTime.Now,
                    Version = "1.0",
                    SongCount = playlist.Count,
                    Songs = playlist
                };

                var json = JsonSerializer.Serialize(cacheData, _jsonOptions);
                await File.WriteAllTextAsync(_cacheFilePath, json);
                
                Console.WriteLine($"已保存 {playlist.Count} 首歌曲到缓存文件");
                Console.WriteLine($"缓存文件大小: {new FileInfo(_cacheFilePath).Length} 字节");
                Console.WriteLine($"文件是否存在: {File.Exists(_cacheFilePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存播放列表缓存失败: {ex.Message}");
            }
        }

        public async Task<List<Song>> LoadPlaylistAsync()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                {
                    Console.WriteLine("缓存文件不存在");
                    return new List<Song>();
                }

                var json = await File.ReadAllTextAsync(_cacheFilePath);
                var cacheData = JsonSerializer.Deserialize<PlaylistCache>(json, _jsonOptions);

                if (cacheData?.Songs == null)
                {
                    Console.WriteLine("缓存数据格式无效");
                    return new List<Song>();
                }

                // 验证文件是否仍然存在
                var validSongs = new List<Song>();
                foreach (var song in cacheData.Songs)
                {
                    if (File.Exists(song.FilePath))
                    {
                        validSongs.Add(song);
                    }
                    else
                    {
                        Console.WriteLine($"文件不存在，跳过: {song.FilePath}");
                    }
                }

                Console.WriteLine($"从缓存加载了 {validSongs.Count} 首歌曲 (原 {cacheData.Songs.Count} 首)");
                Console.WriteLine($"缓存创建时间: {cacheData.SaveTime}");

                return validSongs;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载播放列表缓存失败: {ex.Message}");
                return new List<Song>();
            }
        }

        public async Task ClearCacheAsync()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    File.Delete(_cacheFilePath);
                    Console.WriteLine("播放列表缓存已清除");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清除缓存失败: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        public bool HasCache()
        {
            return File.Exists(_cacheFilePath);
        }


    }

    // 缓存数据结构
    public class PlaylistCache
    {
        public DateTime SaveTime { get; set; }
        public string Version { get; set; } = "1.0";
        public int SongCount { get; set; }
        public List<Song> Songs { get; set; } = new();
    }
}
