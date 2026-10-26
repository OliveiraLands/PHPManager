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
using System.Management.Automation;
using Microsoft.Web.Administration;
using Web.Management.PHP.Config;
using Web.Management.PHP.Install;

namespace Web.Management.PHP.Powershell
{

    /// <summary>
    /// Downloads a PHP for Windows build, registers it with IIS and optionally makes it active.
    /// -Version accepts a branch ("8.4", latest patch release) or an exact version ("8.4.26").
    /// </summary>
    [Cmdlet(VerbsLifecycle.Install, "PHPVersion", SupportsShouldProcess = true)]
    [OutputType(typeof(string))]
    public sealed class InstallPHPVersionCmdlet : BaseCmdlet
    {
        private string _version;
        private SwitchParameter _threadSafe;
        private string _architecture = "x64";
        private string _path;
        private SwitchParameter _activate;
        private SwitchParameter _allowUnverified;

        [Parameter]
        public SwitchParameter AllowUnverified
        {
            get
            {
                return _allowUnverified;
            }
            set
            {
                _allowUnverified = value;
            }
        }

        [Parameter(Mandatory = true, Position = 0)]
        public string Version
        {
            get
            {
                return _version;
            }
            set
            {
                _version = value;
            }
        }

        [Parameter]
        public SwitchParameter ThreadSafe
        {
            get
            {
                return _threadSafe;
            }
            set
            {
                _threadSafe = value;
            }
        }

        [Parameter]
        [ValidateSet("x64", "x86")]
        public string Architecture
        {
            get
            {
                return _architecture;
            }
            set
            {
                _architecture = value;
            }
        }

        [Parameter(Position = 1)]
        public string Path
        {
            get
            {
                return _path;
            }
            set
            {
                _path = value;
            }
        }

        [Parameter]
        public SwitchParameter Activate
        {
            get
            {
                return _activate;
            }
            set
            {
                _activate = value;
            }
        }

        protected override void DoProcessing()
        {
            PHPRelease release = FindRelease();
            if (release == null)
            {
                ReportTerminatingError(new ItemNotFoundException(String.Format(CultureInfo.CurrentCulture,
                    "PHP {0} {1} {2} was not found at {3}.", Version, ThreadSafe.IsPresent ? "TS" : "NTS", Architecture, PHPReleaseCatalog.ReleasesUrl)),
                    "ReleaseNotFound", ErrorCategory.ObjectNotFound);
                return;
            }

            string path = String.IsNullOrEmpty(Path) ? PHPInstaller.GetDefaultInstallDirectory(release) : GetUnresolvedProviderPathFromPSPath(Path);
            if (String.IsNullOrEmpty(release.Sha256))
            {
                if (!AllowUnverified.IsPresent)
                {
                    ReportTerminatingError(new PHPInstallException("ErrorPackageNotVerified",
                        "PHP " + release.Version + " is an archived release published without a SHA-256 checksum, so its integrity cannot be verified. Use -AllowUnverified to install it anyway."),
                        "PackageNotVerified", ErrorCategory.SecurityError);
                    return;
                }
                WriteWarning("PHP " + release.Version + " is an archived release published without a SHA-256 checksum; the download is only protected by HTTPS.");
            }

            if (!ShouldProcess(path, "Install PHP " + release.DisplayName))
            {
                return;
            }

            try
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    ServerManagerWrapper serverManagerWrapper = new ServerManagerWrapper(serverManager, this.SiteName, this.VirtualPath);
                    PHPConfigHelper configHelper = new PHPConfigHelper(serverManagerWrapper);
                    WriteObject(configHelper.InstallPHPRelease(release, path, Activate.IsPresent, AllowUnverified.IsPresent));
                }
            }
            catch (PHPInstallException ex)
            {
                ReportTerminatingError(ex, ex.ResourceKey ?? "InstallFailed", ErrorCategory.InvalidOperation);
            }
        }

        private PHPRelease FindRelease()
        {
            bool isBranch = Version.Split('.').Length == 2;

            // Latest releases first; archives only when an exact older version is requested
            foreach (bool includeArchives in new bool[] { false, true })
            {
                if (includeArchives && isBranch)
                {
                    break;
                }

                foreach (PHPRelease release in PHPReleaseCatalog.GetReleases(includeArchives))
                {
                    bool versionMatches = isBranch
                        ? String.Equals(release.Branch, Version, StringComparison.Ordinal)
                        : String.Equals(release.Version, Version, StringComparison.Ordinal);

                    if (versionMatches &&
                        release.ThreadSafe == ThreadSafe.IsPresent &&
                        String.Equals(release.Architecture, Architecture, StringComparison.OrdinalIgnoreCase))
                    {
                        return release;
                    }
                }
            }

            return null;
        }
    }
}
