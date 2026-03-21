# <div align="left" style="display: flex; align-items: center; line-height: 1.2;"> <img src="./141221163.png" width="32" height="32" style="margin-right: 15px;"> <span style="font-size: 32px; font-weight: bold;">QwenCLI - 智能运维自动化 Agent</span> </div>
<div align="center">

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Win%20%7C%20Mac%20%7C%20Linux-blue?style=flat-square)
![Arch](https://img.shields.io/badge/Arch-x86%20%7C%20x64%20%7C%20ARM32%20%7C%20ARM64-blue?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)
![Version](https://img.shields.io/badge/Version-v1.5.0-orange?style=flat-square)
![Size](https://img.shields.io/badge/%F0%9F%A6%9E%20打包体积-仅%202MB-FF4500?style=for-the-badge)

**让 AI 成为你的运维助手 | 命令行里的智能自动化专家**

**🌐 语言切换 | Language:** [中文](README.md) | [English](README_EN.md)

</div>

---

> ## 🦞 全网最小龙虾，打包后仅 **2MB**！
>
> QwenCLI 采用 .NET AOT 编译技术，发布产物体积极致精简，**单文件可执行，打包后仅约 2MB**，是目前全网同类 AI 命令行工具中体积最小的存在。轻量不失强大，随处复制随处运行，再也不用担心部署负担！

---

## 📖 项目简介

**QwenCLI** 是一款基于千问大语言模型的智能运维自动化命令行工具。它通过自然语言交互，帮助你快速执行系统命令、管理文件、分析日志，让繁琐的运维工作变得简单高效。

只需输入自然语言指令，QwenCLI 就能理解你的意图，自动调用合适的工具完成任务 —— 就像有一个 24 小时在线的运维专家随时待命。

---

## ✨ 核心功能

| 功能 | 描述 |
|------|------|
| 🔧 **命令执行** | 支持跨平台终端命令执行 (ls, dir, git, npm, docker 等) |
| 📄 **文件读取** | 智能读取文本、代码、配置文件内容 |
| ✍️ **文件写入** | 自动生成代码、配置文件并写入本地 |
| 🖼️ **图片分析** | 支持本地截图、照片的识别与分析 |
| 🔍 **内容搜索** | 在指定目录下搜索包含特定关键字（如函数名、变量名、类名等）的文件 |

---

## 🚀 快速开始

### 前置要求

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) 或更高版本
- 阿里云 DashScope API Key ([获取地址](https://dashscope.console.aliyun.com/))

### 安装与运行

```bash
# 克隆项目
git clone https://github.com/anan1213095357/QwenCLI.git
cd QwenCLI/QwenCLI

# 还原依赖
dotnet restore

# 配置 API Key（二选一）
# 方式 1: 复制配置文件并修改
cp appsettings.example.json appsettings.json
# 然后编辑 appsettings.json 填入你的 API Key

# 方式 2: 使用环境变量
setx QWENCLI_API_KEY "sk-your-api-key-here"

# 运行项目
dotnet run
```

### 发布为独立可执行文件

```bash
# 发布 AOT 编译版本（单文件、高性能）
dotnet publish -c Release -r win-x64 --self-contained true
```

---

## ⚙️ 配置说明

### 方式一：配置文件（推荐本地开发）

1. 复制模板文件：
   ```bash
   cp appsettings.example.json appsettings.json
   ```

2. 编辑 `appsettings.json`：
   ```json
   {
     "ApiKey": "sk-your-actual-api-key-here",
     "Endpoint": "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
     "Model": "qwen3.5-plus"
   }
   ```

### 方式二：环境变量（推荐生产环境）

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

> 🔒 **安全提示**: `appsettings.json` 已加入 `.gitignore`，不会被提交到 Git 仓库。请勿手动将包含真实 API Key 的文件上传到公共仓库。

---

## 💡 使用示例

启动 QwenCLI 后，你可以输入各种自然语言指令：

```
> 帮我查看当前目录有哪些文件

> 读取 package.json 的内容并分析依赖

> 创建一个名为 config.json 的配置文件，包含数据库连接信息

> 执行 git status 查看当前仓库状态

> 分析这张截图中的错误信息 (支持图片路径)

> 搜索项目中所有包含 "UserService" 的 C# 文件
```

### 界面预览

```
   ____                       ________    ____ 
  / __ \_      _____  ____   / ____/ /   /  _/ 
 / / / / | /| / / _ \/ __ \ / /   / /    / /   
/ /_/ /| |/ |/ /  __/ / / // /___/ /____/ /    
\___\_\|__/|__/\___/_/ /_/ \____/_____/___/

v1.5.0 | 千问跨平台运维工具 | by 奶茶叔叔

终端运维 Agent 已接入系统。
提示：请输入部署或运维指令 (输入 'exit' 终止连接)
-------------------------------------------------------
```

---

## 🏗️ 项目结构

```
QwenCLI/
├── QwenCLI.csproj            # 项目配置文件
├── Program.cs                # 主程序入口
├── README.md                 # 项目说明文档
├── .gitignore                # Git 忽略规则
├── appsettings.json          # 配置文件（本地使用，不提交）
├── appsettings.example.json  # 配置模板（可提交）
├── Properties/               # 项目属性配置
├── bin/                      # 编译输出目录
└── obj/                      # 临时构建文件
```

---

## 🔬 技术栈

- **运行时**: .NET 10.0
- **编译优化**: AOT (Ahead-of-Time) 编译
- **AI 模型**: 阿里云通义千问 (qwen3.5-plus)
- **HTTP 客户端**: System.Net.Http
- **JSON 处理**: System.Text.Json

### 项目特性

- 🦞 **超小体积** - AOT 编译，打包后**仅约 2MB**，全网最小同类工具
- ✅ **AOT 编译** - 更小的体积、更快的启动速度
- ✅ **跨平台** - 支持 Windows、Linux、macOS
- ✅ **流式对话** - 支持多轮上下文对话
- ✅ **工具调用** - 智能识别并调用合适的工具
- ✅ **错误处理** - 完善的异常捕获与提示
- ✅ **安全配置** - API Key 与代码分离，支持环境变量
- ✅ **编码兼容** - 自动检测文件编码 (UTF-8/GBK)，避免乱码
- ✅ **智能搜索** - 快速定位包含特定关键字的代码文件

---

## 📝 注意事项

1. **API Key 安全**: `appsettings.json` 已加入 `.gitignore`，请勿手动提交包含真实 Key 的文件
2. **命令执行权限**: 工具执行的命令具有与当前用户相同的权限，请谨慎操作
3. **网络依赖**: 需要稳定的网络连接以调用 AI 服务
4. **Token 限制**: 大文件读取会自动截断，防止 Token 溢出
5. **文件修改**: 修改现有文件时，请确保提供精确的 old_content 进行替换

---

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

1. Fork 本项目
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

---

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE) 开源。

---

<div align="center">

**Made with ❤️ by 奶茶叔叔**

如果这个项目对你有帮助，请给一个 ⭐ Star 支持一下！

</div>

