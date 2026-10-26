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
    /// Downloads, verifies and extracts a PHP for Windows build.
    /// </summary>
    public static class PHPInstaller
    {

        /// <summary>
        /// Downloads the release, checks its SHA-256 (when published) and extracts it to targetDirectory.
        /// Returns the full path of php-cgi.exe.
        /// </summary>
        public static string DownloadAndExtract(PHPRelease release, string targetDirectory)
        {
            if (release == null)
            {
                throw new ArgumentNullException("release");
            }

            if (String.IsNullOrEmpty(targetDirectory))
            {
                targetDirectory = GetDefaultInstallDirectory(release);
            }

            targetDirectory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(targetDirectory));

            if (Directory.Exists(targetDirectory) &&
                Directory.GetFileSystemEntries(targetDirectory).Length > 0)
            {
                throw new PHPInstallException("ErrorTargetFolderNotEmpty",
                    String.Format(CultureInfo.InvariantCulture, "The folder {0} is not empty. Choose an empty or new folder.", targetDirectory));
            }

            string zipPath = WebDownloader.GetTempFilePath(release.FileName);
            try
            {
                WebDownloader.DownloadFile(release.Url, zipPath);
                WebDownloader.VerifySha256(zipPath, release.Sha256);

                Directory.CreateDirectory(targetDirectory);
                ZipFile.ExtractToDirectory(zipPath, targetDirectory);
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }

            string phpCgiPath = Path.Combine(targetDirectory, "php-cgi.exe");
            if (!File.Exists(phpCgiPath))
            {
                throw new PHPInstallException("ErrorPHPCannotRun",
                    String.Format(CultureInfo.InvariantCulture, "php-cgi.exe was not found in {0} after extracting {1}.", targetDirectory, release.FileName));
            }

            PHPBuildDetector.EnsurePHPRuns(phpCgiPath);

            return phpCgiPath;
        }

        /// <summary>
        /// Default folder for a release, e.g. C:\Program Files\PHP\8.4.27-nts-x64
        /// </summary>
        public static string GetDefaultInstallDirectory(PHPRelease release)
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return Path.Combine(Path.Combine(programFiles, "PHP"), GetFolderName(release));
        }

        /// <summary>
        /// Folder used for a side by side upgrade: next to the folder of the current PHP version.
        /// </summary>
        public static string GetSideBySideDirectory(string currentPHPDirectory, PHPRelease release)
        {
            string current = currentPHPDirectory.TrimEnd('\\', '/');
            string parent = Path.GetDirectoryName(current);
            if (String.IsNullOrEmpty(parent))
            {
                return GetDefaultInstallDirectory(release);
            }

            return Path.Combine(parent, GetFolderName(release));
        }

        private static string GetFolderName(PHPRelease release)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}",
                release.Version, release.ThreadSafe ? "ts" : "nts", release.Architecture);
        }
    }
}
