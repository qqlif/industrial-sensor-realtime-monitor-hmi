# AI Agent 记忆持久化技能 (MCP)

## 描述
为 AI Agent 提供跨会话记忆持久化能力，基于 SQLite 存储工作上下文、决策记录和文件变更历史。

## 架构
```
AI Agent (Claude Code / Copilot)
		│ JSON-RPC over stdin/stdout
		▼
  McpServerService ─── 协议解析、工具调度
		│
		▼
  AgentMemoryService ─── SQLite 持久化 (agent_memory/agent_memory.db)
		│
		▼
  表: work_memory    ─── 工作记忆（key-value + tags）
  表: file_changes   ─── 文件变更记录
  表: sessions       ─── 会话管理
```

## 核心 API

### 保存记忆
```csharp
await memoryService.SaveMemoryAsync(sessionId, "决策记录", "使用SQLite替代CSV存储", new[] {"架构", "存储"});
```

### 检索记忆
```csharp
var content = await memoryService.GetMemoryAsync("决策记录");
var results = await memoryService.SearchMemoriesAsync(tag: "架构", keyword: "SQLite");
```

### 记录文件变更
```csharp
await memoryService.RecordFileChangeAsync(sessionId, "Services/CsvDataStorage.cs", "modify", "修复时间范围筛选");
```

## MCP 协议工具
| 工具 | 功能 |
|------|------|
| save_memory | 保存工作记忆 |
| get_memory | 按 key 检索 |
| search_memories | 按标签/关键词搜索 |
| record_file_change | 记录文件变更 |
| get_session_summary | 获取会话摘要 |

## 依赖包
```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="9.*" />
```

## 注册到 DI
```csharp
services.AddSingleton<AgentMemoryService>();
services.AddSingleton<McpServerService>();
```
