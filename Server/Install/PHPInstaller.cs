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
using System.Security.AccessControl;
using System.Security.Principal;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Install
{

    /// <summary>
    /// Downloads, verifies and extracts a PHP for Windows build.
    /// </summary>
    public static class PHPInstaller
    {
        // Official PHP zips expand to about 120 MB
        private const long MaxExtractedSize = 1024L * 1024 * 1024;

        /// <summary>
        /// Downloads the release, checks its SHA-256 (when published) and extracts it to targetDirectory.
        /// Returns the full path of php-cgi.exe.
        /// </summary>
        public static string DownloadAndExtract(PHPRelease release, string targetDirectory)
        {
            return DownloadAndExtract(release, targetDirectory, false);
        }

        /// <summary>
        /// Same as DownloadAndExtract(release, targetDirectory). Releases without a published SHA-256
        /// (archived releases) are refused unless allowUnverified is true.
        /// </summary>
        public static string DownloadAndExtract(PHPRelease release, string targetDirectory, bool allowUnverified)
        {
            if (release == null)
            {
                throw new ArgumentNullException("release");
            }

            if (String.IsNullOrEmpty(release.Sha256) && !allowUnverified)
            {
                throw new PHPInstallException("ErrorPackageNotVerified",
                    String.Format(CultureInfo.InvariantCulture, "{0} is published without a SHA-256 checksum, so its integrity cannot be verified. Confirm the installation of an unverified release to continue.", release.FileName));
            }

            if (String.IsNullOrEmpty(targetDirectory))
            {
                targetDirectory = GetDefaultInstallDirectory(release);
            }

            targetDirectory = ValidateInstallDirectory(targetDirectory);

            if (Directory.Exists(targetDirectory) &&
                Directory.GetFileSystemEntries(targetDirectory).Length > 0)
            {
                throw new PHPInstallException("ErrorTargetFolderNotEmpty",
                    String.Format(CultureInfo.InvariantCulture, "The folder {0} is not empty. Choose an empty or new folder.", targetDirectory));
            }

            string tempDirectory = WebDownloader.CreateTempDirectory();
            try
            {
                string zipPath = Path.Combine(tempDirectory, Path.GetFileName(release.FileName));
                WebDownloader.DownloadFile(release.Url, zipPath);
                WebDownloader.VerifySha256(zipPath, release.Sha256);

                EnsureExtractedSize(zipPath, MaxExtractedSize);

                Directory.CreateDirectory(targetDirectory);
                // Lock the folder down before any executable is written into it
                SecureInstallDirectory(targetDirectory);

                try
                {
                    // .NET Framework 4.6.1+ rejects entries that would be written outside the target folder
                    ZipFile.ExtractToDirectory(zipPath, targetDirectory);
                }
                catch (InvalidDataException ex)
                {
                    throw new PHPInstallException("ErrorPackageCorrupted",
                        String.Format(CultureInfo.InvariantCulture, "{0} is damaged and cannot be extracted: {1}", release.FileName, ex.Message), ex);
                }
            }
            finally
            {
                WebDownloader.DeleteTempDirectory(tempDirectory);
            }

            string phpCgiPath = Path.Combine(targetDirectory, "php-cgi.exe");
            if (!File.Exists(phpCgiPath))
            {
                throw new PHPInstallException("ErrorPHPCannotRun",
                    String.Format(CultureInfo.InvariantCulture, "php-cgi.exe was not found in {0} after extracting {1}.", targetDirectory, release.FileName));
            }

            PHPBuildDetector.EnsurePHPRuns(phpCgiPath);

            // The extracted PHP must be exactly the requested build
            PHPBuildInfo build = PHPBuildDetector.Detect(phpCgiPath);
            if (PHPBuildInfo.ParseVersion(build.Version) != PHPBuildInfo.ParseVersion(release.Version) ||
                build.ThreadSafe != release.ThreadSafe ||
                !String.Equals(build.Architecture, release.Architecture, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(build.Compiler, release.Compiler, StringComparison.OrdinalIgnoreCase))
            {
                throw new PHPInstallException("ErrorPackageCorrupted",
                    String.Format(CultureInfo.InvariantCulture, "{0} contains PHP {1} instead of PHP {2}.", release.FileName, build.DisplayName, release.DisplayName));
            }

            return phpCgiPath;
        }

        internal static void EnsureExtractedSize(string zipPath, long maxSize)
        {
            long total = 0;
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        total += entry.Length;
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                throw new PHPInstallException("ErrorPackageCorrupted",
                    String.Format(CultureInfo.InvariantCulture, "{0} is not a valid zip file: {1}", Path.GetFileName(zipPath), ex.Message), ex);
            }

            if (total > maxSize)
            {
                throw new PHPInstallException("ErrorDownloadFailed",
                    String.Format(CultureInfo.InvariantCulture, "{0} expands to {1} bytes, more than the allowed {2}.", Path.GetFileName(zipPath), total, maxSize));
            }
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

        /// <summary>
        /// PHP is executed by IIS, so only administrators may change its files. Inherited permissions are
        /// replaced: a folder created under C:\ would otherwise inherit "Authenticated Users: Modify" and any
        /// local user could replace php-cgi.exe. IIS worker processes and users get read and execute.
        /// </summary>
        public static void SecureInstallDirectory(string directory)
        {
            DirectorySecurity security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);

            InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
            // IIS_IUSRS (application pool identities)
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-5-32-568"),
                FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
            // The account doing the installation (an administrator, or the Web Management Service account
            // for remote connections) must still be able to write the files
            using (WindowsIdentity current = WindowsIdentity.GetCurrent())
            {
                security.AddAccessRule(new FileSystemAccessRule(current.User,
                    FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            }

            Directory.SetAccessControl(directory, security);
        }

        /// <summary>
        /// Only absolute paths on a local fixed drive are accepted: no UNC paths, no device paths and no
        /// system folders such as C:\Windows.
        /// </summary>
        public static string ValidateInstallDirectory(string directory)
        {
            string expanded = Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"'));
            string fullPath;
            try
            {
                if (!Path.IsPathRooted(expanded) || expanded.StartsWith(@"\\", StringComparison.Ordinal) || expanded.StartsWith("//", StringComparison.Ordinal))
                {
                    throw new ArgumentException();
                }

                fullPath = Path.GetFullPath(expanded).TrimEnd('\\');
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
                {
                    throw new PHPInstallException("ErrorInvalidInstallFolder",
                        String.Format(CultureInfo.InvariantCulture, "{0} is not a valid local folder.", directory));
                }
                throw;
            }

            DriveInfo drive = new DriveInfo(Path.GetPathRoot(fullPath));
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
            if (drive.DriveType != DriveType.Fixed ||
                String.Equals(fullPath, Path.GetPathRoot(fullPath).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ||
                String.Equals(fullPath, windows, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(windows + "\\", StringComparison.OrdinalIgnoreCase))
            {
                throw new PHPInstallException("ErrorInvalidInstallFolder",
                    String.Format(CultureInfo.InvariantCulture, "{0} cannot be used to install PHP. Use a folder on a local fixed drive, for example under Program Files.", fullPath));
            }

            return fullPath;
        }
    }
}
