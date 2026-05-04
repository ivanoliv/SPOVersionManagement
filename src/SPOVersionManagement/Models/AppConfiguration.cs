using Newtonsoft.Json;

namespace SPOVersionManagement.Models
{
    public class AppConfiguration
    {
        [JsonProperty("Version")]
        public string Version { get; set; }

        [JsonProperty("AppVersion")]
        public string AppVersion { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("LastModified")]
        public string LastModified { get; set; }

        [JsonProperty("RootPath")]
        public string RootPath { get; set; }

        [JsonProperty("ApplicationFolder")]
        public string ApplicationFolder { get; set; }

        [JsonProperty("Directories")]
        public DirectoryPaths Directories { get; set; }

        [JsonProperty("Files")]
        public FilePaths Files { get; set; }

        [JsonProperty("InputFiles")]
        public InputFilePaths InputFiles { get; set; }

        [JsonProperty("Scripts")]
        public ScriptPaths Scripts { get; set; }

        [JsonProperty("EntraIdApp")]
        public EntraIdAppConfig EntraIdApp { get; set; }

        [JsonProperty("PurviewApp")]
        public PurviewAppConfig PurviewApp { get; set; }

        [JsonProperty("GitHubRepo")]
        public string GitHubRepo { get; set; }

        [JsonProperty("TelemetryEndpoint")]
        public string TelemetryEndpoint { get; set; }

        [JsonProperty("TelemetryEnabled")]
        public bool TelemetryEnabled { get; set; }

        [JsonProperty("TelemetryConsentRequested")]
        public bool TelemetryConsentRequested { get; set; }

        [JsonProperty("TelemetryConsentRequestedAt")]
        public string TelemetryConsentRequestedAt { get; set; }

        [JsonProperty("TelemetrySalt")]
        public string TelemetrySalt { get; set; }

        [JsonProperty("AdminUrl")]
        public string AdminUrl { get; set; }
    }

    public class DirectoryPaths
    {
        [JsonProperty("Root")]
        public string Root { get; set; }
        [JsonProperty("Logs")]
        public string Logs { get; set; }
        [JsonProperty("Data")]
        public string Data { get; set; }
        [JsonProperty("Backup")]
        public string Backup { get; set; }
        [JsonProperty("Config")]
        public string Config { get; set; }
        [JsonProperty("Web")]
        public string Web { get; set; }
        [JsonProperty("App")]
        public string App { get; set; }
    }

    public class FilePaths
    {
        [JsonProperty("JobStatus")]
        public string JobStatus { get; set; }
        [JsonProperty("TenantStorage")]
        public string TenantStorage { get; set; }
        [JsonProperty("ExcludedSites")]
        public string ExcludedSites { get; set; }
        [JsonProperty("AllSites")]
        public string AllSites { get; set; }
        [JsonProperty("SiteExecutionHistory")]
        public string SiteExecutionHistory { get; set; }
        [JsonProperty("DashboardConfig")]
        public string DashboardConfig { get; set; }
        [JsonProperty("Dashboard")]
        public string Dashboard { get; set; }
        [JsonProperty("ExecutionHistory")]
        public string ExecutionHistory { get; set; }
        [JsonProperty("SiteStorage")]
        public string SiteStorage { get; set; }
        [JsonProperty("TenantStorageTimeline")]
        public string TenantStorageTimeline { get; set; }
        [JsonProperty("AppPaths")]
        public string AppPaths { get; set; }
    }

    public class InputFilePaths
    {
        [JsonProperty("IncludeSites")]
        public string IncludeSites { get; set; }
        [JsonProperty("ExcludeSites")]
        public string ExcludeSites { get; set; }
    }

    public class ScriptPaths
    {
        [JsonProperty("MainModule")]
        public string MainModule { get; set; }
        [JsonProperty("FiltersModule")]
        public string FiltersModule { get; set; }
        [JsonProperty("StartScript")]
        public string StartScript { get; set; }
        [JsonProperty("StartScriptApp")]
        public string StartScriptApp { get; set; }
        [JsonProperty("DashboardScript")]
        public string DashboardScript { get; set; }
    }

    public class EntraIdAppConfig
    {
        [JsonProperty("TenantId")]
        public string TenantId { get; set; }
        [JsonProperty("ClientId")]
        public string ClientId { get; set; }
        [JsonProperty("CertificateThumbprint")]
        public string CertificateThumbprint { get; set; }
    }

    public class PurviewAppConfig
    {
        [JsonProperty("ClientId")]
        public string ClientId { get; set; }
        [JsonProperty("CertificateThumbprint")]
        public string CertificateThumbprint { get; set; }
        [JsonProperty("Organization")]
        public string Organization { get; set; }
    }
}
