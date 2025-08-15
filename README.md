# Avalonia 音乐播放器

一个使用 Avalonia UI 框架开发的跨平台音乐播放器应用。

## 功能特性

- 🎵 支持多种音频格式：MP3, WAV, FLAC, M4A, AAC, OGG
- 📁 播放列表管理
- 🎚️ 音量控制
- ⏯️ 播放控制（播放、暂停、停止、上一首、下一首）
- 📊 播放进度显示
- 🏷️ 自动读取音频文件标签信息（标题、艺术家、专辑）
- 🎨 现代化的用户界面

## 技术栈

- **UI框架**: Avalonia 11.3.2
- **音频处理**: NAudio 2.2.1
- **音频标签**: TagLibSharp 2.3.0
- **MVVM框架**: CommunityToolkit.Mvvm 8.2.1
- **目标框架**: .NET 9.0

## 系统要求

- Windows 10/11
- .NET 9.0 Runtime
- 音频设备

## 安装和运行

### 方法一：使用源代码

1. 克隆或下载项目
2. 确保已安装 .NET 9.0 SDK
3. 在项目目录中运行：
   ```bash
   dotnet restore
   dotnet build
   dotnet run
   ```

### 方法二：使用预编译版本

1. 下载最新的发布版本
2. 解压到任意目录
3. 运行 `AvaloniaMusicPlayer.exe`

## 使用说明

### 添加音乐文件

1. 点击"打开文件"按钮选择单个或多个音频文件
2. 点击"打开文件夹"按钮选择包含音频文件的文件夹
3. 支持的文件格式：MP3, WAV, FLAC, M4A, AAC, OGG

### 播放控制

- **播放/暂停**: 点击播放按钮开始播放，再次点击暂停
- **停止**: 点击停止按钮停止播放并重置到开始位置
- **上一首/下一首**: 点击相应按钮切换歌曲
- **进度控制**: 拖动进度条跳转到指定位置
- **音量控制**: 使用右侧的音量滑块调节音量

### 播放列表管理

- 播放列表显示在左侧面板
- 显示歌曲标题、艺术家和时长信息
- 点击"清空列表"按钮清空播放列表

## 项目结构

```
AvaloniaMusicPlayer/
├── Models/
│   └── Song.cs                 # 歌曲数据模型
├── Services/
│   ├── IAudioPlayerService.cs  # 音频播放服务接口
│   └── AudioPlayerService.cs   # 音频播放服务实现
├── ViewModels/
│   └── MainWindowViewModel.cs  # 主窗口视图模型
├── Views/
│   └── MainWindow.axaml        # 主窗口界面
├── Assets/                     # 资源文件
└── Program.cs                  # 应用程序入口
```

## 开发说明

### 架构设计

项目采用 MVVM (Model-View-ViewModel) 架构模式：

- **Model**: 数据模型，表示歌曲信息
- **View**: 用户界面，使用 Avalonia XAML 定义
- **ViewModel**: 视图模型，处理业务逻辑和用户交互
- **Service**: 服务层，处理音频播放等核心功能

### 依赖注入

使用简单的依赖注入模式：
- `IAudioPlayerService` 接口定义音频播放功能
- `AudioPlayerService` 实现具体的音频播放逻辑
- 在 `App.axaml.cs` 中注册服务实例

### 音频处理

- 使用 NAudio 库进行音频播放
- 使用 TagLibSharp 读取音频文件元数据
- 支持实时进度更新和音量控制

## 未来计划

- [ ] 添加音频可视化效果
- [ ] 支持播放模式（循环、随机等）
- [ ] 添加均衡器功能
- [ ] 支持播放列表保存和加载
- [ ] 添加歌词显示功能
- [ ] 支持快捷键操作
- [ ] 添加系统托盘功能

## 贡献

欢迎提交 Issue 和 Pull Request 来改进这个项目。

## 许可证

本项目采用 MIT 许可证。
