﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Windows.Forms;
using Chorus.sync;
using Chorus.Utilities;
using System.Linq;
using Chorus.VcsDrivers;

namespace Chorus.UI.Sync
{
	public class SyncControlModel
	{
		private readonly Synchronizer _synchronizer;
		private readonly BackgroundWorker _backgroundWorker;
		public event EventHandler SynchronizeOver;
		private readonly MultiProgress _progress;
		public StatusProgress StatusProgress { get; private set; }

		public SyncControlModel(ProjectFolderConfiguration projectFolderConfiguration, SyncUIFeatures uiFeatureFlags)
		{
			StatusProgress = new StatusProgress();
			_progress = new MultiProgress(new[] { StatusProgress });
			Features = uiFeatureFlags;
			_synchronizer = Synchronizer.FromProjectConfiguration(projectFolderConfiguration, new NullProgress());
			_backgroundWorker = new BackgroundWorker();
			_backgroundWorker.WorkerSupportsCancellation = true;
			_backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(_backgroundWorker_RunWorkerCompleted);
			_backgroundWorker.DoWork += worker_DoWork;
		}

		void _backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (SynchronizeOver != null)
			{
				if (HasFeature(SyncUIFeatures.PlaySounds))
				{
					UnmanagedMemoryStream stream;
					if (this.StatusProgress.ErrorEncountered)
					{
						stream = Properties.Resources.error;
					}
					else if (this.StatusProgress.WarningEncountered)
					{
						stream = Properties.Resources.warning;
					}
					else
					{
						stream = Properties.Resources.finished;
					}

					using (SoundPlayer player = new SoundPlayer(stream))
					{
						player.Play();
					}
				}
				SynchronizeOver.Invoke(this, null);
			}
		}

		public SyncUIFeatures Features
		{ get; set; }

		public bool EnableSendReceive
		{
			get { return HasFeature(SyncUIFeatures.SendReceiveButton) && !_backgroundWorker.IsBusy; }
		}

		public bool EnableCancel
		{
			get { return _backgroundWorker.IsBusy; }
		}

		public bool ShowTabs
		{
			get {
				return HasFeature(SyncUIFeatures.Log) ||
					HasFeature(SyncUIFeatures.RepositoryChooser) ||
					HasFeature(SyncUIFeatures.TaskList); }
		}

		public bool ShowSyncButton
		{
			get { return HasFeature(SyncUIFeatures.SendReceiveButton); }
		}

		public bool EnableClose
		{
			get { return false; }
		}

		public bool SynchronizingNow
		{
			get { return _backgroundWorker.IsBusy; }
		}


		public bool HasFeature(SyncUIFeatures feature)
		{
			return (Features & feature) == feature;
		}

		public List<RepositoryAddress> GetRepositoriesToList()
		{
			//nb: at the moment, we can't just get it new each time, because it stores the
			//enabled state of the check boxes
			return _synchronizer.GetPotentialSynchronizationSources(new NullProgress());
		}

		public void Sync()
		{
			lock (this)
			{
				if(_backgroundWorker.IsBusy)
					return;
				StatusProgress.Clear();
				SyncOptions options = new SyncOptions();
				options.CheckinDescription = "[chorus] sync";
				options.DoPullFromOthers = true;
				options.DoMergeWithOthers = true;
				options.RepositorySourcesToTry.AddRange(GetRepositoriesToList().Where(r => r.Enabled));

				_backgroundWorker.RunWorkerAsync(new object[] {_synchronizer, options, _progress});
			}
		}

		public void Cancel()
		{
			lock (this)
			{
				if(!_backgroundWorker.IsBusy)
					return;

				_backgroundWorker.CancelAsync();
			}
		}

		static void worker_DoWork(object sender, DoWorkEventArgs e)
		{
			object[] args = e.Argument as object[];
			Synchronizer synchronizer = args[0] as Synchronizer;
			e.Result =  synchronizer.SyncNow(sender as BackgroundWorker, e, args[1] as SyncOptions, args[2] as IProgress);
		}

		public void PathEnabledChanged(RepositoryAddress address, CheckState state)
		{
			address.Enabled = (state == CheckState.Checked);

			//NB: we may someday decide to distinguish between this chorus-app context of "what
			//repos I used last time" and the hgrc default which effect applications (e.g. wesay)
			_synchronizer.SetIsOneOfDefaultSyncAddresses(address, address.Enabled);
		}

		public void AddProgressDisplay(IProgress progress)
		{
			_progress.Add(progress);
		}
	}

	[Flags]
	public enum SyncUIFeatures
	{
		Minimal =0,
		SendReceiveButton=2,
		TaskList=4,
		Log = 8,
		RepositoryChooser = 16,
		PlaySounds = 32,
		Everything = 0xFFFF
	}
}