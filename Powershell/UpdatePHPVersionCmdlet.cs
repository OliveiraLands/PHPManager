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
using Microsoft.Web.Administration;
using Web.Management.PHP.Config;
using Web.Management.PHP.Install;

namespace Web.Management.PHP.Powershell
{

    /// <summary>
    /// Updates the active PHP version to the latest patch release of its branch (side by side):
    /// php.ini settings and extensions are copied and the new version becomes active.
    /// </summary>
    [Cmdlet(VerbsData.Update, "PHPVersion", SupportsShouldProcess = true)]
    [OutputType(typeof(string))]
    public sealed class UpdatePHPVersionCmdlet : BaseCmdlet
    {
        private string _path;

        [Parameter(Position = 0)]
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

        protected override void DoProcessing()
        {
            using (ServerManager serverManager = new ServerManager())
            {
                ServerManagerWrapper serverManagerWrapper = new ServerManagerWrapper(serverManager, this.SiteName, this.VirtualPath);
                PHPConfigHelper configHelper = new PHPConfigHelper(serverManagerWrapper);

                PHPBuildInfo build = configHelper.GetPHPBuildInfo();
                PHPRelease update = PHPReleaseCatalog.GetUpdateFor(build);
                if (update == null)
                {
                    WriteWarning(String.Format(System.Globalization.CultureInfo.CurrentCulture,
                        "PHP {0} is the latest release of the {1} branch.", build.Version, build.Branch));
                    return;
                }

                string path = String.IsNullOrEmpty(Path)
                    ? PHPInstaller.GetSideBySideDirectory(build.Directory, update)
                    : GetUnresolvedProviderPathFromPSPath(Path);

                if (!ShouldProcess(path, "Update PHP " + build.DisplayName + " to " + update.Version))
                {
                    return;
                }

                try
                {
                    WriteObject(configHelper.UpgradePHP(path));
                }
                catch (PHPInstallException ex)
                {
                    ReportTerminatingError(ex, ex.ResourceKey ?? "UpdateFailed", ErrorCategory.InvalidOperation);
                }
            }
        }
    }
}
