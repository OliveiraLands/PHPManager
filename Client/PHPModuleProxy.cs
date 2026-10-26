//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//----------------------------------------------------------------------- 

using System.Collections;
using Microsoft.Web.Management.Client;
using Web.Management.PHP.Config;

namespace Web.Management.PHP
{

    internal sealed class PHPModuleProxy : ModuleServiceProxy
    {

        internal string AddExtension(string extensionPath)
        {
            return (string)Invoke("AddExtension", extensionPath);
        }

        internal void AddOrUpdateSettings(RemoteObjectCollection<PHPIniSetting> settings)
        {
            Invoke("AddOrUpdateSettings", settings.GetData());
        }

        internal void ApplyRecommendedSettings(ArrayList configIssueIndexes)
        {
            Invoke("ApplyRecommendedSettings", configIssueIndexes);
        }

        internal bool CheckForLocalPHPHandler(string siteName, string virtualPath)
        {
            return (bool)Invoke("CheckForLocalPHPHandler", siteName, virtualPath);
        }

        internal string CreatePHPInfo(string siteName)
        {
            return (string)Invoke("CreatePHPInfo", siteName);
        }

        internal RemoteObjectCollection<PHPVersion> GetAllPHPVersions()
        {
            object o = Invoke("GetAllPHPVersions");

            if (o != null)
            {
                RemoteObjectCollection<PHPVersion> versions = new RemoteObjectCollection<PHPVersion>();
                versions.SetData(o);
                return versions;
            }

            return null;
        }

        internal RemoteObjectCollection<PHPConfigIssue> GetConfigIssues()
        {
            object o = Invoke("GetConfigIssues");

            if (o != null)
            {
                RemoteObjectCollection<PHPConfigIssue> configIssues = new RemoteObjectCollection<PHPConfigIssue>();
                configIssues.SetData(o);
                return configIssues;
            }

            return null;
        }

        internal PHPConfigInfo GetPHPConfigInfo()
        {
            object o = Invoke("GetPHPConfigInfo");
            PHPConfigInfo result = null;
            if (o != null)
            {
                result = new PHPConfigInfo();
                result.SetData(o);
            }            
            return result;
        }

        internal string GetPHPIniPhysicalPath()
        {
            return (string)Invoke("GetPHPIniPhysicalPath");
        }

        internal object GetPHPIniSettings()
        {
            return Invoke("GetPHPIniSettings");
        }

        internal ArrayList GetSiteBindings(string siteName)
        {
            return (ArrayList)Invoke("GetSiteBindings", siteName);
        }

        internal ArrayList GetSites()
        {
            return (ArrayList)Invoke("GetSites");
        }

        internal void RegisterPHPWithIIS(string path)
        {
            Invoke("RegisterPHPWithIIS", path);
        }

        internal void RemovePHPInfo(string filePath)
        {
            Invoke("RemovePHPInfo", filePath);
        }

        internal void RemoveSetting(PHPIniSetting setting)
        {
            Invoke("RemovePHPIniSetting", setting.GetData());
        }

        internal void SelectPHPVersion(string name)
        {
            Invoke("SelectPHPVersion", name);
        }

        internal void UpdateExtensions(RemoteObjectCollection<PHPIniExtension> extensions)
        {
            Invoke("UpdateExtensions", extensions.GetData());
        }

        internal void ApplyIniMigration(string sourceHandlerName, RemoteObjectCollection<PHPIniMigrationItem> items)
        {
            Invoke("ApplyIniMigration", sourceHandlerName, items.GetData());
        }

        internal PHPRelease CheckForPHPUpdate()
        {
            object o = Invoke("CheckForPHPUpdate");
            PHPRelease result = null;
            if (o != null)
            {
                result = new PHPRelease();
                result.SetData(o);
            }
            return result;
        }

        internal RemoteObjectCollection<PHPRelease> GetAvailablePHPReleases(bool includeArchives)
        {
            RemoteObjectCollection<PHPRelease> releases = new RemoteObjectCollection<PHPRelease>();
            object o = Invoke("GetAvailablePHPReleases", includeArchives);
            if (o != null)
            {
                releases.SetData(o);
            }
            return releases;
        }

        internal RemoteObjectCollection<PECLPackage> GetCompatiblePECLPackages(string extensionName)
        {
            RemoteObjectCollection<PECLPackage> packages = new RemoteObjectCollection<PECLPackage>();
            object o = Invoke("GetCompatiblePECLPackages", extensionName);
            if (o != null)
            {
                packages.SetData(o);
            }
            return packages;
        }

        internal RemoteObjectCollection<PHPIniMigrationItem> GetIniMigrationPreview(string sourceHandlerName)
        {
            RemoteObjectCollection<PHPIniMigrationItem> items = new RemoteObjectCollection<PHPIniMigrationItem>();
            object o = Invoke("GetIniMigrationPreview", sourceHandlerName);
            if (o != null)
            {
                items.SetData(o);
            }
            return items;
        }

        internal ArrayList GetPECLExtensionNames(string filter)
        {
            return (ArrayList)Invoke("GetPECLExtensionNames", filter);
        }

        internal PHPBuildInfo GetPHPBuildInfo()
        {
            object o = Invoke("GetPHPBuildInfo");
            PHPBuildInfo result = null;
            if (o != null)
            {
                result = new PHPBuildInfo();
                result.SetData(o);
            }
            return result;
        }

        internal string InstallPECLExtension(PECLPackage package, bool overwrite)
        {
            return (string)Invoke("InstallPECLExtension", package.GetData(), overwrite);
        }

        internal string InstallPHPRelease(PHPRelease release, string targetDirectory, bool activate, bool allowUnverified)
        {
            return (string)Invoke("InstallPHPRelease", release.GetData(), targetDirectory, activate, allowUnverified);
        }

        internal bool IsPHPBranchSupported()
        {
            return (bool)Invoke("IsPHPBranchSupported");
        }

        internal string UpgradePHP(string targetDirectory)
        {
            return (string)Invoke("UpgradePHP", targetDirectory);
        }

    }
}