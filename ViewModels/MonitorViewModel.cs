using System.Collections.Concurrent;
using System.Windows.Input;
using System.Windows.Threading;
using 工业传感器实时监控上位机.Models;
using 工业传感器实时监控上位机.Services;

namespace 工业传感器实时监控上位机.ViewModels;

/// <summary>
/// 实时监控视图模型
/// 管理仪表盘数据、实时曲线数据
/// </summary>
public class MonitorViewModel : ViewModelBase, IDisposable
{
    private readonly ISensorSource _sensorSource;   // 传感器数据源
    private readonly IDataStorage _dataStorage;      // 数据持久化服务
    private readonly IAlarmService _alarmService;    // 报警检查服务
    private readonly GlobalConfig _config;            // 全局配置
    private readonly Dispatcher _dispatcher;          // UI 线程调度器，用于跨线程更新 UI
    private readonly CancellationTokenSource _cts = new(); // 取消标记，用于停止后台任务
    private bool _disposed;                           // 是否已释放

    // 各传感器最新数据（绑定到仪表盘显示）
    private SensorData? _temperatureData;  // 温湿度传感器最新数据
    private SensorData? _pressureData;     // 压力传感器最新数据
    private SensorData? _vibrationData;    // 振动传感器最新数据

    // 曲线数据缓存（按传感器名称，用于实时曲线绘制和导出）
    private readonly ConcurrentDictionary<string, List<(DateTime Time, double Value)>> _chartData = new();

    /// <summary>温湿度传感器最新数据</summary>
    public SensorData? TemperatureData
    {
        get => _temperatureData;
        set => SetProperty(ref _temperatureData, value);
    }

    /// <summary>压力传感器最新数据</summary>
    public SensorData? PressureData
    {
        get => _pressureData;
        set => SetProperty(ref _pressureData, value);
    }

    /// <summary>振动传感器最新数据</summary>
    public SensorData? VibrationData
    {
        get => _vibrationData;
        set => SetProperty(ref _vibrationData, value);
    }

    private string _sourceStatus = "就绪";
    /// <summary>数据源状态文本（就绪/运行中/已停止/异常信息）</summary>
    public string SourceStatus
    {
        get => _sourceStatus;
        set => SetProperty(ref _sourceStatus, value);
    }

    private bool _isSourceRunning;
    /// <summary>数据源是否正在运行</summary>
    public bool IsSourceRunning
    {
        get => _isSourceRunning;
        set
        {
            SetProperty(ref _isSourceRunning, value);
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanStop));
        }
    }

    /// <summary>是否可以启动（数据源未运行时）</summary>
    public bool CanStart => !IsSourceRunning;

    /// <summary>是否可以停止（数据源运行时）</summary>
    public bool CanStop => IsSourceRunning;

    /// <summary>曲线数据更新事件，通知 View 刷新实时曲线</summary>
    public event Action? ChartDataUpdated;

    /// <summary>启动数据源命令</summary>
    public ICommand StartCommand { get; }

    /// <summary>停止数据源命令</summary>
    public ICommand StopCommand { get; }

    public MonitorViewModel(ISensorSource sensorSource, IDataStorage dataStorage,
        IAlarmService alarmService, GlobalConfig config)
    {
        _sensorSource = sensorSource;
        _dataStorage = dataStorage;
        _alarmService = alarmService;
        _config = config;
        _dispatcher = Dispatcher.CurrentDispatcher;

        StartCommand = new AsyncRelayCommand(_ => StartAsync());
        StopCommand = new RelayCommand(_ => Stop());

        _sensorSource.DataReceived += OnDataReceived;
        _sensorSource.StatusChanged += OnStatusChanged;
        _sensorSource.ErrorOccurred += OnError;

        // 初始化曲线缓存
        foreach (var sensor in _config.Sensors)
        {
            _chartData[sensor.Name] = new List<(DateTime, double)>();
        }
    }

    /// <summary>
    /// 异步启动数据源
    /// </summary>
    private async Task StartAsync()
    {
        try
        {
            await _sensorSource.StartAsync(_cts.Token);
            IsSourceRunning = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"启动数据源失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 停止数据源
    /// </summary>
    private void Stop()
    {
        _sensorSource.Stop();
        IsSourceRunning = false;
        SourceStatus = "已停止";
    }

    /// <summary>
    /// 数据接收事件处理
    /// 更新仪表盘显示 → 添加到曲线缓存 → 触发曲线刷新 → 报警检查 → 持久化
    /// </summary>
    private void OnDataReceived(SensorData data)
    {
        // 第一步：更新仪表盘数据（切换到 UI 线程执行，避免跨线程访问绑定属性）
        _dispatcher.Invoke(() =>
        {
            switch (data.SensorType)
            {
                case SensorType.温湿度:
                    TemperatureData = data;
                    break;
                case SensorType.压力:
                    PressureData = data;
                    break;
                case SensorType.振动:
                    VibrationData = data;
                    break;
            }
        });

        // 第二步：添加到曲线缓存（供实时曲线绘制和数据导出使用）
        if (_chartData.TryGetValue(data.SensorName, out var list))
        {
            lock (list)
            {
                list.Add((data.Timestamp, data.Value));
                // 限制缓存数量，超出时移除最旧的数据点
                while (list.Count > _config.MaxCachePoints)
                {
                    list.RemoveAt(0);
                }
            }
        }

        // 第三步：触发曲线刷新事件（通过 Dispatcher 确保在 UI 线程执行）
        _dispatcher.Invoke(() => ChartDataUpdated?.Invoke());

        // 第四步：报警检查，判断数据是否超出阈值
        var sensorConfig = _config.Sensors.FirstOrDefault(s => s.Name == data.SensorName);
        if (sensorConfig != null)
        {
            _alarmService.CheckAlarm(data, sensorConfig);
        }

        // 第五步：异步持久化到存储（不阻塞当前流程）
        _ = _dataStorage.SaveSensorDataAsync(data);
    }

    /// <summary>
    /// 数据源状态变化事件处理
    /// </summary>
    private void OnStatusChanged(string status)
    {
        _dispatcher.Invoke(() => SourceStatus = status);
    }

    /// <summary>
    /// 数据源异常事件处理
    /// </summary>
    private void OnError(Exception ex)
    {
        _dispatcher.Invoke(() => SourceStatus = $"异常: {ex.Message}");
    }

    /// <summary>
    /// 获取指定传感器的曲线缓存数据（线程安全）
    /// </summary>
    /// <param name="sensorName">传感器名称</param>
    /// <returns>时间-数值点列表副本</returns>
    public List<(DateTime Time, double Value)> GetChartData(string sensorName)
    {
        if (_chartData.TryGetValue(sensorName, out var list))
        {
            lock (list)
            {
                return new List<(DateTime, double)>(list);
            }
        }
        return new List<(DateTime, double)>();
    }

    /// <summary>
    /// 释放资源，取消后台任务并取消事件订阅
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();
        _sensorSource.DataReceived -= OnDataReceived;
        _sensorSource.StatusChanged -= OnStatusChanged;
        _sensorSource.ErrorOccurred -= OnError;
    }
}
