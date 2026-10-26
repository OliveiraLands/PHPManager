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
using System.IO;
using System.Text.RegularExpressions;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Install
{

    /// <summary>
    /// Compares the php.ini of one PHP installation with the php.ini of another one and copies
    /// the selected settings and extensions.
    /// </summary>
    public static class PHPIniMigrator
    {
        // Settings that describe the PHP installation itself and must not be copied as is
        private static readonly string[] IgnoredSettings = new string[] { "extension_dir" };

        /// <summary>
        /// Settings that differ and extensions that are enabled in the source but not in the target.
        /// </summary>
        public static RemoteObjectCollection<PHPIniMigrationItem> GetPreview(
            PHPIniFile source, string sourceDirectory,
            PHPIniFile target, string targetDirectory, string targetExtensionDirectory)
        {
            RemoteObjectCollection<PHPIniMigrationItem> result = new RemoteObjectCollection<PHPIniMigrationItem>();

            HashSet<string> knownDirectives = GetKnownDirectives(Path.Combine(targetDirectory, "php.exe"));
            HashSet<string> extensionNames = GetExtensionNames(source, targetExtensionDirectory);

            // Settings: when a directive appears more than once the last one wins, as in PHP
            Dictionary<string, PHPIniSetting> sourceSettings = new Dictionary<string, PHPIniSetting>(StringComparer.OrdinalIgnoreCase);
            List<string> order = new List<string>();
            foreach (PHPIniSetting setting in source.Settings)
            {
                if (String.IsNullOrEmpty(setting.Name) || Array.IndexOf(IgnoredSettings, setting.Name.ToLowerInvariant()) >= 0)
                {
                    continue;
                }
                if (!sourceSettings.ContainsKey(setting.Name))
                {
                    order.Add(setting.Name);
                }
                sourceSettings[setting.Name] = setting;
            }

            foreach (string name in order)
            {
                PHPIniSetting setting = sourceSettings[name];
                string value = RewritePaths(setting.Value, sourceDirectory, targetDirectory);
                PHPIniSetting existing = target.GetSetting(name);

                if (existing != null && String.Equals(existing.GetTrimmedValue(), Trim(value), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                PHPIniMigrationItem item = new PHPIniMigrationItem();
                item.Kind = PHPIniMigrationKind.Setting;
                item.Name = name;
                item.Section = String.IsNullOrEmpty(setting.Section) ? "PHP" : setting.Section;
                item.SourceValue = value;
                item.TargetValue = (existing != null) ? existing.Value : String.Empty;
                item.Status = (existing != null) ? PHPIniMigrationStatus.Changed : PHPIniMigrationStatus.New;

                if (String.Equals(name, "zend_extension", StringComparison.OrdinalIgnoreCase))
                {
                    if (!ExtensionFileExists(targetExtensionDirectory, Trim(value)))
                    {
                        item.Status = PHPIniMigrationStatus.MissingExtension;
                    }
                }
                else if (knownDirectives != null && !knownDirectives.Contains(name) && !BelongsToExtension(name, extensionNames))
                {
                    item.Status = PHPIniMigrationStatus.Unsupported;
                }

                item.Selected = (item.Status == PHPIniMigrationStatus.New || item.Status == PHPIniMigrationStatus.Changed);
                result.Add(item);
            }

            // Extensions enabled in the source php.ini
            HashSet<string> targetEnabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PHPIniExtension extension in target.Extensions)
            {
                if (extension.Enabled)
                {
                    targetEnabled.Add(PHPIniExtension.NormalizeName(extension.Name));
                }
            }

            foreach (PHPIniExtension extension in source.Extensions)
            {
                if (!extension.Enabled)
                {
                    continue;
                }

                string name = PHPIniExtension.NormalizeName(extension.Name);
                if (targetEnabled.Contains(name))
                {
                    continue;
                }

                PHPIniMigrationItem item = new PHPIniMigrationItem();
                item.Kind = PHPIniMigrationKind.Extension;
                item.Name = "php_" + name + ".dll";
                item.Section = String.Empty;
                item.SourceValue = extension.Name;
                item.TargetValue = String.Empty;
                item.Status = ExtensionFileExists(targetExtensionDirectory, item.Name) ? PHPIniMigrationStatus.New : PHPIniMigrationStatus.MissingExtension;
                item.Selected = (item.Status == PHPIniMigrationStatus.New);
                result.Add(item);
                targetEnabled.Add(name);
            }

            ResolveMissingExtensionsFromPECL(result, targetDirectory);

            return result;
        }

        /// <summary>
        /// For every extension whose DLL is missing in the target, looks for a PECL build compatible with the
        /// target PHP version. When one exists the item can be copied and the build is installed on apply;
        /// otherwise the item stays MissingExtension and is never copied.
        /// </summary>
        private static void ResolveMissingExtensionsFromPECL(RemoteObjectCollection<PHPIniMigrationItem> items, string targetDirectory)
        {
            PHPBuildInfo targetBuild = null;
            HashSet<string> peclNames = null;

            foreach (PHPIniMigrationItem item in items)
            {
                if (item.Status != PHPIniMigrationStatus.MissingExtension)
                {
                    continue;
                }

                string name = PHPIniExtension.NormalizeName(item.Kind == PHPIniMigrationKind.Extension ? item.Name : Trim(item.SourceValue));
                if (String.IsNullOrEmpty(name))
                {
                    continue;
                }

                try
                {
                    if (targetBuild == null)
                    {
                        targetBuild = PHPBuildDetector.Detect(Path.Combine(targetDirectory, "php-cgi.exe"));
                        peclNames = new HashSet<string>(PECLCatalog.GetExtensionNames(null), StringComparer.OrdinalIgnoreCase);
                    }

                    if (!peclNames.Contains(name))
                    {
                        continue;
                    }

                    List<PECLPackage> packages = PECLCatalog.GetCompatiblePackages(name, targetBuild);
                    if (packages.Count > 0)
                    {
                        item.PECLPackage = packages[0];
                        item.Status = PHPIniMigrationStatus.AvailableFromPECL;
                        item.Selected = true;
                    }
                }
                catch (PHPInstallException)
                {
                    // Without access to downloads.php.net the extension simply stays "missing"
                    return;
                }
            }
        }

        /// <summary>
        /// Writes the selected items to the target php.ini. Items whose extension DLL is missing are only
        /// written after their compatible PECL build has been installed; items that cannot be copied are ignored.
        /// </summary>
        public static void Apply(PHPIniFile target, IEnumerable<PHPIniMigrationItem> items,
                                 string targetDirectory, string targetExtensionDirectory)
        {
            List<PHPIniSetting> settings = new List<PHPIniSetting>();
            List<PHPIniExtension> extensions = new List<PHPIniExtension>();
            PHPBuildInfo targetBuild = null;

            foreach (PHPIniMigrationItem item in items)
            {
                if (!item.Selected || !PHPIniMigrationStatus.CanBeCopied(item.Status))
                {
                    continue;
                }

                if (item.Status == PHPIniMigrationStatus.AvailableFromPECL)
                {
                    PECLPackage package = item.PECLPackage;
                    if (package == null)
                    {
                        continue;
                    }

                    // The build was chosen for this PHP version in the preview; never trust it blindly
                    if (targetBuild == null)
                    {
                        targetBuild = PHPBuildDetector.Detect(Path.Combine(targetDirectory, "php-cgi.exe"));
                    }
                    if (!String.Equals(package.PHPBranch, targetBuild.Branch, StringComparison.Ordinal) ||
                        package.ThreadSafe != targetBuild.ThreadSafe ||
                        !String.Equals(package.Compiler, targetBuild.Compiler, StringComparison.OrdinalIgnoreCase) ||
                        !String.Equals(package.Architecture, targetBuild.Architecture, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string installed = PECLInstaller.Install(package, targetDirectory, targetExtensionDirectory, false);
                    if (item.Kind == PHPIniMigrationKind.Extension)
                    {
                        item.Name = installed;
                    }
                }

                if (item.Kind == PHPIniMigrationKind.Extension)
                {
                    extensions.Add(new PHPIniExtension(item.Name, true));
                }
                else
                {
                    settings.Add(new PHPIniSetting(item.Name, item.SourceValue, item.Section));
                }
            }

            if (settings.Count == 0 && extensions.Count == 0)
            {
                return;
            }

            target.AddOrUpdateSettings(settings);
            target.UpdateExtensions(extensions);
            target.Save(target.FileName);
        }

        /// <summary>
        /// Copies extension DLLs that exist in the source extension directory but not in the target one, together
        /// with DLLs from the source PHP directory that the target PHP directory does not have (PECL dependencies).
        /// Only valid when both builds have the same branch, thread safety, compiler and architecture.
        /// </summary>
        public static void CopyMissingExtensionFiles(string sourceDirectory, string sourceExtensionDirectory,
                                                     string targetDirectory, string targetExtensionDirectory)
        {
            if (Directory.Exists(sourceExtensionDirectory) && Directory.Exists(targetExtensionDirectory))
            {
                foreach (string file in Directory.GetFiles(sourceExtensionDirectory, "php_*.dll"))
                {
                    string target = Path.Combine(targetExtensionDirectory, Path.GetFileName(file));
                    if (!File.Exists(target))
                    {
                        File.Copy(file, target);
                    }
                }
            }

            foreach (string file in Directory.GetFiles(sourceDirectory, "*.dll"))
            {
                string target = Path.Combine(targetDirectory, Path.GetFileName(file));
                if (!File.Exists(target))
                {
                    File.Copy(file, target);
                }
            }
        }

        private static bool BelongsToExtension(string directive, HashSet<string> extensionNames)
        {
            int dot = directive.IndexOf('.');
            if (dot <= 0)
            {
                return false;
            }

            string prefix = directive.Substring(0, dot);
            return extensionNames.Contains(prefix) ||
                   // pdo_mysql.* -> pdo_mysql, opcache.* -> opcache, xdebug.* -> xdebug, mysqlnd.* -> mysqli
                   (String.Equals(prefix, "mysqlnd", StringComparison.OrdinalIgnoreCase) && extensionNames.Contains("mysqli"));
        }

        private static bool ExtensionFileExists(string extensionDirectory, string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                return false;
            }

            if (Path.IsPathRooted(name))
            {
                return File.Exists(name);
            }

            string normalized = PHPIniExtension.NormalizeName(name);
            return File.Exists(Path.Combine(extensionDirectory, "php_" + normalized + ".dll")) ||
                   File.Exists(Path.Combine(extensionDirectory, normalized + ".dll"));
        }

        private static HashSet<string> GetExtensionNames(PHPIniFile source, string targetExtensionDirectory)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PHPIniExtension extension in source.Extensions)
            {
                result.Add(PHPIniExtension.NormalizeName(extension.Name));
            }

            if (Directory.Exists(targetExtensionDirectory))
            {
                foreach (string file in Directory.GetFiles(targetExtensionDirectory, "php_*.dll"))
                {
                    result.Add(PHPIniExtension.NormalizeName(Path.GetFileName(file)));
                }
            }

            // zend_extension=opcache / xdebug
            result.Add("opcache");
            result.Add("xdebug");

            return result;
        }

        /// <summary>
        /// Directives known by the target PHP core (without loading any php.ini), or null if php.exe cannot run.
        /// </summary>
        private static HashSet<string> GetKnownDirectives(string phpExe)
        {
            if (!File.Exists(phpExe))
            {
                return null;
            }

            string output;
            try
            {
                if (PHPBuildDetector.RunPHP(phpExe, "-n -r \"echo implode(PHP_EOL, array_keys(ini_get_all(null, false)));\"", out output) != 0)
                {
                    return null;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }

            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                result.Add(line.Trim());
            }

            return (result.Count > 0) ? result : null;
        }

        internal static string RewritePaths(string value, string sourceDirectory, string targetDirectory)
        {
            if (String.IsNullOrEmpty(value) || String.IsNullOrEmpty(sourceDirectory) || String.IsNullOrEmpty(targetDirectory))
            {
                return value;
            }

            string source = sourceDirectory.TrimEnd('\\', '/');
            string target = targetDirectory.TrimEnd('\\', '/');

            // Replace "C:\PHP\8.4.26" and "C:/PHP/8.4.26" when followed by a separator, a quote or the end of the value
            string result = value;
            foreach (string variant in new string[] { source, source.Replace('\\', '/') })
            {
                string replacement = (variant.IndexOf('/') >= 0) ? target.Replace('\\', '/') : target;
                result = Regex.Replace(result, Regex.Escape(variant) + @"(?=[\\/""';]|$)", replacement.Replace("$", "$$"), RegexOptions.IgnoreCase);
            }

            return result;
        }

        private static string Trim(string value)
        {
            return (value ?? String.Empty).Trim(new char[] { ' ', '"' });
        }
    }
}
