using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPOVersionManagement.Models;

namespace SPOVersionManagement.Services
{
    public class ExecutionHistoryService
    {
        private readonly ConfigurationService _config;

        public ExecutionHistoryService(ConfigurationService config)
        {
            _config = config;
        }

        /// <summary>
        /// Loads ExecutionHistory.csv into a list of ExecutionRecord objects.
        /// </summary>
        public List<ExecutionRecord> LoadExecutionHistory()
        {
            var records = new List<ExecutionRecord>();
            string path = _config.ResolvePath("ExecutionHistory");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return records;

            var lines = File.ReadAllLines(path);
            if (lines.Length < 2) return records;

            string[] headers = ParseCsvLine(lines[0]);
            var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
                headerIndex[headers[i].Trim()] = i;

            for (int row = 1; row < lines.Length; row++)
            {
                if (string.IsNullOrWhiteSpace(lines[row])) continue;
                string[] cols = ParseCsvLine(lines[row]);
                var rec = new ExecutionRecord
                {
                    Timestamp = GetCol(cols, headerIndex, "Timestamp"),
                    SiteUrl = GetCol(cols, headerIndex, "SiteUrl"),
                    JobType = GetCol(cols, headerIndex, "JobType"),
                    WorkItemId = GetCol(cols, headerIndex, "WorkItemId"),
                    Status = GetCol(cols, headerIndex, "Status"),
                    RequestTimeUTC = GetCol(cols, headerIndex, "RequestTimeUTC"),
                    CompleteTimeUTC = GetCol(cols, headerIndex, "CompleteTimeUTC"),
                    DurationMinutes = ParseDouble(GetCol(cols, headerIndex, "DurationMinutes")),
                    ListsProcessed = ParseInt(GetCol(cols, headerIndex, "ListsProcessed")),
                    ListsSynced = ParseInt(GetCol(cols, headerIndex, "ListsSynced")),
                    ListSyncFailed = ParseInt(GetCol(cols, headerIndex, "ListSyncFailed")),
                    FilesProcessed = ParseInt(GetCol(cols, headerIndex, "FilesProcessed")),
                    VersionsProcessed = ParseLong(GetCol(cols, headerIndex, "VersionsProcessed")),
                    VersionsDeleted = ParseLong(GetCol(cols, headerIndex, "VersionsDeleted")),
                    VersionsFailed = ParseInt(GetCol(cols, headerIndex, "VersionsFailed")),
                    StorageReleasedInBytes = ParseLong(GetCol(cols, headerIndex, "StorageReleasedInBytes")),
                    StorageReleasedMB = ParseDouble(GetCol(cols, headerIndex, "StorageReleasedMB")),
                    ErrorMessage = GetCol(cols, headerIndex, "ErrorMessage"),
                    InitialStorageUsedBytes = ParseLong(GetCol(cols, headerIndex, "InitialStorageUsedBytes")),
                    FinalStorageUsedBytes = ParseLong(GetCol(cols, headerIndex, "FinalStorageUsedBytes"))
                };
                records.Add(rec);
            }

            return records;
        }

        /// <summary>
        /// Loads SiteExecutionHistory.json as a dictionary of site URL → site history.
        /// </summary>
        public Dictionary<string, JObject> LoadSiteExecutionHistory()
        {
            string path = _config.ResolvePath("SiteExecutionHistory");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);

            string json = File.ReadAllText(path);
            try
            {
                var token = JToken.Parse(json);
                if (token is JObject root)
                {
                    // Current format: { "LastUpdated": "...", "Sites": { "https://...": {...} } }
                    if (root["Sites"] is JObject sitesObj)
                    {
                        var wrapped = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in sitesObj.Properties())
                        {
                            if (prop.Value is JObject siteObj)
                                wrapped[prop.Name] = siteObj;
                        }
                        return wrapped;
                    }

                    // Legacy format: flat dictionary at root
                    var flat = root.ToObject<Dictionary<string, JObject>>();
                    if (flat != null)
                        return new Dictionary<string, JObject>(flat, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Fall back below
            }

            return JsonConvert.DeserializeObject<Dictionary<string, JObject>>(json)
                   ?? new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads SessionHistory.json as a list of session records.
        /// </summary>
        public List<SessionRecord> LoadSessionHistory()
        {
            string path = Path.Combine(_config.ConfigPath, "SessionHistory.json");
            if (!File.Exists(path))
                return new List<SessionRecord>();

            string json = File.ReadAllText(path);

            // SessionHistory.json can be { "Sessions": [...] } or a plain array
            var token = Newtonsoft.Json.Linq.JToken.Parse(json);
            if (token.Type == Newtonsoft.Json.Linq.JTokenType.Object)
            {
                var sessionsToken = token["Sessions"];
                if (sessionsToken != null)
                    return sessionsToken.ToObject<List<SessionRecord>>() ?? new List<SessionRecord>();
                return new List<SessionRecord>();
            }

            return JsonConvert.DeserializeObject<List<SessionRecord>>(json)
                   ?? new List<SessionRecord>();
        }

        public int ClearSessionState(bool includeCurrentJobStatus = true)
        {
            int deleted = 0;
            string[] files = includeCurrentJobStatus
                ? new[] { "SessionHistory.json", "JobStatus.json" }
                : new[] { "SessionHistory.json" };

            foreach (var file in files)
            {
                string path = Path.Combine(_config.ConfigPath, file);
                if (!File.Exists(path))
                    continue;

                File.Delete(path);
                deleted++;
            }

            return deleted;
        }

        /// <summary>
        /// Loads current JobStatus.json.
        /// </summary>
        public JobStatusData LoadJobStatus()
        {
            string path = _config.ResolvePath("JobStatus");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new JobStatusData();

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<JobStatusData>(json) ?? new JobStatusData();
        }

        /// <summary>
        /// Creates a DataTable suitable for DataGridView binding from execution records.
        /// </summary>
        public DataTable ToDataTable(List<ExecutionRecord> records)
        {
            var dt = new DataTable();
            dt.Columns.Add("Timestamp", typeof(string));
            dt.Columns.Add("Site", typeof(string));
            dt.Columns.Add("Job Type", typeof(string));
            dt.Columns.Add("Status", typeof(string));
            dt.Columns.Add("Duration (min)", typeof(double));
            dt.Columns.Add("Versions Deleted", typeof(long));
            dt.Columns.Add("Storage Freed", typeof(string));
            dt.Columns.Add("Files", typeof(int));
            dt.Columns.Add("URL", typeof(string));

            foreach (var r in records.OrderByDescending(x => x.Timestamp))
            {
                dt.Rows.Add(
                    r.Timestamp,
                    r.SiteName,
                    r.JobType,
                    r.Status,
                    Math.Round(r.DurationMinutes, 1),
                    r.VersionsDeleted,
                    r.StorageReleasedFormatted,
                    r.FilesProcessed,
                    r.SiteUrl
                );
            }

            return dt;
        }

        /// <summary>
        /// Calculates summary stats from execution records.
        /// </summary>
        public (long totalVersionsDeleted, double totalStorageFreedGB, int totalSites, int totalSessions) GetSummaryStats()
        {
            var records = LoadExecutionHistory();
            var sessions = LoadSessionHistory();

            long versions = records.Sum(r => r.VersionsDeleted);
            double storageGB = records.Sum(r => r.StorageReleasedInBytes) / (1024.0 * 1024 * 1024);
            int sites = records.Select(r => r.SiteUrl?.ToLowerInvariant()).Distinct().Count();

            return (versions, storageGB, sites, sessions.Count);
        }

        #region CSV Parsing Helpers

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        private static string GetCol(string[] cols, Dictionary<string, int> index, string name)
        {
            if (!index.ContainsKey(name)) return "";
            int i = index[name];
            return i < cols.Length ? cols[i]?.Trim() ?? "" : "";
        }

        private static int ParseInt(string s) => int.TryParse(s, out int v) ? v : 0;
        private static long ParseLong(string s) => long.TryParse(s, out long v) ? v : 0;
        private static double ParseDouble(string s) => double.TryParse(s, out double v) ? v : 0;

        #endregion
    }
}
