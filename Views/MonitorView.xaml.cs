using System.Windows;
using System.Windows.Controls;
using ScottPlot.WPF;
using 工业传感器实时监控上位机.Models;
using 工业传感器实时监控上位机.ViewModels;

namespace 工业传感器实时监控上位机.Views;

/// <summary>
/// MonitorView.xaml 的交互逻辑
/// 通过 ScottPlot DataStreamer 实现高性能固定容量实时曲线
/// </summary>
public partial class MonitorView : UserControl
{
    private const int MaxPoints = 500;
    private const int RefreshInterval = 10; // 每 10 个点刷新一次
    private int _updateCounter;
    private ScottPlot.Plottables.DataStreamer? _tempStreamer;
    private ScottPlot.Plottables.DataStreamer? _pressureStreamer;
    private ScottPlot.Plottables.DataStreamer? _vibrationStreamer;

    public MonitorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MonitorViewModel vm)
        {
            _tempStreamer = InitializePlot(TemperaturePlot, "时间 (s)", "温度 (°C)", 0xFF42A5F5);
            _pressureStreamer = InitializePlot(PressurePlot, "时间 (s)", "压力 (MPa)", 0xFFFFA726);
            _vibrationStreamer = InitializePlot(VibrationPlot, "时间 (s)", "振动 (mm/s)", 0xFFAB47BC);

            vm.ChartDataUpdated += OnChartDataUpdated;
            Unloaded += (_, _) => vm.ChartDataUpdated -= OnChartDataUpdated;
        }
    }

    private static ScottPlot.Plottables.DataStreamer InitializePlot(WpfPlot plot, string xLabel, string yLabel, uint color)
    {
        plot.Plot.Title(string.Empty);
        plot.Plot.XLabel(xLabel);
        plot.Plot.YLabel(yLabel);

        // 适配暗黑模式主题
        plot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1E1E1E");
        plot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#252526");
        plot.Plot.Axes.Color(ScottPlot.Colors.LightGray);
        plot.Plot.Grid.LineColor = ScottPlot.Color.FromHex("#333333");

        // 中文显示：必须在设置中文文本之后调用 Font.Automatic()
        plot.Plot.Font.Automatic();

        // 使用 DataStreamer：固定容量，自动滚动覆盖旧数据，防止 OOM
        var streamer = plot.Plot.Add.DataStreamer(MaxPoints);
        streamer.Color = new ScottPlot.Color(color);
        streamer.LineWidth = 1.5f;
        streamer.ViewScrollLeft();

        plot.Plot.Axes.AutoScale();
        plot.Refresh();

        return streamer;
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
            UpdateSeries(vm.TemperatureData, TemperaturePlot, _tempStreamer);
            UpdateSeries(vm.PressureData, PressurePlot, _pressureStreamer);
            UpdateSeries(vm.VibrationData, VibrationPlot, _vibrationStreamer);
        });
    }

    private void UpdateSeries(SensorData? data, WpfPlot plot, ScottPlot.Plottables.DataStreamer? streamer)
    {
        if (data == null || streamer == null)
            return;

        // DataStreamer 自动管理 X 轴（自增序号）和固定容量（500点），防 OOM
        streamer.Add(data.Value);

        // 每 N 个点触发一次 AutoScale 和 Refresh，降低刷新频率
        _updateCounter++;
        if (_updateCounter % RefreshInterval == 0)
        {
            plot.Plot.Axes.AutoScale();
            plot.Refresh();
        }
    }
}
