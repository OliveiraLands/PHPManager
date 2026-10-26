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
using System.Globalization;
using System.Text.RegularExpressions;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Install
{

    /// <summary>
    /// Reads the Windows builds of PECL extensions published at downloads.php.net/~windows/pecl.
    /// </summary>
    public static class PECLCatalog
    {
        public const string PECLUrl = "https://downloads.php.net/~windows/pecl/releases/";

        // Number of most recent extension versions inspected when looking for compatible builds
        private const int MaxVersionsToInspect = 8;
        // Stop after this many compatible builds have been found
        private const int MaxPackagesToReturn = 5;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
        private static readonly object CacheLock = new object();
        private static List<string> _namesCache;
        private static DateTime _namesCacheTime;

        private static readonly Regex ExtensionNameRegex = new Regex(@"^[A-Za-z0-9_][A-Za-z0-9_\-]*$");
        private static readonly Regex VersionRegex = new Regex(@"^[0-9][A-Za-z0-9_.\-]*$");

        private static readonly Regex FolderRegex = new Regex(@"href=""(?<name>[A-Za-z0-9_.\-]+)/""", RegexOptions.IgnoreCase);

        /// <summary>
        /// Names of all PECL extensions with Windows builds, optionally filtered by a substring.
        /// </summary>
        public static List<string> GetExtensionNames(string filter)
        {
            List<string> names;
            lock (CacheLock)
            {
                names = (_namesCache != null && DateTime.UtcNow - _namesCacheTime < CacheDuration) ? _namesCache : null;
            }

            if (names == null)
            {
                names = GetFolders(PECLUrl);
                names.Sort(StringComparer.OrdinalIgnoreCase);
                lock (CacheLock)
                {
                    _namesCache = names;
                    _namesCacheTime = DateTime.UtcNow;
                }
            }

            if (String.IsNullOrEmpty(filter))
            {
                return new List<string>(names);
            }

            return names.FindAll(delegate (string name)
            {
                return name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

        /// <summary>
        /// Builds of the given extension that match the PHP build (branch, TS/NTS, compiler and architecture),
        /// most recent version first.
        /// </summary>
        public static List<PECLPackage> GetCompatiblePackages(string extensionName, PHPBuildInfo build)
        {
            if (String.IsNullOrEmpty(extensionName))
            {
                throw new ArgumentNullException("extensionName");
            }

            // The name comes from the user: accept only names that can exist in the PECL index
            if (!ExtensionNameRegex.IsMatch(extensionName))
            {
                throw new ArgumentException("Invalid extension name.", "extensionName");
            }

            string extensionUrl = PECLUrl + Uri.EscapeDataString(extensionName) + "/";
            List<string> versions = GetFolders(extensionUrl);
            // Stable versions first (most recent first), then alpha/beta/RC versions
            versions.Sort(delegate (string x, string y)
            {
                int order = IsPrerelease(x).CompareTo(IsPrerelease(y));
                return (order != 0) ? order : PHPBuildInfo.ParseVersion(y).CompareTo(PHPBuildInfo.ParseVersion(x));
            });

            // php_redis-6.3.0-8.4-nts-vs17-x64.zip
            string suffix = String.Format(CultureInfo.InvariantCulture, "-{0}-{1}-{2}-{3}.zip",
                build.Branch, build.ThreadSafe ? "ts" : "nts", build.Compiler, build.Architecture);

            List<PECLPackage> result = new List<PECLPackage>();
            versions.RemoveAll(delegate (string version) { return !VersionRegex.IsMatch(version) || version.Contains(".."); });

            for (int i = 0; i < versions.Count && i < MaxVersionsToInspect && result.Count < MaxPackagesToReturn; i++)
            {
                string version = versions[i];
                string versionUrl = extensionUrl + Uri.EscapeDataString(version) + "/";
                string expectedFile = "php_" + extensionName + "-" + version + suffix;

                string html = WebDownloader.GetString(versionUrl);
                foreach (Match m in Regex.Matches(html, @"href=""(?<file>[^""/]+\.zip)""", RegexOptions.IgnoreCase))
                {
                    string file = m.Groups["file"].Value;
                    if (String.Equals(file, expectedFile, StringComparison.OrdinalIgnoreCase))
                    {
                        PECLPackage package = new PECLPackage();
                        package.Name = extensionName;
                        package.Version = version;
                        package.PHPBranch = build.Branch;
                        package.ThreadSafe = build.ThreadSafe;
                        package.Compiler = build.Compiler;
                        package.Architecture = build.Architecture;
                        package.FileName = file;
                        package.Url = versionUrl + file;
                        result.Add(package);
                        break;
                    }
                }
            }

            return result;
        }

        private static bool IsPrerelease(string version)
        {
            foreach (char c in version)
            {
                if (Char.IsLetter(c))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> GetFolders(string url)
        {
            string html = WebDownloader.GetString(url);
            List<string> result = new List<string>();
            foreach (Match m in FolderRegex.Matches(html))
            {
                string name = m.Groups["name"].Value;
                if (name != "." && name != ".." && !result.Contains(name))
                {
                    result.Add(name);
                }
            }

            return result;
        }
    }
}
