//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Install
{

    /// <summary>
    /// Reads the list of PHP for Windows builds published at downloads.php.net
    /// (the same files referenced by the PHP.PHP.* winget packages).
    /// </summary>
    public static class PHPReleaseCatalog
    {
        public const string ReleasesUrl = "https://downloads.php.net/~windows/releases/";
        private const string ArchivesFolder = "archives/";

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
        private static readonly object CacheLock = new object();
        private static List<PHPRelease> _latestCache;
        private static List<PHPRelease> _archivesCache;
        private static DateTime _latestCacheTime;
        private static DateTime _archivesCacheTime;

        private static readonly Regex BuildKeyRegex = new Regex(@"^(?<ts>ts|nts)-(?<compiler>vc\d+|vs\d+)-(?<arch>x86|x64)$", RegexOptions.IgnoreCase);
        private static readonly Regex ArchiveFileRegex = new Regex(
            @"href=""(?<file>php-(?<version>\d+\.\d+\.\d+)(?:-\d+)?(?<nts>-nts)?-Win32-(?<compiler>vc\d+|vs\d+)-(?<arch>x86|x64)\.zip)""",
            RegexOptions.IgnoreCase);

        /// <summary>
        /// Returns the latest release of every supported branch and, optionally, older patch releases.
        /// </summary>
        public static List<PHPRelease> GetReleases(bool includeArchives)
        {
            List<PHPRelease> result = new List<PHPRelease>(GetLatestReleases());

            if (includeArchives)
            {
                HashSet<string> latestFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> latestKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (PHPRelease release in result)
                {
                    latestFiles.Add(release.FileName);
                    latestKeys.Add(GetBuildKey(release));
                }

                foreach (PHPRelease release in GetArchivedReleases())
                {
                    if (!latestFiles.Contains(release.FileName) && !latestKeys.Contains(GetBuildKey(release)))
                    {
                        result.Add(release);
                    }
                }
            }

            result.Sort(CompareReleases);
            return result;
        }

        /// <summary>
        /// Returns the latest patch release of the same branch, thread safety, compiler and
        /// architecture as the given build, or null if the build is already up to date.
        /// </summary>
        public static PHPRelease GetUpdateFor(PHPBuildInfo build)
        {
            if (build == null || String.IsNullOrEmpty(build.Version))
            {
                return null;
            }

            Version current = PHPBuildInfo.ParseVersion(build.Version);
            foreach (PHPRelease release in GetLatestReleases())
            {
                if (String.Equals(release.Branch, build.Branch, StringComparison.Ordinal) &&
                    release.ThreadSafe == build.ThreadSafe &&
                    String.Equals(release.Architecture, build.Architecture, StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(release.Compiler, build.Compiler, StringComparison.OrdinalIgnoreCase) &&
                    PHPBuildInfo.ParseVersion(release.Version) > current)
                {
                    return release;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a release published at downloads.php.net. Used to never trust download URLs and
        /// checksums sent by a client: only the version, thread safety, compiler and architecture are taken.
        /// </summary>
        public static PHPRelease FindRelease(string version, bool threadSafe, string compiler, string architecture)
        {
            foreach (bool includeArchives in new bool[] { false, true })
            {
                foreach (PHPRelease release in GetReleases(includeArchives))
                {
                    if (String.Equals(release.Version, version, StringComparison.Ordinal) &&
                        release.ThreadSafe == threadSafe &&
                        String.Equals(release.Compiler, compiler, StringComparison.OrdinalIgnoreCase) &&
                        String.Equals(release.Architecture, architecture, StringComparison.OrdinalIgnoreCase))
                    {
                        return release;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// True when the branch of the given build is still listed as supported on downloads.php.net.
        /// </summary>
        public static bool IsBranchSupported(PHPBuildInfo build)
        {
            foreach (PHPRelease release in GetLatestReleases())
            {
                if (String.Equals(release.Branch, build.Branch, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareReleases(PHPRelease x, PHPRelease y)
        {
            int result = PHPBuildInfo.ParseVersion(y.Version).CompareTo(PHPBuildInfo.ParseVersion(x.Version));
            if (result == 0)
            {
                // NTS first: it is the recommended build for IIS FastCGI
                result = x.ThreadSafe.CompareTo(y.ThreadSafe);
            }
            if (result == 0)
            {
                // x64 first
                result = String.Compare(y.Architecture, x.Architecture, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }

        private static List<PHPRelease> GetArchivedReleases()
        {
            lock (CacheLock)
            {
                if (_archivesCache != null && DateTime.UtcNow - _archivesCacheTime < CacheDuration)
                {
                    return _archivesCache;
                }
            }

            string html = WebDownloader.GetString(ReleasesUrl + ArchivesFolder);
            Dictionary<string, PHPRelease> releases = new Dictionary<string, PHPRelease>(StringComparer.OrdinalIgnoreCase);

            foreach (Match m in ArchiveFileRegex.Matches(html))
            {
                string version = m.Groups["version"].Value;
                // Only PHP 7.0 and later are useful for FastCGI on current IIS versions
                if (PHPBuildInfo.ParseVersion(version).Major < 7)
                {
                    continue;
                }

                PHPRelease release = new PHPRelease();
                release.Version = version;
                release.ThreadSafe = !m.Groups["nts"].Success;
                release.Compiler = m.Groups["compiler"].Value.ToLowerInvariant();
                release.Architecture = m.Groups["arch"].Value.ToLowerInvariant();
                release.FileName = m.Groups["file"].Value;
                release.Url = ReleasesUrl + ArchivesFolder + release.FileName;
                release.IsLatest = false;

                // Rebuilds (php-x.y.z-1-...) replace the original build of the same version
                releases[GetBuildKey(release)] = release;
            }

            List<PHPRelease> result = new List<PHPRelease>(releases.Values);

            lock (CacheLock)
            {
                _archivesCache = result;
                _archivesCacheTime = DateTime.UtcNow;
            }

            return result;
        }

        private static string GetBuildKey(PHPRelease release)
        {
            return String.Join("|", new string[] {
                PHPBuildInfo.ParseVersion(release.Version).ToString(),
                release.ThreadSafe ? "ts" : "nts",
                release.Compiler,
                release.Architecture });
        }

        private static List<PHPRelease> GetLatestReleases()
        {
            lock (CacheLock)
            {
                if (_latestCache != null && DateTime.UtcNow - _latestCacheTime < CacheDuration)
                {
                    return _latestCache;
                }
            }

            string json = WebDownloader.GetString(ReleasesUrl + "releases.json");
            List<PHPRelease> result = ParseReleasesJson(json);

            lock (CacheLock)
            {
                _latestCache = result;
                _latestCacheTime = DateTime.UtcNow;
            }

            return result;
        }

        private static string GetString(IDictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary != null && dictionary.TryGetValue(key, out value) && value != null)
            {
                return value.ToString();
            }

            return String.Empty;
        }

        internal static List<PHPRelease> ParseReleasesJson(string json)
        {
            List<PHPRelease> result = new List<PHPRelease>();

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;
            IDictionary<string, object> branches = serializer.DeserializeObject(json) as IDictionary<string, object>;
            if (branches == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, object> branch in branches)
            {
                IDictionary<string, object> branchData = branch.Value as IDictionary<string, object>;
                if (branchData == null)
                {
                    continue;
                }

                // "8.4.27-1" means the first rebuild of 8.4.27
                string version = GetString(branchData, "version");
                string plainVersion = version;
                int dash = plainVersion.IndexOf('-');
                if (dash > 0)
                {
                    plainVersion = plainVersion.Substring(0, dash);
                }

                foreach (KeyValuePair<string, object> build in branchData)
                {
                    Match m = BuildKeyRegex.Match(build.Key);
                    if (!m.Success)
                    {
                        continue;
                    }

                    IDictionary<string, object> buildData = build.Value as IDictionary<string, object>;
                    IDictionary<string, object> zip = (buildData != null && buildData.ContainsKey("zip")) ? buildData["zip"] as IDictionary<string, object> : null;
                    string path = GetString(zip, "path");
                    if (String.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    PHPRelease release = new PHPRelease();
                    release.Version = plainVersion;
                    release.ThreadSafe = String.Equals(m.Groups["ts"].Value, "ts", StringComparison.OrdinalIgnoreCase);
                    release.Compiler = m.Groups["compiler"].Value.ToLowerInvariant();
                    release.Architecture = m.Groups["arch"].Value.ToLowerInvariant();
                    release.FileName = path;
                    release.Url = ReleasesUrl + path;
                    release.Sha256 = GetString(zip, "sha256");
                    release.FileSize = GetString(zip, "size");
                    release.IsLatest = true;
                    result.Add(release);
                }
            }

            return result;
        }
    }
}
