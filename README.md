# PHP Manager for IIS

PHP Manager for IIS is an extension for **IIS Manager** (`inetmgr`) and **Windows PowerShell** that installs, registers, configures and updates PHP on Internet Information Services (IIS 7 to IIS 10) using FastCGI.

This repository is a fork of [RonaldCarter/PHPManager](https://github.com/RonaldCarter/PHPManager). It updates the project to build with current Visual Studio versions, run on current Windows and IIS releases, and adds PHP download, update, php.ini migration and PECL extension management.

---

## Table of contents

- [Features](#features)
  - [PHP setup](#php-setup)
  - [PHP settings](#php-settings)
  - [PHP extensions](#php-extensions)
  - [PowerShell cmdlets](#powershell-cmdlets)
- [Requirements](#requirements)
- [Install](#install)
- [Configure](#configure)
- [Security and integrity](#security-and-integrity)
- [Build from source](#build-from-source)
- [Troubleshooting](#troubleshooting)
- [Project structure](#project-structure)
- [License](#license)

---

## Features

All features are available in IIS Manager on the **PHP Manager** page at the server, site and application level. Tasks that change the server (install, update, register, extensions) require a server administrator.

### PHP setup

| Feature | Description |
|---|---|
| **Register new PHP version** | Registers an existing `php-cgi.exe` with IIS: creates the FastCGI application, the `*.php` handler mapping, sets `index.php` as default document, creates `php.ini` from `php.ini-production` when it does not exist and applies recommended settings. |
| **Change PHP version** | Several PHP versions can be registered side by side. Selects which one is active for the server, a site or an application. |
| **Check phpinfo()** | Shows the `phpinfo()` output of the active PHP version for a chosen site and URL. |
| **Download and install a PHP version** | Lists the PHP for Windows builds published at [downloads.php.net](https://downloads.php.net/~windows/releases/) (the same files used by the `PHP.PHP.*` winget packages). You choose the version (latest release of each branch or older releases), thread safety (NTS is recommended for IIS) and architecture, the install folder on the IIS server, and whether it becomes the active version. The download is verified with its published SHA-256 checksum, extracted, tested and registered with IIS. Older releases are published without a checksum; the dialog warns about it. |
| **Check for PHP updates** | Compares the active PHP version with the latest release of the same branch, thread safety, compiler and architecture. When an update exists (for example 8.4.26 -> 8.4.27, usually with security fixes) a warning with an **Update now** link is shown on the PHP Manager page. The update is installed **side by side** in a new folder, `php.ini` settings and extensions are copied, and the new version becomes active. The previous version stays registered, so you can switch back with *Change PHP version*. It also warns when the branch of the active version is no longer supported. |
| **Configuration check** | Detects non optimal IIS, FastCGI and `php.ini` settings (default document, `PHPRC`, `PHP_FCGI_MAX_REQUESTS`, `monitorChangesTo`, `extension_dir`, error log, session and upload folders, `cgi.force_redirect`, `cgi.fix_pathinfo`, `fastcgi.impersonate`, `date.timezone`) and fixes them on request. |

### PHP settings

| Feature | Description |
|---|---|
| **Error reporting** | Applies a development or production error reporting profile (`error_reporting`, `display_errors`, `html_errors`, `log_errors`, `fastcgi.logging`, `error_log`). |
| **Runtime limits** | Edits `memory_limit`, `max_execution_time`, `max_input_time`, `post_max_size`, `upload_max_filesize` and `max_file_uploads`. |
| **Manage all settings** | Lists, adds, edits and removes any `php.ini` directive. The file is edited in place, keeping comments and layout. |
| **Copy settings from another PHP version** | Compares the `php.ini` of another registered PHP version with the `php.ini` of the active one and copies the selected settings and enabled extensions. Paths that point to the old PHP folder are changed to the new folder. Directives that the active PHP version does not know are shown but cannot be copied. When an extension DLL does not exist in the active version, a compatible PECL build is searched; if one exists it is installed together with the setting, otherwise the extension cannot be copied. |

### PHP extensions

| Feature | Description |
|---|---|
| **Enable or disable extensions** | Lists every extension DLL in `extension_dir` and enables or disables it in `php.ini`. Both `extension=curl` and `extension=php_curl.dll` styles are recognized. |
| **Add an extension** | Copies an extension DLL that already exists on the IIS server to `extension_dir` and enables it. |
| **Install extension from the internet** | Searches the Windows builds of PECL extensions at [downloads.php.net/~windows/pecl](https://downloads.php.net/~windows/pecl/releases/) and shows only the builds compatible with the active PHP version (branch, thread safety, compiler and architecture). The `php_<name>.dll` is copied to `extension_dir`, dependency DLLs shipped in the package are copied to the PHP folder (DLLs of PHP itself are never replaced) and the extension is enabled. |

### PowerShell cmdlets

The `PHPManagerSnapin` snap-in is registered by the installer. Use it from **Windows PowerShell 5.1 (64-bit) as administrator**:

```powershell
Add-PSSnapin PHPManagerSnapin
Get-Command -PSSnapin PHPManagerSnapin
Get-Help Install-PHPVersion -Full
```

| Cmdlet | Description |
|---|---|
| `Get-PHPVersion` | Lists the registered PHP versions and which one is active. |
| `New-PHPVersion` | Registers an existing PHP folder or `php-cgi.exe` with IIS. |
| `Set-PHPVersion` | Selects the active PHP version. |
| `Get-PHPConfiguration` | Shows the active PHP version, `php.ini` path and configuration summary. |
| `Get-PHPSetting`, `New-PHPSetting`, `Set-PHPSetting`, `Remove-PHPSetting` | Read and change `php.ini` directives. |
| `Get-PHPExtension`, `Set-PHPExtension` | List, enable and disable extensions. |
| `Get-PHPAvailableVersion` | Lists the PHP builds available at downloads.php.net (`-IncludeArchives`, `-ThreadSafe`, `-Architecture`). |
| `Install-PHPVersion` | Downloads, verifies, extracts and registers a PHP version, e.g. `Install-PHPVersion 8.4 -Activate` or `Install-PHPVersion 8.3.30 D:\PHP\8.3.30`. |
| `Update-PHPVersion` | Side by side update of the active version to the latest release of its branch. |
| `Copy-PHPIniSettings` | Copies `php.ini` settings and extensions from another registered version (`-WhatIf` lists the changes only). |
| `Find-PHPExtension` | Searches PECL extensions; `-Compatible` returns the builds for the active PHP version. |
| `Install-PHPExtension` | Installs and enables a compatible PECL extension, e.g. `Install-PHPExtension redis`. |

All cmdlets accept `-SiteName` and `-VirtualPath` to work on a site or application instead of the server level. Cmdlets that change the system support `-WhatIf` and `-Confirm`.

---

## Requirements

**To run PHP Manager**

- Windows 10 / 11 or Windows Server 2016 and later, **64-bit**.
- IIS with the **CGI** role service (it provides FastCGI) and the **IIS Management Console**.
  - Windows client: *Turn Windows features on or off* -> *Internet Information Services* -> *World Wide Web Services* -> *Application Development Features* -> **CGI**, and *Web Management Tools* -> **IIS Management Console**.
  - Windows Server: `Install-WindowsFeature Web-Server, Web-CGI, Web-Mgmt-Console`
- **.NET Framework 4.8**.
- **Microsoft Visual C++ Redistributable 2015-2022 (or later)** for the architecture of the PHP builds you install (x64 and/or x86). PHP does not start without it.
- Internet access from the IIS server to `https://downloads.php.net` for the download, update and PECL features.

**To build PHP Manager**

- Visual Studio 2022 or later (tested with Visual Studio 18) with the **.NET desktop development** workload and the **.NET Framework 4.8 targeting pack**, or the matching Build Tools with MSBuild.
- IIS Management Console installed on the build machine: the projects reference `C:\Windows\System32\inetsrv\Microsoft.Web.Management.dll` and `Microsoft.Web.Administration.dll`.
- Access to nuget.org: the installer project restores the **WiX Toolset 5.0.2** SDK automatically.
- Optional: the free **HeatWave** extension (FireGiant) to open the WiX project inside Visual Studio. Without it the project shows as unloaded in the IDE but still builds with MSBuild.

---

## Install

1. Install the requirements above.
2. Run the installer **as administrator**:

   ```bat
   msiexec /i PHPManagerForIIS.msi /l*v %TEMP%\PHPManagerForIIS-install.log
   ```

   The installer:
   - places `Web.Management.PHP.dll`, `Web.Management.PHP.Client.dll` and `Web.Management.PHP.PowerShell.dll` in the Global Assembly Cache and a copy in `C:\Program Files\PHP Manager for IIS`;
   - registers the **PHP Manager** module in IIS Manager (`administration.config`);
   - registers the `PHPManagerSnapin` PowerShell snap-in;
   - replaces any previous PHP Manager version (same upgrade code), including PHP Manager 1.x.
3. Close and reopen IIS Manager. **PHP Manager** appears at the server, site and application level.

### Upgrading from PHP Manager 1.2 on Windows 11 24H2 / Windows Server 2025

PHP Manager 1.2 cannot be uninstalled on systems without Windows PowerShell 2.0: its uninstaller fails with *Error 1001* and the new installer rolls back. Remove it first, as administrator:

```powershell
powershell.exe -ExecutionPolicy Bypass -File Setup\Remove-PHPManager12.ps1
```

The script backs up the cached 1.2 installer package, disables only the failing PowerShell snap-in action and uninstalls PHP Manager 1.2. Then run `PHPManagerForIIS.msi`.

### Uninstall

Use *Apps -> Installed apps* (or *Programs and Features*) and remove **PHP Manager for IIS**, or run `msiexec /x PHPManagerForIIS.msi`. Registered PHP versions and their files are not removed.

---

## Configure

### First PHP installation

1. Open IIS Manager, select the server and open **PHP Manager**.
2. Click **Download and install a PHP version**, pick the version (for IIS choose an **NTS x64** build), keep or change the folder (default `%ProgramFiles%\PHP\<version>-nts-x64`) and keep **Make it the active PHP version** checked.
3. If PHP Manager shows *PHP configuration is not optimal*, click **View recommendations** and apply them.
4. Use **Check phpinfo()** to confirm that PHP runs.

To use a PHP build that is already on disk, use **Register new PHP version** and select its `php-cgi.exe`.

### Keeping PHP up to date

- PHP Manager checks for updates when the PHP Manager page is opened (server administrators only) and shows **Update now** when a newer release of the same branch exists.
- **Check for PHP updates** installs it side by side, copies `php.ini` and extensions and activates it. If something goes wrong, switch back with **Change PHP version**.
- From a scheduled task: `Add-PSSnapin PHPManagerSnapin; Update-PHPVersion -Confirm:$false`

### Moving to a new PHP branch

1. **Download and install a PHP version** with the new branch (for example 8.5), active or not.
2. With the new version active, use **Copy settings from another PHP version**, select the old version, review the list and apply. Extensions missing in the new version are installed from PECL when a compatible build exists.

### Notes

- **Remote management**: PHP Manager also works through IIS Manager remote connections (Web Management Service, `WMSvc`). All downloads and file operations run on the IIS server, so folders are server paths. Large downloads over a remote connection may hit the WMSvc call timeout; in that case run the PowerShell cmdlets on the server.
- **Proxy / firewall**: the IIS server needs outbound HTTPS to `downloads.php.net`.
- PHP Manager writes `php.ini` keeping its comments, layout and byte order mark.

---

## Security and integrity

PHP Manager downloads and runs code on the IIS server, so every download is checked before anything is installed.

### What the PHP project publishes

| Download | Checksum | Digital signature |
|---|---|---|
| Latest release of each PHP branch (`releases.json`) | SHA-256 | No |
| Older PHP releases (`releases/archives/`) | None | No |
| PECL extension builds (`pecl/releases/`) | None | No |

PHP for Windows binaries and PECL DLLs are not Authenticode signed, and no checksum is published for archived releases or PECL builds.

### Checks applied

**Origin**
- Only `https://downloads.php.net/~windows/` is used (HTTPS, default port, no user information in the URL). The final address after redirects is checked too, so a redirect to HTTP or to another host is rejected.
- Download URLs and checksums are never taken from the client (IIS Manager or PowerShell). The server looks the release or PECL build up again in the official index; the client only sends the version, thread safety, compiler and architecture (PHP) or the extension name and version (PECL).
- Extension names and versions are validated before they are used to build a URL.

**Integrity of PHP releases**
- The SHA-256 published in `releases.json` is verified.
- Releases without a published checksum (archived releases) are refused unless the administrator confirms the installation explicitly (a confirmation in IIS Manager, `-AllowUnverified` in `Install-PHPVersion`).
- The number of bytes received must match `Content-Length`, so a truncated download is detected; damaged zip files are rejected.
- After extraction, `php-cgi.exe` must run and `php.exe` must report exactly the requested version, thread safety, architecture and compiler.

**Integrity of PECL extensions**
- The package is extracted to a temporary folder first. Every DLL must be a valid Windows DLL for the architecture of the active PHP version.
- The extension must load in a copy of the target PHP layout (PHP's own DLLs taking precedence, as at run time) without startup warnings and must add a module. Only then are the files copied to `extension_dir` and the PHP folder.
- Existing DLLs of PHP are never replaced by DLLs from a PECL package.
- Stable extension versions are offered before alpha, beta and RC versions.

**Files and folders**
- New PHP folders get their own permissions instead of inheriting them: SYSTEM, Administrators and the installing account have full control; Users and IIS_IUSRS (application pool identities) can only read and execute. A folder created directly under `C:\` would otherwise inherit *Authenticated Users: Modify*, allowing any local user to replace `php-cgi.exe`, which IIS executes.
- UNC paths, drive roots, relative paths and the Windows folder are refused as install folders.
- Each download uses a new, randomly named temporary folder that is deleted afterwards.
- Downloads are limited to 300 MB and extracted content to 1 GB (512 MB for PECL packages).
- When copying php.ini settings, the server computes the items itself and only takes the selection from the client; names or values containing line breaks are ignored.

### Remaining risk

Without published checksums or signatures, the authenticity of archived PHP releases and PECL builds relies on HTTPS to `downloads.php.net`. The checks above detect corrupted, incomplete, wrong-architecture and incompatible files, but cannot prove who built them. Prefer the latest release of a supported branch, which is verified with SHA-256.

---

## Build from source

```bat
git clone https://github.com/OliveiraLands/PHPManager.git
cd PHPManager
```

### Visual Studio

Open `PHPManager.sln`, select **Release** and build the solution. Output goes to `bin\Release\`, including the installer `bin\Release\PHPManagerForIIS.msi`.

### Command line

```bat
"%ProgramFiles%\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" PHPManager.sln -restore -p:Configuration=Release
```

Adjust the MSBuild path to your Visual Studio edition (`vswhere -latest -find MSBuild\**\Bin\MSBuild.exe` finds it).

### Projects

| Project | Output | Content |
|---|---|---|
| `Server\PHPManager.csproj` | `Web.Management.PHP.dll` | IIS Manager module provider and service: IIS/FastCGI configuration, `php.ini` handling, PHP download, update, migration and PECL logic (`Server\Install`). |
| `Client\PHPManagerClient.csproj` | `Web.Management.PHP.Client.dll` | IIS Manager user interface (pages, dialogs) and resources in 7 languages. |
| `Powershell\PHPManagerPowershell.csproj` | `Web.Management.PHP.PowerShell.dll` | PowerShell snap-in and cmdlets, help in `Powershell\Documentation`. |
| `Setup\PHPManagerSetupHelper` | `Web.Management.PHP.Setup.dll` | Installer class that registers the module in IIS Manager. |
| `Setup\PHPManagerSetup.wixproj` | `PHPManagerForIIS.msi` | WiX 5 installer (`Setup\Package.wxs`). |

### Debugging

- In **Debug**, the post-build events register the assemblies in the GAC with `gacutil` so IIS Manager loads the new build. This only happens when Visual Studio runs **as administrator**; otherwise the build prints a warning and skips it.
- Attach the debugger to `InetMgr.exe` (local connection) or `WMSvc.exe` (remote connection).

### Versioning rules

- `AssemblyVersion` in `SharedAssembyInfo.cs` is part of the strong name stored in `administration.config` and in the snap-in registration: keep it stable.
- `AssemblyFileVersion` (same file) must **increase on every release** and should match `ProductVersion` in `Setup\PHPManagerSetup.wixproj`; Windows Installer only replaces an assembly in the GAC when its file version is higher.
- The assemblies installed in the GAC may only reference assemblies that exist in the GAC (.NET Framework and IIS). Do not reference NuGet versions of `Microsoft.Web.Administration` or `System.IO.Compression*`.

### WiX version

The installer is pinned to **WiX Toolset 5.0.2**, which is free (MS-RL). WiX 6 and later require organizations above a revenue threshold to pay the [Open Source Maintenance Fee](https://docs.firegiant.com/wix/osmf/). Do not update the `WixToolset.*` NuGet packages unless that fee is covered.

---

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| **HTTP 500.0, "The FastCGI process exited unexpectedly"** | PHP stops at startup. Check the Windows *Application* event log (source `PHP-<version>`) and the `error_log` file. Common causes: a directive removed in the PHP version (e.g. `track_errors` on PHP 8+; remove it in *Manage all settings*), an extension built for another PHP version, or a missing Visual C++ Redistributable. |
| **"The installed PHP cannot run"** when installing PHP | Install the latest Microsoft Visual C++ Redistributable for the PHP architecture. |
| **"Cannot download the file"** | The IIS server has no HTTPS access to `downloads.php.net` (proxy, firewall or DNS). |
| **New features do not appear after reinstalling** | IIS Manager is still loading the previous assemblies from the GAC. Check the file version of `C:\Windows\Microsoft.NET\assembly\GAC_MSIL\Web.Management.PHP.Client\*\Web.Management.PHP.Client.dll`; reinstall the MSI built from the current sources and reopen IIS Manager. |
| **Error 1001 while installing over PHP Manager 1.2** | See [Upgrading from PHP Manager 1.2](#upgrading-from-php-manager-12-on-windows-11-24h2--windows-server-2025). |
| **PHP Manager icon missing in IIS Manager** | The CGI/FastCGI role service is not installed, or IIS Manager was open during installation: install CGI and reopen IIS Manager. |

---

## Project structure

```
Client\       IIS Manager UI (pages, dialogs, resources, shared DTOs in Client\Config)
Server\       IIS Manager service and configuration logic; Server\Install = downloads, updates, migration, PECL
Powershell\   PowerShell snap-in, cmdlets and help
Setup\        WiX installer, setup helper and Remove-PHPManager12.ps1
```

---

## License

PHP Manager for IIS is released under the [Microsoft Public License (MS-PL)](https://opensource.org/licenses/MS-PL). Originally created by Ruslan Yakushev; updated by Ronald Carter and contributors in [RonaldCarter/PHPManager](https://github.com/RonaldCarter/PHPManager), from which this repository is forked.
