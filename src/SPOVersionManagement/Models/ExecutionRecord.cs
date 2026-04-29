using System;

namespace SPOVersionManagement.Models
{
    public class ExecutionRecord
    {
        public string Timestamp { get; set; }
        public string SiteUrl { get; set; }
        public string JobType { get; set; }
        public string WorkItemId { get; set; }
        public string Status { get; set; }
        public string RequestTimeUTC { get; set; }
        public string CompleteTimeUTC { get; set; }
        public double DurationMinutes { get; set; }
        public int ListsProcessed { get; set; }
        public int ListsSynced { get; set; }
        public int ListSyncFailed { get; set; }
        public int FilesProcessed { get; set; }
        public long VersionsProcessed { get; set; }
        public long VersionsDeleted { get; set; }
        public int VersionsFailed { get; set; }
        public long StorageReleasedInBytes { get; set; }
        public double StorageReleasedMB { get; set; }
        public string ErrorMessage { get; set; }
        public long InitialStorageUsedBytes { get; set; }
        public long FinalStorageUsedBytes { get; set; }

        public string StorageReleasedFormatted
        {
            get
            {
                if (StorageReleasedMB >= 1024)
                    return $"{StorageReleasedMB / 1024.0:F2} GB";
                return $"{StorageReleasedMB:F1} MB";
            }
        }

        public string SiteName
        {
            get
            {
                if (string.IsNullOrEmpty(SiteUrl)) return "";
                var uri = new Uri(SiteUrl);
                var segments = uri.AbsolutePath.TrimEnd('/').Split('/');
                return segments.Length > 0 ? segments[segments.Length - 1] : SiteUrl;
            }
        }
    }
}
