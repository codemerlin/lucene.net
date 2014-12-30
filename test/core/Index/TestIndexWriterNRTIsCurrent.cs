using System;
using System.IO;
using System.Threading;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Randomized.Generators;
using Lucene.Net.Store;
using Lucene.Net.TestFramework;
using Lucene.Net.Util;
using NUnit.Framework;
using Directory = Lucene.Net.Store.Directory;

namespace Lucene.Net.Test.Index
{
    [TestFixture]
	public class TestIndexWriterNRTIsCurrent : LuceneTestCase
	{
		public class ReaderHolder
		{
			internal volatile DirectoryReader reader;

			internal volatile bool stop = false;
		}

		[Test]
		public virtual void TestIsCurrentWithThreads()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			var holder = new ReaderHolder();
			var threads = new Thread[AtLeast(3)];
		    var readerThreads = new ReaderThread[AtLeast(3)];
			CountdownEvent latch = new CountdownEvent(1);
		    var writerThreadTarget = new WriterThread(holder, writer, AtLeast(500), Random(), latch);
		    var writerThread = new Thread(writerThreadTarget.Run);
			for (int i = 0; i < threads.Length; i++)
			{
			    var readerThread = new ReaderThread(holder, latch);
			    threads[i] = new Thread(readerThread.Run);
			    readerThreads[i] = readerThread;
				threads[i].Start();
			}
			writerThread.Start();
			writerThread.Join();
			bool failed = writerThreadTarget.failed != null;
			if (failed)
			{
                writerThreadTarget.failed.printStackTrace();
			}
			for (int i = 0; i < threads.Length; i++)
			{
				threads[i].Join();
				if (readerThreads[i].failed != null)
				{
                    readerThreads[i].failed.printStackTrace();
					failed = true;
				}
			}
			IsFalse(failed);
			writer.Dispose();
			dir.Dispose();
		}

		public class WriterThread
		{
			private readonly TestIndexWriterNRTIsCurrent.ReaderHolder holder;

			private readonly IndexWriter writer;

			private readonly int numOps;

			private bool countdown = true;

			private readonly CountdownEvent latch;

			internal Exception failed;

			internal WriterThread(TestIndexWriterNRTIsCurrent.ReaderHolder holder, IndexWriter
				 writer, int numOps, Random random, CountdownEvent latch) : base()
			{
				this.holder = holder;
				this.writer = writer;
				this.numOps = numOps;
				this.latch = latch;
			}

			public void Run()
			{
				DirectoryReader currentReader = null;
				Random random = LuceneTestCase.Random();
				try
				{
					Lucene.Net.Documents.Document doc = new Lucene.Net.Documents.Document
						();
					doc.Add(new TextField("id", "1", Field.Store.NO));
					writer.AddDocument(doc);
					holder.reader = currentReader = writer.GetReader(true);
					Term term = new Term("id");
					for (int i = 0; i < numOps && !holder.stop; i++)
					{
						var nextOp = random.NextDouble();
						if (nextOp < 0.3)
						{
							term.Set("id", new BytesRef("1"));
							writer.UpdateDocument(term, doc);
						}
						else
						{
							if (nextOp < 0.5)
							{
								writer.AddDocument(doc);
							}
							else
							{
								term.Set("id", new BytesRef("1"));
								writer.DeleteDocuments(term);
							}
						}
						if (holder.reader != currentReader)
						{
							holder.reader = currentReader;
							if (countdown)
							{
								countdown = false;
								latch.Signal();
							}
						}
						if (random.NextBoolean())
						{
							writer.Commit();
							DirectoryReader newReader = DirectoryReader.OpenIfChanged(currentReader);
							if (newReader != null)
							{
								currentReader.DecRef();
								currentReader = newReader;
							}
							if (currentReader.NumDocs == 0)
							{
								writer.AddDocument(doc);
							}
						}
					}
				}
				catch (Exception e)
				{
					failed = e;
				}
				finally
				{
					holder.reader = null;
					if (countdown)
					{
						latch.Signal();
					}
					if (currentReader != null)
					{
						try
						{
							currentReader.DecRef();
						}
						catch (IOException)
						{
						}
					}
				}
				if (VERBOSE)
				{
					System.Console.Out.WriteLine("writer stopped - forced by reader: " + holder.stop);
				}
			}
		}

		public sealed class ReaderThread
		{
			private readonly TestIndexWriterNRTIsCurrent.ReaderHolder holder;

			private readonly CountdownEvent latch;

			internal Exception failed;

			internal ReaderThread(TestIndexWriterNRTIsCurrent.ReaderHolder holder, CountdownEvent
				 latch) : base()
			{
				this.holder = holder;
				this.latch = latch;
			}

			public void Run()
			{
				try
				{
					latch.Wait();
				}
				catch (Exception e)
				{
					failed = e;
					return;
				}
				DirectoryReader reader;
				while ((reader = holder.reader) != null)
				{
					if (reader.TryIncRef())
					{
						try
						{
							bool current = reader.IsCurrent;
							if (VERBOSE)
							{
								System.Console.Out.WriteLine("Thread: " + Thread.CurrentThread + " Reader: "
									 + reader + " isCurrent:" + current);
							}
							IsFalse(current);
						}
						catch (Exception e)
						{
							if (VERBOSE)
							{
								System.Console.Out.WriteLine("FAILED Thread: " + Thread.CurrentThread +
									 " Reader: " + reader + " isCurrent: false");
							}
							failed = e;
							holder.stop = true;
							return;
						}
						finally
						{
							try
							{
								reader.DecRef();
							}
							catch (IOException e)
							{
								if (failed == null)
								{
									failed = e;
								}
								
							}
						}
					}
				}
			}
		}
	}
}
