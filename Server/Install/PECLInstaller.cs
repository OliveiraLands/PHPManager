//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Install
{

    /// <summary>
    /// Installs a PECL extension build: php_*.dll goes to the extension directory and
    /// any dependency DLL shipped in the zip goes to the PHP directory.
    /// </summary>
    public static class PECLInstaller
    {

        /// <summary>
        /// Returns the file name of the installed extension DLL (e.g. php_redis.dll).
        /// </summary>
        public static string Install(PECLPackage package, string phpDirectory, string extensionDirectory, bool overwrite)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            string zipPath = WebDownloader.GetTempFilePath(package.FileName);
            try
            {
                WebDownloader.DownloadFile(package.Url, zipPath);

                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    string extensionFile = null;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.Name.StartsWith("php_", StringComparison.OrdinalIgnoreCase) &&
                            entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            extensionFile = entry.Name;
                            string target = Path.Combine(extensionDirectory, entry.Name);
                            if (File.Exists(target) && !overwrite)
                            {
                                throw new PHPInstallException("ErrorExtensionFileAlreadyExists",
                                    String.Format(CultureInfo.InvariantCulture, "The file {0} already exists.", target));
                            }
                        }
                    }

                    if (extensionFile == null)
                    {
                        throw new PHPInstallException("ErrorCannotInstallExtension",
                            String.Format(CultureInfo.InvariantCulture, "{0} does not contain a php_*.dll file.", package.FileName));
                    }

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (!entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (String.Equals(entry.Name, extensionFile, StringComparison.OrdinalIgnoreCase))
                        {
                            entry.ExtractToFile(Path.Combine(extensionDirectory, entry.Name), true);
                        }
                        else
                        {
                            // Dependency libraries must be next to php-cgi.exe. Never replace a DLL that
                            // PHP itself ships (e.g. libcrypto), as it may be a different build.
                            string target = Path.Combine(phpDirectory, entry.Name);
                            if (!File.Exists(target))
                            {
                                entry.ExtractToFile(target, false);
                            }
                        }
                    }

                    return extensionFile;
                }
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }
        }
    }
}
