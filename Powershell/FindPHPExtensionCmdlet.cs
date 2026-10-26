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
using Web.Management.PHP.Install;

namespace Web.Management.PHP.Powershell
{

    /// <summary>
    /// Searches the Windows builds of PECL extensions. Without -Compatible it returns extension names;
    /// with -Compatible it returns the builds that match the active PHP version.
    /// </summary>
    [Cmdlet(VerbsCommon.Find, "PHPExtension")]
    [OutputType(typeof(string), typeof(PECLPackage))]
    public sealed class FindPHPExtensionCmdlet : BaseCmdlet
    {
        private string _name;
        private SwitchParameter _compatible;

        [Parameter(Position = 0)]
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

        [Parameter]
        public SwitchParameter Compatible
        {
            get
            {
                return _compatible;
            }
            set
            {
                _compatible = value;
            }
        }

        protected override void DoProcessing()
        {
            WildcardPattern nameWildcard = PrepareWildcardPattern(Name);
            PHPBuildInfo build = null;

            if (Compatible.IsPresent)
            {
                using (ServerManager serverManager = new ServerManager())
                {
                    ServerManagerWrapper serverManagerWrapper = new ServerManagerWrapper(serverManager, this.SiteName, this.VirtualPath);
                    build = new PHPConfigHelper(serverManagerWrapper).GetPHPBuildInfo();
                }
            }

            foreach (string name in PECLCatalog.GetExtensionNames(null))
            {
                if (!nameWildcard.IsMatch(name))
                {
                    continue;
                }

                if (build == null)
                {
                    WriteObject(name);
                }
                else
                {
                    WriteObject(PECLCatalog.GetCompatiblePackages(name, build), true);
                }
            }
        }
    }
}
