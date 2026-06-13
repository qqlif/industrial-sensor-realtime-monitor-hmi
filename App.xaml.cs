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

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 先异步加载配置（在后台线程执行，不阻塞 UI 线程）
            var configTask = LoadConfigAsync();

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // 注册全局配置（单例）- 通过工厂委托延迟解析
                    services.AddSingleton<GlobalConfig>(provider =>
                    {
                        // 使用 Task.Run 在后台线程同步等待已完成的 Task，避免死锁
                        var task = Task.Run(async () => await configTask);
                        return task.GetAwaiter().GetResult();
                    });

                    // 注册服务
                    services.AddSingleton<IDataStorage, CsvDataStorage>();
                    services.AddSingleton<ISensorSource, MockSensorSource>();
                    services.AddSingleton<IAlarmService, AlarmService>();
                    services.AddSingleton<DialogService>();
                    services.AddSingleton<AgentMemoryService>();
                    services.AddSingleton<McpServerService>();

                    // 注册视图模型
                    services.AddTransient<MonitorViewModel>();
                    services.AddTransient<ConfigViewModel>();
                    services.AddTransient<AlarmLogViewModel>();
                    services.AddSingleton<MainViewModel>();

                    // 注册视图
                    services.AddTransient<MonitorView>();
                    services.AddTransient<ConfigView>();
                    services.AddTransient<AlarmLogView>();
                })
                .Build();

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
    }
}
