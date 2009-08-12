﻿using System.IO;
using Chorus.sync;
using Chorus.Utilities;
using Chorus.VcsDrivers;
using NUnit.Framework;

namespace LibChorus.Tests.sync
{
	/// <summary>
	/// These tests (at least any that are not manual-only) should not actually require a usb key
	/// </summary>
	[TestFixture]
	public class UsbRepositorySourceTests
	{
		private ProjectFolderConfiguration _project;
		private StringBuilderProgress _progress;
		private string _pathToTestRoot;
		private string _pathToProjectRoot;

		[SetUp]
		public void Setup()
		{
			_progress = new StringBuilderProgress();
			_pathToTestRoot = Path.Combine(Path.GetTempPath(), "ChorusTest");
			if (Directory.Exists(_pathToTestRoot))
				Directory.Delete(_pathToTestRoot, true);
			Directory.CreateDirectory(_pathToTestRoot);

			_pathToProjectRoot = Path.Combine(_pathToTestRoot, "foo project");
			Directory.CreateDirectory(_pathToProjectRoot);

			string pathToText = WriteTestFile("version one");

			RepositorySetup.MakeRepositoryForTest(_pathToProjectRoot, "bob",_progress);
			_project = new ProjectFolderConfiguration(_pathToProjectRoot);
			_project.IncludePatterns.Add(pathToText);
			_project.FolderPath = _pathToProjectRoot;


			UsbKeyRepositorySource.SetRootDirForAllSourcesDuringUnitTest(_pathToTestRoot);
		}

		private string WriteTestFile(string contents)
		{
			string pathToText = Path.Combine(_pathToProjectRoot, "foo.txt");
			File.WriteAllText(pathToText, contents);
			return pathToText;
		}


		[Test]
		public void SyncNow_OnlyABlankFauxUsbAvailable_UsbGetsClone()
		{
			Synchronizer manager = Synchronizer.FromProjectConfiguration(_project, new NullProgress());
			SyncOptions options = new SyncOptions();
			options.DoMergeWithOthers = true;
			options.DoPushToLocalSources = true;
			options.RepositorySourcesToTry.Add(manager.UsbPath);

			WriteTestFile("version two");

			manager.SyncNow(options, _progress);
			string dir = Path.Combine(UsbKeyRepositorySource.RootDirForUsbSourceDuringUnitTest, "foo project");
			Assert.IsTrue(Directory.Exists(dir));

		}

		[Test]
		public void SyncNow_AlreadySetupFauxUsbAvailable_UsbGetsSync()
		{
			SyncOptions options = new SyncOptions();
			Synchronizer manager = Synchronizer.FromProjectConfiguration(_project, new NullProgress());
			manager.SyncNow(options, _progress);

			options.RepositorySourcesToTry.Add(manager.UsbPath);
			string dir = Path.Combine(UsbKeyRepositorySource.RootDirForUsbSourceDuringUnitTest, "foo project");
			manager.MakeClone(dir, true, _progress);
			string contents = File.ReadAllText(Path.Combine(dir, "foo.txt"));
			Assert.AreEqual("version one", contents);
			WriteTestFile("version two");
			manager.SyncNow(options, _progress);
			contents = File.ReadAllText(Path.Combine(dir, "foo.txt"));
			Assert.AreEqual("version two", contents);
		}
	}
}
