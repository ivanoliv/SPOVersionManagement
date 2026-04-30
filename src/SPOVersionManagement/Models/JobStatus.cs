using System.Collections.Generic;
using Newtonsoft.Json;

namespace SPOVersionManagement.Models
{
    public class JobStatusData
    {
        [JsonProperty("ActiveJobs")]
        public List<ActiveJob> ActiveJobs { get; set; }

        [JsonProperty("CompletedJobs")]
        public List<CompletedJob> CompletedJobs { get; set; }

        [JsonProperty("QueuedSites")]
        public List<QueuedSite> QueuedSites { get; set; }

        public JobStatusData()
        {
            ActiveJobs = new List<ActiveJob>();
            CompletedJobs = new List<CompletedJob>();
            QueuedSites = new List<QueuedSite>();
        }
    }

    public class ActiveJob
    {
        [JsonProperty("SiteUrl")]
        public string SiteUrl { get; set; }
        [JsonProperty("WorkItemId")]
        public string WorkItemId { get; set; }
        [JsonProperty("JobType")]
        public string JobType { get; set; }
        [JsonProperty("StartedAt")]
        public string StartedAt { get; set; }
        [JsonProperty("Status")]
        public string Status { get; set; }
    }

    public class CompletedJob
    {
        [JsonProperty("SiteUrl")]
        public string SiteUrl { get; set; }
        [JsonProperty("WorkItemId")]
        public string WorkItemId { get; set; }
        [JsonProperty("JobType")]
        public string JobType { get; set; }
        [JsonProperty("Status")]
        public string Status { get; set; }
        [JsonProperty("CompletedAt")]
        public string CompletedAt { get; set; }
    }

    public class QueuedSite
    {
        [JsonProperty("SiteUrl")]
        public string SiteUrl { get; set; }
        [JsonProperty("Priority")]
        public int Priority { get; set; }
    }

    public class SessionRecord
    {
        [JsonProperty("SessionId")]
        public string SessionId { get; set; }

        [JsonProperty("StartedAt")]
        public string StartedAt { get; set; }

        [JsonProperty("LastUpdated")]
        public string LastUpdated { get; set; }

        [JsonProperty("Status")]
        public string Status { get; set; }

        [JsonProperty("AdminUrl")]
        public string AdminUrl { get; set; }

        [JsonProperty("Configuration")]
        public SessionConfiguration Configuration { get; set; }

        [JsonProperty("Progress")]
        public SessionProgress Progress { get; set; }
    }

    public class SessionConfiguration
    {
        [JsonProperty("MajorVersionLimit")]
        public int MajorVersionLimit { get; set; }
        [JsonProperty("MajorWithMinorVersionsLimit")]
        public int MajorWithMinorVersionsLimit { get; set; }
        [JsonProperty("MaxConcurrentJobs")]
        public int MaxConcurrentJobs { get; set; }
        [JsonProperty("SyncOnly")]
        public bool SyncOnly { get; set; }
        [JsonProperty("DeleteOnly")]
        public bool DeleteOnly { get; set; }
        [JsonProperty("ZeroVersionAction")]
        public string ZeroVersionAction { get; set; }
        [JsonProperty("GraphReportCSV")]
        public string GraphReportCsv { get; set; }
        [JsonProperty("InputSiteListCSV")]
        public string InputSiteListCsv { get; set; }
        [JsonProperty("InputExclusionSiteListCSV")]
        public string InputExclusionSiteListCsv { get; set; }
        [JsonProperty("InputSiteSyncListCSV")]
        public string InputSiteSyncListCsv { get; set; }
        [JsonProperty("CheckBatchSize")]
        public int CheckBatchSize { get; set; }
        [JsonProperty("CheckBatchDelaySeconds")]
        public int CheckBatchDelaySeconds { get; set; }
        [JsonProperty("DeleteBeforeDays")]
        public int DeleteBeforeDays { get; set; }
    }

    public class SessionProgress
    {
        [JsonProperty("TotalSites")]
        public int TotalSites { get; set; }
        [JsonProperty("ProcessedSites")]
        public int ProcessedSites { get; set; }
        [JsonProperty("QueuedSites")]
        public int QueuedSites { get; set; }
    }

    public class TelemetryPayload
    {
        public string TenantHash { get; set; }
        public string AppVersion { get; set; }
        public string WorkItemId { get; set; }
        public string SiteUrl { get; set; }
        public string JobType { get; set; }
        public long StorageFreedBytes { get; set; }
        public long VersionsDeleted { get; set; }
        public int SitesProcessed { get; set; }
        public string Timestamp { get; set; }
    }

    public class GlobalStats
    {
        [JsonProperty("totalStorageFreedBytes")]
        public long TotalStorageFreedBytes { get; set; }

        [JsonProperty("totalVersionsDeleted")]
        public long TotalVersionsDeleted { get; set; }

        [JsonProperty("totalSessions")]
        public int TotalSessions { get; set; }

        public string StorageFreedFormatted
        {
            get
            {
                double gb = TotalStorageFreedBytes / (1024.0 * 1024 * 1024);
                if (gb >= 1024) return $"{gb / 1024.0:F1} TB freed globally";
                return $"{gb:F1} GB freed globally";
            }
        }
    }
}
