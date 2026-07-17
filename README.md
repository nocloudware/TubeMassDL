# TubeMassDL - Cazador de Enlaces y Descargador Masivo

TubeMassDL is a modern Windows desktop application (.NET 8 WPF) for automatically capturing, queuing, and mass-downloading videos and audio from YouTube and 1800+ supported sites via `yt-dlp`.

## Features

### Clipboard Auto-Capture

Links are captured automatically from the clipboard as you browse. Just copy a URL (Ctrl+C) and it appears in the download queue — no manual input required.

- Supports YouTube, Vimeo, TikTok, Instagram, Facebook, X/Twitter, Twitch, Dailymotion, and direct file URLs (mp4, avi, mkv, zip, pdf, etc.)
- Filters out unsupported URLs automatically
- Duplicate detection

### Playlist Expansion

- Copy a YouTube playlist URL → automatically fetches all video entries via `yt-dlp --flat-playlist`
- Hierarchical tree display with `├──` / `└──` characters
- Double-click to expand/collapse playlists
- Item count shown in the queue

### Download Types

| Type | Description |
|------|-------------|
| Video | Best quality, 4K, 1080p, 720p, 480p, 360p |
| Audio | MP3, M4A, Opus, WAV |
| Formats | MP4, AVI, WebM, MKV |

### Anti-Blocking Engine

When enabled:
- Random sleep intervals (15-45s)
- Rate limiting (5MB/s)
- Extended retries (5 attempts per video)
- Fragment retries (5 attempts)
- No modification timestamps
- `--wait-for-video 30`

### Multi-Language Support

| Language | Locale |
|----------|--------|
| English | en |
| Español | es |
| Français | fr |
| Deutsch | de |
| Português | pt |
| 中文 | zh |
| 日本語 | ja |

Language is auto-detected from the OS on first run, and can be changed at any time from the header selector.

### Dark / Light Theme

Full application theme support via WPF-UI and custom Aether theme dictionaries. Toggle with one click in the header.

### Keyboard Shortcuts

- `Delete` — Remove the selected item (or entire playlist with children)

### Double-Click Actions

- **Playlist** — Expand / collapse
- **Single item (queued)** — Start download
- **Single item (downloading)** — Pause / resume

## Architecture

```
TubeMassDL/
├── TubeMassDL.sln
├── TubeMassDL/                    # Main WPF application
│   ├── App.xaml.cs                # Application startup, service wiring
│   ├── Models/
│   │   ├── CapturedLink.cs        # Clipboard link model
│   │   ├── Enums.cs               # LinkType enum
│   │   ├── SiteInfo.cs            # Site detection result
│   │   └── VideoInfo.cs           # Video metadata model
│   ├── Services/
│   │   ├── ClipboardMonitor.cs    # 500ms clipboard polling
│   │   ├── LinkCollector.cs       # Queue management + playlist expansion
│   │   ├── DownloadManager.cs     # Concurrent download orchestration (max 3)
│   │   ├── YtDlpDownloader.cs     # yt-dlp process wrapper
│   │   ├── HttpDownloader.cs      # Direct file download via HttpClient
│   │   ├── YtdlpUpdater.cs        # Auto-update yt-dlp from GitHub
│   │   ├── SiteDetector.cs        # URL → site name mapping
│   │   └── TaskbarFlashService.cs # Win32 FlashWindowEx API
│   ├── Converters/
│   │   ├── FileListConverters.cs  # Icon, extension, size, status dot converters
│   │   ├── TreeItemConverters.cs  # Playlist expand/collapse visibility
│   │   ├── StatusToColorConverter.cs
│   │   └── ProgressVisibilityConverter.cs
│   ├── Panels/
│   │   └── OptionsPanel.xaml.cs   # Right sidebar: type, quality, format, antiblock
│   ├── Resources/
│   │   ├── QueueItemTemplate.xaml # List item DataTemplate
│   │   ├── app.ico
│   │   ├── tubemassdl-banner.png
│   │   └── tubemassdl-logo.png
│   └── Translations.cs            # 7-language translation dictionary
└── NoCloudware.UI.Core/           # Shared UI library
    ├── Controls/
    │   ├── ShellWindow.xaml        # Main window with header, footer
    │   ├── BaseMainControl.xaml    # Left-right panel layout
    │   ├── FileListBox.xaml        # File list with columns + double-click
    │   ├── LanguageSelector.xaml   # Flag dropdown selector
    │   └── ThemeToggle.xaml        # Dark/light toggle button
    └── Services/
        ├── ThemeService.cs         # ApplicationThemeManager + Aether swap
        └── LanguageService.cs      # Culture detection + switching
```

## Quick Start

### Prerequisites

- .NET 8.0 SDK (Windows)
- yt-dlp.exe in the app directory (auto-downloaded on first run)
- Firefox or Chrome for cookies (optional, for authenticated content)

### Build & Run

```bash
git clone https://github.com/nocloudware/TubeMassDL.git
cd TubeMassDL
dotnet build
dotnet run --project TubeMassDL
```

Or open `TubeMassDL.sln` in Visual Studio and press F5.

### Usage

1. Launch the app
2. Copy any video/playlist URL → it appears in the queue automatically
3. Configure type (video/audio), quality, format, and anti-block in the right panel
4. Click **Download All** or double-click individual items
5. Monitor progress in the status bar

## Configuration

Settings are persisted in `appsettings.json`:

```json
{
  "Settings": {
    "DarkTheme": true,
    "Language": "auto",
    "MaxConcurrentDownloads": 3,
    "DefaultOutputPath": "TubeMassDL",
    "AntiBlockMode": true
  }
}
```

## Tech Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 8.0 |
| UI | WPF + WPF-UI 4.3.0 |
| Language | C# 12.0 |
| MVVM | CommunityToolkit.Mvvm 8.4.0 |
| Download Engine | yt-dlp |
| UI Library | NoCloudware.UI.Core (Aether theme) |

## License

MIT License

## Links

- [Report Issues](https://github.com/nocloudware/TubeMassDL/issues)
- [yt-dlp Documentation](https://github.com/yt-dlp/yt-dlp)
