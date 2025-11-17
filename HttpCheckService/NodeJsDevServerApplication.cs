using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HttpCheckService
{
    public class NodeJsDevServerApplication : IManageableApplication
    {
        private readonly ILogger<NodeJsDevServerApplication> _logger;
        // 请根据您的 Node.js 项目修改这些配置
        private readonly string _targetIp;
        private readonly int _targetPort; // Node.js 开发服务器的常用端口
        private readonly string _projectDirectory; // 重要：请务必修改为您的 Node.js 项目目录
        private Process? _process;

        public string Name => "NodeJsDevServer";

        public NodeJsDevServerApplication(ILogger<NodeJsDevServerApplication> logger, IConfiguration configuration)
        {
            _logger = logger;
            var section = configuration.GetSection("NodeJsDevServer");
            _targetIp = section["TargetIp"] ?? "127.0.0.1";
            _targetPort = int.TryParse(section["TargetPort"], out var port) ? port : 3000;
            _projectDirectory = section["ProjectDirectory"] ?? throw new InvalidOperationException("ProjectDirectory is not configured for NodeJsDevServer.");
        }

        public async Task<bool> IsRunningAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var result = await client.GetAsync($"http://{_targetIp}:{_targetPort}");

                if (result.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Node.js dev server ({Name}) is running.", Name);
                    return true;
                }
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Node.js dev server ({Name}) is not reachable.", Name);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("HTTP request to {Name} timed out.", Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking status of {Name}", Name);
            }

            return false;
        }

        public void Start()
        {
            _logger.LogInformation("Attempting to start {Name}...", Name);
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _logger.LogInformation("Stopping existing process for {Name}...", Name);
                    Stop();
                }

                _logger.LogInformation("Starting new process for {Name} in {Directory}...", Name, _projectDirectory);
                var startInfo = new ProcessStartInfo
                {
                    // 在 Windows 上使用 cmd.exe 来执行 npm 命令
                    FileName = "cmd.exe",
                    Arguments = "/c npm run dev",
                    WorkingDirectory = _projectDirectory,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _process = Process.Start(startInfo);
                if (_process == null)
                {
                    throw new InvalidOperationException($"Failed to start process for {Name}");
                }

                _process.EnableRaisingEvents = true;
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                _process.OutputDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogInformation("[{Name} STDOUT]: {Data}", Name, e.Data); };
                _process.ErrorDataReceived += (sender, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogError("[{Name} STDERR]: {Data}", Name, e.Data); };
                _process.Exited += (sender, e) => { _logger.LogWarning("Process for {Name} exited unexpectedly.", Name); };

                _logger.LogInformation("Started {Name} process in {Directory}.", Name, _projectDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start {Name}: {Message}", Name, ex.Message);
                throw;
            }
        }

        public void Stop()
        {
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    _logger.LogInformation("Stopping process for {Name}...", Name);
                    _process.Kill(true); // 杀死进程及其所有子进程
                    _process.WaitForExit(5000);
                    _logger.LogInformation("Process for {Name} stopped.", Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping process for {Name}: {Message}", Name, ex.Message);
                }
                finally
                {
                    _process.Dispose();
                    _process = null;
                }
            }
        }
    }
} 