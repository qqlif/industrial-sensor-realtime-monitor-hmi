using System.Text.Json.Serialization;

namespace 工业传感器实时监控上位机.Models;

/// <summary>
/// 传感器数据类型枚举
/// </summary>
public enum SensorType
{
    温湿度,
    压力,
    振动
}

/// <summary>
/// 单次传感器采样数据
/// </summary>
public class SensorData
{
    /// <summary>数据唯一标识（GUID）</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>传感器类型（温湿度/压力/振动）</summary>
    [JsonPropertyName("sensorType")]
    public SensorType SensorType { get; set; }

    /// <summary>传感器名称</summary>
    [JsonPropertyName("sensorName")]
    public string SensorName { get; set; } = string.Empty;

    /// <summary>当前采样值</summary>
    [JsonPropertyName("value")]
    public double Value { get; set; }

    /// <summary>数值单位（如 °C、MPa、mm/s）</summary>
    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    /// <summary>采样时间戳</summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>是否处于报警状态</summary>
    [JsonPropertyName("isAlarm")]
    public bool IsAlarm { get; set; }
}
