# 🔒 Assist

> **A powerful all-in-one Windows desktop productivity and developer toolkit built with .NET 10 and WinForms.**

Assist is a modern MDI (Multiple Document Interface) desktop application designed to centralize your daily tools — from password management and task tracking to system diagnostics, developer utilities, network analysis, and AI-driven workflows — all inside a sleek, dark-themed interface.

---

## ✨ Features at a Glance

### 🔐 Password Manager
- AES-encrypted local storage of credentials
- Master password login with session management
- Add, edit, delete, view passwords with eye/copy toggles
- Password generator with configurable strength settings

### ✅ To-Do & Task Management
- Full to-do list with priorities (Critical / High / Normal / Low)
- Due date tracking with overdue/today/this-week filters
- Monthly recurring tasks with auto-advance on completion
- Categories, free-text search, and quick filter bar

### 🖥️ System Tools
| Tool | Description |
|------|-------------|
| **Hardware Diagnostics** | CPU, RAM, disk health, DNS, power plan, SFC checks |
| **Disk Cleaner** | Safe cleanup of temp files, log files, thumbnail cache |
| **System Recovery** | Recovery and repair actions for common Windows issues |
| **Startup Manager** | View and manage Windows startup programs |
| **Performance Monitor** | Live CPU / RAM / disk usage graphs |
| **System Info** | Detailed hardware and OS information |
| **Threat Scanner** | Basic malware / suspicious-file scanner |

### 🌐 Network Tools
| Tool | Description |
|------|-------------|
| **Connection Monitor** | Live multi-target ping matrix with latency heatmap |
| **Network Scanner** | Local network host discovery |
| **IP / Domain Query** | Reverse DNS, GeoIP, WHOIS lookups |
| **Wi-Fi Password Viewer** | Extract saved Wi-Fi credentials |
| **Hosts File Editor** | View and edit the Windows hosts file |
| **Speed Test** | Measure download/upload speeds |

### 🛠️ Developer Tools
| Tool | Description |
|------|-------------|
| **JSON Formatter** | Pretty-print, minify, and validate JSON |
| **XML / Pretty XML** | Format, minify, validate, Base64 encode/decode XML |
| **Regex Tester** | Live regex match/group/replace testing |
| **Hash Generator** | MD5, SHA-1, SHA-256, SHA-512 hashing |
| **UUID Generator** | Bulk GUID/UUID generation |
| **Base64 Converter** | Encode/decode text and files |
| **Color Picker** | RGB/HEX/HSL color picker with clipboard copy |
| **Text Diff** | Side-by-side text comparison |
| **Console Runner** | Run shell commands and capture output |
| **Lorem Ipsum** | Placeholder text generator |

### 💰 Utilities & Converters
| Tool | Description |
|------|-------------|
| **Currency Converter** | Live exchange rates |
| **Unit Converter** | Length, mass, temperature, area, and more |
| **QR Code Generator** | Create QR codes from any text or URL |
| **Turkish Holidays** | Official Turkish public holiday calendar |
| **Earthquake Monitor** | Live AFAD/Kandilli seismic feed |
| **Dictionary** | Turkish and multilingual word lookup |
| **Wikipedia Search** | Quick in-app article browser |
| **Translation Tool** | Translate text between languages |

### 🤖 AI & SDLC Agent
- Multi-agent orchestration (SDLC planning, issue analysis, review)
- `AgentCoordinator` + `EventBus` driven pipeline
- Human Decision Console for agent-in-the-loop approvals
- Timeline view of agent activity and decisions
- AI-assisted task intake and dashboard
- Session manager for managing agent contexts

### 🎯 Productivity Extras
- **Wiggle Mouse** — keeps your PC/session active by micro-moving the cursor on a timer
- **Clipboard History** — persistent clipboard ring with pinning support
- **Notification Center** — in-app toast and alert management
- **Waiting Queue** — simple numbered queue/ticket manager
- **Reports** — system and activity report generation
- **News Feed** — RSS/API news reader with translation

---

## 🏗️ Architecture

```
Assist/
├── Forms/
│   ├── Core/                  # MainMDIForm, LoginForm, SplashForm, SetupWizard
│   ├── Productivity/          # TodoForm, TodoEditForm, ClipboardHistory, Reports
│   ├── SystemTools/
│   │   ├── Monitoring/        # PerformanceMonitor, ConnectionMonitor
│   │   ├── Maintenance/       # DiskCleaner, StartupManager
│   │   └── Troubleshooting/   # HardwareDiagnostics, ThreatScanner, SystemRecovery
│   ├── DeveloperTools/
│   │   ├── Formatters/        # JSON, XML, PrettyXml formatters
│   │   └── Utilities/         # Regex, Hash, UUID, Base64, ColorPicker, TextDiff
│   ├── Network/               # NetworkScanner, IpDomainQuery, Whois, SpeedTest
│   ├── Online/                # News, ExchangeRates, Earthquake, Wikipedia
│   └── Agent/                 # SDLC Dashboard, AgentConsoleHub, HumanDecisionConsole
├── Models/                    # TodoItem, PasswordEntry, NewsItem, SdlcModels, etc.
├── Services/                  # UITheme, TextSanitizer, TodoStore, PasswordStore,
│                              #   NewsService, TranslationService, AutoUpdateService
└── AppConstants.cs            # App-wide constants, paths, build version
```

### Key Design Decisions
- **MDI Shell** — `MainMDIForm` hosts all child windows; Oz click opens a curated startup workspace
- **Dark Theme** — `UITheme` + `ThemeService` apply a consistent dark palette across all controls including grids, menus, and custom-drawn elements
- **Local-first Storage** — Passwords and todos are stored encrypted/JSON in `%APPDATA%\AssistPasswordStore\`
- **Turkish Locale Support** — `TextSanitizer` provides runtime mojibake repair for Turkish characters; GDI charset 162 is set for all fonts
- **Event-driven Agents** — `EventBus` decouples agent components; `AgentCoordinator` manages multi-step AI workflows

---

## 🚀 Getting Started

### Prerequisites
- Windows 10/11 (64-bit)
- [.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Visual Studio 2022+ or Visual Studio 2026 (for development)

### Run from Source

```bash
git clone https://github.com/yoxbensoftware/Assist.git
cd Assist
dotnet run --project Assist/Assist.csproj
```

### Build Release

```bash
dotnet publish Assist/Assist.csproj -c Release -r win-x64 --self-contained true
```

### First Launch
1. On first run, the **Setup Wizard** guides you through creating a master password
2. After login, the MDI shell opens — click the **Oz** logo to launch your startup workspace
3. All data is stored locally; no internet connection is required for core features

---

## 📁 Data Storage

All application data is stored locally at:

```
%APPDATA%\AssistPasswordStore\
├── passwords.dat     # AES-encrypted password vault
├── login.dat         # Hashed master password
└── todos.json        # Task list with recurrence info
```

No telemetry. No cloud sync. Your data stays on your machine.

---

## 🎨 Theming

Assist ships with a dark theme by default. The `ThemeSelectionForm` allows switching theme variants at runtime. Theme settings are persisted across sessions.

---

## 📦 Project Info

| Property | Value |
|----------|-------|
| Platform | Windows (WinForms) |
| Framework | .NET 10 |
| Language | C# 13 |
| Build Version | v1.43 |
| License | MIT |

---

## 🤝 Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'feat: add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

<p align="center">
  Made with ❤️ for Windows power users
</p>
