/*
 * This code is derived from MyJavaLibrary (http://somelinktomycoollibrary)
 * 
 * If this is an open source Java library, include the proper license and copyright attributions here!
 */

using System.Collections.Generic;
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Document;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Sharpen;

namespace Lucene.Net.Index
{
	public class TestFieldsReader : LuceneTestCase
	{
		private static Directory dir;

		private static Lucene.Net.Document.Document testDoc;

		private static FieldInfos.Builder fieldInfos = null;

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.BeforeClass]
		public static void BeforeClass()
		{
			testDoc = new Lucene.Net.Document.Document();
			fieldInfos = new FieldInfos.Builder();
			DocHelper.SetupDoc(testDoc);
			foreach (IndexableField field in testDoc)
			{
				fieldInfos.AddOrUpdate(field.Name(), field.FieldType());
			}
			dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random())).SetMergePolicy(NewLogMergePolicy());
			conf.GetMergePolicy().SetNoCFSRatio(0.0);
			IndexWriter writer = new IndexWriter(dir, conf);
			writer.AddDocument(testDoc);
			writer.Close();
			TestFieldsReader.FaultyIndexInput.doFail = false;
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.AfterClass]
		public static void AfterClass()
		{
			dir.Close();
			dir = null;
			fieldInfos = null;
			testDoc = null;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Test()
		{
			NUnit.Framework.Assert.IsTrue(dir != null);
			NUnit.Framework.Assert.IsTrue(fieldInfos != null);
			IndexReader reader = DirectoryReader.Open(dir);
			Lucene.Net.Document.Document doc = reader.Document(0);
			NUnit.Framework.Assert.IsTrue(doc != null);
			NUnit.Framework.Assert.IsTrue(doc.GetField(DocHelper.TEXT_FIELD_1_KEY) != null);
			Field field = (Field)doc.GetField(DocHelper.TEXT_FIELD_2_KEY);
			NUnit.Framework.Assert.IsTrue(field != null);
			NUnit.Framework.Assert.IsTrue(field.FieldType().StoreTermVectors());
			NUnit.Framework.Assert.IsFalse(field.FieldType().OmitNorms());
			NUnit.Framework.Assert.IsTrue(field.FieldType().IndexOptions() == FieldInfo.IndexOptions
				.DOCS_AND_FREQS_AND_POSITIONS);
			field = (Field)doc.GetField(DocHelper.TEXT_FIELD_3_KEY);
			NUnit.Framework.Assert.IsTrue(field != null);
			NUnit.Framework.Assert.IsFalse(field.FieldType().StoreTermVectors());
			NUnit.Framework.Assert.IsTrue(field.FieldType().OmitNorms());
			NUnit.Framework.Assert.IsTrue(field.FieldType().IndexOptions() == FieldInfo.IndexOptions
				.DOCS_AND_FREQS_AND_POSITIONS);
			field = (Field)doc.GetField(DocHelper.NO_TF_KEY);
			NUnit.Framework.Assert.IsTrue(field != null);
			NUnit.Framework.Assert.IsFalse(field.FieldType().StoreTermVectors());
			NUnit.Framework.Assert.IsFalse(field.FieldType().OmitNorms());
			NUnit.Framework.Assert.IsTrue(field.FieldType().IndexOptions() == FieldInfo.IndexOptions
				.DOCS_ONLY);
			DocumentStoredFieldVisitor visitor = new DocumentStoredFieldVisitor(DocHelper.TEXT_FIELD_3_KEY
				);
			reader.Document(0, visitor);
			IList<IndexableField> fields = visitor.GetDocument().GetFields();
			NUnit.Framework.Assert.AreEqual(1, fields.Count);
			NUnit.Framework.Assert.AreEqual(DocHelper.TEXT_FIELD_3_KEY, fields[0].Name());
			reader.Close();
		}

		public class FaultyFSDirectory : BaseDirectory
		{
			internal Directory fsDir;

			public FaultyFSDirectory(FilePath dir)
			{
				fsDir = NewFSDirectory(dir);
				lockFactory = fsDir.GetLockFactory();
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override IndexInput OpenInput(string name, IOContext context)
			{
				return new TestFieldsReader.FaultyIndexInput(fsDir.OpenInput(name, context));
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override string[] ListAll()
			{
				return fsDir.ListAll();
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override bool FileExists(string name)
			{
				return fsDir.FileExists(name);
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override void DeleteFile(string name)
			{
				fsDir.DeleteFile(name);
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override long FileLength(string name)
			{
				return fsDir.FileLength(name);
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override IndexOutput CreateOutput(string name, IOContext context)
			{
				return fsDir.CreateOutput(name, context);
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override void Sync(ICollection<string> names)
			{
				fsDir.Sync(names);
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override void Close()
			{
				fsDir.Close();
			}
		}

		private class FaultyIndexInput : BufferedIndexInput
		{
			internal IndexInput delegate_;

			internal static bool doFail;

			internal int count;

			private FaultyIndexInput(IndexInput delegate_) : base("FaultyIndexInput(" + delegate_
				 + ")", BufferedIndexInput.BUFFER_SIZE)
			{
				this.delegate_ = delegate_;
			}

			/// <exception cref="System.IO.IOException"></exception>
			private void SimOutage()
			{
				if (doFail && count++ % 2 == 1)
				{
					throw new IOException("Simulated network outage");
				}
			}

			/// <exception cref="System.IO.IOException"></exception>
			protected override void ReadInternal(byte[] b, int offset, int length)
			{
				SimOutage();
				delegate_.Seek(GetFilePointer());
				delegate_.ReadBytes(b, offset, length);
			}

			/// <exception cref="System.IO.IOException"></exception>
			protected override void SeekInternal(long pos)
			{
			}

			public override long Length()
			{
				return delegate_.Length();
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override void Close()
			{
				delegate_.Close();
			}

			public override DataInput Clone()
			{
				TestFieldsReader.FaultyIndexInput i = new TestFieldsReader.FaultyIndexInput(((IndexInput
					)delegate_.Clone()));
				// seek the clone to our current position
				try
				{
					i.Seek(GetFilePointer());
				}
				catch (IOException)
				{
					throw new RuntimeException();
				}
				return i;
			}
		}

		// LUCENE-1262
		/// <exception cref="System.Exception"></exception>
		public virtual void TestExceptions()
		{
			FilePath indexDir = CreateTempDir("testfieldswriterexceptions");
			try
			{
				Directory dir = new TestFieldsReader.FaultyFSDirectory(indexDir);
				IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
					(Random())).SetOpenMode(IndexWriterConfig.OpenMode.CREATE);
				IndexWriter writer = new IndexWriter(dir, iwc);
				for (int i = 0; i < 2; i++)
				{
					writer.AddDocument(testDoc);
				}
				writer.ForceMerge(1);
				writer.Close();
				IndexReader reader = DirectoryReader.Open(dir);
				TestFieldsReader.FaultyIndexInput.doFail = true;
				bool exc = false;
				for (int i_1 = 0; i_1 < 2; i_1++)
				{
					try
					{
						reader.Document(i_1);
					}
					catch (IOException)
					{
						// expected
						exc = true;
					}
					try
					{
						reader.Document(i_1);
					}
					catch (IOException)
					{
						// expected
						exc = true;
					}
				}
				NUnit.Framework.Assert.IsTrue(exc);
				reader.Close();
				dir.Close();
			}
			finally
			{
				TestUtil.Rm(indexDir);
			}
		}
	}
}
