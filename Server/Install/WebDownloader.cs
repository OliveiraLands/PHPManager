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
    /// Only HTTPS URLs of downloads.php.net under /~windows/ are allowed, also after redirects.
    /// </summary>
    internal static class WebDownloader
    {
        private const int StringTimeout = 60 * 1000;
        private const int FileTimeout = 10 * 60 * 1000;
        private const string UserAgent = "PHPManagerForIIS";

        private const string AllowedHost = "downloads.php.net";
        private const string AllowedPathPrefix = "/~windows/";

        // Official PHP zips are about 35 MB and index pages a few hundred KB
        private const long MaxFileSize = 300L * 1024 * 1024;
        private const long MaxStringSize = 10L * 1024 * 1024;

        private static void EnsureSecurityProtocol()
        {
            // downloads.php.net requires TLS 1.2 or later; .NET Framework does not always enable it by default
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }

        /// <summary>
        /// Throws if the URL is not an HTTPS URL of the PHP for Windows download site.
        /// </summary>
        public static void EnsureAllowedUrl(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || !IsAllowedUri(uri))
            {
                throw new PHPInstallException("ErrorDownloadFailed",
                    String.Format(CultureInfo.InvariantCulture, "The URL {0} is not allowed. Only https://{1}{2} can be used.", url, AllowedHost, AllowedPathPrefix));
            }
        }

        private static bool IsAllowedUri(Uri uri)
        {
            return String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                   String.Equals(uri.Host, AllowedHost, StringComparison.OrdinalIgnoreCase) &&
                   uri.IsDefaultPort &&
                   String.IsNullOrEmpty(uri.UserInfo) &&
                   uri.AbsolutePath.StartsWith(AllowedPathPrefix, StringComparison.Ordinal) &&
                   uri.AbsolutePath.IndexOf("/../", StringComparison.Ordinal) < 0;
        }

        private static HttpWebRequest CreateRequest(string url, int timeout)
        {
            EnsureAllowedUrl(url);
            EnsureSecurityProtocol();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = UserAgent;
            request.Timeout = timeout;
            request.ReadWriteTimeout = timeout;
            // No automatic decompression: Content-Length must match the bytes written to disk
            request.AutomaticDecompression = DecompressionMethods.None;
            request.MaximumAutomaticRedirections = 5;
            return request;
        }

        private static long CopyLimited(Stream input, Stream output, long maxSize, string url)
        {
            byte[] buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > maxSize)
                {
                    throw new PHPInstallException("ErrorDownloadFailed",
                        String.Format(CultureInfo.InvariantCulture, "The download of {0} is larger than {1} bytes and was stopped.", url, maxSize));
                }
                output.Write(buffer, 0, read);
            }

            return total;
        }

        public static void DownloadFile(string url, string path)
        {
            try
            {
                HttpWebRequest request = CreateRequest(url, FileTimeout);
                using (WebResponse response = request.GetResponse())
                {
                    EnsureResponse(response, MaxFileSize, url);
                    long received;
                    using (Stream input = response.GetResponseStream())
                    using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        received = CopyLimited(input, output, MaxFileSize, url);
                    }

                    // A connection closed early gives a shorter file without any error
                    if (response.ContentLength >= 0 && received != response.ContentLength)
                    {
                        throw new PHPInstallException("ErrorPackageCorrupted",
                            String.Format(CultureInfo.InvariantCulture, "The download of {0} is incomplete: {1} of {2} bytes received.", url, received, response.ContentLength));
                    }
                }
            }
            catch (Exception ex)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                if (ex is PHPInstallException)
                {
                    throw;
                }
                if (ex is WebException || ex is IOException)
                {
                    throw new PHPInstallException("ErrorDownloadFailed",
                        String.Format(CultureInfo.InvariantCulture, "Download of {0} failed: {1}", url, ex.Message), ex);
                }
                throw;
            }
        }

        private static void EnsureResponse(WebResponse response, long maxSize, string url)
        {
            // Redirects are followed automatically: the final address must still be allowed
            // (for example no redirect from HTTPS to HTTP or to another host)
            if (!IsAllowedUri(response.ResponseUri))
            {
                throw new PHPInstallException("ErrorDownloadFailed",
                    String.Format(CultureInfo.InvariantCulture, "The download of {0} was redirected to {1}, which is not allowed.", url, response.ResponseUri));
            }

            if (response.ContentLength > maxSize)
            {
                throw new PHPInstallException("ErrorDownloadFailed",
                    String.Format(CultureInfo.InvariantCulture, "The download of {0} is larger than {1} bytes.", url, maxSize));
            }
        }

        public static string GetString(string url)
        {
            try
            {
                HttpWebRequest request = CreateRequest(url, StringTimeout);
                using (WebResponse response = request.GetResponse())
                {
                    EnsureResponse(response, MaxStringSize, url);
                    using (Stream input = response.GetResponseStream())
                    using (MemoryStream output = new MemoryStream())
                    {
                        CopyLimited(input, output, MaxStringSize, url);
                        return Encoding.UTF8.GetString(output.ToArray());
                    }
                }
            }
            catch (WebException ex)
            {
                throw new PHPInstallException("ErrorDownloadFailed",
                    String.Format(CultureInfo.InvariantCulture, "Download of {0} failed: {1}", url, ex.Message), ex);
            }
        }

        /// <summary>
        /// Creates a new, uniquely named folder for one download so that a file prepared in advance by
        /// another user (for example in a shared temp folder) can never be used instead of the download.
        /// </summary>
        public static string CreateTempDirectory()
        {
            string directory = Path.Combine(Path.Combine(Path.GetTempPath(), "PHPManager"), Path.GetRandomFileName());
            if (Directory.Exists(directory))
            {
                throw new IOException("Temporary folder already exists: " + directory);
            }
            Directory.CreateDirectory(directory);
            return directory;
        }

        public static void DeleteTempDirectory(string directory)
        {
            try
            {
                if (!String.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
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
