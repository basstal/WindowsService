using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using System;
using System.IO;


namespace HttpCheckService
{

    public class Program
    {
        public static void Main(string[] args)
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "service-http-check.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logPath,
                    rollingInterval: Serilog.RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("Starting service...");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Service terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((builderContext, config) =>
                {
                    config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
                })
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;

                    // 根据配置决定是否注册 Python HTTP 服务器应用
                    if (configuration.GetValue<bool>("PythonHttpServer:Enabled", true))
                    {
                        services.AddSingleton<IManageableApplication, PythonHttpServerApplication>();
                    }

                    // 根据配置决定是否注册 Node.js 开发服务器应用
                    if (configuration.GetValue<bool>("NodeJsDevServer:Enabled", true))
                    {
                        services.AddSingleton<IManageableApplication, NodeJsDevServerApplication>();
                    }

                    services.AddHostedService<Worker>();
                    services.AddWindowsService();
                });
    }
}
