# WPF 实时曲线技能 (ScottPlot)

## 描述
使用 ScottPlot.WPF 实现工业传感器实时数据曲线可视化。

## 实现步骤

### 1. XAML 布局
```xml
<UserControl xmlns:ScottPlot="clr-namespace:ScottPlot.WPF;assembly=ScottPlot.WPF">
	<TabControl>
		<TabItem Header="温湿度">
			<ScottPlot:WpfPlot x:Name="TemperaturePlot" Margin="4"/>
		</TabItem>
	</TabControl>
</UserControl>
```

### 2. 数据缓存（ViewModel）
```csharp
private readonly ConcurrentDictionary<string, List<(DateTime Time, double Value)>> _chartData = new();

// 初始化
foreach (var sensor in _config.Sensors)
{
	_chartData[sensor.Name] = new List<(DateTime, double)>();
}

// 添加数据
if (_chartData.TryGetValue(data.SensorName, out var list))
{
	lock (list)
	{
		list.Add((data.Timestamp, data.Value));
		while (list.Count > _config.MaxCachePoints)
			list.RemoveAt(0);
	}
}
```

### 3. 曲线更新（View Code-Behind）
```csharp
public partial class MonitorView : UserControl
{
	private MonitorViewModel _vm = null!;

	public MonitorView(MonitorViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		Loaded += (s, e) => InitializePlots();
		_vm.ChartDataUpdated += () => Dispatcher.Invoke(UpdateCharts);
	}

	private void InitializePlots()
	{
		TemperaturePlot.Plot.Title("温湿度实时曲线");
		TemperaturePlot.Plot.XLabel("时间");
		TemperaturePlot.Plot.YLabel("°C");
		TemperaturePlot.Plot.AxisAuto();
	}

	private void UpdateCharts()
	{
		var data = _vm.TemperatureChartData;
		if (data.Count > 1)
		{
			TemperaturePlot.Plot.Clear();
			var sig = TemperaturePlot.Plot.Add.Scatter(
				data.Select(d => d.Time.ToOADate()).ToArray(),
				data.Select(d => d.Value).ToArray());
			sig.LineWidth = 1.5;
			TemperaturePlot.Plot.Axes.DateTimeTicks(ScottPlot.TickGenerators.DateTimeAutomatic());
			TemperaturePlot.Refresh();
		}
	}
}
```

## 性能优化
- 限制缓存点数（MaxCachePoints = 200）
- 使用锁保护共享列表（lock）
- 曲线刷新频率控制（非每次数据到达都刷新）
- 使用 ScottPlot 的 SignalPlot 代替 ScatterPlot（大数据量时）

## 依赖包
```xml
<PackageReference Include="ScottPlot.WPF" Version="5.1.*" />
```
