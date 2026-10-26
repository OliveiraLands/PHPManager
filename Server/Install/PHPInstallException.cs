//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Runtime.Serialization;

namespace Web.Management.PHP.Install
{

    /// <summary>
    /// Error raised while downloading or installing PHP or PECL extensions.
    /// ResourceKey is the name of the client resource string shown in IIS Manager.
    /// </summary>
    [Serializable]
    public sealed class PHPInstallException : Exception
    {
        private readonly string _resourceKey;

        public PHPInstallException()
        {
        }

        public PHPInstallException(string message) : base(message)
        {
        }

        public PHPInstallException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public PHPInstallException(string resourceKey, string message) : base(message)
        {
            _resourceKey = resourceKey;
        }

        public PHPInstallException(string resourceKey, string message, Exception innerException) : base(message, innerException)
        {
            _resourceKey = resourceKey;
        }

        private PHPInstallException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _resourceKey = info.GetString("ResourceKey");
        }

        public string ResourceKey
        {
            get
            {
                return _resourceKey;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ResourceKey", _resourceKey);
        }
    }
}
