//-----------------------------------------------------------------------
// <copyright>
// Copyright (C) Ruslan Yakushev for the PHP Manager for IIS project.
//
// This file is subject to the terms and conditions of the Microsoft Public License (MS-PL).
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL for more details.
// </copyright>
//----------------------------------------------------------------------- 

using System.Reflection;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyCompany("PHP Manager Project")]
[assembly: AssemblyProduct("PHP Manager for IIS")]
[assembly: AssemblyCopyright("Copyright © PHP Manager Project 2011")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
// AssemblyVersion is part of the strong name used in administration.config and by the PowerShell snap-in,
// so it stays stable. AssemblyFileVersion must grow on every release: Windows Installer only replaces
// an assembly in the GAC when the new file version is higher (keep it equal to the MSI ProductVersion).
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("3.0.0.0")]