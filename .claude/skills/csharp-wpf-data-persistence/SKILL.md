# C# WPF 数据持久化技能

## 描述
工业上位机数据持久化方案：CSV 文件存储和 SQLite 数据库存储的标准实现。

## 接口抽象
```csharp
public interface IDataStorage
{
	Task SaveSensorDataAsync(SensorData data);
	Task SaveSensorDataBatchAsync(IEnumerable<SensorData> dataList);
	Task ExportSensorDataAsync(DateTime start, DateTime end, string filePath);
	Task SaveAlarmRecordAsync(AlarmRecord record);
	Task ExportAlarmLogAsync(IEnumerable<AlarmRecord> records, string filePath);
	Task<GlobalConfig> LoadConfigAsync();
	Task SaveConfigAsync(GlobalConfig config);
}
```

## CSV 存储实现要点
- 按日期分文件（sensor_data_yyyyMMdd.csv）
- 使用 SemaphoreSlim 控制并发写入
- 批量缓冲写入（每 N 条刷写一次）
- UTF-8 编码
- 文件头包含列名

## SQLite 存储实现要点
- 使用 Microsoft.Data.Sqlite
- 创建索引优化时间范围查询
- 参数化查询防止 SQL 注入
- WAL 模式提升并发性能

## 配置持久化（JSON）
```csharp
public async Task SaveConfigAsync(GlobalConfig config)
{
	var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
	await File.WriteAllTextAsync(_configFilePath, json, Encoding.UTF8);
}

public async Task<GlobalConfig> LoadConfigAsync()
{
	if (File.Exists(_configFilePath))
	{
		var json = await File.ReadAllTextAsync(_configFilePath, Encoding.UTF8);
		return JsonSerializer.Deserialize<GlobalConfig>(json) ?? CreateDefaultConfig();
	}
	return CreateDefaultConfig();
}
```

## 依赖包
```xml
<!-- CSV -->
<!-- 无需额外包，使用 System.IO -->

<!-- SQLite -->
<PackageReference Include="Microsoft.Data.Sqlite" Version="9.*" />
```
