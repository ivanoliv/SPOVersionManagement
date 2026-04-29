using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
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
        /// Generates a machine-derived salt from hardware identity (immutable per machine).
        /// Uses machine name + first active NIC MAC address to create deterministic, non-guessable salt.
        /// Cannot be changed without hardware modification.
        /// </summary>
        public static string GenerateMachineSalt()
        {
            try
            {
                // Combine machine name + first active network interface MAC
                var machineName = Environment.MachineName ?? "unknown";
                var macAddress = "00-00-00-00-00-00";

                var activeNic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up && 
                                         n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                if (activeNic != null)
                    macAddress = activeNic.GetPhysicalAddress().ToString();

                string machineId = $"{machineName}|{macAddress}";

                // Hash machine identity to get deterministic salt
                using (var sha = SHA256.Create())
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(machineId);
                    byte[] hash = sha.ComputeHash(bytes);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 32);
                }
            }
            catch
            {
                // Fallback: use machine name if network access fails
                try
                {
                    using (var sha = SHA256.Create())
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(Environment.MachineName ?? "default");
                        byte[] hash = sha.ComputeHash(bytes);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 32);
                    }
                }
                catch
                {
                    return "default_machine_salt";
                }
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
        /// Sends anonymous session telemetry after a management session completes.
        /// Fire-and-forget — never blocks the UI, failures are silently ignored.
        /// </summary>
        public async Task SendSessionStatsAsync(long storageFreedBytes, long versionsDeleted, int sitesProcessed)
        {
            if (string.IsNullOrEmpty(_endpoint)) return;

            try
            {
                var payload = new TelemetryPayload
                {
                    TenantHash = _tenantHash,
                    AppVersion = _appVersion,
                    StorageFreedBytes = storageFreedBytes,
                    VersionsDeleted = versionsDeleted,
                    SitesProcessed = sitesProcessed,
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
