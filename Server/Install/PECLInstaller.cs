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
using System.IO;
using System.IO.Compression;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Install
{

    /// <summary>
    /// Installs a PECL extension build: php_*.dll goes to the extension directory and
    /// any dependency DLL shipped in the zip goes to the PHP directory.
    /// PECL builds are published without checksums or signatures, so before anything is copied the
    /// package is checked: complete download over HTTPS, valid zip, Windows DLLs for the right
    /// architecture, and the extension must load in the target PHP version.
    /// </summary>
    public static class PECLInstaller
    {
        private const long MaxExtractedSize = 512L * 1024 * 1024;

        private static bool IsSafeFileName(string name)
        {
            return !String.IsNullOrEmpty(name) &&
                   name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                   name.IndexOf("..", StringComparison.Ordinal) < 0 &&
                   name.IndexOf(':') < 0;
        }

        /// <summary>
        /// Returns the file name of the installed extension DLL (e.g. php_redis.dll).
        /// </summary>
        public static string Install(PECLPackage package, string phpDirectory, string extensionDirectory, bool overwrite)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            string tempDirectory = WebDownloader.CreateTempDirectory();
            try
            {
                string zipPath = Path.Combine(tempDirectory, Path.GetFileName(package.FileName));
                WebDownloader.DownloadFile(package.Url, zipPath);
                PHPInstaller.EnsureExtractedSize(zipPath, MaxExtractedSize);

                // 1. Extract the DLLs to a staging folder
                string stageDirectory = Path.Combine(tempDirectory, "stage");
                Directory.CreateDirectory(stageDirectory);
                string extensionFile = null;
                List<string> dependencies = new List<string>();
                try
                {
                    using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            // Only plain DLL file names are extracted; folders in the zip are ignored
                            if (!entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || !IsSafeFileName(entry.Name))
                            {
                                continue;
                            }

                            entry.ExtractToFile(Path.Combine(stageDirectory, entry.Name), true);
                            if (entry.Name.StartsWith("php_", StringComparison.OrdinalIgnoreCase) && extensionFile == null)
                            {
                                extensionFile = entry.Name;
                            }
                            else
                            {
                                dependencies.Add(entry.Name);
                            }
                        }
                    }
                }
                catch (InvalidDataException ex)
                {
                    throw new PHPInstallException("ErrorPackageCorrupted",
                        String.Format(CultureInfo.InvariantCulture, "{0} is damaged and cannot be extracted: {1}", package.FileName, ex.Message), ex);
                }

                if (extensionFile == null)
                {
                    throw new PHPInstallException("ErrorCannotInstallExtension",
                        String.Format(CultureInfo.InvariantCulture, "{0} does not contain a php_*.dll file.", package.FileName));
                }

                string extensionTarget = Path.Combine(extensionDirectory, extensionFile);
                if (File.Exists(extensionTarget) && !overwrite)
                {
                    throw new PHPInstallException("ErrorExtensionFileAlreadyExists",
                        String.Format(CultureInfo.InvariantCulture, "The file {0} already exists.", extensionTarget));
                }

                // 2. Every DLL must be a Windows DLL for the architecture of the package
                foreach (string file in Directory.GetFiles(stageDirectory, "*.dll"))
                {
                    VerifyPortableExecutable(file, package.Architecture);
                }

                // 3. The extension must load in the target PHP version
                VerifyExtensionLoads(package, phpDirectory, stageDirectory, extensionFile);

                // 4. Only now copy the files into the PHP installation
                File.Copy(Path.Combine(stageDirectory, extensionFile), extensionTarget, true);
                foreach (string dependency in dependencies)
                {
                    // Dependency libraries must be next to php-cgi.exe. Never replace a DLL that
                    // PHP itself ships (e.g. libcrypto), as it may be a different build.
                    string target = Path.Combine(phpDirectory, dependency);
                    if (!File.Exists(target))
                    {
                        File.Copy(Path.Combine(stageDirectory, dependency), target, false);
                    }
                }

                return extensionFile;
            }
            finally
            {
                WebDownloader.DeleteTempDirectory(tempDirectory);
            }
        }

        private static void VerifyPortableExecutable(string path, string architecture)
        {
            ushort expected = String.Equals(architecture, "x86", StringComparison.OrdinalIgnoreCase) ? (ushort)0x014C : (ushort)0x8664;
            bool valid = false;
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    if (stream.Length > 0x40 && reader.ReadUInt16() == 0x5A4D)           // "MZ"
                    {
                        stream.Seek(0x3C, SeekOrigin.Begin);
                        int peOffset = reader.ReadInt32();
                        if (peOffset > 0 && peOffset < stream.Length - 24)
                        {
                            stream.Seek(peOffset, SeekOrigin.Begin);
                            if (reader.ReadUInt32() == 0x00004550)                        // "PE\0\0"
                            {
                                ushort machine = reader.ReadUInt16();
                                stream.Seek(peOffset + 22, SeekOrigin.Begin);
                                ushort characteristics = reader.ReadUInt16();
                                valid = (machine == expected) && ((characteristics & 0x2000) != 0);   // IMAGE_FILE_DLL
                            }
                        }
                    }
                }
            }
            catch (EndOfStreamException)
            {
                valid = false;
            }

            if (!valid)
            {
                throw new PHPInstallException("ErrorPackageCorrupted",
                    String.Format(CultureInfo.InvariantCulture, "{0} is not a valid {1} Windows DLL.", Path.GetFileName(path), architecture));
            }
        }

        /// <summary>
        /// Loads the extension in a copy of the target PHP layout (php.exe and its DLLs plus the package DLLs,
        /// PHP's own DLLs taking precedence as they will at run time) and checks that a new module appears
        /// without startup warnings.
        /// </summary>
        private static void VerifyExtensionLoads(PECLPackage package, string phpDirectory, string stageDirectory, string extensionFile)
        {
            string phpExe = Path.Combine(phpDirectory, "php.exe");
            if (!File.Exists(phpExe))
            {
                // Without the CLI the load test is not possible; the PE checks above still apply
                return;
            }

            string testDirectory = Path.Combine(Path.GetDirectoryName(stageDirectory), "test");
            Directory.CreateDirectory(testDirectory);
            File.Copy(Path.Combine(stageDirectory, extensionFile), Path.Combine(testDirectory, extensionFile));
            foreach (string file in Directory.GetFiles(stageDirectory, "*.dll"))
            {
                string target = Path.Combine(testDirectory, Path.GetFileName(file));
                if (!File.Exists(target))
                {
                    File.Copy(file, target);
                }
            }
            File.Copy(phpExe, Path.Combine(testDirectory, "php.exe"));
            foreach (string file in Directory.GetFiles(phpDirectory, "*.dll"))
            {
                File.Copy(file, Path.Combine(testDirectory, Path.GetFileName(file)), true);
            }

            string testPhp = Path.Combine(testDirectory, "php.exe");
            string output;
            string errors;

            PHPBuildDetector.RunPHP(testPhp, "-n -m", testDirectory, out output, out errors);
            int baseline = CountModules(output);

            string extensionPath = Path.Combine(testDirectory, extensionFile);
            int exitCode = PHPBuildDetector.RunPHP(testPhp,
                String.Format(CultureInfo.InvariantCulture, "-n -d \"extension_dir={0}\" -d \"extension={1}\" -m", testDirectory, extensionFile),
                testDirectory, out output, out errors);

            // Xdebug and other Zend extensions must be loaded with zend_extension
            if (ContainsWarning(output + errors) && (output + errors).IndexOf("Zend extension", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                exitCode = PHPBuildDetector.RunPHP(testPhp,
                    String.Format(CultureInfo.InvariantCulture, "-n -d \"zend_extension={0}\" -m", extensionPath),
                    testDirectory, out output, out errors);
            }

            if (exitCode != 0 || ContainsWarning(output + errors) || CountModules(output) <= baseline)
            {
                // The startup messages are on stderr; the module list on stdout is only noise
                string details = String.IsNullOrEmpty(errors.Trim()) ? output.Trim() : errors.Trim();
                if (details.Length > 500)
                {
                    details = details.Substring(0, 500);
                }

                throw new PHPInstallException("ErrorExtensionVerificationFailed",
                    String.Format(CultureInfo.InvariantCulture, "{0} could not be loaded by PHP {1}: {2}", package.DisplayName, package.PHPBranch, details));
            }
        }

        private static bool ContainsWarning(string text)
        {
            return text.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Unable to load", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Fatal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountModules(string output)
        {
            int count = 0;
            foreach (string line in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.StartsWith("[", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
