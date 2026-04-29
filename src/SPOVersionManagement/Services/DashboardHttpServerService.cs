using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SPOVersionManagement.Services
{
    public sealed class DashboardHttpServerService : IDisposable
    {
        private readonly object _sync = new object();
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenTask;
        private string _rootDirectory;
        private int _port;

        public bool IsRunning => _listener != null && _listener.IsListening;
        public int Port => _port;

        public event Action<string> LogOutput;

        public void Start(int port, string rootDirectory)
        {
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
            if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
                throw new DirectoryNotFoundException("Dashboard root directory not found: " + rootDirectory);

            rootDirectory = Path.GetFullPath(rootDirectory);

            lock (_sync)
            {
                if (IsRunning && _port == port &&
                    string.Equals(_rootDirectory, rootDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    LogOutput?.Invoke("Server already running on port " + port);
                    return;
                }

                StopInternal();

                _rootDirectory = rootDirectory;
                _port = port;
                _cts = new CancellationTokenSource();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                
                try
                {
                    _listener.Start();
                    LogOutput?.Invoke($"HTTP listener started on port {port}");
                    LogOutput?.Invoke($"Serving files from: {rootDirectory}");
                    OpenBrowser($"http://localhost:{port}/");
                }
                catch (Exception ex)
                {
                    LogOutput?.Invoke($"Failed to start HTTP listener: {ex.Message}");
                    throw;
                }

                var token = _cts.Token;
                _listenTask = Task.Run(() => ListenLoop(token), token);
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal();
            }
        }

        private void StopInternal()
        {
            try
            {
                _cts?.Cancel();
            }
            catch { }

            try
            {
                if (_listener != null)
                {
                    if (_listener.IsListening)
                    {
                        _listener.Stop();
                        LogOutput?.Invoke("HTTP listener stopped");
                    }
                    _listener.Close();
                }
            }
            catch (Exception ex)
            {
                LogOutput?.Invoke($"Error stopping listener: {ex.Message}");
            }

            _listener = null;

            try
            {
                _listenTask?.Wait(1000);
            }
            catch { }

            _listenTask = null;
            _cts?.Dispose();
            _cts = null;
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                _ = Task.Run(() => HandleRequest(context), token);
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                string absolutePath = context.Request.Url.AbsolutePath ?? "/";
                string relativePath = absolutePath.TrimStart('/');
                if (string.IsNullOrWhiteSpace(relativePath))
                    relativePath = "dashboard.html";

                relativePath = Uri.UnescapeDataString(relativePath)
                    .Replace('/', Path.DirectorySeparatorChar)
                    .TrimStart(Path.DirectorySeparatorChar);

                string fullPath = ResolveFilePath(relativePath);
                if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                {
                    WriteNotFound(context);
                    return;
                }

                byte[] bytes = File.ReadAllBytes(fullPath);
                context.Response.StatusCode = 200;
                context.Response.ContentType = GetMimeType(Path.GetExtension(fullPath));
                context.Response.ContentLength64 = bytes.LongLength;
                context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Flush();
                context.Response.OutputStream.Close();
            }
            catch
            {
                try
                {
                    context.Response.StatusCode = 500;
                    byte[] msg = Encoding.UTF8.GetBytes("Internal server error");
                    context.Response.OutputStream.Write(msg, 0, msg.Length);
                    context.Response.OutputStream.Close();
                }
                catch { }
            }
        }

        private string ResolveFilePath(string relativePath)
        {
            // Try web/ directory first (HTML, JS, CSS)
            string webPath = Path.Combine(_rootDirectory, relativePath);
            string normalized = Path.GetFullPath(webPath);
            if (normalized.StartsWith(Path.GetFullPath(_rootDirectory), StringComparison.OrdinalIgnoreCase) && File.Exists(normalized))
                return normalized;

            // Try config/ directory for JSON files (sibling to web/)
            string rootParent = Path.GetDirectoryName(_rootDirectory);
            if (rootParent != null)
            {
                string configDir = Path.Combine(rootParent, "config");
                string configPath = Path.Combine(configDir, relativePath);
                string normalizedConfig = Path.GetFullPath(configPath);
                if (normalizedConfig.StartsWith(Path.GetFullPath(configDir), StringComparison.OrdinalIgnoreCase) && File.Exists(normalizedConfig))
                    return normalizedConfig;

                // Try Logs/ directory for CSV files
                string logsDir = Path.Combine(rootParent, "Logs");
                string logsPath = Path.Combine(logsDir, relativePath);
                string normalizedLogs = Path.GetFullPath(logsPath);
                if (normalizedLogs.StartsWith(Path.GetFullPath(logsDir), StringComparison.OrdinalIgnoreCase) && File.Exists(normalizedLogs))
                    return normalizedLogs;
            }

            // Fallback: find file by case-insensitive name in web directory.
            string dir = Path.GetDirectoryName(normalized);
            string file = Path.GetFileName(normalized);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !string.IsNullOrEmpty(file))
            {
                var match = Directory.GetFiles(dir)
                    .FirstOrDefault(f => string.Equals(Path.GetFileName(f), file, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match))
                    return match;
            }

            return normalized;
        }

        private static void WriteNotFound(HttpListenerContext context)
        {
            context.Response.StatusCode = 404;
            byte[] msg = Encoding.UTF8.GetBytes("Not found");
            context.Response.OutputStream.Write(msg, 0, msg.Length);
            context.Response.OutputStream.Close();
        }

        private static string GetMimeType(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext))
                return "application/octet-stream";

            switch (ext.ToLowerInvariant())
            {
                case ".html":
                case ".htm":
                    return "text/html; charset=utf-8";
                case ".js":
                    return "application/javascript; charset=utf-8";
                case ".css":
                    return "text/css; charset=utf-8";
                case ".json":
                    return "application/json; charset=utf-8";
                case ".svg":
                    return "image/svg+xml";
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".gif":
                    return "image/gif";
                case ".ico":
                    return "image/x-icon";
                case ".txt":
                    return "text/plain; charset=utf-8";
                default:
                    return "application/octet-stream";
            }
        }

        public static void OpenBrowser(string url)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
