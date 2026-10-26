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
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Web.Management.PHP.Install
{

    /// <summary>
    /// Minimal HTTP helper used to read the PHP and PECL download indexes and to download zip files.
    /// </summary>
    internal static class WebDownloader
    {
        private const int StringTimeout = 60 * 1000;
        private const int FileTimeout = 10 * 60 * 1000;
        private const string UserAgent = "PHPManagerForIIS";

        private static void EnsureSecurityProtocol()
        {
            // downloads.php.net requires TLS 1.2 or later; .NET Framework does not always enable it by default
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }

        private static HttpWebRequest CreateRequest(string url, int timeout)
        {
            EnsureSecurityProtocol();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = UserAgent;
            request.Timeout = timeout;
            request.ReadWriteTimeout = timeout;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return request;
        }

        public static void DownloadFile(string url, string path)
        {
            try
            {
                HttpWebRequest request = CreateRequest(url, FileTimeout);
                using (WebResponse response = request.GetResponse())
                using (Stream input = response.GetResponseStream())
                using (FileStream output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                }
            }
            catch (WebException ex)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                throw new PHPInstallException("ErrorDownloadFailed",
                    String.Format(CultureInfo.InvariantCulture, "Download of {0} failed: {1}", url, ex.Message), ex);
            }
        }

        public static string GetString(string url)
        {
            try
            {
                HttpWebRequest request = CreateRequest(url, StringTimeout);
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                throw new PHPInstallException("ErrorDownloadFailed",
                    String.Format(CultureInfo.InvariantCulture, "Download of {0} failed: {1}", url, ex.Message), ex);
            }
        }

        public static string GetTempFilePath(string fileName)
        {
            string directory = Path.Combine(Path.GetTempPath(), "PHPManager");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Path.GetFileName(fileName));
        }

        public static void VerifySha256(string path, string expectedSha256)
        {
            if (String.IsNullOrEmpty(expectedSha256))
            {
                return;
            }

            string actual;
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }
                actual = sb.ToString();
            }

            if (!String.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
                throw new PHPInstallException("ErrorChecksumMismatch",
                    String.Format(CultureInfo.InvariantCulture, "The SHA-256 of {0} does not match. Expected {1}, got {2}.", Path.GetFileName(path), expectedSha256, actual));
            }
        }
    }
}
