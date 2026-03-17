# <div align="left" style="display: flex; align-items: center; line-height: 1.2;"> <img src="./141221163.png" width="32" height="32" style="margin-right: 15px;"> <span style="font-size: 32px; font-weight: bold;">QwenCLI - Intelligent DevOps Automation Agent</span> </div>
<div align="center">

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-blue?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)
![Version](https://img.shields.io/badge/Version-v1.5.0-orange?style=flat-square)

**Let AI Be Your DevOps Assistant | Intelligent Automation Expert in the Command Line**

**🌐 Language | 语言切换:** [English](README_EN.md) | [中文](README.md)

</div>

---

## 📖 Project Introduction

**QwenCLI** is an intelligent DevOps automation command-line tool based on large language models. Through natural language interaction, it helps you quickly execute system commands, manage files, and analyze logs, making tedious DevOps tasks simple and efficient.

Just input natural language instructions, and QwenCLI will understand your intent and automatically call the appropriate tools to complete the task — like having a 24/7 online DevOps expert ready to assist.

---

## ✨ Core Features

| Feature | Description |
|------|------|
| 🔧 **Command Execution** | Supports cross-platform terminal command execution (ls, dir, git, npm, docker, etc.) |
| 📄 **File Reading** | Intelligently reads text, code, and configuration file contents |
| ✍️ **File Writing** | Automatically generates code and configuration files and writes them locally |
| 🖼️ **Image Analysis** | Supports recognition and analysis of local screenshots and photos |
| 🔍 **Content Search** | Search for files containing specific keywords (such as function names, variable names, class names, etc.) in specified directories |

---

## 🚀 Quick Start

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or higher
- Alibaba Cloud DashScope API Key ([Get Here](https://dashscope.console.aliyun.com/))

### Installation & Running

```bash
# Clone the project
git clone https://github.com/anan1213095357/QwenCLI.git
cd QwenCLI/QwenCLI

# Restore dependencies
dotnet restore

# Configure API Key (choose one)
# Method 1: Copy configuration file and modify
cp appsettings.example.json appsettings.json
# Then edit appsettings.json to fill in your API Key

# Method 2: Use environment variables
setx QWENCLI_API_KEY "sk-your-api-key-here"

# Run the project
dotnet run
```

### Publish as Standalone Executable

```bash
# Publish AOT compiled version (single file, high performance)
dotnet publish -c Release -r win-x64 --self-contained true
```

---

## ⚙️ Configuration

### Method 1: Configuration File (Recommended for Local Development)

1. Copy the template file:
   ```bash
   cp appsettings.example.json appsettings.json
   ```

2. Edit `appsettings.json`:
   ```json
   {
     "ApiKey": "sk-your-actual-api-key-here",
     "Endpoint": "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
     "Model": "qwen3.5-plus"
   }
   ```

### Method 2: Environment Variables (Recommended for Production)

```bash
# Windows
setx QWENCLI_API_KEY "sk-your-api-key-here"
setx QWENCLI_ENDPOINT "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions"
setx QWENCLI_MODEL "qwen3.5-plus"

# Linux / macOS
export QWENCLI_API_KEY="sk-your-api-key-here"
export QWENCLI_ENDPOINT="https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions"
export QWENCLI_MODEL="qwen3.5-plus"
```

> 🔒 **Security Tip**: `appsettings.json` has been added to `.gitignore` and will not be committed to the Git repository. Do not manually upload files containing real API Keys to public repositories.

---

## 💡 Usage Examples

After starting QwenCLI, you can input various natural language instructions:

```
> Help me check what files are in the current directory

> Read the contents of package.json and analyze dependencies

> Create a configuration file named config.json containing database connection information

> Execute git status to check the current repository state

> Analyze the error message in this screenshot (supports image paths)

> Search for all C# files containing "UserService" in the project
```

### Interface Preview

```
   ____                       ________    ____ 
  / __ \_      _____  ____   / ____/ /   /  _/ 
 / / / / | /| / / _ \/ __ \ / /   / /    / /   
/ /_/ /| |/ |/ /  __/ / / // /___/ /____/ /    
\___\_\|__/|__/\___/_/ /_/ \____/_____/___/

v1.5.0 | Cross-Platform DevOps Tool | by using_unfase

Terminal DevOps Agent has accessed the system (supports full file operations).
Tip: Please enter deployment or DevOps instructions (type 'exit' to terminate connection)
-------------------------------------------------------
```

---

## 🏗️ Project Structure

```
QwenCLI/
├── QwenCLI.csproj              # Project configuration file
├── Program.cs                # Main program entry point
├── README.md                 # Project documentation (Chinese)
├── README_EN.md              # Project documentation (English)
├── .gitignore                # Git ignore rules
├── appsettings.json          # Configuration file (for local use, do not commit)
├── appsettings.example.json  # Configuration template (can be committed)
├── Properties/               # Project property configuration
├── bin/                      # Build output directory
└── obj/                      # Temporary build files
```

---

## 🔬 Technology Stack

- **Runtime**: .NET 10.0
- **Compilation Optimization**: AOT (Ahead-of-Time) Compilation
- **AI Model**: Alibaba Cloud Qwen (qwen3.5-plus)
- **HTTP Client**: System.Net.Http
- **JSON Processing**: System.Text.Json

### Project Features

- ✅ **AOT Compilation** - Smaller size, faster startup speed
- ✅ **Cross-Platform** - Supports Windows, Linux, macOS
- ✅ **Streaming Conversation** - Supports multi-turn contextual dialogue
- ✅ **Tool Calling** - Intelligently identifies and calls appropriate tools
- ✅ **Error Handling** - Comprehensive exception capture and prompts
- ✅ **Secure Configuration** - API Key separated from code, supports environment variables
- ✅ **Encoding Compatibility** - Automatically detects file encoding (UTF-8/GBK) to avoid garbled text
- ✅ **Intelligent Search** - Quickly locate code files containing specific keywords

---

## 📝 Notes

1. **API Key Security**: `appsettings.json` has been added to `.gitignore`, do not manually commit files containing real Keys
2. **Command Execution Permissions**: Commands executed by the tool have the same permissions as the current user, please operate with caution
3. **Network Dependency**: Stable network connection is required to call AI services
4. **Token Limit**: Large file reading will be automatically truncated to prevent Token overflow
5. **File Modification**: When modifying existing files, please ensure to provide precise old_content for replacement

---

## 🤝 Contribution Guidelines

Issues and Pull Requests are welcome!

1. Fork this project
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📄 License

This project is open source under the [MIT License](LICENSE).

---

<div align="center">

**Made with ❤️ by using_unsafe**

If this project helps you, please give it a ⭐ Star to show your support!

</div>
