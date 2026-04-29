using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPOVersionManagement.Models;

namespace SPOVersionManagement.Services
{
    public class SiteDataService
    {
        private readonly ConfigurationService _config;
        private List<SiteCatalogEntry> _catalogCache;
        private DateTime _catalogLoadedAt;

        public SiteDataService(ConfigurationService config)
        {
            _config = config;
        }

        public List<SiteCatalogEntry> LoadCatalogSites(bool forceReload = false)
        {
            if (!forceReload && _catalogCache != null && (DateTime.UtcNow - _catalogLoadedAt).TotalMinutes < 5)
                return _catalogCache;

            string path = Path.Combine(_config.LogsPath, "AllSites.json");
            var sites = new List<SiteCatalogEntry>();
            if (!EnsureJsonFile(path, "[]"))
                return sites;

            if (new FileInfo(path).Length == 0)
            {
                SafeWriteAllText(path, "[]");
                return sites;
            }

            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new StreamReader(stream))
                using (var json = new JsonTextReader(reader))
                {
                    var serializer = new JsonSerializer();

                    // AllSites.json can be { "Sites": [...] } or plain [...]
                    if (!json.Read())
                        return sites;

                    if (json.TokenType == JsonToken.StartObject)
                    {
                        // Navigate into "Sites" array inside the wrapper object
                        while (json.Read())
                        {
                            if (json.TokenType == JsonToken.PropertyName && (string)json.Value == "Sites")
                            {
                                json.Read(); // move to StartArray
                                break;
                            }
                            else if (json.TokenType == JsonToken.StartArray || json.TokenType == JsonToken.StartObject)
                            {
                                json.Skip(); // skip non-Sites properties
                            }
                        }
                    }

                    if (json.TokenType != JsonToken.StartArray)
                        return sites;

                    while (json.Read())
                    {
                        if (json.TokenType == JsonToken.StartObject)
                        {
                            var obj = serializer.Deserialize<JObject>(json);
                            if (obj != null)
                                sites.Add(MapShortSite(obj));
                        }
                        else if (json.TokenType == JsonToken.EndArray)
                        {
                            break;
                        }
                    }
                }
            }
            catch
            {
                SafeWriteAllText(path, "[]");
                return sites;
            }

            _catalogCache = sites;
            _catalogLoadedAt = DateTime.UtcNow;
            return sites;
        }

        public List<SiteCatalogEntry> LoadArchiveCandidates()
        {
            return LoadArchiveAnalysisArray("Candidates");
        }

        public List<SiteCatalogEntry> LoadArchivedSites()
        {
            var sites = LoadArchiveAnalysisArray("ArchivedSites");
            if (sites.Count > 0)
                return sites;

            return LoadCatalogSites().Where(s => !string.IsNullOrEmpty(s.ArchiveStatus) && !s.ArchiveStatus.Equals("NotArchived", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public ArchiveQueueData LoadArchiveQueue()
        {
            string path = Path.Combine(_config.LogsPath, "ArchiveQueue.json");
            var empty = new ArchiveQueueData();
            string defaultJson = JsonConvert.SerializeObject(empty, Formatting.Indented);

            if (!EnsureJsonFile(path, defaultJson))
                return empty;

            string content = SafeReadAllText(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                SafeWriteAllText(path, defaultJson);
                return empty;
            }

            try
            {
                return JsonConvert.DeserializeObject<ArchiveQueueData>(content) ?? empty;
            }
            catch
            {
                SafeWriteAllText(path, defaultJson);
                return empty;
            }
        }

        public void SaveArchiveQueue(ArchiveQueueData queue)
        {
            queue.LastUpdated = DateTime.UtcNow.ToString("o");
            string path = Path.Combine(_config.LogsPath, "ArchiveQueue.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(queue, Formatting.Indented));
        }

        public FileArchiveQueueData LoadFileArchiveQueue()
        {
            string path = Path.Combine(_config.LogsPath, "FileArchiveQueue.json");
            var empty = new FileArchiveQueueData();
            string defaultJson = JsonConvert.SerializeObject(empty, Formatting.Indented);

            if (!EnsureJsonFile(path, defaultJson))
                return empty;

            string content = SafeReadAllText(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                SafeWriteAllText(path, defaultJson);
                return empty;
            }

            try
            {
                return JsonConvert.DeserializeObject<FileArchiveQueueData>(content) ?? empty;
            }
            catch
            {
                SafeWriteAllText(path, defaultJson);
                return empty;
            }
        }

        public void SaveFileArchiveQueue(FileArchiveQueueData queue)
        {
            queue.LastUpdated = DateTime.UtcNow.ToString("o");
            string path = Path.Combine(_config.LogsPath, "FileArchiveQueue.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(queue, Formatting.Indented));
        }

        public List<ScopeSiteItem> LoadScopeList(string fileName)
        {
            string path = Path.Combine(_config.RootPath, fileName);
            var items = new List<ScopeSiteItem>();
            if (!File.Exists(path))
                return items;

            var lines = File.ReadAllLines(path);
            bool header = true;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (header)
                {
                    header = false;
                    continue;
                }

                var parts = line.Split(new[] { ',' }, 2);
                string url = parts[0].Trim().Trim('"');
                string reason = parts.Length > 1 ? parts[1].Trim().Trim('"') : string.Empty;
                if (!string.IsNullOrEmpty(url))
                    items.Add(new ScopeSiteItem { SiteUrl = url, Reason = reason });
            }
            return items;
        }

        public void SaveScopeList(string fileName, IEnumerable<ScopeSiteItem> items)
        {
            string path = Path.Combine(_config.RootPath, fileName);
            var lines = new List<string> { "SiteUrl,Reason" };
            foreach (var item in items.Where(i => !string.IsNullOrWhiteSpace(i.SiteUrl)))
            {
                string url = item.SiteUrl.Trim();
                string reason = (item.Reason ?? string.Empty).Trim();
                if (url.Contains(",") || reason.Contains(","))
                    lines.Add($"\"{url}\",\"{reason}\"");
                else
                    lines.Add($"{url},{reason}");
            }

            File.WriteAllLines(path, lines);
        }

        private List<SiteCatalogEntry> LoadArchiveAnalysisArray(string propertyName)
        {
            string path = Path.Combine(_config.LogsPath, "ArchiveAnalysis.json");
            var sites = new List<SiteCatalogEntry>();
            if (!EnsureJsonFile(path, "{}"))
                return sites;

            string content = SafeReadAllText(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                SafeWriteAllText(path, "{}");
                return sites;
            }

            JObject root;
            try
            {
                root = JObject.Parse(content);
            }
            catch
            {
                SafeWriteAllText(path, "{}");
                return sites;
            }

            var arr = root[propertyName] as JArray;
            if (arr == null)
                return sites;

            foreach (var token in arr.OfType<JObject>())
                sites.Add(MapShortSite(token));
            return sites;
        }

        private static bool EnsureJsonFile(string path, string defaultContent)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (!File.Exists(path))
                    File.WriteAllText(path, defaultContent);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string SafeReadAllText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void SafeWriteAllText(string path, string content)
        {
            try
            {
                File.WriteAllText(path, content);
            }
            catch
            {
            }
        }

        private static SiteCatalogEntry MapShortSite(JObject obj)
        {
            return new SiteCatalogEntry
            {
                Url = ReadString(obj, "U", "SiteUrl", "Url"),
                Title = ReadString(obj, "T", "Title"),
                StorageMB = ReadStorageMB(obj),
                LastModified = ReadDate(obj, "D", "LastModified"),
                Created = ReadDate(obj, "C", "Created"),
                ArchiveStatus = ReadString(obj, "AS", "ArchiveStatus"),
                LockState = ReadString(obj, "LS", "LockState"),
                Owner = ReadString(obj, "O", "Owner"),
                Status = ReadString(obj, "ST", "Status"),
                VersionCount = ReadLong(obj, "VC", "VersionCount"),
                VersionSizeMB = ReadDouble(obj, "VS", "VersionSizeMB"),
                Template = ReadString(obj, "TM", "Template"),
                IsInactive = ReadBool(obj, "I", "IsInactive"),
                IsOwnerless = ReadBool(obj, "OL", "IsOwnerless"),
                SuggestedArchiveDate = ReadString(obj, "SA", "SuggestedArchiveDate"),
                IsCandidate = ReadBool(obj, "CT", "IsCandidate"),
                EffectiveDate = ReadString(obj, "ED", "EffectiveDate"),
                ArchivedBy = ReadString(obj, "AB", "ArchivedBy"),
                ArchivedAt = ReadString(obj, "AT", "ArchivedAt")
            };
        }

        private static double ReadStorageMB(JObject obj)
        {
            double mb = ReadDouble(obj, "S", "StorageUsedMB", "StorageUsageCurrentMB", "StorageUsageCurrent");
            if (mb > 0)
                return mb;

            double gb = ReadDouble(obj, "StorageUsageCurrentGB");
            if (gb > 0)
                return gb * 1024.0;

            return 0;
        }

        private static string ReadString(JObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                var token = obj[name];
                if (token != null && token.Type != JTokenType.Null)
                    return token.ToString();
            }
            return string.Empty;
        }

        private static double ReadDouble(JObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                var token = obj[name];
                if (token == null || token.Type == JTokenType.Null)
                    continue;

                if (double.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                    return value;
                if (double.TryParse(token.ToString(), out value))
                    return value;
            }
            return 0;
        }

        private static long ReadLong(JObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                var token = obj[name];
                if (token == null || token.Type == JTokenType.Null)
                    continue;
                if (long.TryParse(token.ToString(), out long value))
                    return value;
            }
            return 0;
        }

        private static bool ReadBool(JObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                var token = obj[name];
                if (token == null || token.Type == JTokenType.Null)
                    continue;
                if (bool.TryParse(token.ToString(), out bool value))
                    return value;
            }
            return false;
        }

        private static DateTime? ReadDate(JObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                var token = obj[name];
                if (token == null || token.Type == JTokenType.Null)
                    continue;
                if (DateTime.TryParse(token.ToString(), out DateTime value))
                    return value;
            }
            return null;
        }
    }
}