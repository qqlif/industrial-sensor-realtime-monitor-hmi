using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// SQLite 数据持久化服务
/// 提供高性能的传感器数据和报警记录存储，支持按时间范围查询和导出
/// 作为 CsvDataStorage 的替代方案，适用于高频数据采集场景
/// 线程安全：每个数据库操作创建独立连接（ADO.NET 连接池自动管理），避免共享连接竞态
/// WAL 模式：允许并发读写，消除数据库锁定异常
/// </summary>
public class SqliteDataStorage : IDataStorage, IDisposable
{
    private readonly string _connectionString;  // SQLite 连接字符串（含 WAL 模式）
    private bool _disposed;                      // 是否已释放
    private bool _initialized;                   // 是否已初始化建表
    private readonly SemaphoreSlim _initLock = new(1, 1);     // 初始化锁
    private readonly SemaphoreSlim _writeLock = new(1, 1);     // 写入锁，保证线程安全

    // 批量写入缓冲区（每 100 条刷写一次，减少 I/O 次数）
    private readonly List<SensorData> _sensorBuffer = new();
    private const int FlushInterval = 100; // 每 100 条刷写一次

    public SqliteDataStorage()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = Path.Combine(baseDir, "data");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "sensordb.sqlite");
        // 启用 Shared Cache 模式，提升同一进程内多连接并发性能
        _connectionString = $"Data Source={dbPath};Cache=Shared";
    }

    /// <summary>
    /// 初始化数据库，创建表结构和索引（线程安全，只执行一次）
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();

            // 开启 WAL 模式：允许并发读写，消除 database is locked 异常
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            await cmd.ExecuteNonQueryAsync();

            // 传感器数据表
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS sensor_data (
                    id TEXT PRIMARY KEY,
                    sensor_type INTEGER NOT NULL,
                    sensor_name TEXT NOT NULL,
                    value REAL NOT NULL,
                    unit TEXT NOT NULL,
                    timestamp TEXT NOT NULL,
                    is_alarm INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_sensor_data_name ON sensor_data(sensor_name);
                CREATE INDEX IF NOT EXISTS idx_sensor_data_time ON sensor_data(timestamp);
                CREATE INDEX IF NOT EXISTS idx_sensor_data_name_time ON sensor_data(sensor_name, timestamp);
                """;
            await cmd.ExecuteNonQueryAsync();

            // 报警记录表
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS alarm_records (
                    id TEXT PRIMARY KEY,
                    sensor_name TEXT NOT NULL,
                    sensor_type INTEGER NOT NULL,
                    alarm_time TEXT NOT NULL,
                    current_value REAL NOT NULL,
                    threshold REAL NOT NULL,
                    alarm_type TEXT NOT NULL,
                    unit TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_alarm_records_name ON alarm_records(sensor_name);
                CREATE INDEX IF NOT EXISTS idx_alarm_records_time ON alarm_records(alarm_time);
                CREATE INDEX IF NOT EXISTS idx_alarm_records_name_time ON alarm_records(sensor_name, alarm_time);
                """;
            await cmd.ExecuteNonQueryAsync();

            // 配置表
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS app_config (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );
                """;
            await cmd.ExecuteNonQueryAsync();

            _initialized = true;
            System.Diagnostics.Debug.WriteLine("[SqliteDataStorage] 数据库初始化完成 (WAL模式)");
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 创建并打开新连接（即开即用，利用 ADO.NET 连接池自动复用）
    /// </summary>
    private async Task<SqliteConnection> CreateConnectionAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }

        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task SaveSensorDataAsync(SensorData data)
    {
        await _writeLock.WaitAsync();
        try
        {
            _sensorBuffer.Add(data);
            if (_sensorBuffer.Count >= FlushInterval)
            {
                await FlushSensorBufferAsync();
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveSensorDataBatchAsync(IEnumerable<SensorData> dataList)
    {
        await _writeLock.WaitAsync();
        try
        {
            _sensorBuffer.AddRange(dataList);
            if (_sensorBuffer.Count >= FlushInterval)
            {
                await FlushSensorBufferAsync();
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// 强制刷写缓冲区（在导出前调用确保数据完整性）
    /// </summary>
    public async Task FlushAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            if (_sensorBuffer.Count > 0)
            {
                await FlushSensorBufferAsync();
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task FlushSensorBufferAsync()
    {
        if (_sensorBuffer.Count == 0) return;

        using var conn = await CreateConnectionAsync();
        using var transaction = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            INSERT OR IGNORE INTO sensor_data (id, sensor_type, sensor_name, value, unit, timestamp, is_alarm)
            VALUES (@id, @sensorType, @sensorName, @value, @unit, @timestamp, @isAlarm)
            """;

        var idParam = cmd.CreateParameter();
        idParam.ParameterName = "@id";
        var sensorTypeParam = cmd.CreateParameter();
        sensorTypeParam.ParameterName = "@sensorType";
        var sensorNameParam = cmd.CreateParameter();
        sensorNameParam.ParameterName = "@sensorName";
        var valueParam = cmd.CreateParameter();
        valueParam.ParameterName = "@value";
        var unitParam = cmd.CreateParameter();
        unitParam.ParameterName = "@unit";
        var timestampParam = cmd.CreateParameter();
        timestampParam.ParameterName = "@timestamp";
        var isAlarmParam = cmd.CreateParameter();
        isAlarmParam.ParameterName = "@isAlarm";

        cmd.Parameters.Add(idParam);
        cmd.Parameters.Add(sensorTypeParam);
        cmd.Parameters.Add(sensorNameParam);
        cmd.Parameters.Add(valueParam);
        cmd.Parameters.Add(unitParam);
        cmd.Parameters.Add(timestampParam);
        cmd.Parameters.Add(isAlarmParam);

        try
        {
            foreach (var data in _sensorBuffer)
            {
                idParam.Value = data.Id;
                sensorTypeParam.Value = (int)data.SensorType;
                sensorNameParam.Value = data.SensorName;
                valueParam.Value = data.Value;
                unitParam.Value = data.Unit;
                timestampParam.Value = data.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                isAlarmParam.Value = data.IsAlarm ? 1 : 0;
                await cmd.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SqliteDataStorage] 批量写入失败: {ex.Message}");
            transaction.Rollback();
        }

        _sensorBuffer.Clear();
    }

    public async Task ExportSensorDataAsync(DateTime start, DateTime end, string filePath)
    {
        // 导出前先刷写缓冲区，确保数据完整性
        await FlushAsync();

        using var conn = await CreateConnectionAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT timestamp, sensor_name, sensor_type, value, unit, is_alarm
            FROM sensor_data
            WHERE timestamp >= @start AND timestamp <= @end
            ORDER BY timestamp ASC
            """;
        cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd HH:mm:ss.fff"));

        using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
        await writer.WriteLineAsync("时间,传感器名称,传感器类型,数值,单位,是否报警");

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var time = reader.GetString(0);
            var name = reader.GetString(1);
            var type = ((SensorType)reader.GetInt32(2)).ToString();
            var value = reader.GetDouble(3).ToString("F2", CultureInfo.InvariantCulture);
            var unit = reader.GetString(4);
            var isAlarm = reader.GetInt32(5) == 1 ? "True" : "False";

            await writer.WriteLineAsync($"{time},{name},{type},{value},{unit},{isAlarm}");
        }
    }

    /// <summary>
    /// 按时间范围查询传感器数据，直接返回 SensorData 对象列表
    /// 避免 ExportSensorDataAsync 写临时文件再读回的 I/O 浪费
    /// </summary>
    public async Task<List<SensorData>> QuerySensorDataAsync(DateTime start, DateTime end, string? sensorName = null)
    {
        // 查询前先刷写缓冲区，确保数据完整性
        await FlushAsync();

        var results = new List<SensorData>();

        using var conn = await CreateConnectionAsync();
        using var cmd = conn.CreateCommand();

        if (string.IsNullOrEmpty(sensorName))
        {
            cmd.CommandText = """
                SELECT timestamp, sensor_name, sensor_type, value, unit, is_alarm
                FROM sensor_data
                WHERE timestamp >= @start AND timestamp <= @end
                ORDER BY timestamp ASC
                """;
        }
        else
        {
            cmd.CommandText = """
                SELECT timestamp, sensor_name, sensor_type, value, unit, is_alarm
                FROM sensor_data
                WHERE timestamp >= @start AND timestamp <= @end AND sensor_name = @sensorName
                ORDER BY timestamp ASC
                """;
            cmd.Parameters.AddWithValue("@sensorName", sensorName);
        }

        cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd HH:mm:ss.fff"));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SensorData
            {
                Id = Guid.NewGuid().ToString("N"),
                Timestamp = DateTime.Parse(reader.GetString(0)),
                SensorName = reader.GetString(1),
                SensorType = (SensorType)reader.GetInt32(2),
                Value = reader.GetDouble(3),
                Unit = reader.GetString(4),
                IsAlarm = reader.GetInt32(5) == 1
            });
        }

        return results;
    }

    public async Task SaveAlarmRecordAsync(AlarmRecord record)
    {
        using var conn = await CreateConnectionAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO alarm_records (id, sensor_name, sensor_type, alarm_time, current_value, threshold, alarm_type, unit)
            VALUES (@id, @sensorName, @sensorType, @alarmTime, @currentValue, @threshold, @alarmType, @unit)
            """;
        cmd.Parameters.AddWithValue("@id", record.Id);
        cmd.Parameters.AddWithValue("@sensorName", record.SensorName);
        cmd.Parameters.AddWithValue("@sensorType", (int)record.SensorType);
        cmd.Parameters.AddWithValue("@alarmTime", record.AlarmTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        cmd.Parameters.AddWithValue("@currentValue", record.CurrentValue);
        cmd.Parameters.AddWithValue("@threshold", record.Threshold);
        cmd.Parameters.AddWithValue("@alarmType", record.AlarmType);
        cmd.Parameters.AddWithValue("@unit", record.Unit);

        try
        {
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SqliteDataStorage] 保存报警记录失败: {ex.Message}");
        }
    }

    public async Task ExportAlarmLogAsync(IEnumerable<AlarmRecord> records, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
        await writer.WriteLineAsync("时间,传感器名称,传感器类型,当前值,阈值,报警类型,单位");

        foreach (var record in records)
        {
            await writer.WriteLineAsync(
                $"{record.AlarmTime:yyyy-MM-dd HH:mm:ss.fff},{record.SensorName},{record.SensorType},{record.CurrentValue.ToString("F2", CultureInfo.InvariantCulture)},{record.Threshold.ToString("F2", CultureInfo.InvariantCulture)},{record.AlarmType},{record.Unit}");
        }
    }

    public async Task<GlobalConfig> LoadConfigAsync()
    {
        try
        {
            using var conn = await CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_config WHERE key = 'global_config'";
            var result = await cmd.ExecuteScalarAsync();

            if (result is string json && !string.IsNullOrEmpty(json))
            {
                return JsonSerializer.Deserialize<GlobalConfig>(json) ?? CreateDefaultConfig();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SqliteDataStorage] 加载配置失败: {ex.Message}");
        }

        return CreateDefaultConfig();
    }

    public async Task SaveConfigAsync(GlobalConfig config)
    {
        try
        {
            using var conn = await CreateConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO app_config (key, value)
                VALUES ('global_config', @value)
                """;
            cmd.Parameters.AddWithValue("@value", JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SqliteDataStorage] 保存配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建默认配置（三个典型传感器：温湿度、压力、振动）
    /// </summary>
    private static GlobalConfig CreateDefaultConfig()
    {
        return new GlobalConfig
        {
            SampleIntervalMs = 500,
            MaxCachePoints = 200,
            Sensors = new List<SensorConfig>
            {
                new() { Name = "温湿度传感器", Type = SensorType.温湿度, Unit = "°C", AlarmUpper = 35.0, AlarmLower = 10.0, MinValue = 15.0, MaxValue = 40.0, Enabled = true },
                new() { Name = "压力传感器", Type = SensorType.压力, Unit = "MPa", AlarmUpper = 10.0, AlarmLower = 1.0, MinValue = 2.0, MaxValue = 12.0, Enabled = true },
                new() { Name = "振动传感器", Type = SensorType.振动, Unit = "mm/s", AlarmUpper = 15.0, AlarmLower = 0.5, MinValue = 1.0, MaxValue = 20.0, Enabled = true }
            }
        };
    }

    /// <summary>
    /// 释放资源，刷写剩余缓冲区
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _writeLock.Wait(TimeSpan.FromSeconds(3));
            try
            {
                if (_sensorBuffer.Count > 0)
                {
                    // 使用 Task.Run 避免在 UI 线程同步等待异步操作导致死锁
                    Task.Run(FlushSensorBufferAsync).GetAwaiter().GetResult();
                }
            }
            catch { }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SqliteDataStorage] Dispose 等待锁超时: {ex.Message}");
        }

        _writeLock.Dispose();
        _initLock.Dispose();
    }
}
