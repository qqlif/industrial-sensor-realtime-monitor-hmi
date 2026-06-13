using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// 多语言本地化服务（单例）
/// 支持中/英文切换，通过 INotifyPropertyChanged 通知 UI 更新
/// 所有文本资源集中管理，支持从 JSON 文件加载，便于扩展更多语言
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    /// <summary>获取单例实例</summary>
    public static LocalizationService Instance =>
        _instance ??= new LocalizationService();

    private string _currentLanguage = "zh-CN";      // 当前语言代码
    private Dictionary<string, string> _strings = new(); // 当前语言的字符串资源字典

    /// <summary>当前语言代码（如 "zh-CN"、"en-US"）</summary>
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                LoadLanguage(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(GetLocalized));
            }
        }
    }

    /// <summary>支持的语言列表（供 UI 选择器绑定）</summary>
    public ObservableCollection<LanguageInfo> SupportedLanguages { get; } = new()
    {
        new() { Code = "zh-CN", DisplayName = "中文", NativeName = "中文" },
        new() { Code = "en-US", DisplayName = "English", NativeName = "English" }
    };

    /// <summary>索引器，方便 XAML 绑定（如 {Binding [AppTitle]}）</summary>
    [System.Runtime.CompilerServices.IndexerName("Localized")]
    public string this[string key] => GetString(key);

    /// <summary>用于 XAML 绑定的辅助方法</summary>
    public string GetLocalized(string key) => GetString(key);

    public LocalizationService()
    {
        LoadLanguage(_currentLanguage);
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    public string GetString(string key)
    {
        return _strings.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// 带格式化参数的本地化字符串
    /// </summary>
    public string Format(string key, params object[] args)
    {
        var template = GetString(key);
        return string.Format(template, args);
    }

    private void LoadLanguage(string languageCode)
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var langDir = Path.Combine(baseDir, "Languages");
            var filePath = Path.Combine(langDir, $"{languageCode}.json");

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            else
            {
                // 内嵌默认语言资源
                _strings = languageCode switch
                {
                    "zh-CN" => GetChineseStrings(),
                    "en-US" => GetEnglishStrings(),
                    _ => GetEnglishStrings()
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Localization] 加载语言失败: {ex.Message}");
            _strings = GetEnglishStrings();
        }
    }

    /// <summary>
    /// 保存语言文件到磁盘（首次运行时生成）
    /// </summary>
    public void EnsureLanguageFilesExist()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var langDir = Path.Combine(baseDir, "Languages");
        Directory.CreateDirectory(langDir);

        var zhPath = Path.Combine(langDir, "zh-CN.json");
        if (!File.Exists(zhPath))
        {
            var zhJson = JsonSerializer.Serialize(GetChineseStrings(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(zhPath, zhJson);
        }

        var enPath = Path.Combine(langDir, "en-US.json");
        if (!File.Exists(enPath))
        {
            var enJson = JsonSerializer.Serialize(GetEnglishStrings(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(enPath, enJson);
        }
    }

    private Dictionary<string, string> GetChineseStrings() => new()
    {
        // 通用
        ["AppTitle"] = "工业传感器实时监控上位机",
        ["AppSubtitle"] = "工业上位机",
        ["Confirm"] = "确认",
        ["Cancel"] = "取消",
        ["Save"] = "保存",
        ["Load"] = "加载",
        ["Export"] = "导出",
        ["Delete"] = "删除",
        ["Refresh"] = "刷新",
        ["Search"] = "搜索",
        ["Filter"] = "筛选",
        ["Status"] = "状态",
        ["Ready"] = "就绪",
        ["Running"] = "运行中",
        ["Stopped"] = "已停止",
        ["Error"] = "错误",
        ["Warning"] = "警告",
        ["Info"] = "提示",
        ["Success"] = "成功",
        ["Failed"] = "失败",

        // 导航
        ["NavMonitor"] = "实时监控",
        ["NavConfig"] = "参数配置",
        ["NavAlarmLog"] = "报警日志",
        ["NavStatistics"] = "数据统计",

        // 监控页面
        ["MonitorTitle"] = "实时监控",
        ["SourceStatus"] = "数据源状态",
        ["StartSource"] = "启动数据源",
        ["StopSource"] = "停止数据源",
        ["TemperatureSensor"] = "温湿度传感器",
        ["PressureSensor"] = "压力传感器",
        ["VibrationSensor"] = "振动传感器",
        ["Unit"] = "单位",
        ["Alarm"] = "报警",
        ["RealTimeChart"] = "实时数据曲线",
        ["TabTemperature"] = "温湿度",
        ["TabPressure"] = "压力",
        ["TabVibration"] = "振动",

        // 配置页面
        ["ConfigTitle"] = "系统参数配置",
        ["GlobalSettings"] = "全局参数",
        ["SampleInterval"] = "采样间隔 (ms)",
        ["MaxCachePoints"] = "曲线缓存点数",
        ["SensorConfig"] = "传感器配置",
        ["SensorName"] = "名称",
        ["AlarmUpper"] = "报警上限",
        ["AlarmLower"] = "报警下限",
        ["MinValue"] = "最小值",
        ["MaxValue"] = "最大值",
        ["Enabled"] = "启用",
        ["SaveConfig"] = "保存配置",
        ["LoadConfig"] = "加载配置",
        ["ConfigSaved"] = "配置已保存",
        ["ConfigLoaded"] = "配置已加载",

        // 报警页面
        ["AlarmLogTitle"] = "报警日志",
        ["FilterSensorName"] = "传感器",
        ["FilterStart"] = "开始",
        ["FilterEnd"] = "结束",
        ["AlarmTime"] = "时间",
        ["AlarmSensorName"] = "传感器名称",
        ["AlarmSensorType"] = "传感器类型",
        ["AlarmCurrentValue"] = "当前值",
        ["AlarmThreshold"] = "阈值",
        ["AlarmType"] = "报警类型",
        ["AlarmUnit"] = "单位",
        ["ClearAlarms"] = "清除记录",
        ["ExportAlarms"] = "导出日志",
        ["NoAlarmRecords"] = "无报警记录",
        ["AlarmUpperLimit"] = "上限超限",
        ["AlarmLowerLimit"] = "下限超限",

        // 统计页面
        ["StatisticsTitle"] = "数据统计",
        ["Calculate"] = "开始统计",
        ["QuickLastHour"] = "最近1小时",
        ["QuickToday"] = "今天",
        ["QuickLastWeek"] = "最近7天",
        ["DataPointCount"] = "数据点数",
        ["MinValueLabel"] = "最小值",
        ["MaxValueLabel"] = "最大值",
        ["AverageValue"] = "平均值",
        ["StdDeviation"] = "标准差",
        ["Trend"] = "趋势",
        ["AlarmCount"] = "报警次数",
        ["Calculating"] = "正在计算统计...",
        ["CalculateComplete"] = "统计完成",
        ["NoData"] = "指定时间范围内无数据",

        // 数据源
        ["MockDataSource"] = "模拟数据源",
        ["SerialDataSource"] = "串口数据源",
        ["TcpDataSource"] = "TCP 数据源",
        ["ReplayDataSource"] = "历史数据回放",
        ["DataSourceStarting"] = "数据源启动中...",
        ["DataSourceRunning"] = "数据源运行中",
        ["DataSourceStopped"] = "数据源已停止",
        ["DataSourceError"] = "数据源异常",

        // 语言
        ["Language"] = "语言",
        ["LanguageZh"] = "中文",
        ["LanguageEn"] = "English",
    };

    private Dictionary<string, string> GetEnglishStrings() => new()
    {
        // General
        ["AppTitle"] = "Industrial Sensor Monitor",
        ["AppSubtitle"] = "SCADA System",
        ["Confirm"] = "Confirm",
        ["Cancel"] = "Cancel",
        ["Save"] = "Save",
        ["Load"] = "Load",
        ["Export"] = "Export",
        ["Delete"] = "Delete",
        ["Refresh"] = "Refresh",
        ["Search"] = "Search",
        ["Filter"] = "Filter",
        ["Status"] = "Status",
        ["Ready"] = "Ready",
        ["Running"] = "Running",
        ["Stopped"] = "Stopped",
        ["Error"] = "Error",
        ["Warning"] = "Warning",
        ["Info"] = "Info",
        ["Success"] = "Success",
        ["Failed"] = "Failed",

        // Navigation
        ["NavMonitor"] = "Monitor",
        ["NavConfig"] = "Configuration",
        ["NavAlarmLog"] = "Alarm Log",
        ["NavStatistics"] = "Statistics",

        // Monitor Page
        ["MonitorTitle"] = "Real-time Monitor",
        ["SourceStatus"] = "Source Status",
        ["StartSource"] = "Start Source",
        ["StopSource"] = "Stop Source",
        ["TemperatureSensor"] = "Temperature",
        ["PressureSensor"] = "Pressure",
        ["VibrationSensor"] = "Vibration",
        ["Unit"] = "Unit",
        ["Alarm"] = "⚠ Alarm",
        ["RealTimeChart"] = "Real-time Chart",
        ["TabTemperature"] = "Temperature",
        ["TabPressure"] = "Pressure",
        ["TabVibration"] = "Vibration",

        // Config Page
        ["ConfigTitle"] = "System Configuration",
        ["GlobalSettings"] = "Global Settings",
        ["SampleInterval"] = "Sample Interval (ms)",
        ["MaxCachePoints"] = "Max Cache Points",
        ["SensorConfig"] = "Sensor Configuration",
        ["SensorName"] = "Name",
        ["AlarmUpper"] = "Upper Limit",
        ["AlarmLower"] = "Lower Limit",
        ["MinValue"] = "Min Value",
        ["MaxValue"] = "Max Value",
        ["Enabled"] = "Enabled",
        ["SaveConfig"] = "Save Config",
        ["LoadConfig"] = "Load Config",
        ["ConfigSaved"] = "Configuration saved",
        ["ConfigLoaded"] = "Configuration loaded",

        // Alarm Page
        ["AlarmLogTitle"] = "Alarm Log",
        ["FilterSensorName"] = "Sensor",
        ["FilterStart"] = "Start",
        ["FilterEnd"] = "End",
        ["AlarmTime"] = "Time",
        ["AlarmSensorName"] = "Sensor Name",
        ["AlarmSensorType"] = "Type",
        ["AlarmCurrentValue"] = "Value",
        ["AlarmThreshold"] = "Threshold",
        ["AlarmType"] = "Alarm Type",
        ["AlarmUnit"] = "Unit",
        ["ClearAlarms"] = "Clear All",
        ["ExportAlarms"] = "Export",
        ["NoAlarmRecords"] = "No alarm records",
        ["AlarmUpperLimit"] = "Upper Limit",
        ["AlarmLowerLimit"] = "Lower Limit",

        // Statistics Page
        ["StatisticsTitle"] = "Data Statistics",
        ["Calculate"] = "Calculate",
        ["QuickLastHour"] = "Last Hour",
        ["QuickToday"] = "Today",
        ["QuickLastWeek"] = "Last 7 Days",
        ["DataPointCount"] = "Data Points",
        ["MinValueLabel"] = "Min",
        ["MaxValueLabel"] = "Max",
        ["AverageValue"] = "Average",
        ["StdDeviation"] = "Std Dev",
        ["Trend"] = "Trend",
        ["AlarmCount"] = "Alarm Count",
        ["Calculating"] = "Calculating...",
        ["CalculateComplete"] = "Calculation complete",
        ["NoData"] = "No data in selected range",

        // Data Sources
        ["MockDataSource"] = "Mock Data Source",
        ["SerialDataSource"] = "Serial Port",
        ["TcpDataSource"] = "TCP Connection",
        ["ReplayDataSource"] = "Data Replay",
        ["DataSourceStarting"] = "Starting...",
        ["DataSourceRunning"] = "Running",
        ["DataSourceStopped"] = "Stopped",
        ["DataSourceError"] = "Error",

        // Language
        ["Language"] = "Language",
        ["LanguageZh"] = "中文",
        ["LanguageEn"] = "English",
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 语言信息
/// 包含语言代码、显示名称和本地名称
/// </summary>
public class LanguageInfo
{
    /// <summary>语言代码（如 "zh-CN"、"en-US"）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>显示名称（如 "中文"、"English"）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>本地语言名称</summary>
    public string NativeName { get; set; } = string.Empty;
}
