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

namespace Web.Management.PHP.Config
{

    /// <summary>
    /// A PHP for Windows build available for download from downloads.php.net.
    /// </summary>
    public sealed class PHPRelease : IRemoteObject
    {
        private object[] _data;

        private const int IndexVersion = 0;
        private const int IndexThreadSafe = 1;
        private const int IndexCompiler = 2;
        private const int IndexArchitecture = 3;
        private const int IndexFileName = 4;
        private const int IndexUrl = 5;
        private const int IndexSha256 = 6;
        private const int IndexSize = 7;
        private const int IndexIsLatest = 8;

        private const int Size = 9;

        public PHPRelease()
        {
            _data = new object[Size];
            Version = String.Empty;
            ThreadSafe = false;
            Compiler = String.Empty;
            Architecture = String.Empty;
            FileName = String.Empty;
            Url = String.Empty;
            Sha256 = String.Empty;
            FileSize = String.Empty;
            IsLatest = false;
        }

        public string Architecture
        {
            get
            {
                return (string)_data[IndexArchitecture];
            }
            set
            {
                _data[IndexArchitecture] = value;
            }
        }

        public string Branch
        {
            get
            {
                return PHPBuildInfo.GetBranch(Version);
            }
        }

        public string Compiler
        {
            get
            {
                return (string)_data[IndexCompiler];
            }
            set
            {
                _data[IndexCompiler] = value;
            }
        }

        public string DisplayName
        {
            get
            {
                return String.Format(CultureInfo.InvariantCulture, "{0} {1} {2} ({3})",
                    Version, ThreadSafe ? "TS" : "NTS", Architecture, Compiler);
            }
        }

        public string FileName
        {
            get
            {
                return (string)_data[IndexFileName];
            }
            set
            {
                _data[IndexFileName] = value;
            }
        }

        public string FileSize
        {
            get
            {
                return (string)_data[IndexSize];
            }
            set
            {
                _data[IndexSize] = value;
            }
        }

        /// <summary>
        /// True when this is the most recent patch release of its branch.
        /// </summary>
        public bool IsLatest
        {
            get
            {
                return (bool)_data[IndexIsLatest];
            }
            set
            {
                _data[IndexIsLatest] = value;
            }
        }

        /// <summary>
        /// SHA-256 of the zip file. Empty for archived releases, which are published without checksums.
        /// </summary>
        public string Sha256
        {
            get
            {
                return (string)_data[IndexSha256];
            }
            set
            {
                _data[IndexSha256] = value;
            }
        }

        public bool ThreadSafe
        {
            get
            {
                return (bool)_data[IndexThreadSafe];
            }
            set
            {
                _data[IndexThreadSafe] = value;
            }
        }

        public string Url
        {
            get
            {
                return (string)_data[IndexUrl];
            }
            set
            {
                _data[IndexUrl] = value;
            }
        }

        public string Version
        {
            get
            {
                return (string)_data[IndexVersion];
            }
            set
            {
                _data[IndexVersion] = value;
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
