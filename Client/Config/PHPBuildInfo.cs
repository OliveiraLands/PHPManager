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
    /// Describes how a PHP build was compiled: version, thread safety, compiler and architecture.
    /// </summary>
    public sealed class PHPBuildInfo : IRemoteObject
    {
        private object[] _data;

        private const int IndexVersion = 0;
        private const int IndexThreadSafe = 1;
        private const int IndexCompiler = 2;
        private const int IndexArchitecture = 3;
        private const int IndexDirectory = 4;
        private const int IndexHandlerName = 5;

        private const int Size = 6;

        public PHPBuildInfo()
        {
            _data = new object[Size];
            Version = String.Empty;
            ThreadSafe = false;
            Compiler = String.Empty;
            Architecture = String.Empty;
            Directory = String.Empty;
            HandlerName = String.Empty;
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

        /// <summary>
        /// Major.minor version, for example "8.4".
        /// </summary>
        public string Branch
        {
            get
            {
                return GetBranch(Version);
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

        public string Directory
        {
            get
            {
                return (string)_data[IndexDirectory];
            }
            set
            {
                _data[IndexDirectory] = value;
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

        public string HandlerName
        {
            get
            {
                return (string)_data[IndexHandlerName];
            }
            set
            {
                _data[IndexHandlerName] = value;
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

        internal static string GetBranch(string version)
        {
            if (String.IsNullOrEmpty(version))
            {
                return String.Empty;
            }

            string[] parts = version.Split('.');
            if (parts.Length < 2)
            {
                return version;
            }

            return parts[0] + "." + parts[1];
        }

        /// <summary>
        /// Parses "x.y.z" (ignoring rebuild suffixes such as "-1" or "RC1") into a comparable Version.
        /// </summary>
        internal static Version ParseVersion(string version)
        {
            if (String.IsNullOrEmpty(version))
            {
                return new Version(0, 0);
            }

            int[] numbers = new int[3];
            string[] parts = version.Split('.');
            for (int i = 0; i < numbers.Length && i < parts.Length; i++)
            {
                string digits = String.Empty;
                foreach (char c in parts[i])
                {
                    if (!Char.IsDigit(c))
                    {
                        break;
                    }
                    digits += c;
                }

                int value;
                Int32.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
                numbers[i] = value;
            }

            return new Version(numbers[0], numbers[1], numbers[2]);
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
