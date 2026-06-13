namespace 工业传感器实时监控上位机.Models;

/// <summary>
/// 单个传感器配置
/// </summary>
public class SensorConfig
{
    /// <summary>传感器名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>传感器类型</summary>
    public SensorType Type { get; set; }

    /// <summary>显示单位</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>报警上限</summary>
    public double AlarmUpper { get; set; }

    /// <summary>报警下限</summary>
    public double AlarmLower { get; set; }

    /// <summary>数据生成最小值（模拟用）</summary>
    public double MinValue { get; set; }

    /// <summary>数据生成最大值（模拟用）</summary>
    public double MaxValue { get; set; }

    /// <summary>是否启用</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 全局配置
/// </summary>
public class GlobalConfig
{
    /// <summary>采样间隔（毫秒）</summary>
    public int SampleIntervalMs { get; set; } = 500;

    /// <summary>曲线缓存点数</summary>
    public int MaxCachePoints { get; set; } = 200;

    /// <summary>传感器配置列表</summary>
    public List<SensorConfig> Sensors { get; set; } = new();
}
