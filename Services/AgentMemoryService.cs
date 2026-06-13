using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// Agent 记忆持久化服务
/// 让 AI Agent 可以跨会话存储和检索工作上下文、决策记录、分析结果
/// 基于 SQLite 轻量存储，无需外部数据库服务
/// </summary>
public class AgentMemoryService : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _connection;
    private bool _disposed;
    private string? _sessionId;

    /// <summary>当前会话 ID</summary>
    public string SessionId => _sessionId ??= $"session_{DateTime.Now:yyyyMMdd_HHmmss}";

    public AgentMemoryService()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var memoryDir = Path.Combine(baseDir, "agent_memory");
        Directory.CreateDirectory(memoryDir);
        _dbPath = Path.Combine(memoryDir, "agent_memory.db");
    }

    /// <summary>
    /// 初始化数据库，创建表结构
    /// </summary>
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        await _connection.OpenAsync();

        using var cmd = _connection.CreateCommand();

        // 工作记忆表：存储关键决策、分析结果
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS work_memory (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                key TEXT NOT NULL,
                content TEXT NOT NULL,
                tags TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                updated_at TEXT NOT NULL DEFAULT (datetime('now','localtime'))
            );
            CREATE INDEX IF NOT EXISTS idx_memory_key ON work_memory(key);
            CREATE INDEX IF NOT EXISTS idx_memory_session ON work_memory(session_id);
            CREATE INDEX IF NOT EXISTS idx_memory_tags ON work_memory(tags);
            """;

        await cmd.ExecuteNonQueryAsync();

        // 会话记录表
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                summary TEXT,
                started_at TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                ended_at TEXT
            );
            """;

        await cmd.ExecuteNonQueryAsync();

        // 文件变更记录表：记录 Agent 对项目文件的修改
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS file_changes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                file_path TEXT NOT NULL,
                change_type TEXT NOT NULL,
                description TEXT,
                changed_at TEXT NOT NULL DEFAULT (datetime('now','localtime'))
            );
            """;

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 保存一条工作记忆
    /// </summary>
    public async Task SaveMemoryAsync(string sessionId, string key, string content, string[]? tags = null)
    {
        var conn = await GetConnectionAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO work_memory (session_id, key, content, tags)
            VALUES (@sessionId, @key, @content, @tags)
            ON CONFLICT(key) DO UPDATE SET
                content = @content,
                tags = @tags,
                updated_at = datetime('now','localtime')
            """;
        cmd.Parameters.AddWithValue("@sessionId", sessionId);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@tags", tags is not null ? string.Join(",", tags) : "");
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 获取一条工作记忆
    /// </summary>
    public async Task<string?> GetMemoryAsync(string key)
    {
        var conn = await GetConnectionAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT content FROM work_memory WHERE key = @key ORDER BY updated_at DESC LIMIT 1";
        cmd.Parameters.AddWithValue("@key", key);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    /// <summary>
    /// 按标签搜索工作记忆
    /// </summary>
    public async Task<List<MemoryEntry>> SearchMemoriesAsync(string? tag = null, string? keyword = null, int limit = 20)
    {
        var conn = await GetConnectionAsync();
        using var cmd = conn.CreateCommand();

        var conditions = new List<string>();
        if (!string.IsNullOrEmpty(tag))
        {
            conditions.Add("tags LIKE @tag");
            cmd.Parameters.AddWithValue("@tag", $"%{tag}%");
        }
        if (!string.IsNullOrEmpty(keyword))
        {
            conditions.Add("(content LIKE @keyword OR key LIKE @keyword)");
            cmd.Parameters.AddWithValue("@keyword", $"%{keyword}%");
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        cmd.CommandText = $"SELECT id, session_id, key, content, tags, created_at, updated_at FROM work_memory {whereClause} ORDER BY updated_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);

        var results = new List<MemoryEntry>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new MemoryEntry
            {
                Id = reader.GetInt32(0),
                SessionId = reader.GetString(1),
                Key = reader.GetString(2),
                Content = reader.GetString(3),
                Tags = reader.IsDBNull(4) ? Array.Empty<string>() : reader.GetString(4).Split(',', StringSplitOptions.RemoveEmptyEntries),
                CreatedAt = DateTime.Parse(reader.GetString(5)),
                UpdatedAt = DateTime.Parse(reader.GetString(6))
            });
        }
        return results;
    }

    /// <summary>
    /// 记录一次文件变更
    /// </summary>
    public async Task RecordFileChangeAsync(string sessionId, string filePath, string changeType, string description)
    {
        var conn = await GetConnectionAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO file_changes (session_id, file_path, change_type, description)
            VALUES (@sessionId, @filePath, @changeType, @description)
            """;
        cmd.Parameters.AddWithValue("@sessionId", sessionId);
        cmd.Parameters.AddWithValue("@filePath", filePath);
        cmd.Parameters.AddWithValue("@changeType", changeType);
        cmd.Parameters.AddWithValue("@description", description);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 获取当前会话的文件变更历史
    /// </summary>
    public async Task<List<FileChangeRecord>> GetFileChangesAsync(string sessionId)
    {
        var conn = await GetConnectionAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, file_path, change_type, description, changed_at FROM file_changes WHERE session_id = @sessionId ORDER BY changed_at DESC";
        cmd.Parameters.AddWithValue("@sessionId", sessionId);

        var results = new List<FileChangeRecord>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new FileChangeRecord
            {
                Id = reader.GetInt32(0),
                FilePath = reader.GetString(1),
                ChangeType = reader.GetString(2),
                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ChangedAt = DateTime.Parse(reader.GetString(4))
            });
        }
        return results;
    }

    /// <summary>
    /// 删除过旧的记忆（保留最近 N 条）
    /// </summary>
    public async Task CleanupOldMemoriesAsync(int keepCount = 500)
    {
        var conn = await GetConnectionAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM work_memory WHERE id NOT IN (
                SELECT id FROM work_memory ORDER BY updated_at DESC LIMIT @keepCount
            )
            """;
        cmd.Parameters.AddWithValue("@keepCount", keepCount);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 导出所有记忆为 JSON（用于备份/迁移）
    /// </summary>
    public async Task<string> ExportMemoriesToJsonAsync()
    {
        var memories = await SearchMemoriesAsync(limit: 10000);
        return JsonSerializer.Serialize(memories, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<SqliteConnection> GetConnectionAsync()
    {
        if (_connection is null || _connection.State != System.Data.ConnectionState.Open)
        {
            await InitializeAsync();
        }
        return _connection!;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
    }
}

/// <summary>
/// 记忆条目
/// 存储 AI Agent 的工作记忆，包含键值内容、标签和时间信息
/// </summary>
public class MemoryEntry
{
    /// <summary>数据库自增 ID</summary>
    public int Id { get; set; }

    /// <summary>所属会话 ID</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>记忆键名，用于检索</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>记忆内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>标签数组，用于分类检索</summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 文件变更记录
/// 记录 AI Agent 对项目文件的增、改、删操作
/// </summary>
public class FileChangeRecord
{
    /// <summary>数据库自增 ID</summary>
    public int Id { get; set; }

    /// <summary>文件路径</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>变更类型（create / modify / delete）</summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>变更描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>变更时间</summary>
    public DateTime ChangedAt { get; set; }
}
