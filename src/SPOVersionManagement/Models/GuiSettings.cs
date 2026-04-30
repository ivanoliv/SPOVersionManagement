using Newtonsoft.Json;

namespace SPOVersionManagement.Models
{
    public class GuiSettings
    {
        // Version Policy
        [JsonProperty("concurrentJobs")]
        public int ConcurrentJobs { get; set; } = 10;

        [JsonProperty("checkBatchSize")]
        public int CheckBatchSize { get; set; } = 10;

        [JsonProperty("checkBatchDelay")]
        public int CheckBatchDelay { get; set; } = 2;

        [JsonProperty("zeroVersionAction")]
        public string ZeroVersionAction { get; set; } = "syncOnly";

        // Delete Mode
        [JsonProperty("deleteByAge")]
        public bool DeleteByAge { get; set; } = false;

        [JsonProperty("majorVersionLimit")]
        public int MajorVersionLimit { get; set; } = 100;

        [JsonProperty("minorVersionLimit")]
        public int MinorVersionLimit { get; set; } = 0;

        [JsonProperty("deleteBeforeDays")]
        public int DeleteBeforeDays { get; set; } = 180;

        // Operation Mode
        [JsonProperty("syncVersionPolicy")]
        public bool SyncVersionPolicy { get; set; } = true;

        [JsonProperty("deleteExcessVersions")]
        public bool DeleteExcessVersions { get; set; } = true;

        [JsonProperty("manageRetentionPolicies")]
        public bool ManageRetentionPolicies { get; set; } = false;

        // Connection
        [JsonProperty("skipGraph")]
        public bool SkipGraph { get; set; } = false;

        // Input Files
        [JsonProperty("includeSitesCsv")]
        public string IncludeSitesCsv { get; set; } = "";

        [JsonProperty("excludeSitesCsv")]
        public string ExcludeSitesCsv { get; set; } = "";

        [JsonProperty("graphReportCsv")]
        public string GraphReportCsv { get; set; } = "";

        [JsonProperty("syncJobListCsv")]
        public string SyncJobListCsv { get; set; } = "";

        [JsonProperty("samReportCsv")]
        public string SamReportCsv { get; set; } = "";

        [JsonProperty("useFileCache")]
        public bool UseFileCache { get; set; } = false;

        [JsonProperty("cacheFilePath")]
        public string CacheFilePath { get; set; } = "";
    }
}
