using System.Windows.Input;
using 工业传感器实时监控上位机.Models;
using 工业传感器实时监控上位机.Services;

namespace 工业传感器实时监控上位机.ViewModels;

/// <summary>
/// 配置视图模型
/// 管理传感器阈值、采样频率、曲线缓存点数等系统参数配置
/// 支持保存/加载配置到持久化存储
/// </summary>
public class ConfigViewModel : ViewModelBase
{
    private readonly IDataStorage _dataStorage;    // 数据持久化服务
    private readonly DialogService _dialogService;  // 弹窗服务
    private readonly GlobalConfig _config;           // 全局配置对象（共享引用）

    // 采样间隔（毫秒）
    private int _sampleIntervalMs;
    /// <summary>采样间隔（毫秒）</summary>
    public int SampleIntervalMs
    {
        get => _sampleIntervalMs;
        set => SetProperty(ref _sampleIntervalMs, value);
    }

    // 曲线缓存点数
    private int _maxCachePoints;
    /// <summary>实时曲线缓存的最大数据点数</summary>
    public int MaxCachePoints
    {
        get => _maxCachePoints;
        set => SetProperty(ref _maxCachePoints, value);
    }

    // 各传感器配置（编辑副本，与全局配置分离以避免未保存时影响运行）
    private SensorConfig _tempSensor = new();
    private SensorConfig _pressureSensor = new();
    private SensorConfig _vibrationSensor = new();

    /// <summary>温湿度传感器配置（编辑副本）</summary>
    public SensorConfig TempSensorConfig
    {
        get => _tempSensor;
        set => SetProperty(ref _tempSensor, value);
    }

    /// <summary>压力传感器配置（编辑副本）</summary>
    public SensorConfig PressureSensorConfig
    {
        get => _pressureSensor;
        set => SetProperty(ref _pressureSensor, value);
    }

    /// <summary>振动传感器配置（编辑副本）</summary>
    public SensorConfig VibrationSensorConfig
    {
        get => _vibrationSensor;
        set => SetProperty(ref _vibrationSensor, value);
    }

    /// <summary>保存配置命令</summary>
    public ICommand SaveCommand { get; }

    /// <summary>加载配置命令</summary>
    public ICommand LoadCommand { get; }

    public ConfigViewModel(IDataStorage dataStorage, DialogService dialogService, GlobalConfig config)
    {
        _dataStorage = dataStorage;
        _dialogService = dialogService;
        _config = config;

        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        LoadCommand = new AsyncRelayCommand(_ => LoadAsync());

        // 从配置加载
        LoadFromConfig();
    }

    /// <summary>
    /// 从全局配置对象加载数据到编辑副本
    /// </summary>
    private void LoadFromConfig()
    {
        SampleIntervalMs = _config.SampleIntervalMs;
        MaxCachePoints = _config.MaxCachePoints;

        foreach (var sensor in _config.Sensors)
        {
            switch (sensor.Type)
            {
                case SensorType.温湿度:
                    TempSensorConfig = new SensorConfig
                    {
                        Name = sensor.Name, Type = sensor.Type, Unit = sensor.Unit,
                        AlarmUpper = sensor.AlarmUpper, AlarmLower = sensor.AlarmLower,
                        MinValue = sensor.MinValue, MaxValue = sensor.MaxValue, Enabled = sensor.Enabled
                    };
                    break;
                case SensorType.压力:
                    PressureSensorConfig = new SensorConfig
                    {
                        Name = sensor.Name, Type = sensor.Type, Unit = sensor.Unit,
                        AlarmUpper = sensor.AlarmUpper, AlarmLower = sensor.AlarmLower,
                        MinValue = sensor.MinValue, MaxValue = sensor.MaxValue, Enabled = sensor.Enabled
                    };
                    break;
                case SensorType.振动:
                    VibrationSensorConfig = new SensorConfig
                    {
                        Name = sensor.Name, Type = sensor.Type, Unit = sensor.Unit,
                        AlarmUpper = sensor.AlarmUpper, AlarmLower = sensor.AlarmLower,
                        MinValue = sensor.MinValue, MaxValue = sensor.MaxValue, Enabled = sensor.Enabled
                    };
                    break;
            }
        }
    }

    /// <summary>
    /// 保存配置：将编辑副本的值写回全局配置并持久化
    /// </summary>
    private async Task SaveAsync()
    {
        try
        {
            _config.SampleIntervalMs = SampleIntervalMs;
            _config.MaxCachePoints = MaxCachePoints;

            UpdateSensorInConfig(TempSensorConfig);
            UpdateSensorInConfig(PressureSensorConfig);
            UpdateSensorInConfig(VibrationSensorConfig);

            await _dataStorage.SaveConfigAsync(_config);
            _dialogService.ShowInfo("配置已保存");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"保存配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从持久化存储加载配置并更新界面
    /// </summary>
    private async Task LoadAsync()
    {
        try
        {
            var loaded = await _dataStorage.LoadConfigAsync();
            _config.SampleIntervalMs = loaded.SampleIntervalMs;
            _config.MaxCachePoints = loaded.MaxCachePoints;
            _config.Sensors = loaded.Sensors;

            LoadFromConfig();
            _dialogService.ShowInfo("配置已加载");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"加载配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 将编辑后的传感器配置更新到全局配置对象
    /// </summary>
    /// <param name="edited">编辑后的传感器配置</param>
    private void UpdateSensorInConfig(SensorConfig edited)
    {
        var existing = _config.Sensors.FirstOrDefault(s => s.Type == edited.Type);
        if (existing != null)
        {
            existing.Name = edited.Name;
            existing.Unit = edited.Unit;
            existing.AlarmUpper = edited.AlarmUpper;
            existing.AlarmLower = edited.AlarmLower;
            existing.MinValue = edited.MinValue;
            existing.MaxValue = edited.MaxValue;
            existing.Enabled = edited.Enabled;
        }
    }
}
