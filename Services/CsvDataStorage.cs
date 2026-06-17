using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// CSV/JSON 文件持久化服务
/// 传感器数据按日期分文件存储到 output/ 目录，配置文件存储在 config/ 目录
/// 使用缓冲区批量写入以提高性能，每 50 条刷写一次
/// </summary>
public class CsvDataStorage : IDataStorage
{
    private readonly string _outputDir;          // 数据文件输出目录
    private readonly string _configDir;           // 配置文件目录
    private readonly string _configFilePath;      // 配置文件完整路径
    private readonly SemaphoreSlim _sensorLock = new(1, 1);   // 传感器数据写入锁
    private readonly SemaphoreSlim _alarmLock = new(1, 1);    // 报警记录写入锁
    private readonly List<SensorData> _sensorBuffer = new();  // 传感器数据写入缓冲区
    private const int FlushInterval = 50; // 每 50 条刷写一次到文件

    public CsvDataStorage()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _outputDir = Path.Combine(baseDir, "output");
        _configDir = Path.Combine(baseDir, "config");
        _configFilePath = Path.Combine(_configDir, "appsettings.json");

        Directory.CreateDirectory(_outputDir);
        Directory.CreateDirectory(_configDir);
    }

    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(3);

    public async Task SaveSensorDataAsync(SensorData data)
    {
        if (!await _sensorLock.WaitAsync(LockTimeout))
        {
            System.Diagnostics.Debug.WriteLine("[CsvDataStorage] SaveSensorDataAsync 获取锁超时，跳过写入");
            return;
        }
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
            _sensorLock.Release();
        }
    }

    public async Task SaveSensorDataBatchAsync(IEnumerable<SensorData> dataList)
    {
        if (!await _sensorLock.WaitAsync(LockTimeout))
        {
            System.Diagnostics.Debug.WriteLine("[CsvDataStorage] SaveSensorDataBatchAsync 获取锁超时，跳过写入");
            return;
        }
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
            _sensorLock.Release();
        }
    }

    private async Task FlushSensorBufferAsync()
    {
        if (_sensorBuffer.Count == 0) return;

        var fileName = $"sensor_data_{DateTime.Now:yyyyMMdd}.csv";
        var filePath = Path.Combine(_outputDir, fileName);
        var isNewFile = !File.Exists(filePath);

        try
        {
            using var writer = new StreamWriter(filePath, append: true, Encoding.UTF8);
            if (isNewFile)
            {
                await writer.WriteLineAsync("时间,传感器名称,传感器类型,数值,单位,是否报警");
            }

            foreach (var data in _sensorBuffer)
            {
                await writer.WriteLineAsync(
                    $"{data.Timestamp:yyyy-MM-dd HH:mm:ss.fff},{data.SensorName},{data.SensorType},{data.Value.ToString("F2", CultureInfo.InvariantCulture)},{data.Unit},{data.IsAlarm}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"写入传感器数据文件失败: {ex.Message}");
        }

        _sensorBuffer.Clear();
    }

    public async Task ExportSensorDataAsync(DateTime start, DateTime end, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        // 扫描 output 目录下所有 sensor_data_ 开头的 CSV 文件
        var csvFiles = Directory.GetFiles(_outputDir, "sensor_data_*.csv")
                                .OrderBy(f => f)
                                .ToList();

        using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
        await writer.WriteLineAsync("时间,传感器名称,传感器类型,数值,单位,是否报警");

        foreach (var csvFile in csvFiles)
        {
            try
            {
                // 使用 FileShare.ReadWrite 允许后台写入流同时读取，避免文件占用冲突
                using var fs = new FileStream(csvFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs, Encoding.UTF8);

                string? line;
                var isFirstLine = true;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (isFirstLine) { isFirstLine = false; continue; } // 跳过表头
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',');
                    if (parts.Length < 6) continue;

                    if (DateTime.TryParse(parts[0], out var timestamp))
                    {
                        if (timestamp >= start && timestamp <= end)
                        {
                            await writer.WriteLineAsync(line);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取文件 {csvFile} 失败: {ex.Message}");
            }
        }
    }

    public async Task SaveAlarmRecordAsync(AlarmRecord record)
    {
        if (!await _alarmLock.WaitAsync(LockTimeout))
        {
            System.Diagnostics.Debug.WriteLine("[CsvDataStorage] SaveAlarmRecordAsync 获取锁超时，跳过写入");
            return;
        }
        try
        {
            var fileName = $"alarm_log_{DateTime.Now:yyyyMMdd}.csv";
            var filePath = Path.Combine(_outputDir, fileName);
            var isNewFile = !File.Exists(filePath);

            using var writer = new StreamWriter(filePath, append: true, Encoding.UTF8);
            if (isNewFile)
            {
                await writer.WriteLineAsync("时间,传感器名称,传感器类型,当前值,阈值,报警类型,单位");
            }

            await writer.WriteLineAsync(
                $"{record.AlarmTime:yyyy-MM-dd HH:mm:ss.fff},{record.SensorName},{record.SensorType},{record.CurrentValue.ToString("F2", CultureInfo.InvariantCulture)},{record.Threshold.ToString("F2", CultureInfo.InvariantCulture)},{record.AlarmType},{record.Unit}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"写入报警日志失败: {ex.Message}");
        }
        finally
        {
            _alarmLock.Release();
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

    /// <summary>
    /// 按时间范围查询传感器数据，直接返回 SensorData 对象列表
    /// 避免 ExportSensorDataAsync 写临时文件再读回的 I/O 浪费
    /// </summary>
    public async Task<List<SensorData>> QuerySensorDataAsync(DateTime start, DateTime end, string? sensorName = null)
    {
        var results = new List<SensorData>();
        var csvFiles = Directory.GetFiles(_outputDir, "sensor_data_*.csv")
                                .OrderBy(f => f)
                                .ToList();

        foreach (var csvFile in csvFiles)
        {
            try
            {
                using var fs = new FileStream(csvFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs, Encoding.UTF8);

                string? line;
                var isFirstLine = true;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (isFirstLine) { isFirstLine = false; continue; }
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',');
                    if (parts.Length < 6) continue;

                    if (DateTime.TryParse(parts[0], out var timestamp) &&
                        timestamp >= start && timestamp <= end &&
                        (sensorName == null || parts[1] == sensorName) &&
                        Enum.TryParse<SensorType>(parts[2], out var sensorType) &&
                        double.TryParse(parts[3], System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var value))
                    {
                        results.Add(new SensorData
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Timestamp = timestamp,
                            SensorName = parts[1],
                            SensorType = sensorType,
                            Value = value,
                            Unit = parts[4],
                            IsAlarm = bool.TryParse(parts[5], out var alarm) && alarm
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取文件 {csvFile} 失败: {ex.Message}");
            }
        }

        return results;
    }

    public async Task<GlobalConfig> LoadConfigAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<GlobalConfig>(json) ?? CreateDefaultConfig();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载配置文件失败: {ex.Message}");
        }

        return CreateDefaultConfig();
    }

    public async Task SaveConfigAsync(GlobalConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configFilePath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存配置文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 强制刷写缓冲区（在导出/关闭前调用确保数据完整性）
    /// </summary>
    public async Task FlushAsync()
    {
        if (!await _sensorLock.WaitAsync(LockTimeout))
        {
            System.Diagnostics.Debug.WriteLine("[CsvDataStorage] FlushAsync 获取锁超时");
            return;
        }
        try
        {
            if (_sensorBuffer.Count > 0)
            {
                await FlushSensorBufferAsync();
            }
        }
        finally
        {
            _sensorLock.Release();
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
                new()
                {
                    Name = "温湿度传感器",
                    Type = SensorType.温湿度,
                    Unit = "°C",
                    AlarmUpper = 35.0,
                    AlarmLower = 10.0,
                    MinValue = 15.0,
                    MaxValue = 40.0,
                    Enabled = true
                },
                new()
                {
                    Name = "压力传感器",
                    Type = SensorType.压力,
                    Unit = "MPa",
                    AlarmUpper = 10.0,
                    AlarmLower = 1.0,
                    MinValue = 2.0,
                    MaxValue = 12.0,
                    Enabled = true
                },
                new()
                {
                    Name = "振动传感器",
                    Type = SensorType.振动,
                    Unit = "mm/s",
                    AlarmUpper = 15.0,
                    AlarmLower = 0.5,
                    MinValue = 1.0,
                    MaxValue = 20.0,
                    Enabled = true
                }
            }
        };
    }
}
