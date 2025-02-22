using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using Serilog;


/*
本项目由XieYimin3@github.com添加注释
    让我们先看一看这个程序的大致启动流程
        1.静态Main方法调用Run；
        2.Run方法调用BuildAvaloniaApp；
        3.BuildAvaloniaApp方法配置Avalonia应用程序；
        4.配置Avalonia应用程序后启动经典桌面生命周期；
        5.接下来让我们转到App类......
*/


namespace OpenUtau.App {
    public class Program {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        /// <summary>
        /// 主程序入口
        /// </summary>
        /// <param name="args"></param>
        [STAThread]
        public static void Main(string[] args) {
            // 注册编码提供程序
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            // 初始化日志
            InitLogging();
            // 以下代码用于防止多次打开进程
            // 获取当前进程名称
            string processName = Process.GetCurrentProcess().ProcessName;
            // 如果进程名称不是 dotnet
            if (processName != "dotnet") {
                // 判断是否有多个进程
                var exists = Process.GetProcessesByName(processName).Count() > 1;
                // 如果有多个进程
                if (exists) {
                    // 输出日志
                    Log.Information($"Process {processName} already open. Exiting.");
                    // 退出
                    return;
                }
            }
            // 各种日志记录
            Log.Information($"{Environment.OSVersion}");
            Log.Information($"{RuntimeInformation.OSDescription} " +
                $"{RuntimeInformation.OSArchitecture} " +
                $"{RuntimeInformation.ProcessArchitecture}");
            Log.Information($"OpenUtau v{Assembly.GetEntryAssembly()?.GetName().Version} " +
                $"{RuntimeInformation.RuntimeIdentifier}");
            Log.Information($"Data path = {PathManager.Inst.DataPath}");
            Log.Information($"Cache path = {PathManager.Inst.CachePath}");
            try {
                // 程序真正开始运行
                Run(args);
                Log.Information($"Exiting.");
            } finally {
                if (!OS.IsMacOS()) {
                    NetMQ.NetMQConfig.Cleanup(/*block=*/false);
                    // Cleanup() hangs on macOS https://github.com/zeromq/netmq/issues/1018
                }
            }
            Log.Information($"Exited.");
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        /// <summary>
        /// 构建 Avalonia 应用程序
        /// </summary>
        /// <returns></returns>
        public static AppBuilder BuildAvaloniaApp() {
            FontManagerOptions fontOptions = new();
            // Linux 下的额外操作，获取默认字体
            if (OS.IsLinux()) {
                using Process process = Process.Start(new ProcessStartInfo("/usr/bin/fc-match")
                {
                    ArgumentList = { "-f", "%{family}" },
                    RedirectStandardOutput = true
                })!;
                process.WaitForExit();

                string fontFamily = process.StandardOutput.ReadToEnd();
                if (!string.IsNullOrEmpty(fontFamily)) {
                    string [] fontFamilies = fontFamily.Split(',');
                    fontOptions.DefaultFamilyName = fontFamilies[0];
                }
            }
            // 配置 Avalonia 应用程序
            return AppBuilder.Configure<App>()
                // 使用平台检测
                .UsePlatformDetect()
                // 使用日志记录
                .LogToTrace()
                // 使用 Reactive UI
                .UseReactiveUI()
                .With(fontOptions)
                .With(new X11PlatformOptions {EnableIme = true});
        }

        /// <summary>
        /// 程序运行
        /// </summary>
        /// <param name="args"></param>
        public static void Run(string[] args)
            // 启动 Avalonia 桌面应用程序
            => BuildAvaloniaApp()
                // 使用经典桌面生命周期
                .StartWithClassicDesktopLifetime(
                    args, ShutdownMode.OnMainWindowClose);

        public static void InitLogging() {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose() // 最低日志等级为 Verbose
                .WriteTo.Debug() // 输出到调试窗口
                .WriteTo.Logger(lc => lc // 输出到文件
                    .MinimumLevel.Information()
                    .WriteTo.File(PathManager.Inst.LogFilePath, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8))
                .WriteTo.Logger(lc => lc // 输出到 Debug 窗口
                    .MinimumLevel.ControlledBy(DebugViewModel.Sink.Inst.LevelSwitch)
                    .WriteTo.Sink(DebugViewModel.Sink.Inst))
                .CreateLogger();
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((sender, args) => {
                Log.Error((Exception)args.ExceptionObject, "Unhandled exception");
            });
            Log.Information("Logging initialized.");
        }
    }
}
