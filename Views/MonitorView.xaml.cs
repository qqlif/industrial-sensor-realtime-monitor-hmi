using System.Windows;
using System.Windows.Controls;
using 工业传感器实时监控上位机.Models;
using 工业传感器实时监控上位机.ViewModels;

namespace 工业传感器实时监控上位机.Views;

/// <summary>
/// MonitorView.xaml 的交互逻辑
/// 通过 ScottPlot 实时曲线刷新
/// </summary>
public partial class MonitorView : UserControl
{
    private const int MaxPoints = 500;
    private readonly List<double> _tempTimes = new();
    private readonly List<double> _tempValues = new();
    private readonly List<double> _pressureTimes = new();
    private readonly List<double> _pressureValues = new();
    private readonly List<double> _vibrationTimes = new();
    private readonly List<double> _vibrationValues = new();
    private double _timeOffset;

    public MonitorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MonitorViewModel vm)
        {
            InitializePlot(TemperaturePlot, "时间 (s)", "温度 (°C)");
            InitializePlot(PressurePlot, "时间 (s)", "压力 (MPa)");
            InitializePlot(VibrationPlot, "时间 (s)", "振动 (mm/s)");

            vm.ChartDataUpdated += OnChartDataUpdated;
            Unloaded += (_, _) => vm.ChartDataUpdated -= OnChartDataUpdated;
        }
    }

    private static void InitializePlot(ScottPlot.WPF.WpfPlot plot, string xLabel, string yLabel)
    {
        plot.Plot.Title(string.Empty);
        plot.Plot.XLabel(xLabel);
        plot.Plot.YLabel(yLabel);
        // 中文显示：必须在设置中文文本之后调用 Font.Automatic()
        plot.Plot.Font.Automatic();
        plot.Plot.Axes.AutoScale();
        plot.Refresh();
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 切换 Tab 后重置坐标轴并刷新图表，解决布局/渲染异常
        TemperaturePlot.Plot.Axes.AutoScale();
        TemperaturePlot.Refresh();
        PressurePlot.Plot.Axes.AutoScale();
        PressurePlot.Refresh();
        VibrationPlot.Plot.Axes.AutoScale();
        VibrationPlot.Refresh();
    }

    private void OnChartDataUpdated()
    {
        if (DataContext is not MonitorViewModel vm)
            return;

        Dispatcher.Invoke(() =>
        {
            UpdateSeries(vm.TemperatureData, _tempTimes, _tempValues, TemperaturePlot, 0x42A5F5);
            UpdateSeries(vm.PressureData, _pressureTimes, _pressureValues, PressurePlot, 0xFFA726);
            UpdateSeries(vm.VibrationData, _vibrationTimes, _vibrationValues, VibrationPlot, 0xAB47BC);
        });
    }

    private void UpdateSeries(SensorData? data, List<double> times, List<double> values,
        ScottPlot.WPF.WpfPlot plot, uint color)
    {
        if (data == null)
            return;

        // 使用相对时间（秒）作为 X 轴
        double t = data.Timestamp.TimeOfDay.TotalSeconds;
        if (_timeOffset == 0)
            _timeOffset = t;

        t -= _timeOffset;
        times.Add(t);
        values.Add(data.Value);

        // 限制数据点数
        while (times.Count > MaxPoints)
        {
            times.RemoveAt(0);
            values.RemoveAt(0);
        }

        plot.Plot.Clear();
        // 重新添加中文轴标签（Clear 会清除之前设置的标签）
        plot.Plot.XLabel("时间 (s)");
        plot.Plot.YLabel(values.Count > 0 ? data.SensorType.ToString() : "");
        // 中文显示：Clear 后需重新应用字体，且必须在设置中文文本之后调用
        plot.Plot.Font.Automatic();

        var scatter = plot.Plot.Add.Scatter(times.ToArray(), values.ToArray());
        scatter.Color = new ScottPlot.Color(color);
        scatter.LineWidth = 1.5f;
        scatter.MarkerSize = 0f;

        plot.Plot.Axes.AutoScale();
        plot.Refresh();
    }
}
