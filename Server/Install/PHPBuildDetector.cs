//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Install
{

    /// <summary>
    /// Detects version, thread safety, compiler and architecture of an installed PHP build.
    /// </summary>
    public static class PHPBuildDetector
    {
        private const int ProcessTimeout = 30 * 1000;

        // Windows NTSTATUS returned when a DLL (usually the Visual C++ runtime) cannot be found
        private const int StatusDllNotFound = unchecked((int)0xC0000135);

        public static PHPBuildInfo Detect(string phpCgiPath)
        {
            string executable = Environment.ExpandEnvironmentVariables(phpCgiPath);
            string directory = Path.GetDirectoryName(executable);

            PHPBuildInfo build = new PHPBuildInfo();
            build.Directory = directory;

            // php.exe (CLI) prints plain text phpinfo; php-cgi.exe prints HTML
            string phpExe = Path.Combine(directory, "php.exe");
            string info;
            if (File.Exists(phpExe) && RunPHP(phpExe, "-n -i", out info) == 0)
            {
                build.Version = GetInfoValue(info, "PHP Version");
                build.ThreadSafe = String.Equals(GetInfoValue(info, "Thread Safety"), "enabled", StringComparison.OrdinalIgnoreCase);
                build.Architecture = GetInfoValue(info, "Architecture").ToLowerInvariant();

                // "PHP Extension Build => API20240924,NTS,VS17"
                string extensionBuild = GetInfoValue(info, "PHP Extension Build");
                string[] parts = extensionBuild.Split(',');
                if (parts.Length >= 3)
                {
                    build.Compiler = parts[parts.Length - 1].Trim().ToLowerInvariant();
                }
            }

            // Fallbacks when php.exe is not available or did not run
            if (String.IsNullOrEmpty(build.Version))
            {
                build.Version = FileVersionInfo.GetVersionInfo(executable).ProductVersion;
                build.ThreadSafe = Directory.GetFiles(directory, "php*ts.dll").Length > 0;
            }
            if (String.IsNullOrEmpty(build.Architecture))
            {
                build.Architecture = GetPEArchitecture(executable);
            }
            if (String.IsNullOrEmpty(build.Compiler))
            {
                build.Compiler = GetDefaultCompiler(build.Version);
            }

            return build;
        }

        /// <summary>
        /// Compiler used by the official builds of a given PHP version.
        /// </summary>
        public static string GetDefaultCompiler(string version)
        {
            Version v = PHPBuildInfo.ParseVersion(version);
            if (v >= new Version(8, 4))
            {
                return "vs17";
            }
            if (v >= new Version(8, 0))
            {
                return "vs16";
            }
            if (v >= new Version(7, 2))
            {
                return "vc15";
            }
            return "vc14";
        }

        private static string GetInfoValue(string info, string name)
        {
            Match m = Regex.Match(info, "^" + Regex.Escape(name) + @"\s*=>\s*(?<value>.*)$", RegexOptions.Multiline);
            return m.Success ? m.Groups["value"].Value.Trim() : String.Empty;
        }

        private static string GetPEArchitecture(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    stream.Seek(0x3C, SeekOrigin.Begin);
                    int peOffset = reader.ReadInt32();
                    stream.Seek(peOffset + 4, SeekOrigin.Begin);
                    ushort machine = reader.ReadUInt16();
                    if (machine == 0x8664)
                    {
                        return "x64";
                    }
                    if (machine == 0x014C)
                    {
                        return "x86";
                    }
                    if (machine == 0xAA64)
                    {
                        return "arm64";
                    }
                }
            }
            catch (IOException)
            {
            }

            return String.Empty;
        }

        /// <summary>
        /// Runs a PHP executable and returns its exit code. Output contains stdout.
        /// </summary>
        public static int RunPHP(string executable, string arguments, out string output)
        {
            string errors;
            return RunPHP(executable, arguments, Path.GetDirectoryName(executable), out output, out errors);
        }

        /// <summary>
        /// Runs a PHP executable in the given working directory and returns its exit code, stdout and stderr.
        /// </summary>
        public static int RunPHP(string executable, string arguments, string workingDirectory, out string output, out string errors)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(executable, arguments);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.WorkingDirectory = workingDirectory;
            // Make sure no php.ini from PHPRC of the calling process is used
            startInfo.EnvironmentVariables.Remove("PHPRC");

            using (Process process = Process.Start(startInfo))
            {
                // Read stderr asynchronously to avoid deadlocks when both pipes fill up
                System.Text.StringBuilder stderr = new System.Text.StringBuilder();
                process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        lock (stderr)
                        {
                            stderr.AppendLine(e.Data);
                        }
                    }
                };
                process.BeginErrorReadLine();
                output = process.StandardOutput.ReadToEnd();

                if (!process.WaitForExit(ProcessTimeout))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    errors = String.Empty;
                    return -1;
                }

                // Wait for the asynchronous stderr reader to finish
                process.WaitForExit();
                lock (stderr)
                {
                    errors = stderr.ToString();
                }
                return process.ExitCode;
            }
        }

        /// <summary>
        /// Checks that a freshly installed php-cgi.exe can start (it fails when the Visual C++ runtime is missing).
        /// </summary>
        public static void EnsurePHPRuns(string phpCgiPath)
        {
            string output;
            int exitCode;
            try
            {
                exitCode = RunPHP(phpCgiPath, "-n -v", out output);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new PHPInstallException("ErrorPHPCannotRun", ex.Message, ex);
            }

            if (exitCode != 0)
            {
                string message = (exitCode == StatusDllNotFound)
                    ? "php-cgi.exe could not start because a DLL is missing. Install the latest Microsoft Visual C++ Redistributable (x64/x86) and try again."
                    : String.Format(CultureInfo.InvariantCulture, "php-cgi.exe exited with code {0}.", exitCode);
                throw new PHPInstallException("ErrorPHPCannotRun", message);
            }
        }
    }
}
