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
using System.Management.Automation;
using Microsoft.Web.Administration;
using Web.Management.PHP.Config;
using Web.Management.PHP.Install;

namespace Web.Management.PHP.Powershell
{

    /// <summary>
    /// Downloads the PECL build of an extension that matches the active PHP version, copies it
    /// to the extension directory and enables it in php.ini.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Install, "PHPExtension", SupportsShouldProcess = true)]
    [OutputType(typeof(string))]
    public sealed class InstallPHPExtensionCmdlet : BaseCmdlet
    {
        private string _name;
        private string _version;
        private SwitchParameter _force;

        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
            }
        }

        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true)]
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
        public SwitchParameter Force
        {
            get
            {
                return _force;
            }
            set
            {
                _force = value;
            }
        }

        protected override void DoProcessing()
        {
            using (ServerManager serverManager = new ServerManager())
            {
                ServerManagerWrapper serverManagerWrapper = new ServerManagerWrapper(serverManager, this.SiteName, this.VirtualPath);
                PHPConfigHelper configHelper = new PHPConfigHelper(serverManagerWrapper);

                List<PECLPackage> packages = configHelper.GetCompatiblePECLPackages(Name);
                PECLPackage package = null;
                foreach (PECLPackage candidate in packages)
                {
                    if (String.IsNullOrEmpty(Version) || String.Equals(candidate.Version, Version, StringComparison.OrdinalIgnoreCase))
                    {
                        package = candidate;
                        break;
                    }
                }

                if (package == null)
                {
                    ReportTerminatingError(new ItemNotFoundException(String.Format(CultureInfo.CurrentCulture,
                        "No build of {0} {1} is available for PHP {2}.", Name, Version, configHelper.GetPHPBuildInfo().DisplayName)),
                        "ExtensionNotFound", ErrorCategory.ObjectNotFound);
                    return;
                }

                if (!ShouldProcess(package.DisplayName, "Install PHP extension"))
                {
                    return;
                }

                try
                {
                    WriteObject(configHelper.InstallPECLExtension(package, Force.IsPresent));
                }
                catch (PHPInstallException ex)
                {
                    ReportTerminatingError(ex, ex.ResourceKey ?? "InstallFailed", ErrorCategory.InvalidOperation);
                }
            }
        }
    }
}
