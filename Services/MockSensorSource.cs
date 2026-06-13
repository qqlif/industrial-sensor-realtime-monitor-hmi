using System.Collections.Concurrent;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// 模拟传感器数据源
/// 根据配置生成温湿度、压力、振动的周期性随机数据
/// 用于开发和测试阶段，无需真实硬件即可验证功能
/// </summary>
public class MockSensorSource : ISensorSource, IDisposable
{
    private readonly GlobalConfig _config;          // 全局配置，包含传感器参数和采样间隔
    private CancellationTokenSource? _cts;           // 取消标记，用于停止后台采集任务
    private Task? _runningTask;                      // 后台数据采集任务
    private readonly Random _random = new();         // 随机数生成器，用于模拟波动
    private readonly ConcurrentDictionary<string, double> _currentValues = new(); // 各传感器当前值缓存

    /// <summary>数据源名称</summary>
    public string SourceName => "模拟数据源";

    /// <summary>是否正在运行</summary>
    public bool IsRunning => _cts is { IsCancellationRequested: false } && _runningTask is { IsCompleted: false };

    /// <summary>数据到达事件</summary>
    public event Action<SensorData>? DataReceived;

    /// <summary>状态变化事件</summary>
    public event Action<string>? StatusChanged;

    /// <summary>异常事件</summary>
    public event Action<Exception>? ErrorOccurred;

    public MockSensorSource(GlobalConfig config)
    {
        _config = config;
        // 初始化各传感器的当前值在中间范围，使数据从中间值开始波动
        foreach (var sensor in _config.Sensors)
        {
            var mid = (sensor.MinValue + sensor.MaxValue) / 2;
            _currentValues[sensor.Name] = mid;
        }
    }

    /// <summary>
    /// 启动模拟数据采集
    /// 在后台线程中按配置的采样间隔周期性生成各传感器数据
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        StatusChanged?.Invoke("数据源启动中...");

        _runningTask = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止模拟数据采集
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        StatusChanged?.Invoke("数据源已停止");
    }

    /// <summary>
    /// 后台数据采集主循环
    /// 依次遍历所有启用的传感器，按采样间隔生成数据并触发 DataReceived 事件
    /// </summary>
    private async Task RunAsync(CancellationToken token)
    {
        StatusChanged?.Invoke("数据源运行中");

        try
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var sensor in _config.Sensors.Where(s => s.Enabled))
                {
                    if (token.IsCancellationRequested) break;

                    var newValue = GenerateSimulatedValue(sensor);
                    _currentValues[sensor.Name] = newValue;

                    var data = new SensorData
                    {
                        SensorType = sensor.Type,
                        SensorName = sensor.Name,
                        Value = Math.Round(newValue, 2),
                        Unit = sensor.Unit,
                        Timestamp = DateTime.Now,
                        IsAlarm = newValue > sensor.AlarmUpper || newValue < sensor.AlarmLower
                    };

                    DataReceived?.Invoke(data);

                    // 每个传感器之间间隔一小段时间，模拟真实采样间隔
                    await Task.Delay(_config.SampleIntervalMs / Math.Max(_config.Sensors.Count(s => s.Enabled), 1), token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止，不做额外处理
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            StatusChanged?.Invoke($"数据源异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成模拟数据，带小幅随机波动和漂移趋势
    /// 偶尔产生毛刺信号以模拟真实传感器干扰
    /// </summary>
    private double GenerateSimulatedValue(SensorConfig sensor)
    {
        var current = _currentValues.GetValueOrDefault(sensor.Name, (sensor.MinValue + sensor.MaxValue) / 2);
        var range = sensor.MaxValue - sensor.MinValue;

        // 随机波动：±5% 范围
        var fluctuation = (_random.NextDouble() - 0.5) * range * 0.1;

        // 偶尔产生毛刺（模拟传感器干扰）
        if (_random.NextDouble() < 0.02)
        {
            fluctuation += (_random.NextDouble() - 0.5) * range * 0.3;
        }

        var newValue = current + fluctuation;

        // 限制在传感器量程范围内
        return Math.Clamp(newValue, sensor.MinValue, sensor.MaxValue);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
