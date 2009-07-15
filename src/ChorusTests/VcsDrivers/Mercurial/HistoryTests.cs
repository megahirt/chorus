using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Chorus.sync;
using Chorus.Tests.merge;
using Chorus.Utilities;
using Chorus.VcsDrivers.Mercurial;
using NUnit.Framework;
using System.Linq;

namespace Chorus.Tests.VcsDrivers.Mercurial
{
	[TestFixture]
	public class HistoryTests
	{
		private string _pathToTestRoot;
		private StringBuilderProgress _progress;
		private ProjectFolderConfiguration _project;
		private string _pathToText;

		[SetUp]
		public void Setup()
		{
			_pathToTestRoot = Path.Combine(Path.GetTempPath(), "ChorusTest");
			if (Directory.Exists(_pathToTestRoot))
				Directory.Delete(_pathToTestRoot, true);
			Directory.CreateDirectory(_pathToTestRoot);


			_pathToText = Path.Combine(_pathToTestRoot, "foo.txt");
			File.WriteAllText(_pathToText, "version one of my pretend txt");

			RepositoryManager.MakeRepositoryForTest(_pathToTestRoot, "bob");

			_project = new ProjectFolderConfiguration(_pathToTestRoot);
			_project.FolderPath = _pathToTestRoot;
			_project.IncludePatterns.Add(_pathToText);
			_project.FolderPath = _pathToTestRoot;

			_progress = new StringBuilderProgress();
		}

		[Test, ExpectedException(typeof(ApplicationException))]
		public void GetHistory_NoHg_GetException()
		{
			using (RepositoryManager.CreateDvcsMissingSimulation())
			{
				RepositoryManager repo = new RepositoryManager(_project.FolderPath, _project, "bob");
				List<Revision> items = repo.GetAllRevisions(_progress);
				Assert.AreEqual(0, items.Count);
			}
		}

		[Test]
		public void GetAllRevisionss_BeforeAnySyncing_EmptyHistory()
		{
			RepositoryManager repo = new RepositoryManager(_project.FolderPath, _project, "bob");
			List<Revision> items = repo.GetAllRevisions(_progress);
			Assert.AreEqual(0, items.Count);
		}

		[Test]
		public void GetAllRevisionss_AfterSyncingTwoTimes_CorrectHistory()
		{
			RepositoryManager repo = new RepositoryManager(_project.FolderPath, _project, "bob");
			SyncOptions options = new SyncOptions();
			options.DoPullFromOthers = false;
			options.DoMergeWithOthers = false;
			options.CheckinDescription = "first one";
			options.DoPushToLocalSources = false;

			repo.SyncNow(options, _progress);
			File.WriteAllText(_pathToText, "version two of my pretend txt");
			options.CheckinDescription = "second one";
			repo.SyncNow(options, _progress);

			List<Revision> items = repo.GetAllRevisions(_progress);
			Assert.AreEqual(2, items.Count);
			Assert.AreEqual("bob", items[0].UserId);
			Assert.AreEqual("second one", items[0].Summary);

			Assert.AreEqual("bob", items[1].UserId);
			Assert.AreEqual("first one", items[1].Summary);
		}

		[Test]
		public void GetTip_BeforeAnySyncing_EmptyString()
		{
			using (var repo = new EmptyRepositoryForTests())
			{
				Assert.IsNull(repo.Repo.GetRepository(_progress).GetTip());
			}
		}

		[Test]
		public void GetTip_AfterSyncing_GetTip()
		{
			using (var repo = new RepositoryWithFilesSetup("dontMatter"))
			{
				Assert.AreEqual("0", repo.Repo.GetRepository(_progress).GetTip().LocalRevisionNumber);
			}
		}
	}
}