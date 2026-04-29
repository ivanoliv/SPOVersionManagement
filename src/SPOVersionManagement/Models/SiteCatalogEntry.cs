using System;

namespace SPOVersionManagement.Models
{
    public class SiteCatalogEntry
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public double StorageMB { get; set; }
        public DateTime? LastModified { get; set; }
        public DateTime? Created { get; set; }
        public string ArchiveStatus { get; set; }
        public string LockState { get; set; }
        public string Owner { get; set; }
        public string Status { get; set; }
        public long VersionCount { get; set; }
        public double VersionSizeMB { get; set; }
        public string Template { get; set; }
        public bool IsInactive { get; set; }
        public bool IsOwnerless { get; set; }
        public string SuggestedArchiveDate { get; set; }
        public bool IsCandidate { get; set; }
        public string EffectiveDate { get; set; }
        public string ArchivedBy { get; set; }
        public string ArchivedAt { get; set; }

        public string StorageDisplay => StorageMB >= 1024 ? $"{StorageMB / 1024.0:F2} GB" : $"{StorageMB:F0} MB";
        public string VersionSizeDisplay => VersionSizeMB >= 1024 ? $"{VersionSizeMB / 1024.0:F2} GB" : $"{VersionSizeMB:F2} MB";
        public string LastModifiedDisplay => LastModified?.ToString("dd/MM/yyyy") ?? "-";
        public string CreatedDisplay => Created?.ToString("dd/MM/yyyy") ?? "-";
    }

    public class ArchiveQueueData
    {
        public string LastUpdated { get; set; }
        public System.Collections.Generic.List<ArchiveQueueItem> Sites { get; set; } = new System.Collections.Generic.List<ArchiveQueueItem>();
    }

    public class ArchiveQueueItem
    {
        public string SiteUrl { get; set; }
        public string Title { get; set; }
        public double StorageUsedMB { get; set; }
        public int DaysInactive { get; set; }
        public string LastModified { get; set; }
        public string Owner { get; set; }
        public string QueuedAt { get; set; }
        public string Status { get; set; }
        public string Source { get; set; }
        public string ArchivedAt { get; set; }
        public string ArchiveStatus { get; set; }
        public string LockState { get; set; }
        public string Error { get; set; }
    }

    public class ScopeSiteItem
    {
        public string SiteUrl { get; set; }
        public string Reason { get; set; }
    }

    public class FileArchiveQueueData
    {
        public string LastUpdated { get; set; }
        public System.Collections.Generic.List<FileArchiveQueueItem> Items { get; set; } = new System.Collections.Generic.List<FileArchiveQueueItem>();
    }

    public class FileArchiveQueueItem
    {
        public string SiteUrl { get; set; }
        public string FileUrl { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string FileExtension { get; set; }
        public double FileSizeMB { get; set; }
        public string LastModified { get; set; }
        public string QueuedAt { get; set; }
        public string Status { get; set; }
        public string Source { get; set; }
        public string Error { get; set; }
    }
}