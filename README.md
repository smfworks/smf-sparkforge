# 🔥 SparkForge

**Run AI models locally on your Windows PC — zero setup required.**

SparkForge is a native Windows desktop app that bundles everything needed to chat with local AI models. Built for non-technical users who want privacy-first, offline-capable AI without touching a terminal.

## ✨ Features

- **Zero-config chat** — connects to Ollama automatically, streams responses in real-time
- **Markdown rendering** — code blocks, tables, lists, headers, syntax highlighting
- **Model management** — browse and download models from the Ollama library with progress bars
- **Hardware detection** — detects GPU, RAM, NPU, and recommends the right model size for your machine
- **Performance benchmarking** — test tokens/sec for each installed model
- **Setup wizard** — guides first-time users through Ollama install, hardware check, and model download
- **System tray** — minimize to tray, start with Windows, stay out of your way
- **Dark/light themes** — Catppuccin Mocha and Latte, toggle with one click
- **Conversation management** — multiple chats, rename, delete, per-conversation system prompts
- **Privacy-first** — everything runs locally, zero telemetry, no cloud dependency

## 📦 Install

Download the latest installer from [Releases](https://github.com/smfworks/smf-sparkforge/releases).

### Requirements

- Windows 10/11 (x64)
- [Ollama](https://ollama.com/download/windows) (the setup wizard will help you install it)

## 🛠 Build from Source

```bash
# Prerequisites: .NET 8 SDK
git clone https://github.com/smfworks/smf-sparkforge.git
cd smf-sparkforge/SparkForge
dotnet build
dotnet run
```

### Build Installer

```bash
# Publish self-contained build
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../publish

# Build installer (requires Inno Setup 6)
iscc installer/installer.iss
```

## 🏗 Architecture

```
SparkForge/
├── Models/           # Data models (Conversation, ChatMessage, OllamaModel, HardwareInfo)
├── Services/         # Business logic
│   ├── OllamaService.cs      # Ollama REST API client (streaming chat, model management)
│   ├── ChatStorageService.cs # SQLite persistence with schema versioning
│   ├── HardwareService.cs    # WMI-based GPU/CPU/RAM/NPU detection
│   ├── Logger.cs             # File rotation logger + Windows Event Log
│   ├── MarkdownService.cs    # Markdown → HTML converter
│   ├── ThemeService.cs       # Catppuccin Mocha/Latte theme engine
│   └── TrayService.cs        # System tray icon + minimize-to-tray
├── ViewModels/
│   └── MainViewModel.cs      # MVVM: streaming chat, conversation CRUD, commands
├── Views/
│   ├── BenchmarkDialog       # Model speed testing
│   ├── ModelDownloadDialog   # Ollama library browser + download
│   ├── SetupWizard           # 4-step first-run wizard
│   └── SystemPromptDialog    # Per-conversation prompt editor
├── Converters/               # WPF value converters
├── MainWindow.xaml           # WebView2 chat + sidebar UI
└── App.xaml                  # Entry point + crash handlers
```

## 📋 Tech Stack

- **Framework:** .NET 8 WPF
- **Chat UI:** WebView2 with custom Markdown CSS
- **Database:** SQLite (Microsoft.Data.Sqlite)
- **MVVM:** CommunityToolkit.Mvvm
- **Packaging:** Inno Setup 6

## 🔒 Privacy

SparkForge runs entirely on your machine. No data leaves your computer. No telemetry. No analytics. No cloud dependency. Your conversations are stored in a local SQLite database at `%LOCALAPPDATA%\SparkForge\`.

## 📄 License

MIT — see [LICENSE](LICENSE)

## 👤 Author

Built by [SMF Works](https://smfworks.com) — forging the future of business, work & life with AI.
