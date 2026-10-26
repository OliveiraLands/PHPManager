//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Management.Automation;
using Web.Management.PHP.Config;
using Web.Management.PHP.Install;

namespace Web.Management.PHP.Powershell
{

    /// <summary>
    /// Lists the PHP for Windows builds available at downloads.php.net.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "PHPAvailableVersion")]
    [OutputType(typeof(PHPRelease))]
    public sealed class GetPHPAvailableVersionCmdlet : BaseCmdlet
    {
        private string _version;
        private SwitchParameter _includeArchives;
        private SwitchParameter _threadSafe;
        private string _architecture;

        [Parameter(Position = 0)]
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
        public SwitchParameter IncludeArchives
        {
            get
            {
                return _includeArchives;
            }
            set
            {
                _includeArchives = value;
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

        protected override void DoProcessing()
        {
            WildcardPattern versionWildcard = PrepareWildcardPattern(Version);
            bool filterThreadSafe = MyInvocation.BoundParameters.ContainsKey("ThreadSafe");

            foreach (PHPRelease release in PHPReleaseCatalog.GetReleases(IncludeArchives.IsPresent))
            {
                if (!versionWildcard.IsMatch(release.Version) && !versionWildcard.IsMatch(release.Branch))
                {
                    continue;
                }
                if (filterThreadSafe && release.ThreadSafe != ThreadSafe.IsPresent)
                {
                    continue;
                }
                if (!String.IsNullOrEmpty(Architecture) && !String.Equals(release.Architecture, Architecture, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                WriteObject(release);
            }
        }
    }
}
