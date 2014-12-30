using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.TestFramework;
using Lucene.Net.TestFramework.Util;
using NUnit.Framework;

namespace Lucene.Net.Test.Index
{
    [TestFixture]
	public class TestIndexWriterForceMerge : LuceneTestCase
	{
		[Test]
		public virtual void TestPartialMerge()
		{
			Directory dir = NewDirectory();
			Lucene.Net.Documents.Document doc = new Lucene.Net.Documents.Document
				();
			doc.Add(NewStringField("content", "aaa", Field.Store.NO));
			int incrMin = TEST_NIGHTLY ? 15 : 40;
			for (int numDocs = 10; numDocs < 500; numDocs += TestUtil.NextInt(Random(), incrMin
				, 5 * incrMin))
			{
				LogDocMergePolicy ldmp = new LogDocMergePolicy();
				ldmp.MinMergeDocs = (1);
				ldmp.MergeFactor = (5);
				IndexWriter writer = new IndexWriter(dir, ((IndexWriterConfig)NewIndexWriterConfig
					(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetOpenMode(IndexWriterConfig.OpenMode
					.CREATE).SetMaxBufferedDocs(2)).SetMergePolicy(ldmp));
				for (int j = 0; j < numDocs; j++)
				{
					writer.AddDocument(doc);
				}
				writer.Dispose();
				SegmentInfos sis = new SegmentInfos();
				sis.Read(dir);
				int segCount = sis.Count;
				ldmp = new LogDocMergePolicy();
				ldmp.MergeFactor = (5);
				writer = new IndexWriter(dir, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
					(Random())).SetMergePolicy(ldmp));
				writer.ForceMerge(3);
				writer.Dispose();
				sis = new SegmentInfos();
				sis.Read(dir);
				int optSegCount = sis.Count;
				if (segCount < 3)
				{
					AreEqual(segCount, optSegCount);
				}
				else
				{
					AreEqual(3, optSegCount);
				}
			}
			dir.Dispose();
		}

		[Test]
		public virtual void TestMaxNumSegments2()
		{
			Directory dir = NewDirectory();
			Lucene.Net.Documents.Document doc = new Lucene.Net.Documents.Document
				();
			doc.Add(NewStringField("content", "aaa", Field.Store.NO));
			LogDocMergePolicy ldmp = new LogDocMergePolicy();
			ldmp.MinMergeDocs = (1);
			ldmp.MergeFactor = (4);
			IndexWriter writer = new IndexWriter(dir, ((IndexWriterConfig)NewIndexWriterConfig
				(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetMaxBufferedDocs(2)).SetMergePolicy
				(ldmp).SetMergeScheduler(new ConcurrentMergeScheduler()));
			for (int iter = 0; iter < 10; iter++)
			{
				for (int i = 0; i < 19; i++)
				{
					writer.AddDocument(doc);
				}
				writer.Commit();
				writer.WaitForMerges();
				writer.Commit();
				SegmentInfos sis = new SegmentInfos();
				sis.Read(dir);
				int segCount = sis.Count;
				writer.ForceMerge(7);
				writer.Commit();
				writer.WaitForMerges();
				sis = new SegmentInfos();
				sis.Read(dir);
				int optSegCount = sis.Count;
				if (segCount < 7)
				{
					AreEqual(segCount, optSegCount);
				}
				else
				{
					AssertEquals("seg: " + segCount, 7, optSegCount);
				}
			}
			writer.Dispose();
			dir.Dispose();
		}

		/// <summary>
		/// Make sure forceMerge doesn't use any more than 1X
		/// starting index size as its temporary free space
		/// required.
		/// </summary>
		/// <remarks>
		/// Make sure forceMerge doesn't use any more than 1X
		/// starting index size as its temporary free space
		/// required.
		/// </remarks>
		[Test]
		public virtual void TestForceMergeTempSpaceUsage()
		{
			MockDirectoryWrapper dir = NewMockDirectory();
			IndexWriter writer = new IndexWriter(dir, ((IndexWriterConfig)NewIndexWriterConfig
				(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetMaxBufferedDocs(10)).SetMergePolicy
				(NewLogMergePolicy()));
			if (VERBOSE)
			{
				System.Console.Out.WriteLine("TEST: config1=" + writer.Config);
			}
			for (int j = 0; j < 500; j++)
			{
				TestIndexWriter.AddDocWithIndex(writer, j);
			}
			int termIndexInterval = writer.Config.TermIndexInterval;
			// force one extra segment w/ different doc store so
			// we see the doc stores get merged
			writer.Commit();
			TestIndexWriter.AddDocWithIndex(writer, 500);
			writer.Dispose();
			if (VERBOSE)
			{
				System.Console.Out.WriteLine("TEST: start disk usage");
			}
			long startDiskUsage = 0;
			string[] files = dir.ListAll();
			for (int i = 0; i < files.Length; i++)
			{
				startDiskUsage += dir.FileLength(files[i]);
				if (VERBOSE)
				{
					System.Console.Out.WriteLine(files[i] + ": " + dir.FileLength(files[i]));
				}
			}
			dir.ResetMaxUsedSizeInBytes();
			dir.SetTrackDiskUsage(true);
			// Import to use same term index interval else a
			// smaller one here could increase the disk usage and
			// cause a false failure:
			writer = new IndexWriter(dir, ((IndexWriterConfig)NewIndexWriterConfig(TEST_VERSION_CURRENT
				, new MockAnalyzer(Random())).SetOpenMode(IndexWriterConfig.OpenMode.APPEND).SetTermIndexInterval
				(termIndexInterval)).SetMergePolicy(NewLogMergePolicy()));
			writer.ForceMerge(1);
			writer.Dispose();
			long maxDiskUsage = dir.GetMaxUsedSizeInBytes();
			AssertTrue("forceMerge used too much temporary space: starting usage was "
				 + startDiskUsage + " bytes; max temp usage was " + maxDiskUsage + " but should have been "
				 + (4 * startDiskUsage) + " (= 4X starting usage)", maxDiskUsage <= 4 * startDiskUsage
				);
			dir.Dispose();
		}

		// Test calling forceMerge(1, false) whereby forceMerge is kicked
		// off but we don't wait for it to finish (but
		// writer.close()) does wait
		[Test]
		public virtual void TestBackgroundForceMerge()
		{
			Directory dir = NewDirectory();
			for (int pass = 0; pass < 2; pass++)
			{
				IndexWriter writer = new IndexWriter(dir, ((IndexWriterConfig)NewIndexWriterConfig
					(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetOpenMode(IndexWriterConfig.OpenMode
					.CREATE).SetMaxBufferedDocs(2)).SetMergePolicy(NewLogMergePolicy(51)));
				Lucene.Net.Documents.Document doc = new Lucene.Net.Documents.Document
					();
				doc.Add(NewStringField("field", "aaa", Field.Store.NO));
				for (int i = 0; i < 100; i++)
				{
					writer.AddDocument(doc);
				}
				writer.ForceMerge(1, false);
				if (0 == pass)
				{
					writer.Dispose();
					DirectoryReader reader = DirectoryReader.Open(dir);
					AreEqual(1, reader.Leaves.Count);
					reader.Dispose();
				}
				else
				{
					// Get another segment to flush so we can verify it is
					// NOT included in the merging
					writer.AddDocument(doc);
					writer.AddDocument(doc);
					writer.Dispose();
					DirectoryReader reader = DirectoryReader.Open(dir);
					IsTrue(reader.Leaves.Count > 1);
					reader.Dispose();
					SegmentInfos infos = new SegmentInfos();
					infos.Read(dir);
					AreEqual(2, infos.Count);
				}
			}
			dir.Dispose();
		}
	}
}
