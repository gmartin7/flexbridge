﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Chorus;
using FLEx_ChorusPlugin.Model;
using FLEx_ChorusPlugin.View;

namespace FLEx_ChorusPlugin.Controller
{
	class ObtainProjectController : IFwBridgeController, IDisposable
	{
		private readonly ChorusSystem _chorusSystem;
		private readonly IGetSharedProject _getSharedProject;
		private readonly IStartupNewView _startupNewView;

		/// <summary>
		/// Constructs the ObtainProjectController with the given options.
		/// </summary>
		/// <param name="options">(Not currently used, remove later if no use reveals its self.)</param>
		public ObtainProjectController(Dictionary<string, string> options)
		{
			_startupNewView = new StartupNewView();
			_startupNewView.Startup += StartupNewViewStartupHandler;
			_getSharedProject = new GetSharedProject();
			MainForm = new ObtainProjectView();
			MainForm.Controls.Add((Control)_startupNewView);
		}

		private void StartupNewViewStartupHandler(object sender, StartupNewEventArgs e)
		{
			// This handler can't really work (yet) in an environment where the local system has an extant project,
			// and the local user wants to collaborate with a remote user,
			// where the FW language project is the 'same' on both computers.
			// That is, we don't (yet) support merging the two, since they hav eno common ancestor.
			// Odds are they each have crucial objects, such as LangProject or LexDb, that need to be singletons,
			// but which have different guids.
			// (Consider G & J Andersen's case, where each has an FW 6 system.
			// They likely want to be able to merge the two systems they have, but that is not (yet) supported.)

			_getSharedProject.GetSharedProjectUsing(MainForm, e.ExtantRepoSource, e.ProjectFolder);
		}

		public void Dispose()
		{
			_startupNewView.Startup -= StartupNewViewStartupHandler;
		}

		public Form MainForm {
			get;
			set;
		}

		public ChorusSystem ChorusSystem
		{
			get { return _chorusSystem; }
		}

		public LanguageProject CurrentProject
		{
			get { throw new NotImplementedException(); }
		}
	}
}
