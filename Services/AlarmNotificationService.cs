using System.Media;
using System.Windows;
using System.Windows.Threading;
using 工业传感器实时监控上位机.Models;

namespace 工业传感器实时监控上位机.Services;

/// <summary>
/// 报警通知服务
/// 提供报警音效播放和系统托盘气泡通知
/// 通过订阅 IAlarmService.AlarmTriggered 事件自动响应报警
/// </summary>
public class AlarmNotificationService : IDisposable
{
    private readonly IAlarmService _alarmService;    // 报警服务
    private bool _disposed;                           // 是否已释放
    private bool _soundEnabled = true;                // 是否启用报警音效
    private bool _trayNotificationEnabled = true;     // 是否启用托盘通知

    /// <summary>是否启用报警音效</summary>
    public bool SoundEnabled
    {
        get => _soundEnabled;
        set => _soundEnabled = value;
    }

    /// <summary>是否启用托盘通知</summary>
    public bool TrayNotificationEnabled
    {
        get => _trayNotificationEnabled;
        set => _trayNotificationEnabled = value;
    }

    public AlarmNotificationService(IAlarmService alarmService)
    {
        _alarmService = alarmService;

        // 监听报警触发事件
        _alarmService.AlarmTriggered += OnAlarmTriggered;
    }

    private void OnAlarmTriggered(AlarmRecord record)
    {
        // 播放报警音效（异步，不阻塞 UI）
        if (_soundEnabled)
        {
            _ = Task.Run(() => PlayAlarmSound());
        }

        // 显示托盘通知
        if (_trayNotificationEnabled)
        {
            ShowTrayNotification(record);
        }
    }

    /// <summary>
    /// 播放系统报警音效
    /// 使用系统默认的 Asterisk 声音，无需额外音频文件
    /// </summary>
    private void PlayAlarmSound()
    {
        try
        {
            // 使用系统内置的报警声音
            System.Media.SystemSounds.Asterisk.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlarmNotification] 播放音效失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 播放自定义 WAV 报警音效（如果文件存在）
    /// 将 alarm.wav 放在程序运行目录即可生效
    /// </summary>
    public void PlayCustomAlarmSound()
    {
        try
        {
            var soundPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "alarm.wav");

            if (System.IO.File.Exists(soundPath))
            {
                using var player = new SoundPlayer(soundPath);
                player.Play();
            }
            else
            {
                // 回退到系统声音
                SystemSounds.Asterisk.Play();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlarmNotification] 播放自定义音效失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 显示系统托盘气泡通知
    /// 使用 WPF 的 NotifyIcon 实现（需 Windows.Forms 命名空间）
    /// 若不可用则回退到 MessageBox 弹窗
    /// </summary>
    private void ShowTrayNotification(AlarmRecord record)
    {
        try
        {
            // 使用 Application.Current.Dispatcher 在 UI 线程上操作
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 尝试使用 Windows.Forms 的 NotifyIcon（需要额外引用）
                    // 如果不可用，使用 MaterialDesign 的 Snackbar 或简单弹窗
                    ShowNotificationFallback(record);
                }
                catch
                {
                    ShowNotificationFallback(record);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlarmNotification] 托盘通知失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 非阻塞通知：创建临时 Toast 窗口，3 秒后自动关闭
    /// 不阻塞 UI 线程，不影响实时数据刷新和图表渲染
    /// </summary>
    private void ShowNotificationFallback(AlarmRecord record)
    {
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            try
            {
                var alarmType = record.AlarmType == "上限超限" ? "↑" : "↓";
                var toast = new Window
                {
                    Title = "传感器报警",
                    Width = 360,
                    Height = 160,
                    WindowStyle = WindowStyle.ToolWindow,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Top = SystemParameters.WorkArea.Top + 12,
                    Left = SystemParameters.WorkArea.Right - 372,
                    Topmost = true,
                    ShowInTaskbar = false,
                    ResizeMode = ResizeMode.NoResize,
                    Background = System.Windows.Media.Brushes.DarkRed,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 13,
                    Content = new System.Windows.Controls.StackPanel
                    {
                        Margin = new Thickness(16),
                        Children =
                        {
                            new System.Windows.Controls.TextBlock
                            {
                                Text = "⚠ 传感器报警",
                                FontSize = 16,
                                FontWeight = FontWeights.Bold,
                                Margin = new Thickness(0, 0, 0, 12)
                            },
                            new System.Windows.Controls.TextBlock
                            {
                                Text = $"传感器: {record.SensorName}"
                            },
                            new System.Windows.Controls.TextBlock
                            {
                                Text = $"报警类型: {record.AlarmType} {alarmType}"
                            },
                            new System.Windows.Controls.TextBlock
                            {
                                Text = $"当前值: {record.CurrentValue:F2} {record.Unit}  (阈值: {record.Threshold:F2} {record.Unit})"
                            },
                            new System.Windows.Controls.TextBlock
                            {
                                Text = $"时间: {record.AlarmTime:HH:mm:ss.fff}",
                                Margin = new Thickness(0, 0, 0, 8)
                            }
                        }
                    }
                };

                toast.Show();

                // 3 秒后自动关闭，不阻塞 UI
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    toast.Close();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlarmNotification] Toast 通知失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 测试报警通知（用于配置页面测试）
    /// </summary>
    public void TestNotification()
    {
        var testRecord = new AlarmRecord
        {
            SensorName = "测试传感器",
            SensorType = SensorType.温湿度,
            AlarmTime = DateTime.Now,
            CurrentValue = 99.99,
            Threshold = 50.0,
            AlarmType = "测试报警",
            Unit = "°C"
        };

        OnAlarmTriggered(testRecord);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _alarmService.AlarmTriggered -= OnAlarmTriggered;
    }
}
