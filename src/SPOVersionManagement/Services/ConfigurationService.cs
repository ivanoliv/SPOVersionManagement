using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPOVersionManagement.Models;

namespace SPOVersionManagement.Services
{
    public class ConfigurationService
    {
        private readonly string _rootPath;
        private string _logsPath;
        private string _configPath;
        private string _webPath;
        private bool _hasWritePermission;

        public AppConfiguration AppConfig { get; private set; }
        public DashboardConfiguration DashboardConfig { get; private set; }

        public string RootPath => _rootPath;
        public string LogsPath => _logsPath;
        public string ConfigPath => _configPath;
        public string WebPath => _webPath;
        public bool HasWritePermission => _hasWritePermission;
        public string PermissionMessage { get; private set; }

        public ConfigurationService(string rootPath)
        {
            _rootPath = rootPath;
            _logsPath = Path.Combine(rootPath, "Logs");
            _configPath = Path.Combine(rootPath, "config");
            _webPath = Path.Combine(rootPath, "web");
            CheckPermissions();
            Load();
        }

        private void CheckPermissions()
        {
            _hasWritePermission = false;
            PermissionMessage = "";

            try
            {
                if (!Directory.Exists(_logsPath))
                    Directory.CreateDirectory(_logsPath);
                if (!Directory.Exists(_configPath))
                    Directory.CreateDirectory(_configPath);
                if (!Directory.Exists(_webPath))
                    Directory.CreateDirectory(_webPath);

                string testFile = Path.Combine(_configPath, ".writetest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                _hasWritePermission = true;
            }
            catch (UnauthorizedAccessException)
            {
                PermissionMessage = $"No write permission to '{_logsPath}'. Run as Administrator or change the data directory under Config > Directories.";
            }
            catch (Exception ex)
            {
                PermissionMessage = $"Cannot write to data folder: {ex.Message}";
            }
        }

        /// <summary>
        /// Suggests a writable user-local directory as alternative.
        /// </summary>
        public string GetUserDataFolderSuggestion()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SPOVersionManagement", "config");
        }

        public void SwitchToUserDataFolder()
        {
            string userDir = GetUserDataFolderSuggestion();
            if (!Directory.Exists(userDir))
                Directory.CreateDirectory(userDir);

            // Copy existing configs if they don't exist in the new location
            CopyIfMissing("AppPaths.json", _configPath, userDir);
            CopyIfMissing("DashboardConfig.json", _configPath, userDir);

            _configPath = userDir;
            CheckPermissions();
            Load();
        }

        private void CopyIfMissing(string file, string src, string dest)
        {
            string srcFile = Path.Combine(src, file);
            string destFile = Path.Combine(dest, file);
            if (File.Exists(srcFile) && !File.Exists(destFile))
                File.Copy(srcFile, destFile);
        }

        public void Load()
        {
            LoadAppConfig();
            LoadDashboardConfig();
        }

        private void LoadAppConfig()
        {
            string path = Path.Combine(_configPath, "AppPaths.json");
            if (!File.Exists(path))
            {
                // Create default AppPaths.json on first run
                if (!Directory.Exists(_configPath))
                    Directory.CreateDirectory(_configPath);

                var defaults = new AppConfiguration
                {
                    Version = "1.3",
                    AppVersion = "2.1.3.3",
                    GitHubRepo = "ivanoliv/SPOVersionManagement",
                    TelemetryEnabled = false
                };
                string defaultJson = JsonConvert.SerializeObject(defaults, Formatting.Indented);
                File.WriteAllText(path, defaultJson);
                AppConfig = defaults;
                return;
            }

            string json = File.ReadAllText(path);
            AppConfig = JsonConvert.DeserializeObject<AppConfiguration>(json);

            if (string.IsNullOrEmpty(AppConfig.GitHubRepo))
                AppConfig.GitHubRepo = "ivanoliv/SPOVersionManagement";
        }

        private void LoadDashboardConfig()
        {
            string path = Path.Combine(_configPath, "DashboardConfig.json");
            if (!File.Exists(path))
            {
                DashboardConfig = new DashboardConfiguration
                {
                    Language = "en",
                    Currency = new CurrencyConfig { Symbol = "$", Code = "USD", Position = "before", DecimalSeparator = ".", ThousandsSeparator = "," },
                    CostPerTBYear = 13000m,
                    DateFormat = "MM/dd/yyyy",
                    ReexecutionDays = 0,
                    ZeroVersionAction = "ask",
                    RefreshIntervalSeconds = 3,
                    DashboardPort = 8080,
                    DashboardLaunchMode = "app"
                };
                return;
            }

            string json = File.ReadAllText(path);
            DashboardConfig = JsonConvert.DeserializeObject<DashboardConfiguration>(json);

            if (DashboardConfig.DashboardPort <= 0)
                DashboardConfig.DashboardPort = 8080;
            if (string.IsNullOrWhiteSpace(DashboardConfig.DashboardLaunchMode))
                DashboardConfig.DashboardLaunchMode = "app";
            if (DashboardConfig.ReexecutionDays == null)
                DashboardConfig.ReexecutionDays = 0;
            if (string.IsNullOrWhiteSpace(DashboardConfig.ZeroVersionAction))
                DashboardConfig.ZeroVersionAction = "ask";
        }

        public void SaveAppConfig()
        {
            if (!_hasWritePermission)
                throw new UnauthorizedAccessException(PermissionMessage);

            string path = Path.Combine(_configPath, "AppPaths.json");

            JObject existing = File.Exists(path)
                ? JObject.Parse(File.ReadAllText(path))
                : new JObject();

            JObject updated = JObject.FromObject(AppConfig, JsonSerializer.Create(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            }));

            existing.Merge(updated, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Ignore
            });

            existing["LastModified"] = DateTime.UtcNow.ToString("o");

            File.WriteAllText(path, existing.ToString(Formatting.Indented));
        }

        public void SaveDashboardConfig()
        {
            if (!_hasWritePermission)
                throw new UnauthorizedAccessException(PermissionMessage);

            string path = Path.Combine(_configPath, "DashboardConfig.json");

            JObject existing = File.Exists(path)
                ? JObject.Parse(File.ReadAllText(path))
                : new JObject();

            JObject updated = JObject.FromObject(DashboardConfig, JsonSerializer.Create(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            }));

            existing.Merge(updated, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Ignore
            });

            File.WriteAllText(path, existing.ToString(Formatting.Indented));
        }

        public GuiSettings LoadGuiSettings()
        {
            string path = Path.Combine(_configPath, "GuiSettings.json");
            if (!File.Exists(path))
                return new GuiSettings();

            try
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<GuiSettings>(json) ?? new GuiSettings();
            }
            catch
            {
                return new GuiSettings();
            }
        }

        public void SaveGuiSettings(GuiSettings settings)
        {
            if (!_hasWritePermission) return;

            string path = Path.Combine(_configPath, "GuiSettings.json");
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Backs up all database and config files to a user-chosen directory.
        /// </summary>
        public int BackupData(string destinationFolder)
        {
            if (!Directory.Exists(destinationFolder))
                Directory.CreateDirectory(destinationFolder);

            string[] configFilesToBackup = new[]
            {
                "AppPaths.json", "DashboardConfig.json", "JobStatus.json",
                "SiteExecutionHistory.json", "SessionHistory.json",
                "AllSites.json", "TenantStorage.json", "TenantStorageTimeline.json",
                "ExcludedSites.json", "RetentionPolicyDatabase.json",
                "RetentionPolicyLog.json", "ArchiveAnalysis.json", "ArchiveQueue.json"
            };

            string[] logsFilesToBackup = new[]
            {
                "ExecutionHistory.csv", "SiteStorage.csv"
            };

            int copied = 0;
            foreach (var f in configFilesToBackup)
            {
                string src = Path.Combine(_configPath, f);
                if (File.Exists(src))
                {
                    File.Copy(src, Path.Combine(destinationFolder, f), overwrite: true);
                    copied++;
                }
            }

            foreach (var f in logsFilesToBackup)
            {
                string src = Path.Combine(_logsPath, f);
                if (File.Exists(src))
                {
                    File.Copy(src, Path.Combine(destinationFolder, f), overwrite: true);
                    copied++;
                }
            }

            // Also backup include/exclude CSVs from root
            string[] rootFiles = new[] { "IncludeSites.csv", "ExcludeSites.csv" };
            foreach (var f in rootFiles)
            {
                string src = Path.Combine(_rootPath, f);
                if (File.Exists(src))
                {
                    File.Copy(src, Path.Combine(destinationFolder, f), overwrite: true);
                    copied++;
                }
            }

            return copied;
        }

        public int ResetLocalExecutionDatabases()
        {
            return ResetLocalExecutionDatabases("both");
        }

        /// <summary>
        /// Resets local databases based on the specified reset type.
        /// resetType: "sites" (sites data only), "tenant" (tenant data only), or "both" (default)
        /// </summary>
        public int ResetLocalExecutionDatabases(string resetType = "both")
        {
            if (!_hasWritePermission)
                throw new UnauthorizedAccessException(PermissionMessage);

            var filesToClear = new List<string>();

            // Sites data: execution history, sessions, job status
            if (resetType == "sites" || resetType == "both")
            {
                filesToClear.AddRange(new[]
                {
                    "SiteExecutionHistory.json",
                    "ExecutionHistory.csv",
                    "SessionHistory.json",
                    "JobStatus.json",
                    "AllSites.json",
                    "ArchiveAnalysis.json",
                    "ArchiveQueue.json",
                    "FileArchiveQueue.json"
                });
            }

            // Tenant data: storage tracking
            if (resetType == "tenant" || resetType == "both")
            {
                filesToClear.AddRange(new[]
                {
                    "TenantStorage.json",
                    "TenantStorageTimeline.json"
                });
            }

            int cleared = 0;
            foreach (var fileName in filesToClear)
            {
                string path = Path.Combine(_configPath, fileName);
                if (!File.Exists(path))
                    continue;

                File.Delete(path);
                cleared++;
            }

            // Consent can be requested again only after a full reset.
            if (string.Equals(resetType, "both", StringComparison.OrdinalIgnoreCase))
            {
                AppConfig.TelemetryEnabled = false;
                AppConfig.TelemetryConsentRequested = false;
                AppConfig.TelemetryConsentRequestedAt = null;
                AppConfig.TelemetrySalt = null;
                SaveAppConfig();
            }

            return cleared;
        }

        public string ResolvePath(string fileKey)
        {
            if (AppConfig.Files == null) return null;
            var prop = typeof(FilePaths).GetProperty(fileKey);
            if (prop == null) return null;
            string filename = prop.GetValue(AppConfig.Files) as string;
            if (string.IsNullOrEmpty(filename)) return null;
            // CSV files go to Logs/, JSON files go to config/
            if (filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(_logsPath, filename);
            return Path.Combine(_configPath, filename);
        }

        public string ResolveScriptPath(string scriptKey)
        {
            if (AppConfig.Scripts == null) return null;
            var prop = typeof(ScriptPaths).GetProperty(scriptKey);
            if (prop == null) return null;
            string filename = prop.GetValue(AppConfig.Scripts) as string;
            if (string.IsNullOrEmpty(filename)) return null;
            return Path.Combine(_rootPath, filename);
        }
    }
}
