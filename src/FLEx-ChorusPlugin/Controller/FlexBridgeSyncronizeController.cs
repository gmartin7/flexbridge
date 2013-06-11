﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using Chorus;
using FLEx_ChorusPlugin.Infrastructure;
using FLEx_ChorusPlugin.Infrastructure.DomainServices;
using TriboroughBridge_ChorusPlugin;
using TriboroughBridge_ChorusPlugin.Controller;
using TriboroughBridge_ChorusPlugin.View;

namespace FLEx_ChorusPlugin.Controller
{
	[Export(typeof(IBridgeController))]
	internal sealed class FlexBridgeSyncronizeController : ISyncronizeController
	{
		private ISynchronizeProject _flexProjectSynchronizer;
		private MainBridgeForm _mainBridgeForm;
		private string _projectDir;
		private string _projectName;
		private Dictionary<string, string> _options;

		#region IBridgeController implementation

		public void InitializeController(MainBridgeForm mainForm, Dictionary<string, string> options, ControllerType controllerType)
		{
			// // -p <$fwroot>\foo\foo.fwdata
			_options = options;
			_projectDir = Path.GetDirectoryName(options["-p"]);
			_projectName = Path.GetFileNameWithoutExtension(options["-p"]);
			_mainBridgeForm = mainForm;
			_flexProjectSynchronizer = new SynchronizeFlexProject();
			string writingSystem;
			options.TryGetValue("-ws", out writingSystem);
			ChorusSystem = Utilities.InitializeChorusSystem(_projectDir, options["-u"], "flex", writingSystem, FlexFolderSystem.ConfigureChorusProjectFolder);
			if (ChorusSystem.Repository.Identifier == null)
			{
				// Write an empty custom prop file to get something in the default branch at rev 0.
				// The custom prop file will always exist and can be empty, so start it as empty (null).
				// This basic rev 0 commit will then allow for a roll back if the soon to follow main commit fails on a validation problem.
				FileWriterService.WriteCustomPropertyFile(Path.Combine(_projectDir, SharedConstants.CustomPropertiesFilename), null);
				ChorusSystem.Repository.AddAndCheckinFile(Path.Combine(_projectDir, SharedConstants.CustomPropertiesFilename));
			}
			ChorusSystem.EnsureAllNotesRepositoriesLoaded();
		}

		public ChorusSystem ChorusSystem { get; private set; }

		public IEnumerable<ControllerType> SupportedControllerActions
		{
			get { return new List<ControllerType> { ControllerType.SendReceive }; }
		}

		public IEnumerable<BridgeModelType> SupportedModels
		{
			get { return new List<BridgeModelType>{ BridgeModelType.Flex }; }
		}

		#endregion

		#region ISyncronizeController

		public void Syncronize()
		{
			ChangesReceived = _flexProjectSynchronizer.SynchronizeProject(_options, _mainBridgeForm, ChorusSystem, _projectDir, _projectName);
		}

		public bool ChangesReceived { get; private set; }

		#endregion

		#region Implementation of IDisposable

		/// <summary>
		/// Finalizer, in case client doesn't dispose it.
		/// Force Dispose(false) if not already called (i.e. m_isDisposed is true)
		/// </summary>
		~FlexBridgeSyncronizeController()
		{
			Dispose(false);
			// The base class finalizer is called automatically.
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing,
		/// or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		private bool IsDisposed { get; set; }

		/// <summary>
		/// Executes in two distinct scenarios.
		///
		/// 1. If disposing is true, the method has been called directly
		/// or indirectly by a user's code via the Dispose method.
		/// Both managed and unmanaged resources can be disposed.
		///
		/// 2. If disposing is false, the method has been called by the
		/// runtime from inside the finalizer and you should not reference (access)
		/// other managed objects, as they already have been garbage collected.
		/// Only unmanaged resources can be disposed.
		/// </summary>
		/// <remarks>
		/// If any exceptions are thrown, that is fine.
		/// If the method is being done in a finalizer, it will be ignored.
		/// If it is thrown by client code calling Dispose,
		/// it needs to be handled by fixing the issue.
		/// </remarks>
		private void Dispose(bool disposing)
		{
			if (IsDisposed)
				return;

			if (disposing)
			{
				if (_mainBridgeForm != null)
					_mainBridgeForm.Dispose();

				if (ChorusSystem != null)
					ChorusSystem.Dispose();
			}
			_mainBridgeForm = null;
			ChorusSystem = null;

			IsDisposed = true;
		}

		#endregion
	}
}
