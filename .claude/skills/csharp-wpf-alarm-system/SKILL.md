# C# WPF 报警系统技能

## 描述
工业上位机多传感器阈值报警系统的标准实现。

## 接口设计
```csharp
public interface IAlarmService
{
	ObservableCollection<AlarmRecord> AlarmRecords { get; }
	bool CheckAlarm(SensorData data, SensorConfig config);
	event Action<AlarmRecord>? AlarmTriggered;
	void ClearAlarms();
	IEnumerable<AlarmRecord> FilterAlarms(string? sensorName, DateTime? start, DateTime? end);
}
```

## 报警记录模型
```csharp
public class AlarmRecord
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string SensorName { get; set; } = string.Empty;
	public SensorType SensorType { get; set; }
	public DateTime AlarmTime { get; set; } = DateTime.Now;
	public double CurrentValue { get; set; }
	public double Threshold { get; set; }
	public string AlarmType { get; set; } = string.Empty; // "上限超限" / "下限超限"
	public string Unit { get; set; } = string.Empty;
}
```

## 报警检查逻辑
```csharp
public bool CheckAlarm(SensorData data, SensorConfig config)
{
	if (data.Value > config.AlarmUpper)
	{
		AddAlarmRecord(new AlarmRecord
		{
			SensorName = data.SensorName,
			AlarmTime = data.Timestamp,
			CurrentValue = data.Value,
			Threshold = config.AlarmUpper,
			AlarmType = "上限超限",
			Unit = data.Unit
		});
		return true;
	}
	if (data.Value < config.AlarmLower)
	{
		AddAlarmRecord(new AlarmRecord
		{
			SensorName = data.SensorName,
			AlarmTime = data.Timestamp,
			CurrentValue = data.Value,
			Threshold = config.AlarmLower,
			AlarmType = "下限超限",
			Unit = data.Unit
		});
		return true;
	}
	return false;
}
```

## 去重机制
```csharp
// 5 秒内同传感器同类型不重复记录
var lastRecord = AlarmRecords.LastOrDefault(r =>
	r.SensorName == record.SensorName &&
	r.AlarmType == record.AlarmType &&
	(record.AlarmTime - r.AlarmTime).TotalSeconds < 5);
if (lastRecord != null) return;
```

## UI 报警可见性绑定
```xml
<UserControl.Resources>
	<BooleanToVisibilityConverter x:Key="BoolToVisConverter"/>
</UserControl.Resources>

<Border Visibility="{Binding IsAlarm, Converter={StaticResource BoolToVisConverter}}">
	<TextBlock Text="⚠ 报警" Foreground="White"/>
</Border>
```
