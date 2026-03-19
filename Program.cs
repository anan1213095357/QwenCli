using System;                         // 提供基础类和控制台操作
using System.Diagnostics;             // 提供与系统进程交互的功能（用于执行CMD/Bash命令）
using System.IO;                      // 提供文件和文件夹的读写操作
using System.Net.Http.Headers;        // 提供处理 HTTP 请求头的功能（用于设置API的Token）
using System.Runtime.InteropServices; // 提供获取系统信息的功能（判断是Windows还是Linux/Mac）
using System.Text;                    // 提供文本编码功能（处理UTF-8、GBK等）
using System.Text.Json.Nodes;         // 提供轻量级的 JSON 解析和构建功能
using System.Collections.Generic;     // 【新增】提供 List 等集合功能
using System.Linq;                    // 【新增】提供 LINQ 查询功能
using System.Text.RegularExpressions; // 【新增】提供正则表达式功能

// ==========================================
// 1. 配置与初始化阶段
// ==========================================

// 定义一个获取配置的快捷方法。
// 逻辑：先尝试获取环境变量 -> 如果没有，再从 appsettings.json 文件中读 -> 如果还没有，就用传入的默认值(def)。
string GetConfig(string key, string def = "")
{
    // 尝试读取环境变量
    var envValue = Environment.GetEnvironmentVariable(key);
    if (envValue != null) return envValue;

    // 尝试读取本地配置文件
    if (File.Exists("appsettings.json"))
    {
        var json = JsonNode.Parse(File.ReadAllText("appsettings.json"));
        var jsonValue = json?[key]?.ToString();
        if (jsonValue != null) return jsonValue;
    }

    // 都找不到，返回默认值
    return def;
}

// 🌟【新增功能：自动生成默认配置文件】🌟
if (!File.Exists("appsettings.json"))
{
    var defaultConfig = new JsonObject
    {
        ["ApiKey"] = "your_api_key_here",
        ["Model"] = "qwen3.5-plus",
        ["Endpoint"] = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions"
    };

    // 设置 JSON 序列化选项，使输出带有缩进，方便阅读和修改
    var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText("appsettings.json", defaultConfig.ToJsonString(options), Encoding.UTF8);

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("检测到缺少配置文件，已自动在当前目录生成默认的 appsettings.json 文件。");
    Console.WriteLine("请打开网址 https://bailian.console.aliyun.com/cn-beijing?tab=model#/api-key 申请千问大模型，Ctrl + 鼠标左键打开！");
    Console.WriteLine("请打开该文件，将 'your_api_key_here' 替换为您真实的 千问ApiKey，然后重新运行本程序。");
    Console.ReadKey();
    Console.ResetColor();

    return; // 终止当前运行，等待用户填写配置
}

// 定义 Agent 能够使用的5个核心工具（以 JSON 数组格式定义给 AI 大模型看，告诉它有哪些能力）
var tools = JsonNode.Parse("""
[
    { "type": "function", "function": { "name": "execute_command", "description": "执行终端命令", "parameters": { "type": "object", "properties": { "command": { "type": "string" } }, "required": ["command"] } } },
    { "type": "function", "function": { "name": "read_file", "description": "读文件", "parameters": { "type": "object", "properties": { "file_path": { "type": "string" } }, "required": ["file_path"] } } },
    { "type": "function", "function": { "name": "write_file", "description": "写文件或修改文件。如果源文件存在且只需修改局部，必须提供 old_content 进行精确替换；若是新建文件或打算全量覆盖，则无需提供 old_content。注意：如果该文件存在你必须提供一模一样的旧代码。这个方法只能替换单次如果多地方需要修改，你需要调用多次。", "parameters": { "type": "object", "properties": { "file_path": { "type": "string" }, "content": { "type": "string", "description": "要写入的新内容或替换后的内容" }, "old_content": { "type": "string", "description": "(可选) 源文件中需要被替换的精确文本块。如果不为空，将只在原文中替换该部分。" } }, "required": ["file_path", "content"] } } },
    { "type": "function", "function": { "name": "read_local_image", "description": "看图", "parameters": { "type": "object", "properties": { "file_path": { "type": "string" } }, "required": ["file_path"] } } },
    { "type": "function", "function": { "name": "search_content", "description": "在指定目录下搜索包含特定关键字（如函数名、变量名、类名等）的文件。当你不知道需要修改的代码在哪个文件时，使用此工具查找。", "parameters": { "type": "object", "properties": { "keyword": { "type": "string", "description": "要搜索的代码片段或关键字" }, "directory": { "type": "string", "description": "(可选) 搜索的根目录路径，默认为当前目录 \".\"" }, "file_pattern": { "type": "string", "description": "(可选) 文件通配符，如 \"*.cs\" 或 \"*.json\"，默认 \"*.*\"" } }, "required": ["keyword"] } } }
]
""");

// 注册额外的编码提供程序（为了在 Windows 下支持 GBK 编码输出，防止中文乱码）
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// 将控制台的输入输出统一设置为 UTF-8 编码
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// 获取 API Key
var apiKey = GetConfig("ApiKey");
// 检查 API Key 是否有效
if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("your"))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("错误：未配置千问 API Key！请设置环境变量或修改 appsettings.json。");
    Console.ResetColor(); // 恢复默认颜色
    Console.ReadKey();
    return; // 退出程序
}

// 创建 HTTP 客户端，用于向大模型服务器发送网络请求
using var client = new HttpClient();
// 设置超时时间为 10 分钟（因为大模型思考或生成代码可能需要较长时间）
client.Timeout = TimeSpan.FromMinutes(10);
// 设置请求头中的身份验证信息 (Bearer Token)
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

// 初始化聊天记录数组，并放入第一条 System 提示词（告诉大模型它的身份和规则）
var chatHistory = new JsonArray
{
    new JsonObject
    {
        ["role"] = "system",
        ["content"] = $"你是一个高级运维自动化 Agent。当前运行环境：{RuntimeInformation.OSDescription}。你具备五种核心能力并配有对应工具：1.执行终端命令 2.读取本地文件 3.写入/覆盖本地文件 4.查看本地图片 5.全局搜索包含特定关键字的文件。请根据用户的需求自主推理。禁止回复 emo 表情控制台无法显示。注意：当用户让你修改现有文件的时候你必须看一下当前这个要修改的文件是否存在。如果是修改文件那么用户现有的逻辑就你千万别修改，你只需要修改涉及到的部分就行了，不要删减代码。如果不知道代码在哪，请先用搜索工具查找。"
    }
};

// ==========================================
// 2. 界面与交互逻辑
// ==========================================

// 定义一个异步方法，用于在等待大模型响应时，在控制台显示 "正在思考中..." 的旋转动画
async Task Think(CancellationToken ct)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.CursorVisible = false; // 隐藏控制台光标，让动画看起来更干净

    // 循环播放动画，直到接收到取消信号 (ct.IsCancellationRequested 变为 true)
    for (int i = 0; !ct.IsCancellationRequested; i++)
    {
        // 使用 \r 回到行首覆盖输出，实现动画效果
        Console.Write($"\r {"-\\|/"[i % 4]} 正在思考中...");
        try
        {
            await Task.Delay(100, ct); // 每隔 0.1 秒切换一下动画帧
        }
        catch { /* 忽略任务取消时产生的异常 */ }
    }

    // 思考结束后，清理动画文字并恢复光标
    Console.Write("\r".PadRight(30) + "\r");
    Console.ResetColor();
    Console.CursorVisible = true;
}

// 清屏并打印酷炫的开场 Logo
Console.Clear();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine(@"
    ____                        ________    ____ 
   / __ \_      _____  ____    / ____/ /   /  _/ 
  / / / / | /| / / _ \/ __ \  / /   / /    / /   
 / /_/ /| |/ |/ /  __/ / / / / /___/ /____/ /    
 \____/ |__/|__/\___/_/ /_/  \____/_____/___/    
");
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("v1.5.0 | 千问跨平台运维工具 by 奶茶叔叔");
Console.WriteLine("开源地址： https://github.com/anan1213095357/QwenCli \n");
// 打印当前连接的模型名称
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"终端运维 Agent 已接入系统。当前模型：[ {GetConfig("Model", "qwen3.5-plus")} ]");
Console.ResetColor();

// 进入主对话循环：一直跑，直到用户输入 exit
while (true)
{
    Console.Write("\n> "); // 提示用户输入
    var input = Console.ReadLine(); // 读取用户输入

    // 如果用户直接按回车（没输入任何东西），则跳过本次循环
    if (string.IsNullOrEmpty(input)) continue;

    // 如果用户输入 exit，则跳出循环，结束程序
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

    // 将用户输入的内容加入到聊天记录中
    chatHistory.Add(new JsonObject { ["role"] = "user", ["content"] = input });

    bool isDone = false; // 标记本轮对话是否完成（因为 Agent 可能会连续调用多个工具）

    // 当 Agent 还在调用工具且没有给出最终回复时，保持在这个循环内
    while (!isDone)
    {
        // 创建一个用于取消 "正在思考中" 动画的令牌
        using var cts = new CancellationTokenSource();
        // 启动思考动画任务
        var animTask = Think(cts.Token);

        // 组装要发送给大模型的 JSON 数据载荷
        var payload = new JsonObject
        {
            ["model"] = GetConfig("Model", "qwen3.5-plus"), // 使用的模型
            ["messages"] = chatHistory.DeepClone(),         // 所有的历史对话
            ["tools"] = tools!.DeepClone(),                 // Agent可用的工具
            ["enable_search"] = true                        // 允许联网/搜索
        };

        // 发送 POST 请求到大模型 API 接口
        var endpoint = GetConfig("Endpoint", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions");
        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        var res = await client.PostAsync(endpoint, content);

        // 解析大模型返回的结果
        var responseString = await res.Content.ReadAsStringAsync();
        var msg = JsonNode.Parse(responseString)?["choices"]?[0]?["message"];

        // 收到回复后，立即取消等待动画
        cts.Cancel();
        await animTask;

        // 如果没有收到有效消息，跳出当前处理
        if (msg == null) break;

        // 把大模型的回复（或者它想调用的工具请求）原样存入聊天记录
        chatHistory.Add(msg.DeepClone());

        // 判断大模型是否决定要调用工具 (tool_calls 是否有内容)
        var toolCalls = msg["tool_calls"]?.AsArray();
        if (toolCalls != null && toolCalls.Count > 0)
        {
            // 遍历大模型请求调用的每一个工具
            foreach (var call in toolCalls)
            {
                // 获取工具名称
                var fnName = call["function"]?["name"]?.ToString() ?? "";
                // 解析大模型传给工具的参数
                var tempArgs = JsonNode.Parse(call["function"]?["arguments"]?.ToString() ?? "{}");

                // 在控制台打印提示，告诉用户 Agent 正在偷偷干什么
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n[Agent 正在调用]: {fnName}");
                Console.ResetColor();

                // 使用 C# 8.0 的 switch 表达式，根据工具名字去执行对应的本地方法
                string result = fnName switch
                {
                    "execute_command" => RunCmd(tempArgs?["command"]?.ToString()),
                    "read_file" => ReadFile(tempArgs?["file_path"]?.ToString()),
                    "write_file" => WriteFile(
                                        tempArgs?["file_path"]?.ToString(),
                                        tempArgs?["content"]?.ToString(),
                                        tempArgs?["old_content"]?.ToString()
                                    ),
                    "read_local_image" => ReadImg(tempArgs?["file_path"]?.ToString()),
                    "search_content" => SearchContent(
                                            tempArgs?["directory"]?.ToString(),
                                            tempArgs?["keyword"]?.ToString(),
                                            tempArgs?["file_pattern"]?.ToString()
                                        ),
                    _ => "[未知工具] 不支持的工具调用" // 如果匹配不到已知的工具名
                };

                // 执行完工具后，将工具的执行结果封装成一条消息，存入聊天记录中，以便大模型继续分析
                chatHistory.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["name"] = fnName,
                    ["content"] = result,
                    ["tool_call_id"] = call["id"]?.ToString()
                });
            }
            // 注意：这里没把 isDone 设为 true，所以会继续进入 while(!isDone) 循环，把工具结果发给大模型
        }
        else
        {
            // 如果大模型没有调用工具，而是输出了普通文本，说明它得出结论了
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"\n{msg["content"]}"); // 打印大模型说的话
            Console.ResetColor();
            isDone = true; // 结束本轮对话内部循环，回到主循环等待用户输入下一个问题
        }
    }
}

// ==========================================
// 3. 具体工具的底层实现代码
// ==========================================

// 工具 1：执行终端命令
string RunCmd(string? cmd)
{
    if (string.IsNullOrEmpty(cmd)) return "[执行失败] 命令为空";

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"[执行中] {cmd}");
    Console.ResetColor();

    // 判断当前系统是不是 Windows
    bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    // 如果是 Windows 用 GBK 编码防乱码，否则统一用 UTF-8
    Encoding consoleEncoding = isWin ? Encoding.GetEncoding("GBK") : Encoding.UTF8;

    using var p = new Process();
    // 组装进程启动信息。Windows 使用 cmd.exe，Linux/Mac 使用 /bin/bash
    p.StartInfo = new ProcessStartInfo(isWin ? "cmd.exe" : "/bin/bash", isWin ? $"/c {cmd}" : $"-c \"{cmd}\"")
    {
        RedirectStandardOutput = true, // 允许代码截获标准输出
        RedirectStandardError = true,  // 允许代码截获错误输出
        UseShellExecute = false,       // 必须为 false 才能重定向输出
        CreateNoWindow = true,         // 不弹出黑框框
        StandardOutputEncoding = consoleEncoding,
        StandardErrorEncoding = consoleEncoding
    };

    var outputBuilder = new StringBuilder(); // 用于收集正常输出
    var errorBuilder = new StringBuilder();  // 用于收集错误输出

    // 绑定输出接收事件，一边执行一边在控制台打印，同时存入 StringBuilder
    p.OutputDataReceived += (sender, e) => {
        if (e.Data != null)
        {
            Console.WriteLine(e.Data);
            outputBuilder.AppendLine(e.Data);
        }
    };
    p.ErrorDataReceived += (sender, e) => {
        if (e.Data != null)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(e.Data);
            Console.ResetColor();
            errorBuilder.AppendLine(e.Data);
        }
    };

    try
    {
        p.Start();             // 启动进程
        p.BeginOutputReadLine(); // 开始异步读取输出
        p.BeginErrorReadLine();  // 开始异步读取错误
        p.WaitForExit();       // 阻塞等待命令执行结束
    }
    catch (Exception ex)
    {
        return $"[执行异常] {ex.Message}";
    }

    string err = errorBuilder.ToString();
    string outStr = outputBuilder.ToString();

    // ========= 【核心改动】：在返回前压缩日志，防止 Token 爆炸 =========
    var errLines = err.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    var outLines = outStr.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

    var compressedErr = UniversalLogCompressor.CompressLogs(errLines);
    var compressedOut = UniversalLogCompressor.CompressLogs(outLines);

    string finalErr = string.Join("\n", compressedErr).Trim();
    string finalOut = string.Join("\n", compressedOut).Trim();

    // 如果有错误信息，把错误信息和正常信息一起返回给大模型看
    if (!string.IsNullOrWhiteSpace(finalErr))
    {
        return $"[标准错误/进度信息]\n{finalErr}\n[标准输出]\n{finalOut}";
    }

    return finalOut;
}

// 工具 2：读取文件
string ReadFile(string? path)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"[读取文件] {path}");
    Console.ResetColor();

    if (path == null || !File.Exists(path)) return "[文件不存在]";

    // 调用智能编码读取方法
    return ReadTextSmart(path, out _);
}

// 工具 3：写入/修改文件
string WriteFile(string? path, string? content, string? oldContent = null)
{
    if (path == null) return "[写入失败] 路径为空";
    try
    {
        // 如果指定的目录不存在，先创建多级目录，防止报错
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // 默认采用不带 BOM 的 UTF-8 编码进行写入
        Encoding targetEncoding = new UTF8Encoding(false);
        string currentText = "";

        // 如果文件已经存在，先读出原内容，并获取它的原始编码格式
        if (File.Exists(path))
        {
            currentText = ReadTextSmart(path, out targetEncoding);
        }

        // 逻辑分支：如果是局部替换修改 (oldContent 不为空)
        if (File.Exists(path) && !string.IsNullOrEmpty(oldContent))
        {
            // 统一换行符格式为 \n，防止因为 Windows(\r\n) 和 Linux(\n) 的差异导致匹配失败
            currentText = currentText.Replace("\r\n", "\n");
            string normalizedOld = oldContent.Replace("\r\n", "\n");
            string normalizedNew = content?.Replace("\r\n", "\n") ?? "";

            // 检查要替换的内容是否真的在原文件中存在
            if (!currentText.Contains(normalizedOld))
            {
                return "[修改失败] 未在原文件中找到精准匹配的 old_content。这通常是因为代码缩进或换行不一致导致，请重新读取文件并完全复制原代码块进行替换。";
            }

            // 执行替换操作
            string newText = currentText.Replace(normalizedOld, normalizedNew);
            File.WriteAllText(path, newText, targetEncoding);
            return $"[修改成功] 文件局部内容已更新：{path}";
        }

        // 逻辑分支：全量覆盖或新建文件
        File.WriteAllText(path, content ?? "", targetEncoding);
        return $"[写入成功] 文件已全量保存：{path}";
    }
    catch (Exception ex)
    {
        return $"[写入/修改失败] {ex.Message}";
    }
}

// 辅助工具：智能判断编码读取文本（防止把 GBK 的文件读成乱码）
string ReadTextSmart(string path, out Encoding detectedEncoding)
{
    byte[] bytes = File.ReadAllBytes(path);
    try
    {
        // 尝试用严格的 UTF-8 读取（如果格式不对会抛异常）
        detectedEncoding = new UTF8Encoding(false, true);
        return detectedEncoding.GetString(bytes);
    }
    catch
    {
        // 如果 UTF-8 报错了，就退化使用 GBK 编码读取
        detectedEncoding = Encoding.GetEncoding("GBK");
        return detectedEncoding.GetString(bytes);
    }
}

// 工具 4：读取本地图片并转为 Base64 发给大模型
string ReadImg(string? path)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"[读取图片] {path}");
    Console.ResetColor();

    if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "[文件不存在]";

    try
    {
        var b = File.ReadAllBytes(path);
        // 限制一下大小，如果超过 2MB 就不读了，防止把内存撑爆或者超过API请求上限
        if (b.Length > 2000000) return "[图片过大] 请上传小于 2MB 的图片";

        // 拼装 Base64 图片格式 (Data URI scheme)
        string ext = Path.GetExtension(path).Substring(1); // 获取扩展名，去掉前面的点 (如 jpg)
        return $"data:image/{ext};base64,{Convert.ToBase64String(b)}";
    }
    catch (Exception ex)
    {
        return $"[读取失败] {ex.Message}";
    }
}

// 工具 5：全文搜索关键字（用于找代码写在哪个文件里）
string SearchContent(string? dir, string? keyword, string? pattern)
{
    // 处理默认参数
    dir = string.IsNullOrEmpty(dir) ? "." : dir;           // 默认当前目录
    pattern = string.IsNullOrEmpty(pattern) ? "*.*" : pattern; // 默认匹配所有文件

    if (string.IsNullOrEmpty(keyword)) return "[搜索失败] 关键字不能为空";

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"[搜索文件] 目录: {dir} | 关键词: '{keyword}' | 过滤: {pattern}");
    Console.ResetColor();

    if (!Directory.Exists(dir)) return "[搜索失败] 目录不存在";

    try
    {
        var results = new StringBuilder();
        // 递归遍历目录下所有的文件
        var files = Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories);
        int matchCount = 0;

        foreach (var file in files)
        {
            // 过滤掉项目开发中常见的大体积/无关文件夹，提高搜索效率
            if (file.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            try
            {
                // 读取文件内容并检查是否包含关键字
                string content = File.ReadAllText(file, Encoding.UTF8);
                if (content.Contains(keyword))
                {
                    results.AppendLine($"- {file}"); // 记录匹配到的文件路径
                    matchCount++;

                    // 为了防止结果太多塞爆大模型的上下文窗口，限制最多返回 15 个文件
                    if (matchCount >= 15)
                    {
                        results.AppendLine("...[结果过多，已截断前 15 个文件。请尝试使用更具体的关键字搜索]...");
                        break;
                    }
                }
            }
            catch { /* 忽略没权限读或读取报错的文件 */ }
        }

        if (matchCount == 0) return $"[搜索结果] 未找到包含关键字 '{keyword}' 的文件。";
        return $"[搜索成功] 找到包含关键字的文件列表如下：\n{results}";
    }
    catch (Exception ex)
    {
        return $"[搜索异常] {ex.Message}";
    }
}

// ==========================================
// 4. 新增：全能日志压缩算法类
// ==========================================
public class UniversalLogCompressor
{
    // 相似度阈值：0.0 到 1.0。0.7 意味着只要有 70% 的字符相同，就认为是重复进度
    private const double SimilarityThreshold = 0.7;

    public static List<string> CompressLogs(IEnumerable<string> rawLogs)
    {
        var compressedLogs = new List<string>();
        var currentBlock = new List<string>();

        foreach (var rawLine in rawLogs)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            // 预处理：清洗掉控制台颜色代码（ANSI Escape Codes），这对计算相似度和 LLM 都没有意义
            string line = Regex.Replace(rawLine, @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", "");

            if (currentBlock.Count == 0)
            {
                currentBlock.Add(line);
            }
            else
            {
                // 与当前块的【最后一行】进行比较，允许进度条发生渐变
                double similarity = CalculateSimilarityFast(currentBlock.Last(), line);

                if (similarity >= SimilarityThreshold)
                {
                    currentBlock.Add(line);
                }
                else
                {
                    FlushBlock(compressedLogs, currentBlock);
                    currentBlock.Clear();
                    currentBlock.Add(line);
                }
            }
        }
        
        FlushBlock(compressedLogs, currentBlock);
        return compressedLogs;
    }

    private static void FlushBlock(List<string> result, List<string> block)
    {
        if (block.Count == 0) return;

        if (block.Count <= 2)
        {
            result.AddRange(block);
        }
        else
        {
            // 核心折叠逻辑：保留首尾，说明过程
            result.Add(block.First());
            result.Add($"    ... [盘古 AI: 已折叠 {block.Count - 2} 行高相似度进度/重复输出] ...");
            result.Add(block.Last());
        }
    }

    // 高度优化的快速字符串相似度计算 (0.0 = 完全不同, 1.0 = 完全相同)
    private static double CalculateSimilarityFast(string s, string t)
    {
        if (s == t) return 1.0;
        if (s.Length == 0 || t.Length == 0) return 0.0;

        int maxLength = Math.Max(s.Length, t.Length);
        int minLength = Math.Min(s.Length, t.Length);

        // 剪枝：如果两行长度差异悬殊（比如相差 50% 以上），直接认为不相似，节省 CPU
        if ((double)minLength / maxLength < 0.5) return 0.0;

        // 使用两个一维数组替代二维数组，极大降低 GC 压力
        int[] v0 = new int[t.Length + 1];
        int[] v1 = new int[t.Length + 1];

        for (int i = 0; i < v0.Length; i++) v0[i] = i;

        for (int i = 0; i < s.Length; i++)
        {
            v1[0] = i + 1;
            for (int j = 0; j < t.Length; j++)
            {
                int cost = (s[i] == t[j]) ? 0 : 1;
                v1[j + 1] = Math.Min(Math.Min(v1[j] + 1, v0[j + 1] + 1), v0[j] + cost);
            }
            for (int j = 0; j < v0.Length; j++) v0[j] = v1[j];
        }

        int distance = v1[t.Length];
        return 1.0 - ((double)distance / maxLength);
    }
}
