using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;

// ==========================================
// 0. 环境初始化与路径修正
// ==========================================
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

// ==========================================
// 1. 配置与初始化阶段
// ==========================================

string GetConfig(string key, string def = "")
{
    var envValue = Environment.GetEnvironmentVariable(key);
    if (envValue != null) return envValue;

    if (File.Exists("appsettings.json"))
    {
        var json = JsonNode.Parse(File.ReadAllText("appsettings.json"));
        var jsonValue = json?[key]?.ToString();
        if (jsonValue != null) return jsonValue;
    }
    return def;
}

if (!File.Exists("appsettings.json"))
{
    var defaultConfig = new JsonObject
    {
        ["ApiKey"] = "your_api_key_here",
        ["Model"] = "qwen3.5-plus",
        ["Endpoint"] = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions"
    };

    var options = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText("appsettings.json", defaultConfig.ToJsonString(options), Encoding.UTF8);

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("检测到缺少配置文件，已自动在当前目录生成默认的 appsettings.json 文件。");
    Console.WriteLine("请替换 'your_api_key_here' 为真实的 ApiKey 后重新运行。");
    Console.ReadKey();
    Console.ResetColor();
    return;
}

// 🌟【核心改动】：新增了记忆管理专属工具
var tools = JsonNode.Parse("""
[
    { "type": "function", "function": { "name": "execute_command", "description": "执行终端命令", "parameters": { "type": "object", "properties": { "command": { "type": "string" } }, "required": ["command"] } } },
    { "type": "function", "function": { "name": "read_file", "description": "读文件", "parameters": { "type": "object", "properties": { "file_path": { "type": "string" } }, "required": ["file_path"] } } },
    { "type": "function", "function": { "name": "write_file", "description": "写文件或局部修改文件。", "parameters": { "type": "object", "properties": { "file_path": { "type": "string" }, "content": { "type": "string" }, "old_content": { "type": "string", "description": "(可选) 精确替换内容。" } }, "required": ["file_path", "content"] } } },
    { "type": "function", "function": { "name": "search_content", "description": "全局搜索关键字。", "parameters": { "type": "object", "properties": { "keyword": { "type": "string" }, "directory": { "type": "string" }, "file_pattern": { "type": "string" } }, "required": ["keyword"] } } },
    { "type": "function", "function": { "name": "update_summary", "description": "覆写当前任务的全局摘要。每当你解决了一个阶段性问题或发现了重要信息，务必调用此工具将最新进度和接下来的计划写入摘要，以免在后续对话中遗忘。", "parameters": { "type": "object", "properties": { "new_summary": { "type": "string", "description": "完整的最新摘要内容，这会完全覆盖旧摘要。" } }, "required": ["new_summary"] } } },
    { "type": "function", "function": { "name": "require_full_context", "description": "请求完整历史记录。如果你发现当前的摘要信息不足以支撑推理（例如需要查看几步之前的具体代码、路径或长段报错），调用此工具，系统会在下一轮附带所有历史对话给你。", "parameters": { "type": "object", "properties": {} } } }
]
""");

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var apiKey = GetConfig("ApiKey");
if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("your")) return;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.Write("请输入本机 Sudo/Admin 密码(用于全自动提权，无密码或不提供请直接回车): ");
Console.ResetColor();
string sudoPassword = ReadPasswordHidden();

string sudoInstruction = string.IsNullOrEmpty(sudoPassword) 
    ? "" 
    : $"用户已授权 sudo 密码。如果遇到提权，请严格使用: echo '{sudoPassword.Replace("'", "'\\''")}' | sudo -S <命令> 。";

// 🌟【核心改动】：动态记忆管理变量
string checkpointPath = "full_history.json";
string summaryPath = "session_summary.txt";
JsonArray fullHistory = new JsonArray();
string currentSummary = "暂无任务进展";

// 加载历史摘要
if (File.Exists(summaryPath)) currentSummary = File.ReadAllText(summaryPath, Encoding.UTF8);

// 加载历史记录并询问是否恢复
if (File.Exists(checkpointPath))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("\n发现上次未完成的任务存档。是否继续执行上次的任务？(Y/N): ");
    var key = Console.ReadKey().Key;
    Console.WriteLine();
    Console.ResetColor();

    if (key == ConsoleKey.Y)
    {
        try
        {
            var savedState = JsonNode.Parse(File.ReadAllText(checkpointPath))?.AsArray();
            if (savedState != null) fullHistory = savedState;
            Console.WriteLine($"✅ 记忆已恢复！当前已有摘要：\n{currentSummary}\n");
        }
        catch { File.Delete(checkpointPath); }
    }
    else
    {
        File.Delete(checkpointPath);
        File.Delete(summaryPath);
        currentSummary = "暂无任务进展";
    }
}

using var client = new HttpClient();
client.Timeout = TimeSpan.FromMinutes(10);
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

// ==========================================
// 2. 界面与交互逻辑
// ==========================================

async Task Think(CancellationToken ct)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.CursorVisible = false;
    for (int i = 0; !ct.IsCancellationRequested; i++)
    {
        Console.Write($"\r {"-\\|/"[i % 4]} 正在思考中...");
        try { await Task.Delay(100, ct); } catch { }
    }
    Console.Write("\r".PadRight(30) + "\r");
    Console.ResetColor();
    Console.CursorVisible = true;
}

Console.Clear();
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("v2.0.0 | 千问跨平台运维工具 (动态摘要架构) by 奶茶叔叔\n");
Console.ResetColor();

while (true)
{
    Console.Write("\n> ");
    var input = Console.ReadLine();

    if (string.IsNullOrEmpty(input)) continue;
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

    // 🌟【核心改动】：本轮交互的局部上下文
    JsonArray currentRoundMessages = new JsonArray();
    bool useFullContext = false; // 标记是否触发了完整上下文加载

    var userMsg = new JsonObject { ["role"] = "user", ["content"] = input };
    currentRoundMessages.Add(userMsg.DeepClone());
    fullHistory.Add(userMsg.DeepClone());
    SaveData(fullHistory, checkpointPath);

    bool isDone = false;

    while (!isDone)
    {
        using var cts = new CancellationTokenSource();
        var animTask = Think(cts.Token);

        // 🌟【核心改动】：组装动态 Payload，节省海量 Token
        string systemPromptText = $@"你是一个高级运维自动化 Agent。当前系统：{RuntimeInformation.OSDescription}。
{sudoInstruction}
【记忆管理规则】：你目前处于【摘要驱动模式】。为节省Token，你默认只能看到下方的【当前任务摘要】和用户的【最新指令】。
如果你发现依据摘要不足以写出代码或执行命令，请立即调用 `require_full_context` 工具获取完整记录。
每完成一个阶段性任务，务必调用 `update_summary` 覆盖更新当前的进展！

【当前任务摘要】：
{currentSummary}";

        var payloadMessages = new JsonArray();
        payloadMessages.Add(new JsonObject { ["role"] = "system", ["content"] = systemPromptText });

        // 根据大模型的意图，决定是塞入完整历史，还是只塞入本轮对话
        if (useFullContext)
        {
            foreach (var msg in fullHistory) payloadMessages.Add(msg.DeepClone());
        }
        else
        {
            foreach (var msg in currentRoundMessages) payloadMessages.Add(msg.DeepClone());
        }

        var payload = new JsonObject
        {
            ["model"] = GetConfig("Model", "qwen3.5-plus"),
            ["messages"] = payloadMessages,
            ["tools"] = tools!.DeepClone(),
            ["enable_search"] = true
        };

        var endpoint = GetConfig("Endpoint", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions");
        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        HttpResponseMessage res;
        
        try 
        {
            res = await client.PostAsync(endpoint, content);
        }
        catch(Exception ex)
        {
            cts.Cancel(); await animTask;
            Console.WriteLine($"\n[网络错误] 请求 API 失败: {ex.Message}");
            break;
        }

        var responseString = await res.Content.ReadAsStringAsync();
        var msg = JsonNode.Parse(responseString)?["choices"]?[0]?["message"];

        cts.Cancel(); await animTask;

        if (msg == null) break;

        currentRoundMessages.Add(msg.DeepClone());
        fullHistory.Add(msg.DeepClone());
        SaveData(fullHistory, checkpointPath);

        var toolCalls = msg["tool_calls"]?.AsArray();
        if (toolCalls != null && toolCalls.Count > 0)
        {
            foreach (var call in toolCalls)
            {
                var fnName = call["function"]?["name"]?.ToString() ?? "";
                var tempArgs = JsonNode.Parse(call["function"]?["arguments"]?.ToString() ?? "{}");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n[Agent 正在调用]: {fnName}");
                Console.ResetColor();

                string result = "";
                // 🌟【核心改动】：处理新的记忆管理工具
                if (fnName == "update_summary")
                {
                    currentSummary = tempArgs?["new_summary"]?.ToString() ?? currentSummary;
                    File.WriteAllText(summaryPath, currentSummary, Encoding.UTF8);
                    result = "[系统提示] 任务摘要已成功更新。";
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[内部状态] 摘要已更新:\n{currentSummary}");
                    Console.ResetColor();
                }
                else if (fnName == "require_full_context")
                {
                    useFullContext = true; // 触发全量加载开关
                    result = "[系统提示] 已为你开启完整历史上下文，请基于刚收到的完整记录继续推理。";
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[内部状态] Agent 主动读取了历史上下文。");
                    Console.ResetColor();
                }
                else // 处理常规工具
                {
                    result = fnName switch
                    {
                        "execute_command" => RunCmd(tempArgs?["command"]?.ToString()),
                        "read_file" => ReadFile(tempArgs?["file_path"]?.ToString()),
                        "write_file" => WriteFile(tempArgs?["file_path"]?.ToString(), tempArgs?["content"]?.ToString(), tempArgs?["old_content"]?.ToString()),
                        "search_content" => SearchContent(tempArgs?["directory"]?.ToString(), tempArgs?["keyword"]?.ToString(), tempArgs?["file_pattern"]?.ToString()),
                        _ => "[未知工具] 不支持的工具调用"
                    };
                }

                var toolResultMsg = new JsonObject
                {
                    ["role"] = "tool",
                    ["name"] = fnName,
                    ["content"] = result,
                    ["tool_call_id"] = call["id"]?.ToString()
                };

                currentRoundMessages.Add(toolResultMsg.DeepClone());
                fullHistory.Add(toolResultMsg.DeepClone());
                SaveData(fullHistory, checkpointPath); 
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"\n{msg["content"]}");
            Console.ResetColor();
            isDone = true; 
        }
    }
}

// ==========================================
// 3. 辅助方法与底层工具
// ==========================================

void SaveData(JsonArray data, string path)
{
    try { File.WriteAllText(path, data.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8); } catch { }
}

string ReadPasswordHidden()
{
    var pwd = new StringBuilder();
    while (true)
    {
        ConsoleKeyInfo i = Console.ReadKey(true);
        if (i.Key == ConsoleKey.Enter) break;
        else if (i.Key == ConsoleKey.Backspace)
        {
            if (pwd.Length > 0) { pwd.Remove(pwd.Length - 1, 1); Console.Write("\b \b"); }
        }
        else if (i.KeyChar != '\u0000') { pwd.Append(i.KeyChar); Console.Write("*"); }
    }
    Console.WriteLine();
    return pwd.ToString();
}

string RunCmd(string? cmd)
{
    if (string.IsNullOrEmpty(cmd)) return "[执行失败] 命令为空";
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"[执行中] {cmd}");
    Console.ResetColor();

    bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    Encoding consoleEncoding = isWin ? Encoding.GetEncoding("GBK") : Encoding.UTF8;

    using var p = new Process();
    p.StartInfo = new ProcessStartInfo(isWin ? "cmd.exe" : "/bin/bash", isWin ? $"/c {cmd}" : $"-c \"{cmd}\"")
    {
        RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        StandardOutputEncoding = consoleEncoding, StandardErrorEncoding = consoleEncoding
    };

    var outputBuilder = new StringBuilder();
    var errorBuilder = new StringBuilder();

    p.OutputDataReceived += (sender, e) => { if (e.Data != null) { Console.WriteLine(e.Data); outputBuilder.AppendLine(e.Data); } };
    p.ErrorDataReceived += (sender, e) => { if (e.Data != null) { Console.ForegroundColor = ConsoleColor.DarkYellow; Console.WriteLine(e.Data); Console.ResetColor(); errorBuilder.AppendLine(e.Data); } };

    try { p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit(); }
    catch (Exception ex) { return $"[执行异常] {ex.Message}"; }

    var errLines = errorBuilder.ToString().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    var outLines = outputBuilder.ToString().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    string finalErr = string.Join("\n", UniversalLogCompressor.CompressLogs(errLines)).Trim();
    string finalOut = string.Join("\n", UniversalLogCompressor.CompressLogs(outLines)).Trim();

    if (!string.IsNullOrWhiteSpace(finalErr)) return $"[标准错误/进度信息]\n{finalErr}\n[标准输出]\n{finalOut}";
    return finalOut;
}

string ReadFile(string? path)
{
    Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine($"[读取文件] {path}"); Console.ResetColor();
    if (path == null || !File.Exists(path)) return "[文件不存在]";
    return ReadTextSmart(path, out _);
}

string WriteFile(string? path, string? content, string? oldContent = null)
{
    if (path == null) return "[写入失败] 路径为空";
    try
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        Encoding targetEncoding = new UTF8Encoding(false);
        string currentText = File.Exists(path) ? ReadTextSmart(path, out targetEncoding) : "";

        if (File.Exists(path) && !string.IsNullOrEmpty(oldContent))
        {
            currentText = currentText.Replace("\r\n", "\n");
            string normalizedOld = oldContent.Replace("\r\n", "\n");
            string normalizedNew = content?.Replace("\r\n", "\n") ?? "";

            if (!currentText.Contains(normalizedOld)) return "[修改失败] 未精准匹配到 old_content，请重新读取文件确认内容。";
            File.WriteAllText(path, currentText.Replace(normalizedOld, normalizedNew), targetEncoding);
            return $"[修改成功] 文件局部已更新：{path}";
        }

        File.WriteAllText(path, content ?? "", targetEncoding);
        return $"[写入成功] 文件已全量保存：{path}";
    }
    catch (Exception ex) { return $"[写入/修改失败] {ex.Message}"; }
}

string ReadTextSmart(string path, out Encoding detectedEncoding)
{
    byte[] bytes = File.ReadAllBytes(path);
    try { detectedEncoding = new UTF8Encoding(false, true); return detectedEncoding.GetString(bytes); }
    catch { detectedEncoding = Encoding.GetEncoding("GBK"); return detectedEncoding.GetString(bytes); }
}

string SearchContent(string? dir, string? keyword, string? pattern)
{
    dir = string.IsNullOrEmpty(dir) ? "." : dir; pattern = string.IsNullOrEmpty(pattern) ? "*.*" : pattern;
    if (string.IsNullOrEmpty(keyword)) return "[搜索失败] 关键字为空";
    Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine($"[搜索] '{keyword}'"); Console.ResetColor();
    if (!Directory.Exists(dir)) return "[搜索失败] 目录不存在";

    try
    {
        var results = new StringBuilder(); int matchCount = 0;
        foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            try
            {
                if (File.ReadAllText(file, Encoding.UTF8).Contains(keyword))
                {
                    results.AppendLine($"- {file}"); matchCount++;
                    if (matchCount >= 15) { results.AppendLine("...[结果过多已截断]..."); break; }
                }
            }
            catch { }
        }
        return matchCount == 0 ? $"[未找到] '{keyword}'" : $"[搜索成功]：\n{results}";
    }
    catch (Exception ex) { return $"[异常] {ex.Message}"; }
}

public class UniversalLogCompressor
{
    public static List<string> CompressLogs(IEnumerable<string> rawLogs)
    {
        var compressedLogs = new List<string>(); var currentBlock = new List<string>();
        foreach (var rawLine in rawLogs)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;
            string line = Regex.Replace(rawLine, @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", "");
            if (currentBlock.Count == 0 || CalculateSimilarityFast(currentBlock.Last(), line) >= 0.7) { currentBlock.Add(line); }
            else { FlushBlock(compressedLogs, currentBlock); currentBlock.Clear(); currentBlock.Add(line); }
        }
        FlushBlock(compressedLogs, currentBlock); return compressedLogs;
    }
    private static void FlushBlock(List<string> result, List<string> block)
    {
        if (block.Count == 0) return;
        if (block.Count <= 2) result.AddRange(block);
        else { result.Add(block.First()); result.Add($"    ... [折叠 {block.Count - 2} 行] ..."); result.Add(block.Last()); }
    }
    private static double CalculateSimilarityFast(string s, string t)
    {
        if (s == t) return 1.0; if (s.Length == 0 || t.Length == 0) return 0.0;
        int max = Math.Max(s.Length, t.Length); if ((double)Math.Min(s.Length, t.Length) / max < 0.5) return 0.0;
        int[] v0 = new int[t.Length + 1], v1 = new int[t.Length + 1];
        for (int i = 0; i < v0.Length; i++) v0[i] = i;
        for (int i = 0; i < s.Length; i++) {
            v1[0] = i + 1;
            for (int j = 0; j < t.Length; j++) v1[j + 1] = Math.Min(Math.Min(v1[j] + 1, v0[j + 1] + 1), v0[j] + (s[i] == t[j] ? 0 : 1));
            for (int j = 0; j < v0.Length; j++) v0[j] = v1[j];
        }
        return 1.0 - ((double)v1[t.Length] / max);
    }
}
