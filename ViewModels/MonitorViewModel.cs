using System.Collections.Concurrent;
using System.Windows.Input;
using System.Windows.Threading;
using 工业传感器实时监控上位机.Models;
using 工业传感器实时监控上位机.Services;

namespace 工业传感器实时监控上位机.ViewModels;

/// <summary>
/// 实时监控视图模型
/// 管理仪表盘数据、实时曲线数据，支持多数据源切换（Mock/串口/TCP）
/// </summary>
public class MonitorViewModel : ViewModelBase, IDisposable
{
    private readonly Func<string, ISensorSource> _sourceFactory; // 数据源工厂
    private ISensorSource? _currentSource;                        // 当前数据源实例

    private readonly IDataStorage _dataStorage;      // 数据持久化服务
    private readonly IAlarmService _alarmService;    // 报警检查服务
    private readonly GlobalConfig _config;            // 全局配置
    private readonly Dispatcher _dispatcher;          // UI 线程调度器
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    // 各传感器最新数据
    private SensorData? _temperatureData;
    private SensorData? _pressureData;
    private SensorData? _vibrationData;

    // 曲线数据缓存
    private readonly ConcurrentDictionary<string, List<(DateTime Time, double Value)>> _chartData = new();

    // --- 多数据源切换绑定属性 ---
    private string _selectedSourceType = "Mock";
    /// <summary>当前选中的数据源类型 (Mock, COM, TCP)</summary>
    public string SelectedSourceType
    {
        get => _selectedSourceType;
        set => SetProperty(ref _selectedSourceType, value);
    }

    private string _portName = "COM1";
    /// <summary>串口号</summary>
    public string PortName
    {
        get => _portName;
        set => SetProperty(ref _portName, value);
    }

    private string _tcpHost = "127.0.0.1";
    /// <summary>TCP 主机地址</summary>
    public string TcpHost
    {
        get => _tcpHost;
        set => SetProperty(ref _tcpHost, value);
    }

    private int _tcpPort = 502;
    /// <summary>TCP 端口号</summary>
    public int TcpPort
    {
        get => _tcpPort;
        set => SetProperty(ref _tcpPort, value);
    }
    // -------------------------

    public SensorData? TemperatureData
    {
        get => _temperatureData;
        set => SetProperty(ref _temperatureData, value);
    }

    public SensorData? PressureData
    {
        get => _pressureData;
        set => SetProperty(ref _pressureData, value);
    }

    public SensorData? VibrationData
    {
        get => _vibrationData;
        set => SetProperty(ref _vibrationData, value);
    }

    private string _sourceStatus = "就绪";
    public string SourceStatus
    {
        get => _sourceStatus;
        set => SetProperty(ref _sourceStatus, value);
    }

    private bool _isSourceRunning;
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

    public bool CanStart => !IsSourceRunning;
    public bool CanStop => IsSourceRunning;
    public event Action? ChartDataUpdated;

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    /// <summary>
    /// 构造函数注入数据源工厂和依赖服务
    /// </summary>
    public MonitorViewModel(Func<string, ISensorSource> sourceFactory, IDataStorage dataStorage,
        IAlarmService alarmService, GlobalConfig config)
    {
        _sourceFactory = sourceFactory;
        _dataStorage = dataStorage;
        _alarmService = alarmService;
        _config = config;
        _dispatcher = Dispatcher.CurrentDispatcher;

        StartCommand = new AsyncRelayCommand(_ => StartAsync());
        StopCommand = new RelayCommand(_ => Stop());

        // 初始化曲线缓存
        foreach (var sensor in _config.Sensors)
        {
            _chartData[sensor.Name] = new List<(DateTime, double)>();
        }

        // 初始化默认数据源（模拟）
        SwitchSource();
    }

    /// <summary>
    /// 核心逻辑：切换数据源并初始化参数
    /// 自动释放旧源硬件资源（串口/TCP连接）
    /// </summary>
    private void SwitchSource()
    {
        // 1. 停止并释放旧数据源
        if (_currentSource != null)
        {
            _currentSource.Stop();
            _currentSource.DataReceived -= OnDataReceived;
            _currentSource.StatusChanged -= OnStatusChanged;
            _currentSource.ErrorOccurred -= OnError;

            if (_currentSource is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        // 2. 通过工厂创建新数据源
        _currentSource = _sourceFactory(SelectedSourceType);

        // 3. 将 UI 参数传递给具体的数据源实例
        if (_currentSource is SerialSensorSource comSource)
        {
            comSource.PortName = PortName;
        }
        else if (_currentSource is TcpSensorSource tcpSource)
        {
            tcpSource.Host = TcpHost;
            tcpSource.Port = TcpPort;
        }

        // 4. 重新绑定事件
        _currentSource.DataReceived += OnDataReceived;
        _currentSource.StatusChanged += OnStatusChanged;
        _currentSource.ErrorOccurred += OnError;

        SourceStatus = $"已准备: {_currentSource.SourceName}";
    }

    /// <summary>
    /// 异步启动数据源
    /// </summary>
    private async Task StartAsync()
    {
        try
        {
            // 每次启动前应用最新的 UI 选项和参数
            SwitchSource();

            await _currentSource!.StartAsync(_cts.Token);
            IsSourceRunning = true;
        }
        catch (Exception ex)
        {
            SourceStatus = $"启动失败: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"启动数据源失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 停止数据源
    /// </summary>
    private void Stop()
    {
        _currentSource?.Stop();
        IsSourceRunning = false;
        SourceStatus = "已停止";
    }

    /// <summary>
    /// 数据接收事件处理
    /// 更新仪表盘显示 → 添加到曲线缓存 → 触发曲线刷新 → 报警检查 → 持久化
    /// </summary>
    private void OnDataReceived(SensorData data)
    {
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

        if (_chartData.TryGetValue(data.SensorName, out var list))
        {
            lock (list)
            {
                list.Add((data.Timestamp, data.Value));
                while (list.Count > _config.MaxCachePoints)
                    list.RemoveAt(0);
            }
        }

        _dispatcher.Invoke(() => ChartDataUpdated?.Invoke());

        var sensorConfig = _config.Sensors.FirstOrDefault(s => s.Name == data.SensorName);
        if (sensorConfig != null)
            _alarmService.CheckAlarm(data, sensorConfig);

        _ = _dataStorage.SaveSensorDataAsync(data);
    }

    private void OnStatusChanged(string status)
    {
        _dispatcher.Invoke(() => SourceStatus = status);
    }

    private void OnError(Exception ex)
    {
        _dispatcher.Invoke(() => SourceStatus = $"异常: {ex.Message}");
    }

    public List<(DateTime Time, double Value)> GetChartData(string sensorName)
    {
        if (_chartData.TryGetValue(sensorName, out var list))
        {
            lock (list)
                return new List<(DateTime, double)>(list);
        }
        return new List<(DateTime, double)>();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();

        if (_currentSource != null)
        {
            _currentSource.Stop();
            _currentSource.DataReceived -= OnDataReceived;
            _currentSource.StatusChanged -= OnStatusChanged;
            _currentSource.ErrorOccurred -= OnError;
            if (_currentSource is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
