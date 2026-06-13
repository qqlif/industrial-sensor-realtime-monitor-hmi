using System.Text.Json.Serialization;

namespace 工业传感器实时监控上位机.Models;

/// <summary>
/// 报警记录
/// 记录传感器超限事件的时间、数值、阈值等详细信息
/// </summary>
public class AlarmRecord
{
    /// <summary>报警记录唯一标识（GUID）</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>触发报警的传感器名称</summary>
    [JsonPropertyName("sensorName")]
    public string SensorName { get; set; } = string.Empty;

    /// <summary>传感器类型</summary>
    [JsonPropertyName("sensorType")]
    public SensorType SensorType { get; set; }

    /// <summary>报警发生时间</summary>
    [JsonPropertyName("alarmTime")]
    public DateTime AlarmTime { get; set; } = DateTime.Now;

    /// <summary>触发报警时的传感器当前值</summary>
    [JsonPropertyName("currentValue")]
    public double CurrentValue { get; set; }

    /// <summary>触发报警的阈值（上限或下限）</summary>
    [JsonPropertyName("threshold")]
    public double Threshold { get; set; }

    /// <summary>报警类型（"上限超限" / "下限超限"）</summary>
    [JsonPropertyName("alarmType")]
    public string AlarmType { get; set; } = string.Empty;

    /// <summary>数值单位</summary>
    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;
}
