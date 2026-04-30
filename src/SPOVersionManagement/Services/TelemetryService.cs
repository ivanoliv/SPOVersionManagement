using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SPOVersionManagement.Models;

namespace SPOVersionManagement.Services
{
    public class TelemetryService
    {
        private readonly string _endpoint;
        private readonly string _tenantHash;
        private readonly string _appVersion;
        private readonly string _salt;
        private static readonly HttpClient _http;

        static TelemetryService()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Creates a telemetry service. tenantId is hashed immediately, never stored in plain text.
        /// </summary>
        public TelemetryService(string endpoint, string tenantId, string appVersion, string salt = null)
        {
            _endpoint = (endpoint ?? string.Empty).TrimEnd('/');
            _appVersion = appVersion;
            _salt = string.IsNullOrWhiteSpace(salt) ? "default" : salt.Trim();
            _tenantHash = HashTenantId(tenantId, _salt);
        }

        /// <summary>
        /// Generates a deterministic salt derived from the TenantId itself.
        /// This ensures the same tenant always produces the same hash regardless of which machine runs the tool.
        /// Important: always run from a consistent environment so the tenant hash remains stable for deduplication.
        /// </summary>
        public static string GenerateTenantSalt(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return "spo-vm-default-salt";

            using (var sha = SHA256.Create())
            {
                // Deterministic: same tenantId always produces same salt
                string input = tenantId.ToLowerInvariant().Trim() + "|spo-vm-telemetry";
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 32);
            }
        }

        /// <summary>
        /// SHA256 hash of TenantId + local secret salt.
        /// </summary>
        private static string HashTenantId(string tenantId, string salt)
        {
            if (string.IsNullOrEmpty(tenantId)) return "anonymous";

            using (var sha = SHA256.Create())
            {
                string key = tenantId.ToLowerInvariant().Trim() + "|" + (salt ?? "default");
                byte[] bytes = Encoding.UTF8.GetBytes(key);
                byte[] hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Hashes a WorkItemId (GUID or synthetic key) so no raw identifiers are sent.
        /// Same WorkItemId always produces same hash — used for deduplication on the backend.
        /// </summary>
        public static string HashWorkItemId(string workItemId)
        {
            if (string.IsNullOrWhiteSpace(workItemId)) return "";

            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(workItemId.ToLowerInvariant().Trim());
                byte[] hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 32);
            }
        }

        /// <summary>
        /// Hashes a site URL so no raw URLs are sent. Same URL always produces same hash.
        /// </summary>
        public static string HashSiteUrl(string siteUrl)
        {
            if (string.IsNullOrWhiteSpace(siteUrl)) return "";

            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(siteUrl.ToLowerInvariant().Trim());
                byte[] hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 16);
            }
        }

        /// <summary>
        /// Sends telemetry for a single completed site job.
        /// WorkItemId and SiteUrl are hashed before sending — no raw GUIDs or URLs leave the machine.
        /// Fire-and-forget — never blocks the UI.
        /// </summary>
        public async Task SendSiteCompletionAsync(string workItemId, string siteUrl, string jobType, 
            long storageFreedBytes, long versionsDeleted)
        {
            if (string.IsNullOrEmpty(_endpoint)) return;

            try
            {
                var payload = new TelemetryPayload
                {
                    TenantHash = _tenantHash,
                    AppVersion = _appVersion,
                    WorkItemId = HashWorkItemId(workItemId),
                    SiteUrl = HashSiteUrl(siteUrl),
                    JobType = jobType ?? "",
                    StorageFreedBytes = storageFreedBytes,
                    VersionsDeleted = versionsDeleted,
                    SitesProcessed = 1,
                    Timestamp = DateTime.UtcNow.ToString("o")
                };

                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _http.PostAsync(_endpoint + "/api/telemetry", content).ConfigureAwait(false);
            }
            catch
            {
                // Telemetry is best-effort — never throw
            }
        }

        /// <summary>
        /// Sends a batch of historical site completions in a single request.
        /// Used on first run to sync existing execution history without sending duplicates.
        /// All identifiers are hashed before sending.
        /// </summary>
        public async Task SendBatchAsync(TelemetryPayload[] items)
        {
            if (string.IsNullOrEmpty(_endpoint) || items == null || items.Length == 0) return;

            try
            {
                var batch = new { items };
                string json = JsonConvert.SerializeObject(batch);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _http.PostAsync(_endpoint + "/api/telemetry/batch", content).ConfigureAwait(false);
            }
            catch
            {
                // Telemetry is best-effort — never throw
            }
        }

        /// <summary>
        /// Builds a TelemetryPayload for a completed job (all fields hashed).
        /// Use with SendBatchAsync for bulk historical sync.
        /// </summary>
        public TelemetryPayload BuildPayload(string workItemId, string siteUrl, string jobType,
            long storageFreedBytes, long versionsDeleted, string timestamp = null)
        {
            return new TelemetryPayload
            {
                TenantHash = _tenantHash,
                AppVersion = _appVersion,
                WorkItemId = HashWorkItemId(workItemId),
                SiteUrl = HashSiteUrl(siteUrl),
                JobType = jobType ?? "",
                StorageFreedBytes = storageFreedBytes,
                VersionsDeleted = versionsDeleted,
                SitesProcessed = 1,
                Timestamp = timestamp ?? DateTime.UtcNow.ToString("o")
            };
        }

        /// <summary>
        /// Fetches aggregated global stats (total freed, tenants, etc.) for display.
        /// </summary>
        public async Task<GlobalStats> GetGlobalStatsAsync()
        {
            if (string.IsNullOrEmpty(_endpoint))
                return new GlobalStats();

            try
            {
                var response = await _http.GetAsync(_endpoint + "/api/stats").ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return new GlobalStats();

                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<GlobalStats>(json) ?? new GlobalStats();
            }
            catch
            {
                return new GlobalStats();
            }
        }
    }
}
