﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Chorus;
using Chorus.VcsDrivers.Mercurial;
using Chorus.sync;
using Microsoft.Win32;
using Palaso.Progress;

namespace TriboroughBridge_ChorusPlugin
{
	public static class Utilities
	{
		public const string FailureFilename = "FLExImportFailure.notice";
		public const string FwXmlExtension = "." + FwXmlExtensionNoPeriod;
		public const string FwXmlExtensionNoPeriod = "fwdata";
		public const string FwLockExtension = ".lock";
		public const string FwXmlLockExtension = FwXmlExtension + FwLockExtension;
		public const string FwDb4oExtension = "." + FwDb4oExtensionNoPeriod;
		public const string FwDb4oExtensionNoPeriod = "fwdb";
		public const string LiftExtension = ".lift";
		public const string FlexExtension = "_model_version";
		public const string LiftRangesExtension = ".lift-ranges";
		public const string OtherRepositories = "OtherRepositories";
		public const string LIFT = "LIFT";
		private static string _testingProjectsPath;

		internal static void SetProjectsPathForTests(string testProjectsPath)
		{
			_testingProjectsPath = testProjectsPath;
		}

		/// <summary>
		/// Strips file URI prefix from the beginning of a file URI string, and keeps
		/// a beginning slash if in Linux.
		/// eg "file:///C:/Windows" becomes "C:/Windows" in Windows, and
		/// "file:///usr/bin" becomes "/usr/bin" in Linux.
		/// Returns the input unchanged if it does not begin with "file:".
		///
		/// Does not convert the result into a valid path or a path using current platform
		/// path separators.
		/// fileString does not neet to be a valid URI. We would like to treat it as one
		/// but since we import files with file URIs that can be produced by other
		/// tools (eg LIFT) we can't guarantee that they will always be valid.
		///
		/// File URIs, and their conversation to paths, are more complex, with hosts,
		/// forward slashes, and escapes, but just stripping the file URI prefix is
		/// what's currently needed.
		/// Different places in code need "file://', or "file:///" removed.
		///
		/// See uri.LocalPath, http://en.wikipedia.org/wiki/File_URI , and
		/// http://blogs.msdn.com/b/ie/archive/2006/12/06/file-uris-in-windows.aspx .
		/// </summary>
		public static string StripFilePrefix(string fileString)
		{
			if (String.IsNullOrEmpty(fileString))
				return fileString;

			var prefix = Uri.UriSchemeFile + ":";

			if (!fileString.StartsWith(prefix))
				return fileString;

			var path = fileString.Substring(prefix.Length);
			// Trim any number of beginning slashes
			path = path.TrimStart('/');
			// Prepend slash on Linux
			if (IsUnix)
				path = '/' + path;

			return path;
		}

		public static XElement CreateFromBytes(byte[] xmlData)
		{
			using (var memStream = new MemoryStream(xmlData))
			{
				// This loads the MemoryStream as Utf8 xml. (I checked.)
				return XElement.Load(memStream);
			}
		}

		/// <summary>
		/// Returns <c>true</c> if we're running on Unix, otherwise <c>false</c>.
		/// </summary>
		public static bool IsUnix
		{
			get { return Environment.OSVersion.Platform == PlatformID.Unix; }
		}

		/// <summary>
		/// Creates and initializes the ChorusSystem for use in FLExBridge
		/// </summary>
		public static ChorusSystem InitializeChorusSystem(string directoryName, string user, Action<ProjectFolderConfiguration> configure)
		{
			var system = new ChorusSystem(directoryName);
			system.Init(user);
			if (configure != null)
				configure(system.ProjectFolderConfiguration);
			return system;
		}

		public static string HgDataFolder(string path)
		{
			return Path.Combine(path, BridgeTrafficCop.hg, "store", "data");
		}

		public static string LiftOffset(string path)
		{
			return Path.Combine(path, OtherRepositories, Path.GetFileName(path) + "_" + LIFT);
		}

		public static string ProjectsPath
		{
			get
			{
				if (_testingProjectsPath != null)
					return _testingProjectsPath;

				using (var hkcu = Registry.CurrentUser)
				{
					var hkcuPath = GetFwPath(hkcu, "ProjectsDir");
					if (hkcuPath != null)
						return hkcuPath;
				}

				using (var hklm = Registry.LocalMachine)
				{
					return GetFwPath(hklm, "ProjectsDir");
				}
			}
		}

		public static string FwAssemblyPath
		{
			get
			{
				if (_testingProjectsPath != null)
					return _testingProjectsPath;

				using (var hkcu = Registry.CurrentUser)
				{
					var hkcuPath = GetFwPath(hkcu, "FwExeDir");
					if (hkcuPath != null)
						return GetDevOffset(hkcuPath);
					hkcuPath = GetFwPath(hkcu, "RootCodeDir");
					if (hkcuPath != null)
						return GetDevOffset(hkcuPath);
				}

				using (var hklm = Registry.LocalMachine)
				{
					var hklmPath = GetFwPath(hklm, "FwExeDir");
					if (hklmPath != null)
						return GetDevOffset(hklmPath);
					return GetDevOffset(GetFwPath(hklm, "RootCodeDir"));
				}
			}
		}

		private static string GetDevOffset(string path)
		{
			if (path.EndsWith("DistFiles"))
			{
				var baseDir = Path.GetDirectoryName(path);
				var fixitExe = Directory.GetFiles(Path.Combine(baseDir, "Output"), "FixFwData.exe", SearchOption.AllDirectories).First();
				return Path.GetDirectoryName(fixitExe);
			}
			return path;
		}

		private static string GetFwPath(RegistryKey registryKey, string property)
		{
			using (var softwareSubKey = registryKey.OpenSubKey("Software"))
			{
				if (softwareSubKey == null)
					return null;
				using (var silKey = softwareSubKey.OpenSubKey("SIL"))
				{
					if (silKey == null)
						return null;
					using (var fwKey = silKey.OpenSubKey("FieldWorks"))
					{
						if (fwKey == null)
							return null;
						using (var version8Key = fwKey.OpenSubKey("8"))
						{
							if (version8Key == null)
							{
								using (var version7Key = fwKey.OpenSubKey("7.0"))
								{
									return version7Key == null ? null : GetValue(version7Key, property);
								}
							}
							return GetValue(version8Key, property);
						}
					}
				}
			}
		}

		private static string GetValue(RegistryKey registryKey, string value)
		{
			var result = registryKey.GetValue(value) as string;
			if (result == null || result.Trim() == string.Empty)
				return null;
			if (result.Length > 3)
				result = result.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			return result;
		}

		public static bool FolderIsEmpty(string folder)
		{
			return Directory.GetDirectories(folder).Length == 0 && Directory.GetFiles(folder).Length == 0;
		}

		public static Dictionary<string, Revision> CollectAllBranchHeads(string repoPath)
		{
			var retval = new Dictionary<string, Revision>();

			var repo = new HgRepository(repoPath, new NullProgress());
			foreach (var head in repo.GetHeads())
			{
				var branch = head.Branch;
				if (branch == String.Empty)
				{
					branch = "default";
				}
				if (retval.ContainsKey(branch))
				{
					// Use the higher rev number since it has more than one head of the same branch.
					var extantRevNumber = int.Parse(retval[branch].Number.LocalRevisionNumber);
					var currentRevNumber = int.Parse(head.Number.LocalRevisionNumber);
					if (currentRevNumber > extantRevNumber)
					{
						// Use the newer head of a branch.
						retval[branch] = head;
					}
					//else
					//{
					//    // 'else' case: The one we already have is newer, so keep it.
					//}
				}
				else
				{
					// New branch, so add it.
					retval.Add(branch, head);
				}
			}

			return retval;
		}
	}
}
