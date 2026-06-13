using System.IO;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// 数据统计服务
/// 对传感器历史数据进行统计分析，计算最大值、最小值、平均值、标准差、趋势等
/// </summary>
public class DataStatisticsService
{
    private readonly IDataStorage _storage;

    public DataStatisticsService(IDataStorage storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// 计算指定传感器在时间段内的统计数据
    /// </summary>
    public async Task<SensorStatistics> CalculateStatisticsAsync(
        string sensorName, DateTime start, DateTime end)
    {
        // 导出数据到临时文件进行分析
        var tempFile = Path.Combine(Path.GetTempPath(), $"stats_{Guid.NewGuid():N}.csv");
        var values = new List<double>();
        var timestamps = new List<DateTime>();

        try
        {
            await _storage.ExportSensorDataAsync(start, end, tempFile);

            if (File.Exists(tempFile))
            {
                var lines = await File.ReadAllLinesAsync(tempFile);

                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');

                    if (parts.Length >= 6 &&
                        parts[1] == sensorName &&
                        DateTime.TryParse(parts[0], out var ts) &&
                        double.TryParse(parts[3], System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var value))
                    {
                        values.Add(value);
                        timestamps.Add(ts);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataStatistics] 统计数据失败: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }

        if (values.Count == 0)
        {
            return new SensorStatistics { SensorName = sensorName, DataPointCount = 0 };
        }

        var avg = values.Average();
        var min = values.Min();
        var max = values.Max();
        var variance = values.Select(v => Math.Pow(v - avg, 2)).Average();
        var stdDev = Math.Sqrt(variance);

        // 计算趋势（线性回归斜率）
        var trend = CalculateTrend(values);
        var trendDescription = trend switch
        {
            > 0.01 => "上升趋势 ↑",
            < -0.01 => "下降趋势 ↓",
            _ => "平稳趋势 →"
        };

        // 计算超限比例
        var config = await LoadSensorConfigAsync(sensorName);
        var upperLimitCount = 0;
        var lowerLimitCount = 0;

        if (config != null)
        {
            upperLimitCount = values.Count(v => v > config.AlarmUpper);
            lowerLimitCount = values.Count(v => v < config.AlarmLower);
        }

        return new SensorStatistics
        {
            SensorName = sensorName,
            DataPointCount = values.Count,
            MinValue = min,
            MaxValue = max,
            AverageValue = avg,
            StdDeviation = stdDev,
            Trend = trend,
            TrendDescription = trendDescription,
            UpperLimitExceedCount = upperLimitCount,
            LowerLimitExceedCount = lowerLimitCount,
            StartTime = start,
            EndTime = end,
            Unit = config?.Unit ?? ""
        };
    }

    /// <summary>
    /// 计算线性回归斜率，表示趋势方向
    /// </summary>
    private static double CalculateTrend(List<double> values)
    {
        if (values.Count < 2) return 0;

        var n = values.Count;
        var indices = Enumerable.Range(0, n).Select(i => (double)i).ToArray();

        var sumX = indices.Sum();
        var sumY = values.Sum();
        var sumXY = indices.Zip(values, (x, y) => x * y).Sum();
        var sumX2 = indices.Sum(x => x * x);

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        return double.IsNaN(slope) ? 0 : slope;
    }

    private async Task<SensorConfig?> LoadSensorConfigAsync(string sensorName)
    {
        try
        {
            var config = await _storage.LoadConfigAsync();
            return config.Sensors.FirstOrDefault(s => s.Name == sensorName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 计算所有传感器的统计摘要
    /// </summary>
    public async Task<List<SensorStatistics>> CalculateAllStatisticsAsync(DateTime start, DateTime end)
    {
        var config = await _storage.LoadConfigAsync();
        var results = new List<SensorStatistics>();

        foreach (var sensor in config.Sensors.Where(s => s.Enabled))
        {
            var stats = await CalculateStatisticsAsync(sensor.Name, start, end);
            results.Add(stats);
        }

        return results;
    }
}

/// <summary>
/// 传感器统计结果
/// 包含数据点数、最小值、最大值、平均值、标准差、趋势方向和报警次数
/// 提供格式化文本属性供 UI 绑定
/// </summary>
public class SensorStatistics
{
    /// <summary>传感器名称</summary>
    public string SensorName { get; set; } = string.Empty;

    /// <summary>数据点总数</summary>
    public int DataPointCount { get; set; }

    /// <summary>最小值</summary>
    public double MinValue { get; set; }

    /// <summary>最大值</summary>
    public double MaxValue { get; set; }

    /// <summary>平均值</summary>
    public double AverageValue { get; set; }

    /// <summary>标准差（反映数据离散程度）</summary>
    public double StdDeviation { get; set; }

    /// <summary>趋势斜率（线性回归）</summary>
    public double Trend { get; set; }

    /// <summary>趋势描述文本（上升/下降/平稳）</summary>
    public string TrendDescription { get; set; } = string.Empty;

    /// <summary>超上限次数</summary>
    public int UpperLimitExceedCount { get; set; }

    /// <summary>超下限次数</summary>
    public int LowerLimitExceedCount { get; set; }

    /// <summary>统计起始时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>统计结束时间</summary>
    public DateTime EndTime { get; set; }

    /// <summary>数值单位</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>范围文本（如 "15.32 ~ 38.76 °C"）</summary>
    public string RangeText => $"{MinValue:F2} ~ {MaxValue:F2} {Unit}";

    /// <summary>平均值文本</summary>
    public string AverageText => $"{AverageValue:F2} {Unit}";

    /// <summary>标准差文本</summary>
    public string StdDevText => $"{StdDeviation:F2} {Unit}";

    /// <summary>报警次数文本</summary>
    public string AlarmCountText => $"上限超限: {UpperLimitExceedCount}次 / 下限超限: {LowerLimitExceedCount}次";
}
