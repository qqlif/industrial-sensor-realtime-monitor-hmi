using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using 工业传感器实时监控上位机.Models;
using 工业传感器实时监控上位机.Services;
using 工业传感器实时监控上位机.ViewModels;
using 工业传感器实时监控上位机.Views;

namespace 工业传感器实时监控上位机
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// 配置依赖注入容器
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;

        public App()
        {
            // 注册全局未捕获异常处理，防止未处理异常导致进程静默崩溃
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 先安全地等待异步配置加载完成，避免死锁
            var globalConfig = await LoadConfigAsync();

            // 2. 然后再构建 Host，直接注入已加载的实例
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // 注册全局配置（单例）- 直接注入已加载的实例
                    services.AddSingleton(globalConfig);

                    // 注册服务
                    services.AddSingleton<IDataStorage, CsvDataStorage>();
                    // 注册所有可用数据源为 Transient（每次切换都是干净的新实例）
                    services.AddTransient<MockSensorSource>();
                    services.AddTransient<SerialSensorSource>();
                    services.AddTransient<TcpSensorSource>();
                    // 注册数据源工厂委托，ViewModel 通过 key 获取对应实例
                    services.AddSingleton<Func<string, ISensorSource>>(serviceProvider => key =>
                    {
                        return key switch
                        {
                            "COM" => serviceProvider.GetRequiredService<SerialSensorSource>(),
                            "TCP" => serviceProvider.GetRequiredService<TcpSensorSource>(),
                            _ => serviceProvider.GetRequiredService<MockSensorSource>()
                        };
                    });
                    services.AddSingleton<IAlarmService, AlarmService>();
                    services.AddSingleton<DialogService>();
                    services.AddSingleton<AlarmNotificationService>();
                    services.AddSingleton<AgentMemoryService>();
                    services.AddSingleton<McpServerService>();
                    services.AddSingleton<DataStatisticsService>();

                    // 注册视图模型（与应用生命周期等长，由 MainViewModel 统一管理导航）
                    services.AddSingleton<MonitorViewModel>();
                    services.AddSingleton<ConfigViewModel>();
                    services.AddSingleton<AlarmLogViewModel>();
                    services.AddSingleton<StatisticsViewModel>();
                    services.AddSingleton<MainViewModel>();

                    // 注册视图
                    services.AddTransient<MonitorView>();
                    services.AddTransient<ConfigView>();
                    services.AddTransient<AlarmLogView>();
                    services.AddTransient<StatisticsView>();
                })
                .Build();

            // 解析 AlarmNotificationService 使其开始监听报警事件
            _host.Services.GetRequiredService<AlarmNotificationService>();

            // 解析 MainViewModel 并设置为主窗口 DataContext
            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
            mainWindow.Show();

            // 初始化 Agent 记忆服务（后台运行）
            _ = Task.Run(async () =>
            {
                try
                {
                    var memoryService = _host.Services.GetRequiredService<AgentMemoryService>();
                    await memoryService.InitializeAsync();
                    System.Diagnostics.Debug.WriteLine($"[AgentMemory] 记忆服务已初始化，会话ID: {memoryService.SessionId}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AgentMemory] 初始化失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 在后台线程异步加载配置，避免阻塞 UI 线程
        /// </summary>
        private static async Task<GlobalConfig> LoadConfigAsync()
        {
            try
            {
                var storage = new CsvDataStorage();
                var config = await storage.LoadConfigAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine("[App] 配置加载成功");
                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] 配置加载失败，使用默认配置: {ex.Message}");
                return new GlobalConfig();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            _host?.Dispose();
        }

        /// <summary>
        /// UI 线程未捕获异常处理（WPF 调度器异常）
        /// </summary>
        private static void OnDispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[App] UI线程未处理异常: {e.Exception.Message}");
            e.Handled = true; // 阻止进程崩溃
        }

        /// <summary>
        /// Task 后台线程未观察异常处理
        /// </summary>
        private static void OnUnobservedTaskException(object? sender,
            UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception?.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"[App] 后台Task未处理异常: {e.Exception.InnerException.Message}");
            }
            e.SetObserved(); // 阻止进程崩溃
        }

        /// <summary>
        /// AppDomain 级别的未处理异常（最后防线）
        /// </summary>
        private static void OnUnhandledException(object sender,
            System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] 未处理异常: {ex.Message}");
            }
        }
    }
}
