using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
string GetConfig(string key, string def = "") => Environment.GetEnvironmentVariable(key) ?? (File.Exists("appsettings.json") ? JsonNode.Parse(File.ReadAllText("appsettings.json"))?[key]?.ToString() : null) ?? def;
var tools = JsonNode.Parse("""
[
    { "type": "function", "function": { "name": "execute_command", "description": "执行终端命令", "parameters": { "type": "object", "properties": { "command": { "type": "string" } }, "required": ["command"] } } },
    { "type": "function", "function": { "name": "read_file", "description": "读文件", "parameters": { "type": "object", "properties": { "file_path": { "type": "string" } }, "required": ["file_path"] } } },
    { "type": "function", "function": { "name": "write_file", "description": "写文件或修改文件。如果源文件存在且只需修改局部，必须提供 old_content 进行精确替换；若是新建文件或打算全量覆盖，则无需提供 old_content。注意：如果该文件存在你必须提供一模一样的旧代码。这个方法只能替换单次如果多地方需要修改，你需要调用多次。", "parameters": { "type": "object", "properties": { "file_path": { "type": "string" }, "content": { "type": "string", "description": "要写入的新内容或替换后的内容" }, "old_content": { "type": "string", "description": "(可选) 源文件中需要被替换的精确文本块。如果不为空，将只在原文中替换该部分。" } }, "required": ["file_path", "content"] } } },
    { "type": "function", "function": { "name": "read_local_image", "description": "看图", "parameters": { "type": "object", "properties": { "file_path": { "type": "string" } }, "required": ["file_path"] } } },
    { "type": "function", "function": { "name": "search_content", "description": "在指定目录下搜索包含特定关键字（如函数名、变量名、类名等）的文件。当你不知道需要修改的代码在哪个文件时，使用此工具查找。", "parameters": { "type": "object", "properties": { "keyword": { "type": "string", "description": "要搜索的代码片段或关键字" }, "directory": { "type": "string", "description": "(可选) 搜索的根目录路径，默认为当前目录 \".\"" }, "file_pattern": { "type": "string", "description": "(可选) 文件通配符，如 \"*.cs\" 或 \"*.json\"，默认 \"*.*\"" } }, "required": ["keyword"] } } }
]
""");
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;
var apiKey = GetConfig("ApiKey"); 
if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("your"))
{
    Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("错误：未配置 API Key！请设置环境变量或修改 appsettings.json。"); Console.ResetColor();
    return;
}
using var client = new HttpClient(); client.Timeout = TimeSpan.FromMinutes(10);
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
var chatHistory = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = $"你是一个高级运维自动化 Agent。当前运行环境：{RuntimeInformation.OSDescription}。你具备五种核心能力并配有对应工具：1.执行终端命令 2.读取本地文件 3.写入/覆盖本地文件 4.查看本地图片 5.全局搜索包含特定关键字的文件。请根据用户的需求自主推理。禁止回复 emo 表情控制台无法显示。注意：当用户让你修改现有文件的时候你必须看一下当前这个要修改的文件是否存在。如果是修改文件那么用户现有的逻辑就你千万别修改，你只需要修改涉及到的部分就行了，不要删减代码。如果不知道代码在哪，请先用搜索工具查找。" } };
async Task Think(CancellationToken ct)
{
    Console.ForegroundColor = ConsoleColor.Cyan; Console.CursorVisible = false;
    for (int i = 0; !ct.IsCancellationRequested; i++) { Console.Write($"\r {"-\\|/"[i % 4]} 正在思考中..."); try { await Task.Delay(100, ct); } catch { } }
    Console.Write("\r".PadRight(30) + "\r"); Console.ResetColor(); Console.CursorVisible = true;
}
Console.Clear(); Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine(@"
   ____                       ________    ____ 
  / __ \_      _____  ____   / ____/ /   /  _/ 
 / / / / | /| / / _ \/ __ \ / /   / /    / /   
/ /_/ /| |/ |/ /  __/ / / // /___/ /____/ /    
\___\_\|__/|__/\___/_/ /_/ \____/_____/___/    
");
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("v1.5.0 | 千问跨平台运维工具 by 奶茶叔叔\n");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"终端运维 Agent 已接入系统。当前模型：[ {GetConfig("Model", "qwen3.5-plus")} ]"); Console.ResetColor();
while (true)
{
    Console.Write("\n> ");
    if (Console.ReadLine() is not { Length: > 0 } input) continue;
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
    chatHistory.Add(new JsonObject { ["role"] = "user", ["content"] = input });
    bool isDone = false;
    while (!isDone)
    {
        using var cts = new CancellationTokenSource();
        var animTask = Think(cts.Token);
        var payload = new JsonObject { ["model"] = GetConfig("Model", "qwen3.5-plus"), ["messages"] = chatHistory.DeepClone(), ["tools"] = tools!.DeepClone(), ["enable_search"] = true };
        var res = await client.PostAsync(GetConfig("Endpoint", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions"), new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"));
        var msg = JsonNode.Parse(await res.Content.ReadAsStringAsync())?["choices"]?[0]?["message"];
        cts.Cancel(); await animTask;
        if (msg == null) break;
        chatHistory.Add(msg.DeepClone());
        if (msg["tool_calls"]?.AsArray() is { Count: > 0 } calls)
        {
            foreach (var call in calls)
            {
                var fn = call["function"]?["name"]?.ToString() ?? "";
                var tempArgs = JsonNode.Parse(call["function"]?["arguments"]?.ToString() ?? "{}");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n[Agent 正在调用]: {fn}"); Console.ResetColor();
                string result = fn switch
                {
                    "execute_command" => RunCmd(tempArgs?["command"]?.ToString()),
                    "read_file" => ReadFile(tempArgs?["file_path"]?.ToString()),
                    "write_file" => WriteFile(tempArgs?["file_path"]?.ToString(), tempArgs?["content"]?.ToString(), tempArgs?["old_content"]?.ToString()),
                    "read_local_image" => ReadImg(tempArgs?["file_path"]?.ToString()),
                    "search_content" => SearchContent(tempArgs?["directory"]?.ToString(), tempArgs?["keyword"]?.ToString(), tempArgs?["file_pattern"]?.ToString()),
                    _ => "[未知工具] 不支持的工具调用"
                };
                chatHistory.Add(new JsonObject { ["role"] = "tool", ["name"] = fn, ["content"] = result, ["tool_call_id"] = call["id"]?.ToString() });
            }
        }
        else { Console.ForegroundColor = ConsoleColor.White; Console.WriteLine($"\n{msg["content"]}"); Console.ResetColor(); isDone = true; }
    }
}
string RunCmd(string? cmd)
{
    if (string.IsNullOrEmpty(cmd)) return "[执行失败] 命令为空";
    Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine($"[执行中] {cmd}"); Console.ResetColor();
    bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    Encoding consoleEncoding = isWin ? Encoding.GetEncoding("GBK") : Encoding.UTF8;
    using var p = new Process();
    p.StartInfo = new ProcessStartInfo(isWin ? "cmd.exe" : "/bin/bash", isWin ? $"/c {cmd}" : $"-c \"{cmd}\"")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = consoleEncoding,
        StandardErrorEncoding = consoleEncoding
    };
    var outputBuilder = new StringBuilder();
    var errorBuilder = new StringBuilder();
    p.OutputDataReceived += (sender, e) => { if (e.Data != null) { Console.WriteLine(e.Data); outputBuilder.AppendLine(e.Data); } };
    p.ErrorDataReceived += (sender, e) => { if (e.Data != null) { Console.ForegroundColor = ConsoleColor.DarkYellow; Console.WriteLine(e.Data); Console.ResetColor(); errorBuilder.AppendLine(e.Data); } };
    try { p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit(); }
    catch (Exception ex) { return $"[执行异常] {ex.Message}"; }
    string err = errorBuilder.ToString();
    string outStr = outputBuilder.ToString();
    if (!string.IsNullOrWhiteSpace(err)) return $"[标准错误/进度信息]\n{err}\n[标准输出]\n{outStr}";
    return outStr;
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
        if (Path.GetDirectoryName(path) is { Length: > 0 } dir) Directory.CreateDirectory(dir);
        Encoding targetEncoding = new UTF8Encoding(false);
        string currentText = "";
        if (File.Exists(path)) currentText = ReadTextSmart(path, out targetEncoding);
        if (File.Exists(path) && !string.IsNullOrEmpty(oldContent))
        {
            currentText = currentText.Replace("\r\n", "\n");
            string normalizedOld = oldContent.Replace("\r\n", "\n");
            string normalizedNew = content?.Replace("\r\n", "\n") ?? "";
            if (!currentText.Contains(normalizedOld))
                return "[修改失败] 未在原文件中找到精准匹配的 old_content。这通常是因为代码缩进或换行不一致导致，请重新读取文件并完全复制原代码块进行替换。";
            string newText = currentText.Replace(normalizedOld, normalizedNew);
            File.WriteAllText(path, newText, targetEncoding);
            return $"[修改成功] 文件局部内容已更新：{path}";
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
string ReadImg(string? path)
{
    Console.ForegroundColor = ConsoleColor.DarkGray; Console.WriteLine($"[读取图片] {path}"); Console.ResetColor();
    if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "[文件不存在]";
    try { var b = File.ReadAllBytes(path); return b.Length > 2000000 ? "[图片过大] 请上传小于 2MB 的图片" : $"data:image/{Path.GetExtension(path)[1..]};base64,{Convert.ToBase64String(b)}"; }
    catch (Exception ex) { return $"[读取失败] {ex.Message}"; }
}
string SearchContent(string? dir, string? keyword, string? pattern)
{
    dir = string.IsNullOrEmpty(dir) ? "." : dir;
    pattern = string.IsNullOrEmpty(pattern) ? "*.*" : pattern;
    if (string.IsNullOrEmpty(keyword)) return "[搜索失败] 关键字不能为空";
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"[搜索文件] 目录: {dir} | 关键词: '{keyword}' | 过滤: {pattern}");
    Console.ResetColor();
    if (!Directory.Exists(dir)) return "[搜索失败] 目录不存在";
    try
    {
        var results = new StringBuilder();
        var files = Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories);
        int matchCount = 0;
        foreach (var file in files)
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}"))
                continue;
            try
            {
                string content = File.ReadAllText(file, Encoding.UTF8);
                if (content.Contains(keyword))
                {
                    results.AppendLine($"- {file}");
                    matchCount++;
                    if (matchCount >= 15)
                    {
                        results.AppendLine("...[结果过多，已截断前 15 个文件。请尝试使用更具体的关键字搜索]..."); break;
                    }
                }
            }
            catch { }
        }
        if (matchCount == 0) return $"[搜索结果] 未找到包含关键字 '{keyword}' 的文件。";
        return $"[搜索成功] 找到包含关键字的文件列表如下：\n{results}";
    }
    catch (Exception ex) { return $"[搜索异常] {ex.Message}"; }
}