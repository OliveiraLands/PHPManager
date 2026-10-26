//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace Web.Management.PHP.Config
{

    public static class PHPIniMigrationKind
    {
        public const int Setting = 0;
        public const int Extension = 1;
    }

    public static class PHPIniMigrationStatus
    {
        // The setting or extension does not exist in the target php.ini
        public const int New = 0;
        // The setting exists in the target php.ini with a different value
        public const int Changed = 1;
        // The directive is not known by the target PHP version
        public const int Unsupported = 2;
        // The extension DLL does not exist in the target extension directory and there is no
        // compatible PECL build: the item cannot be copied
        public const int MissingExtension = 3;
        // The extension DLL does not exist in the target extension directory, but a PECL build
        // compatible with the target PHP version is available and is installed when the item is copied
        public const int AvailableFromPECL = 4;

        /// <summary>
        /// True when the item can be written to the target php.ini.
        /// </summary>
        public static bool CanBeCopied(int status)
        {
            return status == New || status == Changed || status == AvailableFromPECL;
        }
    }

    /// <summary>
    /// One setting or extension that can be copied from a source php.ini to a target php.ini.
    /// </summary>
    public sealed class PHPIniMigrationItem : IRemoteObject
    {
        private object[] _data;

        private const int IndexKind = 0;
        private const int IndexName = 1;
        private const int IndexSection = 2;
        private const int IndexSourceValue = 3;
        private const int IndexTargetValue = 4;
        private const int IndexStatus = 5;
        private const int IndexSelected = 6;
        private const int IndexPECLPackage = 7;

        private const int Size = 8;

        public PHPIniMigrationItem()
        {
            _data = new object[Size];
            Kind = PHPIniMigrationKind.Setting;
            Name = String.Empty;
            Section = String.Empty;
            SourceValue = String.Empty;
            TargetValue = String.Empty;
            Status = PHPIniMigrationStatus.New;
            Selected = false;
        }

        public int Kind
        {
            get
            {
                return (int)_data[IndexKind];
            }
            set
            {
                _data[IndexKind] = value;
            }
        }

        public string Name
        {
            get
            {
                return (string)_data[IndexName];
            }
            set
            {
                _data[IndexName] = value;
            }
        }

        /// <summary>
        /// PECL build installed for an item with status AvailableFromPECL, otherwise null.
        /// </summary>
        public PECLPackage PECLPackage
        {
            get
            {
                if (_data[IndexPECLPackage] == null)
                {
                    return null;
                }

                PECLPackage package = new PECLPackage();
                package.SetData(_data[IndexPECLPackage]);
                return package;
            }
            set
            {
                _data[IndexPECLPackage] = (value != null) ? value.GetData() : null;
            }
        }

        public string Section
        {
            get
            {
                return (string)_data[IndexSection];
            }
            set
            {
                _data[IndexSection] = value;
            }
        }

        public bool Selected
        {
            get
            {
                return (bool)_data[IndexSelected];
            }
            set
            {
                _data[IndexSelected] = value;
            }
        }

        /// <summary>
        /// Value that will be written to the target php.ini (paths already rewritten to the target PHP folder).
        /// </summary>
        public string SourceValue
        {
            get
            {
                return (string)_data[IndexSourceValue];
            }
            set
            {
                _data[IndexSourceValue] = value;
            }
        }

        public int Status
        {
            get
            {
                return (int)_data[IndexStatus];
            }
            set
            {
                _data[IndexStatus] = value;
            }
        }

        /// <summary>
        /// Current value in the target php.ini, empty when the setting is not there.
        /// </summary>
        public string TargetValue
        {
            get
            {
                return (string)_data[IndexTargetValue];
            }
            set
            {
                _data[IndexTargetValue] = value;
            }
        }

        #region IRemoteObject Members

        public object GetData()
        {
            return _data;
        }

        public void SetData(object o)
        {
            _data = (object[])o;
        }

        #endregion
    }
}
