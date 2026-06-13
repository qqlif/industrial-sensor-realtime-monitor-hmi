using System.IO;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// 历史数据回放服务
/// 从 CSV/SQLite 加载历史数据，按原始时间间隔或加速/减速回放
/// 用于离线分析、演示和调试
/// </summary>
public class DataReplayService : ISensorSource, IDisposable
{
    private readonly IDataStorage _storage;
    private CancellationTokenSource? _cts;
    private Task? _replayTask;
    private bool _disposed;

    // 加载的历史数据
    private List<SensorData> _historicalData = new();
    private int _currentIndex;

    // 回放参数
    private DateTime _startTime;
    private DateTime _endTime;
    private double _speedMultiplier = 1.0; // 1.0 = 原始速度, 2.0 = 两倍速
    private bool _loopEnabled;

    public string SourceName => "历史数据回放";

    public bool IsRunning => _replayTask is { IsCompleted: false };

    /// <summary>回放速度倍率（1.0=原始速度，2.0=两倍速）</summary>
    public double SpeedMultiplier
    {
        get => _speedMultiplier;
        set => _speedMultiplier = Math.Max(0.1, Math.Min(100.0, value));
    }

    /// <summary>是否循环回放</summary>
    public bool LoopEnabled
    {
        get => _loopEnabled;
        set => _loopEnabled = value;
    }

    /// <summary>已加载的数据总量</summary>
    public int TotalDataCount => _historicalData.Count;

    /// <summary>当前回放进度（0.0 ~ 1.0）</summary>
    public double Progress => _historicalData.Count > 0
        ? (double)_currentIndex / _historicalData.Count
        : 0.0;

    /// <summary>回放状态变化事件</summary>
    public event Action<ReplayStatus>? ReplayStatusChanged;

    public event Action<SensorData>? DataReceived;
    public event Action<string>? StatusChanged;
    public event Action<Exception>? ErrorOccurred;

    public DataReplayService(IDataStorage storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// 加载指定时间段的历史数据
    /// </summary>
    public async Task LoadDataAsync(DateTime start, DateTime end)
    {
        _startTime = start;
        _endTime = end;

        // 导出到临时文件再读取（利用 CsvDataStorage 的导出功能）
        var tempFile = Path.Combine(Path.GetTempPath(), $"replay_{Guid.NewGuid():N}.csv");
        try
        {
            await _storage.ExportSensorDataAsync(start, end, tempFile);

            if (File.Exists(tempFile))
            {
                var lines = await File.ReadAllLinesAsync(tempFile);
                _historicalData.Clear();

                foreach (var line in lines.Skip(1)) // 跳过表头
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');

                    if (parts.Length >= 6 &&
                        DateTime.TryParse(parts[0], out var timestamp) &&
                        double.TryParse(parts[3], System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var value))
                    {
                        _historicalData.Add(new SensorData
                        {
                            Timestamp = timestamp,
                            SensorName = parts[1],
                            SensorType = Enum.TryParse<SensorType>(parts[2], out var st) ? st : SensorType.温湿度,
                            Value = value,
                            Unit = parts[4],
                            IsAlarm = bool.TryParse(parts[5], out var alarm) && alarm
                        });
                    }
                }

                _historicalData = _historicalData.OrderBy(d => d.Timestamp).ToList();
                _currentIndex = 0;

                System.Diagnostics.Debug.WriteLine(
                    $"[DataReplayService] 已加载 {_historicalData.Count} 条历史数据，时间范围: {start:HH:mm:ss} ~ {end:HH:mm:ss}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataReplayService] 加载历史数据失败: {ex.Message}");
            ErrorOccurred?.Invoke(ex);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }

        ReplayStatusChanged?.Invoke(new ReplayStatus
        {
            TotalCount = _historicalData.Count,
            CurrentIndex = 0,
            Progress = 0,
            IsLoaded = _historicalData.Count > 0
        });
    }

    /// <summary>
    /// 从内存数据列表直接加载（用于测试或程序内生成的数据）
    /// </summary>
    public void LoadDataFromList(List<SensorData> dataList)
    {
        _historicalData = dataList.OrderBy(d => d.Timestamp).ToList();
        _currentIndex = 0;

        ReplayStatusChanged?.Invoke(new ReplayStatus
        {
            TotalCount = _historicalData.Count,
            CurrentIndex = 0,
            Progress = 0,
            IsLoaded = _historicalData.Count > 0
        });
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning) return Task.CompletedTask;

        if (_historicalData.Count == 0)
        {
            StatusChanged?.Invoke("没有已加载的历史数据");
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        StatusChanged?.Invoke($"开始回放 {_historicalData.Count} 条历史数据 (x{_speedMultiplier:F1})");

        _replayTask = Task.Run(() => ReplayAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        StatusChanged?.Invoke("回放已停止");
    }

    private async Task ReplayAsync(CancellationToken token)
    {
        try
        {
            do
            {
                _currentIndex = 0;

                while (_currentIndex < _historicalData.Count && !token.IsCancellationRequested)
                {
                    var data = _historicalData[_currentIndex];

                    // 计算与上一条数据的时间间隔
                    TimeSpan delay;
                    if (_currentIndex > 0)
                    {
                        var prev = _historicalData[_currentIndex - 1];
                        delay = data.Timestamp - prev.Timestamp;
                    }
                    else
                    {
                        delay = TimeSpan.FromMilliseconds(100); // 默认间隔
                    }

                    // 应用速度倍率
                    var adjustedDelay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds / _speedMultiplier);

                    // 最小延迟 10ms，避免 CPU 空转
                    if (adjustedDelay.TotalMilliseconds < 10)
                        adjustedDelay = TimeSpan.FromMilliseconds(10);

                    await Task.Delay(adjustedDelay, token);

                    if (token.IsCancellationRequested) break;

                    // 更新时间戳为当前时间，模拟实时数据
                    data.Timestamp = DateTime.Now;
                    DataReceived?.Invoke(data);

                    _currentIndex++;

                    // 每 10 条更新一次状态
                    if (_currentIndex % 10 == 0)
                    {
                        var progress = (double)_currentIndex / _historicalData.Count;
                        ReplayStatusChanged?.Invoke(new ReplayStatus
                        {
                            TotalCount = _historicalData.Count,
                            CurrentIndex = _currentIndex,
                            Progress = progress,
                            IsLoaded = true
                        });
                    }
                }

                if (token.IsCancellationRequested) break;

                // 回放完成
                if (_loopEnabled)
                {
                    StatusChanged?.Invoke($"回放完成，重新开始循环 (x{_speedMultiplier:F1})");
                }
                else
                {
                    StatusChanged?.Invoke("回放完成");
                    ReplayStatusChanged?.Invoke(new ReplayStatus
                    {
                        TotalCount = _historicalData.Count,
                        CurrentIndex = _historicalData.Count,
                        Progress = 1.0,
                        IsLoaded = true,
                        IsCompleted = true
                    });
                }

            } while (_loopEnabled && !token.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            StatusChanged?.Invoke($"回放异常: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}

/// <summary>
/// 回放状态信息
/// 包含总数据量、当前进度、是否加载完成等信息
/// </summary>
public class ReplayStatus
{
    /// <summary>总数据条数</summary>
    public int TotalCount { get; set; }

    /// <summary>当前回放到的索引</summary>
    public int CurrentIndex { get; set; }

    /// <summary>回放进度（0.0 ~ 1.0）</summary>
    public double Progress { get; set; }

    /// <summary>数据是否已加载</summary>
    public bool IsLoaded { get; set; }

    /// <summary>回放是否已完成</summary>
    public bool IsCompleted { get; set; }

    /// <summary>进度文本（如 "150 / 200 (75.0%)"）</summary>
    public string ProgressText => $"{CurrentIndex} / {TotalCount} ({Progress * 100:F1}%)";
}
