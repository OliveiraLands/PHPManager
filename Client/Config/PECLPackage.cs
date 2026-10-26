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
    /// A Windows build of a PECL extension that matches a given PHP build.
    /// </summary>
    public sealed class PECLPackage : IRemoteObject
    {
        private object[] _data;

        private const int IndexName = 0;
        private const int IndexVersion = 1;
        private const int IndexPHPBranch = 2;
        private const int IndexThreadSafe = 3;
        private const int IndexCompiler = 4;
        private const int IndexArchitecture = 5;
        private const int IndexFileName = 6;
        private const int IndexUrl = 7;

        private const int Size = 8;

        public PECLPackage()
        {
            _data = new object[Size];
            Name = String.Empty;
            Version = String.Empty;
            PHPBranch = String.Empty;
            ThreadSafe = false;
            Compiler = String.Empty;
            Architecture = String.Empty;
            FileName = String.Empty;
            Url = String.Empty;
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
                return String.Format(CultureInfo.InvariantCulture, "{0} {1} (PHP {2} {3} {4} {5})",
                    Name, Version, PHPBranch, ThreadSafe ? "TS" : "NTS", Compiler, Architecture);
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

        public string PHPBranch
        {
            get
            {
                return (string)_data[IndexPHPBranch];
            }
            set
            {
                _data[IndexPHPBranch] = value;
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
