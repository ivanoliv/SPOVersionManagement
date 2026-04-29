using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SPOVersionManagement.Models;

namespace SPOVersionManagement.Services
{
    public class GitHubUpdateService
    {
        private readonly string _repoSlug;
        private readonly string _currentVersion;
        private static readonly HttpClient _http;

        static GitHubUpdateService()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("SPOVersionManagement", "1.0"));
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            // Set timeout to 10 seconds for faster failure on unreachable networks
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        public GitHubUpdateService(string repoSlug, string currentVersion)
        {
            _repoSlug = repoSlug;
            _currentVersion = currentVersion ?? "0.0.0.0";
        }

        /// <summary>
        /// Checks GitHub for the latest release asynchronously with cancellation support.
        /// Returns null on error or timeout.
        /// </summary>
        public async Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"https://api.github.com/repos/{_repoSlug}/releases/latest";
                
                // Use CancellationToken with combined timeout (HttpClient timeout + explicit token)
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(12)); // 2 second buffer over HttpClient timeout
                    
                    var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                        .ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                        return null;

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<GitHubRelease>(json);
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout or cancellation - return null silently
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns true if the remote release version is newer than the current version.
        /// </summary>
        public bool IsNewerVersion(GitHubRelease release)
        {
            if (release == null) return false;

            Version current, remote;
            if (!Version.TryParse(_currentVersion, out current)) return false;
            if (!Version.TryParse(release.VersionNumber, out remote)) return false;

            return remote > current;
        }

        /// <summary>
        /// Downloads a release asset (ZIP) to the specified path with progress reporting.
        /// </summary>
        public async Task<bool> DownloadAssetAsync(GitHubAsset asset, string destinationPath,
            IProgress<int> progress = null, CancellationToken cancellationToken = default)
        {
            if (asset == null || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
                return false;

            try
            {
                using (var response = await _http.GetAsync(asset.BrowserDownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1;

                    using (var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var fileStream = new System.IO.FileStream(destinationPath,
                        System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None,
                        bufferSize: 81920, useAsync: true))
                    {
                        byte[] buffer = new byte[81920];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                            .ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken)
                                .ConfigureAwait(false);
                            totalRead += bytesRead;

                            if (totalBytes > 0 && progress != null)
                            {
                                int pct = (int)((totalRead * 100) / totalBytes);
                                progress.Report(pct);
                            }
                        }
                    }
                }
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets all releases (for release notes history) with cancellation support.
        /// </summary>
        public async Task<GitHubRelease[]> GetAllReleasesAsync(int perPage = 10,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"https://api.github.com/repos/{_repoSlug}/releases?per_page={perPage}";
                
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(12));
                    
                    var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                        .ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                        return new GitHubRelease[0];

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<GitHubRelease[]>(json) ?? new GitHubRelease[0];
                }
            }
            catch (OperationCanceledException)
            {
                return new GitHubRelease[0];
            }
            catch
            {
                return new GitHubRelease[0];
            }
        }
    }
}
