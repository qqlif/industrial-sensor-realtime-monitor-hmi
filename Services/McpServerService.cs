using System.Text.Json;
using System.Text.Json.Nodes;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// MCP (Model Context Protocol) Server 服务
/// 通过标准 I/O 与 AI Agent 通信，提供记忆持久化工具
/// 协议：JSON-RPC over stdin/stdout
/// </summary>
public class McpServerService : IDisposable
{
    private readonly AgentMemoryService _memoryService;  // Agent 记忆持久化服务
    private readonly string _sessionId;                   // 当前会话 ID
    private bool _running;                                // 是否正在运行
    private bool _disposed;                               // 是否已释放

    public McpServerService(AgentMemoryService memoryService)
    {
        _memoryService = memoryService;
        _sessionId = $"session_{DateTime.Now:yyyyMMdd_HHmmss}";
    }

    /// <summary>当前会话 ID</summary>
    public string SessionId => _sessionId;

    /// <summary>
    /// 启动 MCP Server（监听 stdin）
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _running = true;

        // 发送初始化完成通知
        await SendLogAsync($"MCP Server 已启动，会话ID: {_sessionId}");

        // 监听标准输入
        while (_running && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var line = await Console.In.ReadLineAsync(cancellationToken);
                if (line is null) break;

                if (!string.IsNullOrWhiteSpace(line))
                {
                    await HandleRequestAsync(line);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                await SendErrorAsync($"处理请求异常: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 处理 JSON-RPC 请求
    /// </summary>
    private async Task HandleRequestAsync(string requestJson)
    {
        try
        {
            var request = JsonNode.Parse(requestJson);
            if (request is null)
            {
                await SendErrorAsync("无效的请求格式");
                return;
            }

            var method = request["method"]?.GetValue<string>() ?? "";
            var id = request["id"]?.GetValue<int>();

            switch (method)
            {
                case "initialize":
                    await SendResponseAsync(id, new { protocolVersion = "2025-03-26", capabilities = new { tools = true } });
                    break;

                case "tools/list":
                    var toolsList = new List<object>
                    {
                        new
                        {
                            name = "save_memory",
                            description = "保存一条工作记忆，用于跨会话持久化关键决策、分析结果、上下文信息",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    key = new { type = "string", description = "记忆键名，用于检索" },
                                    content = new { type = "string", description = "记忆内容" },
                                    tags = new { type = "string", description = "逗号分隔的标签，用于分类检索" }
                                },
                                required = new[] { "key", "content" }
                            }
                        },
                        new
                        {
                            name = "get_memory",
                            description = "根据键名获取一条工作记忆",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    key = new { type = "string", description = "记忆键名" }
                                },
                                required = new[] { "key" }
                            }
                        },
                        new
                        {
                            name = "search_memories",
                            description = "按标签或关键词搜索工作记忆",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    tag = new { type = "string", description = "标签筛选" },
                                    keyword = new { type = "string", description = "关键词搜索" },
                                    limit = new { type = "number", description = "返回条数上限" }
                                }
                            }
                        },
                        new
                        {
                            name = "record_file_change",
                            description = "记录对项目文件的修改",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new
                                {
                                    filePath = new { type = "string", description = "文件路径" },
                                    changeType = new { type = "string", description = "变更类型: create/modify/delete" },
                                    description = new { type = "string", description = "变更描述" }
                                },
                                required = new[] { "filePath", "changeType" }
                            }
                        },
                        new
                        {
                            name = "get_session_summary",
                            description = "获取当前会话的工作摘要",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new { },
                                required = Array.Empty<string>()
                            }
                        }
                    };

                    await SendResponseAsync(id, new { tools = toolsList });
                    break;

                case "tools/call":
                    var toolName = request["params"]?["name"]?.GetValue<string>() ?? "";
                    var args = request["params"]?["arguments"];

                    switch (toolName)
                    {
                        case "save_memory":
                            await HandleSaveMemoryAsync(id, args);
                            break;
                        case "get_memory":
                            await HandleGetMemoryAsync(id, args);
                            break;
                        case "search_memories":
                            await HandleSearchMemoriesAsync(id, args);
                            break;
                        case "record_file_change":
                            await HandleRecordFileChangeAsync(id, args);
                            break;
                        case "get_session_summary":
                            await HandleGetSessionSummaryAsync(id);
                            break;
                        default:
                            await SendErrorAsync($"未知工具: {toolName}", id);
                            break;
                    }
                    break;

                default:
                    await SendResponseAsync(id, new { });
                    break;
            }
        }
        catch (JsonException ex)
        {
            await SendErrorAsync($"JSON 解析错误: {ex.Message}");
        }
    }

    private async Task HandleSaveMemoryAsync(int? id, JsonNode? args)
    {
        var key = args?["key"]?.GetValue<string>() ?? "";
        var content = args?["content"]?.GetValue<string>() ?? "";
        var tags = args?["tags"]?.GetValue<string>();

        await _memoryService.SaveMemoryAsync(_sessionId, key, content, tags?.Split(',', StringSplitOptions.RemoveEmptyEntries));
        await SendResponseAsync(id, new { success = true, key });
    }

    private async Task HandleGetMemoryAsync(int? id, JsonNode? args)
    {
        var key = args?["key"]?.GetValue<string>() ?? "";
        var content = await _memoryService.GetMemoryAsync(key);
        await SendResponseAsync(id, new { key, content });
    }

    private async Task HandleSearchMemoriesAsync(int? id, JsonNode? args)
    {
        var tag = args?["tag"]?.GetValue<string>();
        var keyword = args?["keyword"]?.GetValue<string>();
        var limit = args?["limit"]?.GetValue<int>() ?? 20;

        var results = await _memoryService.SearchMemoriesAsync(tag, keyword, limit);
        await SendResponseAsync(id, new { memories = results });
    }

    private async Task HandleRecordFileChangeAsync(int? id, JsonNode? args)
    {
        var filePath = args?["filePath"]?.GetValue<string>() ?? "";
        var changeType = args?["changeType"]?.GetValue<string>() ?? "";
        var description = args?["description"]?.GetValue<string>() ?? "";

        await _memoryService.RecordFileChangeAsync(_sessionId, filePath, changeType, description);
        await SendResponseAsync(id, new { success = true });
    }

    private async Task HandleGetSessionSummaryAsync(int? id)
    {
        var changes = await _memoryService.GetFileChangesAsync(_sessionId);
        var memories = await _memoryService.SearchMemoriesAsync(limit: 50);

        var summary = new
        {
            sessionId = _sessionId,
            fileChanges = changes.Count,
            memories = memories.Count,
            recentChanges = changes.Take(10).Select(c => new { c.FilePath, c.ChangeType, c.Description }).ToList()
        };

        await SendResponseAsync(id, summary);
    }

    /// <summary>
    /// 发送 JSON-RPC 响应到 stdout
    /// </summary>
    private async Task SendResponseAsync(int? id, object result)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = JsonSerializer.SerializeToNode(result)
        };
        await WriteLineAsync(response.ToJsonString());
    }

    /// <summary>
    /// 发送 JSON-RPC 错误到 stdout
    /// </summary>
    private async Task SendErrorAsync(string message, int? id = null)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject
            {
                ["code"] = -32000,
                ["message"] = message
            }
        };
        await WriteLineAsync(response.ToJsonString());
    }

    /// <summary>
    /// 发送日志消息到 stderr（MCP 协议约定 stdout 为 JSON-RPC，stderr 为日志）
    /// </summary>
    private async Task SendLogAsync(string message)
    {
        await Console.Error.WriteLineAsync($"[MCP Server] {message}");
    }

    private async Task WriteLineAsync(string json)
    {
        await Console.Out.WriteLineAsync(json);
        await Console.Out.FlushAsync();
    }

    public void Stop()
    {
        _running = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
