/*
 * This code is derived from MyJavaLibrary (http://somelinktomycoollibrary)
 * 
 * If this is an open source Java library, include the proper license and copyright attributions here!
 */

using System;
using System.Collections.Generic;
using System.IO;
using Com.Carrotsearch.Randomizedtesting.Generators;
using Lucene.Net.Analysis;
using Lucene.Net.Codecs;
using Lucene.Net.Codecs.Asserting;
using Lucene.Net.Codecs.Lucene40;
using Lucene.Net.Codecs.Lucene41;
using Lucene.Net.Codecs.Lucene42;
using Lucene.Net.Codecs.Lucene45;
using Lucene.Net.Codecs.Lucene46;
using Lucene.Net.Document;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Sharpen;

namespace Lucene.Net.Index
{
	public class TestNumericDocValuesUpdates : LuceneTestCase
	{
		private Lucene.Net.Document.Document Doc(int id)
		{
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("id", "doc-" + id, Field.Store.NO));
			// make sure we don't set the doc's value to 0, to not confuse with a document that's missing values
			doc.Add(new NumericDocValuesField("val", id + 1));
			return doc;
		}

		/// <exception cref="System.IO.IOException"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdatesAreFlushed()
		{
			Directory dir = NewDirectory();
			IndexWriter writer = new IndexWriter(dir, ((IndexWriterConfig)NewIndexWriterConfig
				(TEST_VERSION_CURRENT, new MockAnalyzer(Random(), MockTokenizer.WHITESPACE, false
				)).SetRAMBufferSizeMB(0.00000001)));
			writer.AddDocument(Doc(0));
			// val=1
			writer.AddDocument(Doc(1));
			// val=2
			writer.AddDocument(Doc(3));
			// val=2
			writer.Commit();
			NUnit.Framework.Assert.AreEqual(1, writer.GetFlushDeletesCount());
			writer.UpdateNumericDocValue(new Term("id", "doc-0"), "val", 5L);
			NUnit.Framework.Assert.AreEqual(2, writer.GetFlushDeletesCount());
			writer.UpdateNumericDocValue(new Term("id", "doc-1"), "val", 6L);
			NUnit.Framework.Assert.AreEqual(3, writer.GetFlushDeletesCount());
			writer.UpdateNumericDocValue(new Term("id", "doc-2"), "val", 7L);
			NUnit.Framework.Assert.AreEqual(4, writer.GetFlushDeletesCount());
			writer.GetConfig().SetRAMBufferSizeMB(1000d);
			writer.UpdateNumericDocValue(new Term("id", "doc-2"), "val", 7L);
			NUnit.Framework.Assert.AreEqual(4, writer.GetFlushDeletesCount());
			writer.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestSimple()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			// make sure random config doesn't flush on us
			conf.SetMaxBufferedDocs(10);
			conf.SetRAMBufferSizeMB(IndexWriterConfig.DISABLE_AUTO_FLUSH);
			IndexWriter writer = new IndexWriter(dir, conf);
			writer.AddDocument(Doc(0));
			// val=1
			writer.AddDocument(Doc(1));
			// val=2
			if (Random().NextBoolean())
			{
				// randomly commit before the update is sent
				writer.Commit();
			}
			writer.UpdateNumericDocValue(new Term("id", "doc-0"), "val", 2L);
			// doc=0, exp=2
			DirectoryReader reader;
			if (Random().NextBoolean())
			{
				// not NRT
				writer.Close();
				reader = DirectoryReader.Open(dir);
			}
			else
			{
				// NRT
				reader = DirectoryReader.Open(writer, true);
				writer.Close();
			}
			NUnit.Framework.Assert.AreEqual(1, reader.Leaves().Count);
			AtomicReader r = ((AtomicReader)reader.Leaves()[0].Reader());
			NumericDocValues ndv = r.GetNumericDocValues("val");
			NUnit.Framework.Assert.AreEqual(2, ndv.Get(0));
			NUnit.Framework.Assert.AreEqual(2, ndv.Get(1));
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateFewSegments()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			conf.SetMaxBufferedDocs(2);
			// generate few segments
			conf.SetMergePolicy(NoMergePolicy.COMPOUND_FILES);
			// prevent merges for this test
			IndexWriter writer = new IndexWriter(dir, conf);
			int numDocs = 10;
			long[] expectedValues = new long[numDocs];
			for (int i = 0; i < numDocs; i++)
			{
				writer.AddDocument(Doc(i));
				expectedValues[i] = i + 1;
			}
			writer.Commit();
			// update few docs
			for (int i_1 = 0; i_1 < numDocs; i_1++)
			{
				if (Random().NextDouble() < 0.4)
				{
					long value = (i_1 + 1) * 2;
					writer.UpdateNumericDocValue(new Term("id", "doc-" + i_1), "val", value);
					expectedValues[i_1] = value;
				}
			}
			DirectoryReader reader;
			if (Random().NextBoolean())
			{
				// not NRT
				writer.Close();
				reader = DirectoryReader.Open(dir);
			}
			else
			{
				// NRT
				reader = DirectoryReader.Open(writer, true);
				writer.Close();
			}
			foreach (AtomicReaderContext context in reader.Leaves())
			{
				AtomicReader r = ((AtomicReader)context.Reader());
				NumericDocValues ndv = r.GetNumericDocValues("val");
				NUnit.Framework.Assert.IsNotNull(ndv);
				for (int i_2 = 0; i_2 < r.MaxDoc(); i_2++)
				{
					long expected = expectedValues[i_2 + context.docBase];
					long actual = ndv.Get(i_2);
					NUnit.Framework.Assert.AreEqual(expected, actual);
				}
			}
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestReopen()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			writer.AddDocument(Doc(0));
			writer.AddDocument(Doc(1));
			bool isNRT = Random().NextBoolean();
			DirectoryReader reader1;
			if (isNRT)
			{
				reader1 = DirectoryReader.Open(writer, true);
			}
			else
			{
				writer.Commit();
				reader1 = DirectoryReader.Open(dir);
			}
			// update doc
			writer.UpdateNumericDocValue(new Term("id", "doc-0"), "val", 10L);
			// update doc-0's value to 10
			if (!isNRT)
			{
				writer.Commit();
			}
			// reopen reader and 
			//HM:revisit 
			//assert only it sees the update
			DirectoryReader reader2 = DirectoryReader.OpenIfChanged(reader1);
			NUnit.Framework.Assert.IsNotNull(reader2);
			NUnit.Framework.Assert.IsTrue(reader1 != reader2);
			NUnit.Framework.Assert.AreEqual(1, ((AtomicReader)reader1.Leaves()[0].Reader()).GetNumericDocValues
				("val").Get(0));
			NUnit.Framework.Assert.AreEqual(10, ((AtomicReader)reader2.Leaves()[0].Reader()).
				GetNumericDocValues("val").Get(0));
			IOUtils.Close(writer, reader1, reader2, dir);
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdatesAndDeletes()
		{
			// create an index with a segment with only deletes, a segment with both
			// deletes and updates and a segment with only updates
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			conf.SetMaxBufferedDocs(10);
			// control segment flushing
			conf.SetMergePolicy(NoMergePolicy.COMPOUND_FILES);
			// prevent merges for this test
			IndexWriter writer = new IndexWriter(dir, conf);
			for (int i = 0; i < 6; i++)
			{
				writer.AddDocument(Doc(i));
				if (i % 2 == 1)
				{
					writer.Commit();
				}
			}
			// create 2-docs segments
			// delete doc-1 and doc-2
			writer.DeleteDocuments(new Term("id", "doc-1"), new Term("id", "doc-2"));
			// 1st and 2nd segments
			// update docs 3 and 5
			writer.UpdateNumericDocValue(new Term("id", "doc-3"), "val", 17L);
			writer.UpdateNumericDocValue(new Term("id", "doc-5"), "val", 17L);
			DirectoryReader reader;
			if (Random().NextBoolean())
			{
				// not NRT
				writer.Close();
				reader = DirectoryReader.Open(dir);
			}
			else
			{
				// NRT
				reader = DirectoryReader.Open(writer, true);
				writer.Close();
			}
			AtomicReader slow = SlowCompositeReaderWrapper.Wrap(reader);
			Bits liveDocs = slow.GetLiveDocs();
			bool[] expectedLiveDocs = new bool[] { true, false, false, true, true, true };
			for (int i_1 = 0; i_1 < expectedLiveDocs.Length; i_1++)
			{
				NUnit.Framework.Assert.AreEqual(expectedLiveDocs[i_1], liveDocs.Get(i_1));
			}
			long[] expectedValues = new long[] { 1, 2, 3, 17, 5, 17 };
			NumericDocValues ndv = slow.GetNumericDocValues("val");
			for (int i_2 = 0; i_2 < expectedValues.Length; i_2++)
			{
				NUnit.Framework.Assert.AreEqual(expectedValues[i_2], ndv.Get(i_2));
			}
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdatesWithDeletes()
		{
			// update and delete different documents in the same commit session
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			conf.SetMaxBufferedDocs(10);
			// control segment flushing
			IndexWriter writer = new IndexWriter(dir, conf);
			writer.AddDocument(Doc(0));
			writer.AddDocument(Doc(1));
			if (Random().NextBoolean())
			{
				writer.Commit();
			}
			writer.DeleteDocuments(new Term("id", "doc-0"));
			writer.UpdateNumericDocValue(new Term("id", "doc-1"), "val", 17L);
			DirectoryReader reader;
			if (Random().NextBoolean())
			{
				// not NRT
				writer.Close();
				reader = DirectoryReader.Open(dir);
			}
			else
			{
				// NRT
				reader = DirectoryReader.Open(writer, true);
				writer.Close();
			}
			AtomicReader r = ((AtomicReader)reader.Leaves()[0].Reader());
			NUnit.Framework.Assert.IsFalse(r.GetLiveDocs().Get(0));
			NUnit.Framework.Assert.AreEqual(17, r.GetNumericDocValues("val").Get(1));
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateAndDeleteSameDocument()
		{
			// update and delete same document in same commit session
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			conf.SetMaxBufferedDocs(10);
			// control segment flushing
			IndexWriter writer = new IndexWriter(dir, conf);
			writer.AddDocument(Doc(0));
			writer.AddDocument(Doc(1));
			if (Random().NextBoolean())
			{
				writer.Commit();
			}
			writer.DeleteDocuments(new Term("id", "doc-0"));
			writer.UpdateNumericDocValue(new Term("id", "doc-0"), "val", 17L);
			DirectoryReader reader;
			if (Random().NextBoolean())
			{
				// not NRT
				writer.Close();
				reader = DirectoryReader.Open(dir);
			}
			else
			{
				// NRT
				reader = DirectoryReader.Open(writer, true);
				writer.Close();
			}
			AtomicReader r = ((AtomicReader)reader.Leaves()[0].Reader());
			NUnit.Framework.Assert.IsFalse(r.GetLiveDocs().Get(0));
			NUnit.Framework.Assert.AreEqual(1, r.GetNumericDocValues("val").Get(0));
			// deletes are currently applied first
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestMultipleDocValuesTypes()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			conf.SetMaxBufferedDocs(10);
			// prevent merges
			IndexWriter writer = new IndexWriter(dir, conf);
			for (int i = 0; i < 4; i++)
			{
				Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
					();
				doc.Add(new StringField("dvUpdateKey", "dv", Field.Store.NO));
				doc.Add(new NumericDocValuesField("ndv", i));
				doc.Add(new BinaryDocValuesField("bdv", new BytesRef(Sharpen.Extensions.ToString(
					i))));
				doc.Add(new SortedDocValuesField("sdv", new BytesRef(Sharpen.Extensions.ToString(
					i))));
				doc.Add(new SortedSetDocValuesField("ssdv", new BytesRef(Sharpen.Extensions.ToString
					(i))));
				doc.Add(new SortedSetDocValuesField("ssdv", new BytesRef(Sharpen.Extensions.ToString
					(i * 2))));
				writer.AddDocument(doc);
			}
			writer.Commit();
			// update all docs' ndv field
			writer.UpdateNumericDocValue(new Term("dvUpdateKey", "dv"), "ndv", 17L);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			AtomicReader r = ((AtomicReader)reader.Leaves()[0].Reader());
			NumericDocValues ndv = r.GetNumericDocValues("ndv");
			BinaryDocValues bdv = r.GetBinaryDocValues("bdv");
			SortedDocValues sdv = r.GetSortedDocValues("sdv");
			SortedSetDocValues ssdv = r.GetSortedSetDocValues("ssdv");
			BytesRef scratch = new BytesRef();
			for (int i_1 = 0; i_1 < r.MaxDoc(); i_1++)
			{
				NUnit.Framework.Assert.AreEqual(17, ndv.Get(i_1));
				bdv.Get(i_1, scratch);
				NUnit.Framework.Assert.AreEqual(new BytesRef(Sharpen.Extensions.ToString(i_1)), scratch
					);
				sdv.Get(i_1, scratch);
				NUnit.Framework.Assert.AreEqual(new BytesRef(Sharpen.Extensions.ToString(i_1)), scratch
					);
				ssdv.SetDocument(i_1);
				long ord = ssdv.NextOrd();
				ssdv.LookupOrd(ord, scratch);
				NUnit.Framework.Assert.AreEqual(i_1, System.Convert.ToInt32(scratch.Utf8ToString(
					)));
				if (i_1 != 0)
				{
					ord = ssdv.NextOrd();
					ssdv.LookupOrd(ord, scratch);
					NUnit.Framework.Assert.AreEqual(i_1 * 2, System.Convert.ToInt32(scratch.Utf8ToString
						()));
				}
				NUnit.Framework.Assert.AreEqual(SortedSetDocValues.NO_MORE_ORDS, ssdv.NextOrd());
			}
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestMultipleNumericDocValues()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			conf.SetMaxBufferedDocs(10);
			// prevent merges
			IndexWriter writer = new IndexWriter(dir, conf);
			for (int i = 0; i < 2; i++)
			{
				Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
					();
				doc.Add(new StringField("dvUpdateKey", "dv", Field.Store.NO));
				doc.Add(new NumericDocValuesField("ndv1", i));
				doc.Add(new NumericDocValuesField("ndv2", i));
				writer.AddDocument(doc);
			}
			writer.Commit();
			// update all docs' ndv1 field
			writer.UpdateNumericDocValue(new Term("dvUpdateKey", "dv"), "ndv1", 17L);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			AtomicReader r = ((AtomicReader)reader.Leaves()[0].Reader());
			NumericDocValues ndv1 = r.GetNumericDocValues("ndv1");
			NumericDocValues ndv2 = r.GetNumericDocValues("ndv2");
			for (int i_1 = 0; i_1 < r.MaxDoc(); i_1++)
			{
				NUnit.Framework.Assert.AreEqual(17, ndv1.Get(i_1));
				NUnit.Framework.Assert.AreEqual(i_1, ndv2.Get(i_1));
			}
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestDocumentWithNoValue()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			for (int i = 0; i < 2; i++)
			{
				Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
					();
				doc.Add(new StringField("dvUpdateKey", "dv", Field.Store.NO));
				if (i == 0)
				{
					// index only one document with value
					doc.Add(new NumericDocValuesField("ndv", 5));
				}
				writer.AddDocument(doc);
			}
			writer.Commit();
			// update all docs' ndv field
			writer.UpdateNumericDocValue(new Term("dvUpdateKey", "dv"), "ndv", 17L);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			AtomicReader r = ((AtomicReader)reader.Leaves()[0].Reader());
			NumericDocValues ndv = r.GetNumericDocValues("ndv");
			for (int i_1 = 0; i_1 < r.MaxDoc(); i_1++)
			{
				NUnit.Framework.Assert.AreEqual(17, ndv.Get(i_1));
			}
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUnsetValue()
		{
			AssumeTrue("codec does not support docsWithField", DefaultCodecSupportsDocsWithField
				());
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			for (int i = 0; i < 2; i++)
			{
				Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
					();
				doc.Add(new StringField("id", "doc" + i, Field.Store.NO));
				doc.Add(new NumericDocValuesField("ndv", 5));
				writer.AddDocument(doc);
			}
			writer.Commit();
			// unset the value of 'doc0'
			writer.UpdateNumericDocValue(new Term("id", "doc0"), "ndv", null);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			AtomicReader r = ((AtomicReader)reader.Leaves()[0].Reader());
			NumericDocValues ndv = r.GetNumericDocValues("ndv");
			for (int i_1 = 0; i_1 < r.MaxDoc(); i_1++)
			{
				if (i_1 == 0)
				{
					NUnit.Framework.Assert.AreEqual(0, ndv.Get(i_1));
				}
				else
				{
					NUnit.Framework.Assert.AreEqual(5, ndv.Get(i_1));
				}
			}
			Bits docsWithField = r.GetDocsWithField("ndv");
			NUnit.Framework.Assert.IsFalse(docsWithField.Get(0));
			NUnit.Framework.Assert.IsTrue(docsWithField.Get(1));
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestUnsetAllValues()
		{
			AssumeTrue("codec does not support docsWithField", DefaultCodecSupportsDocsWithField
				());
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			for (int i = 0; i < 2; i++)
			{
				Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
					();
				doc.Add(new StringField("id", "doc", Field.Store.NO));
				doc.Add(new NumericDocValuesField("ndv", 5));
				writer.AddDocument(doc);
			}
			writer.Commit();
			// unset the value of 'doc'
			writer.UpdateNumericDocValue(new Term("id", "doc"), "ndv", null);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			AtomicReader r = ((AtomicReader)reader.Leaves()[0].Reader());
			NumericDocValues ndv = r.GetNumericDocValues("ndv");
			for (int i_1 = 0; i_1 < r.MaxDoc(); i_1++)
			{
				NUnit.Framework.Assert.AreEqual(0, ndv.Get(i_1));
			}
			Bits docsWithField = r.GetDocsWithField("ndv");
			NUnit.Framework.Assert.IsFalse(docsWithField.Get(0));
			NUnit.Framework.Assert.IsFalse(docsWithField.Get(1));
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateNonNumericDocValuesField()
		{
			// we don't support adding new fields or updating existing non-numeric-dv
			// fields through numeric updates
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("key", "doc", Field.Store.NO));
			doc.Add(new StringField("foo", "bar", Field.Store.NO));
			writer.AddDocument(doc);
			// flushed document
			writer.Commit();
			writer.AddDocument(doc);
			// in-memory document
			try
			{
				writer.UpdateNumericDocValue(new Term("key", "doc"), "ndv", 17L);
				NUnit.Framework.Assert.Fail("should not have allowed creating new fields through update"
					);
			}
			catch (ArgumentException)
			{
			}
			// ok
			try
			{
				writer.UpdateNumericDocValue(new Term("key", "doc"), "foo", 17L);
				NUnit.Framework.Assert.Fail("should not have allowed updating an existing field to numeric-dv"
					);
			}
			catch (ArgumentException)
			{
			}
			// ok
			writer.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestDifferentDVFormatPerField()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			conf.SetCodec(new _Lucene46Codec_554());
			IndexWriter writer = new IndexWriter(dir, conf);
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("key", "doc", Field.Store.NO));
			doc.Add(new NumericDocValuesField("ndv", 5));
			doc.Add(new SortedDocValuesField("sorted", new BytesRef("value")));
			writer.AddDocument(doc);
			// flushed document
			writer.Commit();
			writer.AddDocument(doc);
			// in-memory document
			writer.UpdateNumericDocValue(new Term("key", "doc"), "ndv", 17L);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			AtomicReader r = SlowCompositeReaderWrapper.Wrap(reader);
			NumericDocValues ndv = r.GetNumericDocValues("ndv");
			SortedDocValues sdv = r.GetSortedDocValues("sorted");
			BytesRef scratch = new BytesRef();
			for (int i = 0; i < r.MaxDoc(); i++)
			{
				NUnit.Framework.Assert.AreEqual(17, ndv.Get(i));
				sdv.Get(i, scratch);
				NUnit.Framework.Assert.AreEqual(new BytesRef("value"), scratch);
			}
			reader.Close();
			dir.Close();
		}

		private sealed class _Lucene46Codec_554 : Lucene46Codec
		{
			public _Lucene46Codec_554()
			{
			}

			public override DocValuesFormat GetDocValuesFormatForField(string field)
			{
				return new Lucene45DocValuesFormat();
			}
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateSameDocMultipleTimes()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("key", "doc", Field.Store.NO));
			doc.Add(new NumericDocValuesField("ndv", 5));
			writer.AddDocument(doc);
			// flushed document
			writer.Commit();
			writer.AddDocument(doc);
			// in-memory document
			writer.UpdateNumericDocValue(new Term("key", "doc"), "ndv", 17L);
			// update existing field
			writer.UpdateNumericDocValue(new Term("key", "doc"), "ndv", 3L);
			// update existing field 2nd time in this commit
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			AtomicReader r = SlowCompositeReaderWrapper.Wrap(reader);
			NumericDocValues ndv = r.GetNumericDocValues("ndv");
			for (int i = 0; i < r.MaxDoc(); i++)
			{
				NUnit.Framework.Assert.AreEqual(3, ndv.Get(i));
			}
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestSegmentMerges()
		{
			Directory dir = NewDirectory();
			Random random = Random();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(random));
			IndexWriter writer = new IndexWriter(dir, conf.Clone());
			int docid = 0;
			int numRounds = AtLeast(10);
			for (int rnd = 0; rnd < numRounds; rnd++)
			{
				Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
					();
				doc.Add(new StringField("key", "doc", Field.Store.NO));
				doc.Add(new NumericDocValuesField("ndv", -1));
				int numDocs = AtLeast(30);
				for (int i = 0; i < numDocs; i++)
				{
					doc.RemoveField("id");
					doc.Add(new StringField("id", Sharpen.Extensions.ToString(docid++), Field.Store.NO
						));
					writer.AddDocument(doc);
				}
				long value = rnd + 1;
				writer.UpdateNumericDocValue(new Term("key", "doc"), "ndv", value);
				if (random.NextDouble() < 0.2)
				{
					// randomly delete some docs
					writer.DeleteDocuments(new Term("id", Sharpen.Extensions.ToString(random.Next(docid
						))));
				}
				// randomly commit or reopen-IW (or nothing), before forceMerge
				if (random.NextDouble() < 0.4)
				{
					writer.Commit();
				}
				else
				{
					if (random.NextDouble() < 0.1)
					{
						writer.Close();
						writer = new IndexWriter(dir, conf.Clone());
					}
				}
				// add another document with the current value, to be sure forceMerge has
				// something to merge (for instance, it could be that CMS finished merging
				// all segments down to 1 before the delete was applied, so when
				// forceMerge is called, the index will be with one segment and deletes
				// and some MPs might now merge it, thereby invalidating test's
				// assumption that the reader has no deletes).
				doc = new Lucene.Net.Document.Document();
				doc.Add(new StringField("id", Sharpen.Extensions.ToString(docid++), Field.Store.NO
					));
				doc.Add(new StringField("key", "doc", Field.Store.NO));
				doc.Add(new NumericDocValuesField("ndv", value));
				writer.AddDocument(doc);
				writer.ForceMerge(1, true);
				DirectoryReader reader;
				if (random.NextBoolean())
				{
					writer.Commit();
					reader = DirectoryReader.Open(dir);
				}
				else
				{
					reader = DirectoryReader.Open(writer, true);
				}
				NUnit.Framework.Assert.AreEqual(1, reader.Leaves().Count);
				AtomicReader r = ((AtomicReader)reader.Leaves()[0].Reader());
				NUnit.Framework.Assert.IsNull("index should have no deletes after forceMerge", r.
					GetLiveDocs());
				NumericDocValues ndv = r.GetNumericDocValues("ndv");
				NUnit.Framework.Assert.IsNotNull(ndv);
				for (int i_1 = 0; i_1 < r.MaxDoc(); i_1++)
				{
					NUnit.Framework.Assert.AreEqual(value, ndv.Get(i_1));
				}
				reader.Close();
			}
			writer.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateDocumentByMultipleTerms()
		{
			// make sure the order of updates is respected, even when multiple terms affect same document
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("k1", "v1", Field.Store.NO));
			doc.Add(new StringField("k2", "v2", Field.Store.NO));
			doc.Add(new NumericDocValuesField("ndv", 5));
			writer.AddDocument(doc);
			// flushed document
			writer.Commit();
			writer.AddDocument(doc);
			// in-memory document
			writer.UpdateNumericDocValue(new Term("k1", "v1"), "ndv", 17L);
			writer.UpdateNumericDocValue(new Term("k2", "v2"), "ndv", 3L);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			AtomicReader r = SlowCompositeReaderWrapper.Wrap(reader);
			NumericDocValues ndv = r.GetNumericDocValues("ndv");
			for (int i = 0; i < r.MaxDoc(); i++)
			{
				NUnit.Framework.Assert.AreEqual(3, ndv.Get(i));
			}
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestManyReopensAndFields()
		{
			Directory dir = NewDirectory();
			Random random = Random();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(random));
			LogMergePolicy lmp = NewLogMergePolicy();
			lmp.SetMergeFactor(3);
			// merge often
			conf.SetMergePolicy(lmp);
			IndexWriter writer = new IndexWriter(dir, conf);
			bool isNRT = random.NextBoolean();
			DirectoryReader reader;
			if (isNRT)
			{
				reader = DirectoryReader.Open(writer, true);
			}
			else
			{
				writer.Commit();
				reader = DirectoryReader.Open(dir);
			}
			int numFields = random.Next(4) + 3;
			// 3-7
			long[] fieldValues = new long[numFields];
			bool[] fieldHasValue = new bool[numFields];
			Arrays.Fill(fieldHasValue, true);
			for (int i = 0; i < fieldValues.Length; i++)
			{
				fieldValues[i] = 1;
			}
			int numRounds = AtLeast(15);
			int docID = 0;
			for (int i_1 = 0; i_1 < numRounds; i_1++)
			{
				int numDocs = AtLeast(5);
				//      System.out.println("[" + Thread.currentThread().getName() + "]: round=" + i + ", numDocs=" + numDocs);
				for (int j = 0; j < numDocs; j++)
				{
					Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
						();
					doc.Add(new StringField("id", "doc-" + docID, Field.Store.NO));
					doc.Add(new StringField("key", "all", Field.Store.NO));
					// update key
					// add all fields with their current value
					for (int f = 0; f < fieldValues.Length; f++)
					{
						doc.Add(new NumericDocValuesField("f" + f, fieldValues[f]));
					}
					writer.AddDocument(doc);
					++docID;
				}
				// if field's value was unset before, unset it from all new added documents too
				for (int field = 0; field < fieldHasValue.Length; field++)
				{
					if (!fieldHasValue[field])
					{
						writer.UpdateNumericDocValue(new Term("key", "all"), "f" + field, null);
					}
				}
				int fieldIdx = random.Next(fieldValues.Length);
				string updateField = "f" + fieldIdx;
				if (random.NextBoolean())
				{
					//        System.out.println("[" + Thread.currentThread().getName() + "]: unset field '" + updateField + "'");
					fieldHasValue[fieldIdx] = false;
					writer.UpdateNumericDocValue(new Term("key", "all"), updateField, null);
				}
				else
				{
					fieldHasValue[fieldIdx] = true;
					writer.UpdateNumericDocValue(new Term("key", "all"), updateField, ++fieldValues[fieldIdx
						]);
				}
				//        System.out.println("[" + Thread.currentThread().getName() + "]: updated field '" + updateField + "' to value " + fieldValues[fieldIdx]);
				if (random.NextDouble() < 0.2)
				{
					int deleteDoc = random.Next(docID);
					// might also delete an already deleted document, ok!
					writer.DeleteDocuments(new Term("id", "doc-" + deleteDoc));
				}
				//        System.out.println("[" + Thread.currentThread().getName() + "]: deleted document: doc-" + deleteDoc);
				// verify reader
				if (!isNRT)
				{
					writer.Commit();
				}
				//      System.out.println("[" + Thread.currentThread().getName() + "]: reopen reader: " + reader);
				DirectoryReader newReader = DirectoryReader.OpenIfChanged(reader);
				NUnit.Framework.Assert.IsNotNull(newReader);
				reader.Close();
				reader = newReader;
				//      System.out.println("[" + Thread.currentThread().getName() + "]: reopened reader: " + reader);
				NUnit.Framework.Assert.IsTrue(reader.NumDocs() > 0);
				// we delete at most one document per round
				foreach (AtomicReaderContext context in reader.Leaves())
				{
					AtomicReader r = ((AtomicReader)context.Reader());
					//        System.out.println(((SegmentReader) r).getSegmentName());
					Bits liveDocs = r.GetLiveDocs();
					for (int field_1 = 0; field_1 < fieldValues.Length; field_1++)
					{
						string f = "f" + field_1;
						NumericDocValues ndv = r.GetNumericDocValues(f);
						Bits docsWithField = r.GetDocsWithField(f);
						NUnit.Framework.Assert.IsNotNull(ndv);
						int maxDoc = r.MaxDoc();
						for (int doc = 0; doc < maxDoc; doc++)
						{
							if (liveDocs == null || liveDocs.Get(doc))
							{
								//              System.out.println("doc=" + (doc + context.docBase) + " f='" + f + "' vslue=" + ndv.get(doc));
								if (fieldHasValue[field_1])
								{
									NUnit.Framework.Assert.IsTrue(docsWithField.Get(doc));
									NUnit.Framework.Assert.AreEqual("invalid value for doc=" + doc + ", field=" + f +
										 ", reader=" + r, fieldValues[field_1], ndv.Get(doc));
								}
								else
								{
									NUnit.Framework.Assert.IsFalse(docsWithField.Get(doc));
								}
							}
						}
					}
				}
			}
			//      System.out.println();
			IOUtils.Close(writer, reader, dir);
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateSegmentWithNoDocValues()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			// prevent merges, otherwise by the time updates are applied
			// (writer.close()), the segments might have merged and that update becomes
			// legit.
			conf.SetMergePolicy(NoMergePolicy.COMPOUND_FILES);
			IndexWriter writer = new IndexWriter(dir, conf);
			// first segment with NDV
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("id", "doc0", Field.Store.NO));
			doc.Add(new NumericDocValuesField("ndv", 3));
			writer.AddDocument(doc);
			doc = new Lucene.Net.Document.Document();
			doc.Add(new StringField("id", "doc4", Field.Store.NO));
			// document without 'ndv' field
			writer.AddDocument(doc);
			writer.Commit();
			// second segment with no NDV
			doc = new Lucene.Net.Document.Document();
			doc.Add(new StringField("id", "doc1", Field.Store.NO));
			writer.AddDocument(doc);
			doc = new Lucene.Net.Document.Document();
			doc.Add(new StringField("id", "doc2", Field.Store.NO));
			// document that isn't updated
			writer.AddDocument(doc);
			writer.Commit();
			// update document in the first segment - should not affect docsWithField of
			// the document without NDV field
			writer.UpdateNumericDocValue(new Term("id", "doc0"), "ndv", 5L);
			// update document in the second segment - field should be added and we should
			// be able to handle the other document correctly (e.g. no NPE)
			writer.UpdateNumericDocValue(new Term("id", "doc1"), "ndv", 5L);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			foreach (AtomicReaderContext context in reader.Leaves())
			{
				AtomicReader r = ((AtomicReader)context.Reader());
				NumericDocValues ndv = r.GetNumericDocValues("ndv");
				Bits docsWithField = r.GetDocsWithField("ndv");
				NUnit.Framework.Assert.IsNotNull(docsWithField);
				NUnit.Framework.Assert.IsTrue(docsWithField.Get(0));
				NUnit.Framework.Assert.AreEqual(5L, ndv.Get(0));
				NUnit.Framework.Assert.IsFalse(docsWithField.Get(1));
				NUnit.Framework.Assert.AreEqual(0L, ndv.Get(1));
			}
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateSegmentWithPostingButNoDocValues()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			// prevent merges, otherwise by the time updates are applied
			// (writer.close()), the segments might have merged and that update becomes
			// legit.
			conf.SetMergePolicy(NoMergePolicy.COMPOUND_FILES);
			IndexWriter writer = new IndexWriter(dir, conf);
			// first segment with NDV
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("id", "doc0", Field.Store.NO));
			doc.Add(new StringField("ndv", "mock-value", Field.Store.NO));
			doc.Add(new NumericDocValuesField("ndv", 5));
			writer.AddDocument(doc);
			writer.Commit();
			// second segment with no NDV
			doc = new Lucene.Net.Document.Document();
			doc.Add(new StringField("id", "doc1", Field.Store.NO));
			doc.Add(new StringField("ndv", "mock-value", Field.Store.NO));
			writer.AddDocument(doc);
			writer.Commit();
			// update document in the second segment
			writer.UpdateNumericDocValue(new Term("id", "doc1"), "ndv", 5L);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			foreach (AtomicReaderContext context in reader.Leaves())
			{
				AtomicReader r = ((AtomicReader)context.Reader());
				NumericDocValues ndv = r.GetNumericDocValues("ndv");
				for (int i = 0; i < r.MaxDoc(); i++)
				{
					NUnit.Framework.Assert.AreEqual(5L, ndv.Get(i));
				}
			}
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateNumericDVFieldWithSameNameAsPostingField()
		{
			// this used to fail because FieldInfos.Builder neglected to update
			// globalFieldMaps.docValueTypes map
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("f", "mock-value", Field.Store.NO));
			doc.Add(new NumericDocValuesField("f", 5));
			writer.AddDocument(doc);
			writer.Commit();
			writer.UpdateNumericDocValue(new Term("f", "mock-value"), "f", 17L);
			writer.Close();
			DirectoryReader r = DirectoryReader.Open(dir);
			NumericDocValues ndv = ((AtomicReader)r.Leaves()[0].Reader()).GetNumericDocValues
				("f");
			NUnit.Framework.Assert.AreEqual(17, ndv.Get(0));
			r.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateOldSegments()
		{
			Codec[] oldCodecs = new Codec[] { new Lucene40RWCodec(), new Lucene41RWCodec(), new 
				Lucene42RWCodec(), new Lucene45RWCodec() };
			Directory dir = NewDirectory();
			bool oldValue = OLD_FORMAT_IMPERSONATION_IS_ACTIVE;
			// create a segment with an old Codec
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			conf.SetCodec(oldCodecs[Random().Next(oldCodecs.Length)]);
			OLD_FORMAT_IMPERSONATION_IS_ACTIVE = true;
			IndexWriter writer = new IndexWriter(dir, conf);
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("id", "doc", Field.Store.NO));
			doc.Add(new NumericDocValuesField("f", 5));
			writer.AddDocument(doc);
			writer.Close();
			conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random()));
			writer = new IndexWriter(dir, conf);
			writer.UpdateNumericDocValue(new Term("id", "doc"), "f", 4L);
			OLD_FORMAT_IMPERSONATION_IS_ACTIVE = false;
			try
			{
				writer.Close();
				NUnit.Framework.Assert.Fail("should not have succeeded to update a segment written with an old Codec"
					);
			}
			catch (NotSupportedException)
			{
				writer.Rollback();
			}
			finally
			{
				OLD_FORMAT_IMPERSONATION_IS_ACTIVE = oldValue;
			}
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestStressMultiThreading()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			// create index
			int numThreads = TestUtil.NextInt(Random(), 3, 6);
			int numDocs = AtLeast(2000);
			for (int i = 0; i < numDocs; i++)
			{
				Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
					();
				doc.Add(new StringField("id", "doc" + i, Field.Store.NO));
				double group = Random().NextDouble();
				string g;
				if (group < 0.1)
				{
					g = "g0";
				}
				else
				{
					if (group < 0.5)
					{
						g = "g1";
					}
					else
					{
						if (group < 0.8)
						{
							g = "g2";
						}
						else
						{
							g = "g3";
						}
					}
				}
				doc.Add(new StringField("updKey", g, Field.Store.NO));
				for (int j = 0; j < numThreads; j++)
				{
					long value = Random().Next();
					doc.Add(new NumericDocValuesField("f" + j, value));
					doc.Add(new NumericDocValuesField("cf" + j, value * 2));
				}
				// control, always updated to f * 2
				writer.AddDocument(doc);
			}
			CountDownLatch done = new CountDownLatch(numThreads);
			AtomicInteger numUpdates = new AtomicInteger(AtLeast(100));
			// same thread updates a field as well as reopens
			Sharpen.Thread[] threads = new Sharpen.Thread[numThreads];
			for (int i_1 = 0; i_1 < threads.Length; i_1++)
			{
				string f = "f" + i_1;
				string cf = "cf" + i_1;
				threads[i_1] = new _Thread_1014(numUpdates, writer, f, cf, numDocs, done, "UpdateThread-"
					 + i_1);
			}
			//              System.out.println("[" + Thread.currentThread().getName() + "] numUpdates=" + numUpdates + " updateTerm=" + t);
			// sometimes unset a value
			// delete a random document
			//                System.out.println("[" + Thread.currentThread().getName() + "] deleteDoc=doc" + doc);
			// commit every 20 updates on average
			//                  System.out.println("[" + Thread.currentThread().getName() + "] commit");
			// reopen NRT reader (apply updates), on average once every 10 updates
			//                  System.out.println("[" + Thread.currentThread().getName() + "] open NRT");
			//                  System.out.println("[" + Thread.currentThread().getName() + "] reopen NRT");
			//            System.out.println("[" + Thread.currentThread().getName() + "] DONE");
			// suppress this exception only if there was another exception
			foreach (Sharpen.Thread t in threads)
			{
				t.Start();
			}
			done.Await();
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			foreach (AtomicReaderContext context in reader.Leaves())
			{
				AtomicReader r = ((AtomicReader)context.Reader());
				for (int i_2 = 0; i_2 < numThreads; i_2++)
				{
					NumericDocValues ndv = r.GetNumericDocValues("f" + i_2);
					NumericDocValues control = r.GetNumericDocValues("cf" + i_2);
					Bits docsWithNdv = r.GetDocsWithField("f" + i_2);
					Bits docsWithControl = r.GetDocsWithField("cf" + i_2);
					Bits liveDocs = r.GetLiveDocs();
					for (int j = 0; j < r.MaxDoc(); j++)
					{
						if (liveDocs == null || liveDocs.Get(j))
						{
							NUnit.Framework.Assert.AreEqual(docsWithNdv.Get(j), docsWithControl.Get(j));
							if (docsWithNdv.Get(j))
							{
								NUnit.Framework.Assert.AreEqual(control.Get(j), ndv.Get(j) * 2);
							}
						}
					}
				}
			}
			reader.Close();
			dir.Close();
		}

		private sealed class _Thread_1014 : Sharpen.Thread
		{
			public _Thread_1014(AtomicInteger numUpdates, IndexWriter writer, string f, string
				 cf, int numDocs, CountDownLatch done, string baseArg1) : base(baseArg1)
			{
				this.numUpdates = numUpdates;
				this.writer = writer;
				this.f = f;
				this.cf = cf;
				this.numDocs = numDocs;
				this.done = done;
			}

			public override void Run()
			{
				DirectoryReader reader = null;
				bool success = false;
				try
				{
					Random random = LuceneTestCase.Random();
					while (numUpdates.GetAndDecrement() > 0)
					{
						double group = random.NextDouble();
						Term t;
						if (group < 0.1)
						{
							t = new Term("updKey", "g0");
						}
						else
						{
							if (group < 0.5)
							{
								t = new Term("updKey", "g1");
							}
							else
							{
								if (group < 0.8)
								{
									t = new Term("updKey", "g2");
								}
								else
								{
									t = new Term("updKey", "g3");
								}
							}
						}
						if (random.NextBoolean())
						{
							writer.UpdateNumericDocValue(t, f, null);
							writer.UpdateNumericDocValue(t, cf, null);
						}
						else
						{
							long updValue = random.Next();
							writer.UpdateNumericDocValue(t, f, updValue);
							writer.UpdateNumericDocValue(t, cf, updValue * 2);
						}
						if (random.NextDouble() < 0.2)
						{
							int doc = random.Next(numDocs);
							writer.DeleteDocuments(new Term("id", "doc" + doc));
						}
						if (random.NextDouble() < 0.05)
						{
							writer.Commit();
						}
						if (random.NextDouble() < 0.1)
						{
							if (reader == null)
							{
								reader = DirectoryReader.Open(writer, true);
							}
							else
							{
								DirectoryReader r2 = DirectoryReader.OpenIfChanged(reader, writer, true);
								if (r2 != null)
								{
									reader.Close();
									reader = r2;
								}
							}
						}
					}
					success = true;
				}
				catch (IOException e)
				{
					throw new RuntimeException(e);
				}
				finally
				{
					if (reader != null)
					{
						try
						{
							reader.Close();
						}
						catch (IOException e)
						{
							if (success)
							{
								throw new RuntimeException(e);
							}
						}
					}
					done.CountDown();
				}
			}

			private readonly AtomicInteger numUpdates;

			private readonly IndexWriter writer;

			private readonly string f;

			private readonly string cf;

			private readonly int numDocs;

			private readonly CountDownLatch done;
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateDifferentDocsInDifferentGens()
		{
			// update same document multiple times across generations
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			conf.SetMaxBufferedDocs(4);
			IndexWriter writer = new IndexWriter(dir, conf);
			int numDocs = AtLeast(10);
			for (int i = 0; i < numDocs; i++)
			{
				Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
					();
				doc.Add(new StringField("id", "doc" + i, Field.Store.NO));
				long value = Random().Next();
				doc.Add(new NumericDocValuesField("f", value));
				doc.Add(new NumericDocValuesField("cf", value * 2));
				writer.AddDocument(doc);
			}
			int numGens = AtLeast(5);
			for (int i_1 = 0; i_1 < numGens; i_1++)
			{
				int doc = Random().Next(numDocs);
				Term t = new Term("id", "doc" + doc);
				long value = Random().NextLong();
				writer.UpdateNumericDocValue(t, "f", value);
				writer.UpdateNumericDocValue(t, "cf", value * 2);
				DirectoryReader reader = DirectoryReader.Open(writer, true);
				foreach (AtomicReaderContext context in reader.Leaves())
				{
					AtomicReader r = ((AtomicReader)context.Reader());
					NumericDocValues fndv = r.GetNumericDocValues("f");
					NumericDocValues cfndv = r.GetNumericDocValues("cf");
					for (int j = 0; j < r.MaxDoc(); j++)
					{
						NUnit.Framework.Assert.AreEqual(cfndv.Get(j), fndv.Get(j) * 2);
					}
				}
				reader.Close();
			}
			writer.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestChangeCodec()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			conf.SetMergePolicy(NoMergePolicy.COMPOUND_FILES);
			// disable merges to simplify test assertions.
			conf.SetCodec(new _Lucene46Codec_1156());
			IndexWriter writer = new IndexWriter(dir, conf.Clone());
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("id", "d0", Field.Store.NO));
			doc.Add(new NumericDocValuesField("f1", 5L));
			doc.Add(new NumericDocValuesField("f2", 13L));
			writer.AddDocument(doc);
			writer.Close();
			// change format
			conf.SetCodec(new _Lucene46Codec_1171());
			writer = new IndexWriter(dir, conf.Clone());
			doc = new Lucene.Net.Document.Document();
			doc.Add(new StringField("id", "d1", Field.Store.NO));
			doc.Add(new NumericDocValuesField("f1", 17L));
			doc.Add(new NumericDocValuesField("f2", 2L));
			writer.AddDocument(doc);
			writer.UpdateNumericDocValue(new Term("id", "d0"), "f1", 12L);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			AtomicReader r = SlowCompositeReaderWrapper.Wrap(reader);
			NumericDocValues f1 = r.GetNumericDocValues("f1");
			NumericDocValues f2 = r.GetNumericDocValues("f2");
			NUnit.Framework.Assert.AreEqual(12L, f1.Get(0));
			NUnit.Framework.Assert.AreEqual(13L, f2.Get(0));
			NUnit.Framework.Assert.AreEqual(17L, f1.Get(1));
			NUnit.Framework.Assert.AreEqual(2L, f2.Get(1));
			reader.Close();
			dir.Close();
		}

		private sealed class _Lucene46Codec_1156 : Lucene46Codec
		{
			public _Lucene46Codec_1156()
			{
			}

			public override DocValuesFormat GetDocValuesFormatForField(string field)
			{
				return new Lucene45DocValuesFormat();
			}
		}

		private sealed class _Lucene46Codec_1171 : Lucene46Codec
		{
			public _Lucene46Codec_1171()
			{
			}

			public override DocValuesFormat GetDocValuesFormatForField(string field)
			{
				return new AssertingDocValuesFormat();
			}
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestAddIndexes()
		{
			Directory dir1 = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir1, conf);
			int numDocs = AtLeast(50);
			int numTerms = TestUtil.NextInt(Random(), 1, numDocs / 5);
			ICollection<string> randomTerms = new HashSet<string>();
			while (randomTerms.Count < numTerms)
			{
				randomTerms.AddItem(TestUtil.RandomSimpleString(Random()));
			}
			// create first index
			for (int i = 0; i < numDocs; i++)
			{
				Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
					();
				doc.Add(new StringField("id", RandomPicks.RandomFrom(Random(), randomTerms), Field.Store
					.NO));
				doc.Add(new NumericDocValuesField("ndv", 4L));
				doc.Add(new NumericDocValuesField("control", 8L));
				writer.AddDocument(doc);
			}
			if (Random().NextBoolean())
			{
				writer.Commit();
			}
			// update some docs to a random value
			long value = Random().Next();
			Term term = new Term("id", RandomPicks.RandomFrom(Random(), randomTerms));
			writer.UpdateNumericDocValue(term, "ndv", value);
			writer.UpdateNumericDocValue(term, "control", value * 2);
			writer.Close();
			Directory dir2 = NewDirectory();
			conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random()));
			writer = new IndexWriter(dir2, conf);
			if (Random().NextBoolean())
			{
				writer.AddIndexes(dir1);
			}
			else
			{
				DirectoryReader reader = DirectoryReader.Open(dir1);
				writer.AddIndexes(reader);
				reader.Close();
			}
			writer.Close();
			DirectoryReader reader_1 = DirectoryReader.Open(dir2);
			foreach (AtomicReaderContext context in reader_1.Leaves())
			{
				AtomicReader r = ((AtomicReader)context.Reader());
				NumericDocValues ndv = r.GetNumericDocValues("ndv");
				NumericDocValues control = r.GetNumericDocValues("control");
				for (int i_1 = 0; i_1 < r.MaxDoc(); i_1++)
				{
					NUnit.Framework.Assert.AreEqual(ndv.Get(i_1) * 2, control.Get(i_1));
				}
			}
			reader_1.Close();
			IOUtils.Close(dir1, dir2);
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestDeleteUnusedUpdatesFiles()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("id", "d0", Field.Store.NO));
			doc.Add(new NumericDocValuesField("f", 1L));
			writer.AddDocument(doc);
			// create first gen of update files
			writer.UpdateNumericDocValue(new Term("id", "d0"), "f", 2L);
			writer.Commit();
			int numFiles = dir.ListAll().Length;
			DirectoryReader r = DirectoryReader.Open(dir);
			NUnit.Framework.Assert.AreEqual(2L, ((AtomicReader)r.Leaves()[0].Reader()).GetNumericDocValues
				("f").Get(0));
			r.Close();
			// create second gen of update files, first gen should be deleted
			writer.UpdateNumericDocValue(new Term("id", "d0"), "f", 5L);
			writer.Commit();
			NUnit.Framework.Assert.AreEqual(numFiles, dir.ListAll().Length);
			r = DirectoryReader.Open(dir);
			NUnit.Framework.Assert.AreEqual(5L, ((AtomicReader)r.Leaves()[0].Reader()).GetNumericDocValues
				("f").Get(0));
			r.Close();
			writer.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestTonsOfUpdates()
		{
			// LUCENE-5248: make sure that when there are many updates, we don't use too much RAM
			Directory dir = NewDirectory();
			Random random = Random();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(random));
			conf.SetRAMBufferSizeMB(IndexWriterConfig.DEFAULT_RAM_BUFFER_SIZE_MB);
			conf.SetMaxBufferedDocs(IndexWriterConfig.DISABLE_AUTO_FLUSH);
			// don't flush by doc
			IndexWriter writer = new IndexWriter(dir, conf);
			// test data: lots of documents (few 10Ks) and lots of update terms (few hundreds)
			int numDocs = AtLeast(20000);
			int numNumericFields = AtLeast(5);
			int numTerms = TestUtil.NextInt(random, 10, 100);
			// terms should affect many docs
			ICollection<string> updateTerms = new HashSet<string>();
			while (updateTerms.Count < numTerms)
			{
				updateTerms.AddItem(TestUtil.RandomSimpleString(random));
			}
			//    System.out.println("numDocs=" + numDocs + " numNumericFields=" + numNumericFields + " numTerms=" + numTerms);
			// build a large index with many NDV fields and update terms
			for (int i = 0; i < numDocs; i++)
			{
				Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
					();
				int numUpdateTerms = TestUtil.NextInt(random, 1, numTerms / 10);
				for (int j = 0; j < numUpdateTerms; j++)
				{
					doc.Add(new StringField("upd", RandomPicks.RandomFrom(random, updateTerms), Field.Store
						.NO));
				}
				for (int j_1 = 0; j_1 < numNumericFields; j_1++)
				{
					long val = random.Next();
					doc.Add(new NumericDocValuesField("f" + j_1, val));
					doc.Add(new NumericDocValuesField("cf" + j_1, val * 2));
				}
				writer.AddDocument(doc);
			}
			writer.Commit();
			// commit so there's something to apply to
			// set to flush every 2048 bytes (approximately every 12 updates), so we get
			// many flushes during numeric updates
			writer.GetConfig().SetRAMBufferSizeMB(2048.0 / 1024 / 1024);
			int numUpdates = AtLeast(100);
			//    System.out.println("numUpdates=" + numUpdates);
			for (int i_1 = 0; i_1 < numUpdates; i_1++)
			{
				int field = random.Next(numNumericFields);
				Term updateTerm = new Term("upd", RandomPicks.RandomFrom(random, updateTerms));
				long value = random.Next();
				writer.UpdateNumericDocValue(updateTerm, "f" + field, value);
				writer.UpdateNumericDocValue(updateTerm, "cf" + field, value * 2);
			}
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			foreach (AtomicReaderContext context in reader.Leaves())
			{
				for (int i_2 = 0; i_2 < numNumericFields; i_2++)
				{
					AtomicReader r = ((AtomicReader)context.Reader());
					NumericDocValues f = r.GetNumericDocValues("f" + i_2);
					NumericDocValues cf = r.GetNumericDocValues("cf" + i_2);
					for (int j = 0; j < r.MaxDoc(); j++)
					{
						NUnit.Framework.Assert.AreEqual("reader=" + r + ", field=f" + i_2 + ", doc=" + j, 
							cf.Get(j), f.Get(j) * 2);
					}
				}
			}
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdatesOrder()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("upd", "t1", Field.Store.NO));
			doc.Add(new StringField("upd", "t2", Field.Store.NO));
			doc.Add(new NumericDocValuesField("f1", 1L));
			doc.Add(new NumericDocValuesField("f2", 1L));
			writer.AddDocument(doc);
			writer.UpdateNumericDocValue(new Term("upd", "t1"), "f1", 2L);
			// update f1 to 2
			writer.UpdateNumericDocValue(new Term("upd", "t1"), "f2", 2L);
			// update f2 to 2
			writer.UpdateNumericDocValue(new Term("upd", "t2"), "f1", 3L);
			// update f1 to 3
			writer.UpdateNumericDocValue(new Term("upd", "t2"), "f2", 3L);
			// update f2 to 3
			writer.UpdateNumericDocValue(new Term("upd", "t1"), "f1", 4L);
			// update f1 to 4 (but not f2)
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			NUnit.Framework.Assert.AreEqual(4, ((AtomicReader)reader.Leaves()[0].Reader()).GetNumericDocValues
				("f1").Get(0));
			NUnit.Framework.Assert.AreEqual(3, ((AtomicReader)reader.Leaves()[0].Reader()).GetNumericDocValues
				("f2").Get(0));
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateAllDeletedSegment()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("id", "doc", Field.Store.NO));
			doc.Add(new NumericDocValuesField("f1", 1L));
			writer.AddDocument(doc);
			writer.AddDocument(doc);
			writer.Commit();
			writer.DeleteDocuments(new Term("id", "doc"));
			// delete all docs in the first segment
			writer.AddDocument(doc);
			writer.UpdateNumericDocValue(new Term("id", "doc"), "f1", 2L);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			NUnit.Framework.Assert.AreEqual(1, reader.Leaves().Count);
			NUnit.Framework.Assert.AreEqual(2L, ((AtomicReader)reader.Leaves()[0].Reader()).GetNumericDocValues
				("f1").Get(0));
			reader.Close();
			dir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestUpdateTwoNonexistingTerms()
		{
			Directory dir = NewDirectory();
			IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer
				(Random()));
			IndexWriter writer = new IndexWriter(dir, conf);
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			doc.Add(new StringField("id", "doc", Field.Store.NO));
			doc.Add(new NumericDocValuesField("f1", 1L));
			writer.AddDocument(doc);
			// update w/ multiple nonexisting terms in same field
			writer.UpdateNumericDocValue(new Term("c", "foo"), "f1", 2L);
			writer.UpdateNumericDocValue(new Term("c", "bar"), "f1", 2L);
			writer.Close();
			DirectoryReader reader = DirectoryReader.Open(dir);
			NUnit.Framework.Assert.AreEqual(1, reader.Leaves().Count);
			NUnit.Framework.Assert.AreEqual(1L, ((AtomicReader)reader.Leaves()[0].Reader()).GetNumericDocValues
				("f1").Get(0));
			reader.Close();
			dir.Close();
		}
	}
}
