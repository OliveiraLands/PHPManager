//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//-----------------------------------------------------------------------

using System.Management.Automation;
using Microsoft.Web.Administration;
using Web.Management.PHP.Config;

namespace Web.Management.PHP.Powershell
{

    /// <summary>
    /// Copies the php.ini settings and enabled extensions of another registered PHP version
    /// (identified by its handler name, see Get-PHPVersion) to the php.ini of the active PHP version.
    /// Directives unknown to the active version and extensions without a DLL are listed but not copied.
    /// </summary>
    [Cmdlet(VerbsCommon.Copy, "PHPIniSettings", SupportsShouldProcess = true)]
    [OutputType(typeof(PHPIniMigrationItem))]
    public sealed class CopyPHPIniSettingsCmdlet : BaseCmdlet
    {
        private string _sourceHandlerName;

        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("HandlerName")]
        public string SourceHandlerName
        {
            get
            {
                return _sourceHandlerName;
            }
            set
            {
                _sourceHandlerName = value;
            }
        }

        protected override void DoProcessing()
        {
            using (ServerManager serverManager = new ServerManager())
            {
                ServerManagerWrapper serverManagerWrapper = new ServerManagerWrapper(serverManager, this.SiteName, this.VirtualPath);
                PHPConfigHelper configHelper = new PHPConfigHelper(serverManagerWrapper);

                RemoteObjectCollection<PHPIniMigrationItem> items = configHelper.GetIniMigrationPreview(SourceHandlerName);
                WriteObject(items, true);

                if (items.Count > 0 && ShouldProcess(configHelper.PHPIniFilePath, "Copy php.ini settings from " + SourceHandlerName))
                {
                    configHelper.ApplyIniMigration(items);
                }
            }
        }
    }
}
