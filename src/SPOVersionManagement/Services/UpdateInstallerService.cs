using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Linq;

namespace SPOVersionManagement.Services
{
    public class UpdateInstallerService
    {
        private readonly string _rootPath;

        public event Action<string> OnProgress;
        public event Action<int> OnPercentage;

        public UpdateInstallerService(string rootPath)
        {
            _rootPath = rootPath;
        }

        /// <summary>
        /// Extracts a release ZIP and merges configurations, preserving user settings.
        /// Returns true on success.
        /// </summary>
        public bool InstallUpdate(string zipPath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SPOVersionManagement_Update_" + Guid.NewGuid().ToString("N").Substring(0, 8));

            try
            {
                // 1. Extract ZIP
                Report("Extracting update package...");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                ZipFile.ExtractToDirectory(zipPath, tempDir);
                Report("Package extracted.");
                OnPercentage?.Invoke(20);

                // Find the root folder inside the ZIP (may have a subfolder)
                string sourceDir = tempDir;
                var subDirs = Directory.GetDirectories(tempDir);
                if (subDirs.Length == 1 && Directory.GetFiles(tempDir).Length == 0)
                    sourceDir = subDirs[0];

                // 2. Backup current configs
                Report("Backing up current configuration...");
                string backupDir = Path.Combine(_rootPath, "Logs", "Backup",
                    "PreUpdate_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(backupDir);
                BackupFile("Logs\\AppPaths.json", backupDir);
                BackupFile("Logs\\DashboardConfig.json", backupDir);
                OnPercentage?.Invoke(30);

                // 3. Merge AppPaths.json
                Report("Merging AppPaths.json (preserving your settings)...");
                MergeJsonConfig(
                    Path.Combine(_rootPath, "Logs", "AppPaths.json"),
                    Path.Combine(sourceDir, "Logs", "AppPaths.json"));
                OnPercentage?.Invoke(50);

                // 4. Merge DashboardConfig.json
                Report("Merging DashboardConfig.json...");
                MergeJsonConfig(
                    Path.Combine(_rootPath, "Logs", "DashboardConfig.json"),
                    Path.Combine(sourceDir, "Logs", "DashboardConfig.json"));
                OnPercentage?.Invoke(60);

                // 5. Copy updated files (skip config JSONs and user data)
                Report("Installing updated files...");
                CopyUpdatedFiles(sourceDir, _rootPath);
                OnPercentage?.Invoke(90);

                // 6. Clean up
                Report("Cleaning up...");
                try { Directory.Delete(tempDir, true); } catch { }
                try { File.Delete(zipPath); } catch { }
                OnPercentage?.Invoke(100);

                Report("Update installed successfully! Please restart the application.");
                return true;
            }
            catch (Exception ex)
            {
                Report($"Update failed: {ex.Message}");
                // Attempt cleanup
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Merges a new JSON config into an existing one:
        /// - Preserves all existing user values
        /// - Adds new keys from the update that don't exist locally
        /// - Does NOT overwrite existing values
        /// </summary>
        private void MergeJsonConfig(string existingPath, string newPath)
        {
            if (!File.Exists(newPath)) return;

            JObject existing;
            if (File.Exists(existingPath))
            {
                existing = JObject.Parse(File.ReadAllText(existingPath));
            }
            else
            {
                // No existing file — just copy the new one
                string dir = Path.GetDirectoryName(existingPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(newPath, existingPath);
                return;
            }

            JObject incoming = JObject.Parse(File.ReadAllText(newPath));

            // Add new keys only (don't overwrite existing)
            MergeNewKeys(existing, incoming);

            File.WriteAllText(existingPath, existing.ToString(Newtonsoft.Json.Formatting.Indented));
        }

        private void MergeNewKeys(JObject target, JObject source)
        {
            foreach (var prop in source.Properties())
            {
                if (target[prop.Name] == null)
                {
                    // New key — add it
                    target[prop.Name] = prop.Value.DeepClone();
                }
                else if (prop.Value.Type == JTokenType.Object && target[prop.Name].Type == JTokenType.Object)
                {
                    // Recurse into nested objects
                    MergeNewKeys((JObject)target[prop.Name], (JObject)prop.Value);
                }
                // Existing scalar values are preserved — no overwrite
            }
        }

        /// <summary>
        /// Copies updated script/module/HTML files, skipping user data files.
        /// </summary>
        private void CopyUpdatedFiles(string sourceDir, string destDir)
        {
            string[] updateExtensions = { ".ps1", ".psm1", ".html", ".js", ".css", ".dll", ".csv" };
            string[] skipFiles = { "AppPaths.json", "DashboardConfig.json", "ExecutionHistory.csv",
                                   "SiteExecutionHistory.json", "SessionHistory.json", "JobStatus.json",
                                   "AllSites.json", "TenantStorage.json", "TenantStorageTimeline.json",
                                   "ExcludedSites.json", "RetentionPolicyDatabase.json", "RetentionPolicyLog.json",
                                   "IncludeSites.csv", "ExcludeSites.csv", "SiteStorage.csv" };

            foreach (var sourceFile in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                string relativePath = sourceFile.Substring(sourceDir.Length).TrimStart('\\', '/');
                string fileName = Path.GetFileName(sourceFile);

                // Skip user data files
                bool skip = false;
                foreach (var sf in skipFiles)
                {
                    if (fileName.Equals(sf, StringComparison.OrdinalIgnoreCase))
                    {
                        skip = true;
                        break;
                    }
                }
                if (skip) continue;

                // Skip Backup folder contents
                if (relativePath.StartsWith("Logs\\Backup", StringComparison.OrdinalIgnoreCase))
                    continue;

                string destFile = Path.Combine(destDir, relativePath);
                string destFileDir = Path.GetDirectoryName(destFile);
                if (!Directory.Exists(destFileDir))
                    Directory.CreateDirectory(destFileDir);

                File.Copy(sourceFile, destFile, overwrite: true);
            }
        }

        private void BackupFile(string relativePath, string backupDir)
        {
            string source = Path.Combine(_rootPath, relativePath);
            if (!File.Exists(source)) return;
            string dest = Path.Combine(backupDir, Path.GetFileName(relativePath));
            File.Copy(source, dest, overwrite: true);
        }

        private void Report(string message)
        {
            OnProgress?.Invoke(message);
        }
    }
}
