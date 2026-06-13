# C# WPF 异常处理技能

## 描述
工业上位机软件的异常处理标准模式，确保应用在异常后保持可用。

## 异常处理三原则

### 1. 捕获（Catch）
所有可能抛出异常的操作必须 try-catch：
- 异步方法（async Task）
- 文件 IO（读写、创建目录）
- 外部服务调用（数据源、网络）
- UI 调度（Dispatcher.Invoke）

### 2. 记录（Log）
```csharp
try { ... }
catch (Exception ex)
{
	System.Diagnostics.Debug.WriteLine($"[方法名] 操作描述失败: {ex.Message}");
	// 不吞异常，不抛出新异常
}
```

### 3. 恢复（Recover）
```csharp
try
{
	await _sensorSource.StartAsync(_cts.Token);
	IsSourceRunning = true;
}
catch (Exception ex)
{
	System.Diagnostics.Debug.WriteLine($"启动数据源失败: {ex.Message}");
	SourceStatus = $"启动失败: {ex.Message}";
	// 应用保持可用，UI 显示错误状态
}
```

## Dispose 模式（防止重复释放）
```csharp
private bool _disposed;

public void Dispose()
{
	if (_disposed) return;
	_disposed = true;

	_cts.Cancel();
	_cts.Dispose();
	// 取消事件订阅
	_source.DataReceived -= OnDataReceived;
}
```

## CancellationToken 安全使用
```csharp
private CancellationTokenSource _cts = new();

// 取消时
_cts?.Cancel();
_cts?.Dispose();
_cts = null;  // 或重新 new

// 使用时
await Task.Delay(interval, _cts.Token);  // 抛出 OperationCanceledException
```

## 常见异常处理模式

| 场景 | 处理方式 |
|------|---------|
| 异步操作取消 | catch OperationCanceledException，正常返回 |
| 文件写入失败 | 记录日志，不中断主流程 |
| 数据源异常 | 更新 UI 状态，允许重试 |
| 配置加载失败 | 使用默认配置，记录警告 |
| UI 线程异常 | AppDomain.CurrentDomain.UnhandledException 全局捕获 |
