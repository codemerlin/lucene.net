using System;
using System.Text;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.TestFramework;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.TestFramework.Index;
using Lucene.Net.Util;
using NUnit.Framework;

namespace Lucene.Net.Test.Index
{
	public class TestOmitTf : LuceneTestCase
	{
		public class SimpleSimilarity : TFIDFSimilarity
		{
			public override float DecodeNormValue(long norm)
			{
				return norm;
			}

			public override long EncodeNormValue(float f)
			{
				return (long)f;
			}

			public override float QueryNorm(float sumOfSquaredWeights)
			{
				return 1.0f;
			}

			public override float Coord(int overlap, int maxOverlap)
			{
				return 1.0f;
			}

			public override float LengthNorm(FieldInvertState state)
			{
				return state.Boost;
			}

			public override float Tf(float freq)
			{
				return freq;
			}

			public override float SloppyFreq(int distance)
			{
				return 2.0f;
			}

			public override float Idf(long docFreq, long numDocs)
			{
				return 1.0f;
			}

			public override Explanation IdfExplain(CollectionStatistics collectionStats, TermStatistics
				[] termStats)
			{
				return new Explanation(1.0f, "Inexplicable");
			}

			public override float ScorePayload(int doc, int start, int end, BytesRef payload)
			{
				return 1.0f;
			}
		}

		private static readonly FieldType omitType = new FieldType(TextField.TYPE_NOT_STORED
			);

		private static readonly FieldType normalType = new FieldType(TextField.TYPE_NOT_STORED
			);

		static TestOmitTf()
		{
			omitType.IndexOptions = (FieldInfo.IndexOptions.DOCS_ONLY);
		}

		// Tests whether the DocumentWriter correctly enable the
		// omitTermFreqAndPositions bit in the FieldInfo
		[Test]
		public virtual void TestOmitTermFreqAndPositions()
		{
			Directory ram = NewDirectory();
			Analyzer analyzer = new MockAnalyzer(Random());
			IndexWriter writer = new IndexWriter(ram, NewIndexWriterConfig(TEST_VERSION_CURRENT
				, analyzer));
			Lucene.Net.Documents.Document d = new Lucene.Net.Documents.Document();
			// this field will have Tf
			Field f1 = NewField("f1", "This field has term freqs", normalType);
			d.Add(f1);
			// this field will NOT have Tf
			Field f2 = NewField("f2", "This field has NO Tf in all docs", omitType);
			d.Add(f2);
			writer.AddDocument(d);
			writer.ForceMerge(1);
			// now we add another document which has term freq for field f2 and not for f1 and verify if the SegmentMerger
			// keep things constant
			d = new Lucene.Net.Documents.Document();
			// Reverse
			f1 = NewField("f1", "This field has term freqs", omitType);
			d.Add(f1);
			f2 = NewField("f2", "This field has NO Tf in all docs", normalType);
			d.Add(f2);
			writer.AddDocument(d);
			// force merge
			writer.ForceMerge(1);
			// flush
			writer.Dispose();
			SegmentReader reader = GetOnlySegmentReader(DirectoryReader.Open(ram));
			FieldInfos fi = reader.FieldInfos;
			AssertEquals("OmitTermFreqAndPositions field bit should be set."
				, FieldInfo.IndexOptions.DOCS_ONLY, fi.FieldInfo("f1").IndexOptionsValue);
			AssertEquals("OmitTermFreqAndPositions field bit should be set."
				, FieldInfo.IndexOptions.DOCS_ONLY, fi.FieldInfo("f2").IndexOptionsValue);
			reader.Dispose();
			ram.Dispose();
		}

		// Tests whether merging of docs that have different
		// omitTermFreqAndPositions for the same field works
		[Test]
		public virtual void TestMixedMerge()
		{
			Directory ram = NewDirectory();
			Analyzer analyzer = new MockAnalyzer(Random());
			IndexWriter writer = new IndexWriter(ram, ((IndexWriterConfig)NewIndexWriterConfig
				(TEST_VERSION_CURRENT, analyzer).SetMaxBufferedDocs(3)).SetMergePolicy(NewLogMergePolicy
				(2)));
			Lucene.Net.Documents.Document d = new Lucene.Net.Documents.Document();
			// this field will have Tf
			Field f1 = NewField("f1", "This field has term freqs", normalType);
			d.Add(f1);
			// this field will NOT have Tf
			Field f2 = NewField("f2", "This field has NO Tf in all docs", omitType);
			d.Add(f2);
			for (int i = 0; i < 30; i++)
			{
				writer.AddDocument(d);
			}
			// now we add another document which has term freq for field f2 and not for f1 and verify if the SegmentMerger
			// keep things constant
			d = new Lucene.Net.Documents.Document();
			// Reverese
			f1 = NewField("f1", "This field has term freqs", omitType);
			d.Add(f1);
			f2 = NewField("f2", "This field has NO Tf in all docs", normalType);
			d.Add(f2);
			for (int i_1 = 0; i_1 < 30; i_1++)
			{
				writer.AddDocument(d);
			}
			// force merge
			writer.ForceMerge(1);
			// flush
			writer.Dispose();
			SegmentReader reader = GetOnlySegmentReader(DirectoryReader.Open(ram));
			FieldInfos fi = reader.FieldInfos;
			AssertEquals("OmitTermFreqAndPositions field bit should be set."
				, FieldInfo.IndexOptions.DOCS_ONLY, fi.FieldInfo("f1").IndexOptionsValue);
			AssertEquals("OmitTermFreqAndPositions field bit should be set."
				, FieldInfo.IndexOptions.DOCS_ONLY, fi.FieldInfo("f2").IndexOptionsValue);
			reader.Dispose();
			ram.Dispose();
		}

		// Make sure first adding docs that do not omitTermFreqAndPositions for
		// field X, then adding docs that do omitTermFreqAndPositions for that same
		// field, 
		[Test]
		public virtual void TestMixedRAM()
		{
			Directory ram = NewDirectory();
			Analyzer analyzer = new MockAnalyzer(Random());
			IndexWriter writer = new IndexWriter(ram, ((IndexWriterConfig)NewIndexWriterConfig
				(TEST_VERSION_CURRENT, analyzer).SetMaxBufferedDocs(10)).SetMergePolicy(NewLogMergePolicy
				(2)));
			Lucene.Net.Documents.Document d = new Lucene.Net.Documents.Document();
			// this field will have Tf
			Field f1 = NewField("f1", "This field has term freqs", normalType);
			d.Add(f1);
			// this field will NOT have Tf
			Field f2 = NewField("f2", "This field has NO Tf in all docs", omitType);
			d.Add(f2);
			for (int i = 0; i < 5; i++)
			{
				writer.AddDocument(d);
			}
			for (int i_1 = 0; i_1 < 20; i_1++)
			{
				writer.AddDocument(d);
			}
			// force merge
			writer.ForceMerge(1);
			// flush
			writer.Dispose();
			SegmentReader reader = GetOnlySegmentReader(DirectoryReader.Open(ram));
			FieldInfos fi = reader.FieldInfos;
			AssertEquals("OmitTermFreqAndPositions field bit should not be set."
				, FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS, fi.FieldInfo("f1").IndexOptionsValue);
			AssertEquals("OmitTermFreqAndPositions field bit should be set."
				, FieldInfo.IndexOptions.DOCS_ONLY, fi.FieldInfo("f2").IndexOptionsValue);
			reader.Dispose();
			ram.Dispose();
		}

		/// <exception cref="System.Exception"></exception>
		private void AssertNoPrx(Directory dir)
		{
			string[] files = dir.ListAll();
			for (int i = 0; i < files.Length; i++)
			{
				IsFalse(files[i].EndsWith(".prx"));
				IsFalse(files[i].EndsWith(".pos"));
			}
		}

		// Verifies no *.prx exists when all fields omit term freq:
		[Test]
		public virtual void TestNoPrxFile()
		{
			Directory ram = NewDirectory();
			Analyzer analyzer = new MockAnalyzer(Random());
			IndexWriter writer = new IndexWriter(ram, ((IndexWriterConfig)NewIndexWriterConfig
				(TEST_VERSION_CURRENT, analyzer).SetMaxBufferedDocs(3)).SetMergePolicy(NewLogMergePolicy
				()));
			LogMergePolicy lmp = (LogMergePolicy)writer.Config.MergePolicy;
			lmp.MergeFactor = (2);
			lmp.SetNoCFSRatio(0.0);
			Lucene.Net.Documents.Document d = new Lucene.Net.Documents.Document();
			Field f1 = NewField("f1", "This field has term freqs", omitType);
			d.Add(f1);
			for (int i = 0; i < 30; i++)
			{
				writer.AddDocument(d);
			}
			writer.Commit();
			AssertNoPrx(ram);
			// now add some documents with positions, and check
			// there is no prox after full merge
			d = new Lucene.Net.Documents.Document();
			f1 = NewTextField("f1", "This field has positions", Field.Store.NO);
			d.Add(f1);
			for (int i_1 = 0; i_1 < 30; i_1++)
			{
				writer.AddDocument(d);
			}
			// force merge
			writer.ForceMerge(1);
			// flush
			writer.Dispose();
			AssertNoPrx(ram);
			ram.Dispose();
		}

		// Test scores with one field with Term Freqs and one without, otherwise with equal content 
		[Test]
		public virtual void TestBasic()
		{
			Directory dir = NewDirectory();
			Analyzer analyzer = new MockAnalyzer(Random());
			IndexWriter writer = new IndexWriter(dir, ((IndexWriterConfig)NewIndexWriterConfig
				(TEST_VERSION_CURRENT, analyzer).SetMaxBufferedDocs(2)).SetSimilarity(new TestOmitTf.SimpleSimilarity
				()).SetMergePolicy(NewLogMergePolicy(2)));
			StringBuilder sb = new StringBuilder(265);
			string term = "term";
			for (int i = 0; i < 30; i++)
			{
				Lucene.Net.Documents.Document d = new Lucene.Net.Documents.Document();
				sb.Append(term).Append(" ");
				string content = sb.ToString();
				Field noTf = NewField("noTf", content + (i % 2 == 0 ? string.Empty : " notf"), omitType
					);
				d.Add(noTf);
				Field tf = NewField("tf", content + (i % 2 == 0 ? " tf" : string.Empty), normalType
					);
				d.Add(tf);
				writer.AddDocument(d);
			}
			//System.out.println(d);
			writer.ForceMerge(1);
			// flush
			writer.Dispose();
			IndexReader reader = DirectoryReader.Open(dir);
			IndexSearcher searcher = NewSearcher(reader);
			searcher.Similarity = (new TestOmitTf.SimpleSimilarity());
			Term a = new Term("noTf", term);
			Term b = new Term("tf", term);
			Term c = new Term("noTf", "notf");
			Term d_1 = new Term("tf", "tf");
			TermQuery q1 = new TermQuery(a);
			TermQuery q2 = new TermQuery(b);
			TermQuery q3 = new TermQuery(c);
			TermQuery q4 = new TermQuery(d_1);
			PhraseQuery pq = new PhraseQuery();
			pq.Add(a);
			pq.Add(c);
			try
			{
				searcher.Search(pq, 10);
				Fail("did not hit expected exception");
			}
			catch (Exception e)
			{
				Exception cause = e;
				// If the searcher uses an executor service, the IAE is wrapped into other exceptions
				while (cause.InnerException != null)
				{
					cause = cause.InnerException;
				}
				if (!(cause is InvalidOperationException))
				{
					throw new Exception("Expected an IAE", e);
				}
			}
			// else OK because positions are not indexed
			searcher.Search(q1, new AnonCountingHitCollector());
			//System.out.println("Q1: Doc=" + doc + " score=" + score);
			//System.out.println(CountingHitCollector.getCount());
			searcher.Search(q2, new AnonCountingHitCollector2());
			//System.out.println("Q2: Doc=" + doc + " score=" + score);
			//System.out.println(CountingHitCollector.getCount());
			searcher.Search(q3, new AnonCountingHitCollector3());
			//System.out.println("Q1: Doc=" + doc + " score=" + score);
			//System.out.println(CountingHitCollector.getCount());
			searcher.Search(q4, new AnonCountingHitCollector4());
			//System.out.println("Q1: Doc=" + doc + " score=" + score);
			//System.out.println(CountingHitCollector.getCount());
			BooleanQuery bq = new BooleanQuery();
			bq.Add(q1, Occur.MUST);
			bq.Add(q4, Occur.MUST);
			searcher.Search(bq, new AnonCountingHitCollector5());
			//System.out.println("BQ: Doc=" + doc + " score=" + score);
			AssertEquals(15, TestOmitTf.CountingHitCollector.GetCount());
			reader.Dispose();
			dir.Dispose();
		}

		private sealed class AnonCountingHitCollector : TestOmitTf.CountingHitCollector
		{
		    private Scorer scorer;

			public override void SetScorer(Scorer scorer)
			{
				this.scorer = scorer;
			}

			/// <exception cref="System.IO.IOException"></exception>
			public sealed override void Collect(int doc)
			{
				float score = this.scorer.Score();
				AssertTrue("got score=" + score, score == 1.0f);
				base.Collect(doc);
			}
		}

		private sealed class AnonCountingHitCollector2 : TestOmitTf.CountingHitCollector
		{
		    private Scorer scorer;

			public sealed override void SetScorer(Scorer scorer)
			{
				this.scorer = scorer;
			}

			/// <exception cref="System.IO.IOException"></exception>
			public sealed override void Collect(int doc)
			{
				float score = this.scorer.Score();
				AssertEquals(1.0f + doc, score, 0.00001f);
				base.Collect(doc);
			}
		}

		private sealed class AnonCountingHitCollector3 : TestOmitTf.CountingHitCollector
		{
		    private Scorer scorer;

			public sealed override void SetScorer(Scorer scorer)
			{
				this.scorer = scorer;
			}

			/// <exception cref="System.IO.IOException"></exception>
			public sealed override void Collect(int doc)
			{
				float score = this.scorer.Score();
				IsTrue(score == 1.0f);
				IsFalse(doc % 2 == 0);
				base.Collect(doc);
			}
		}

		private sealed class AnonCountingHitCollector4 : TestOmitTf.CountingHitCollector
		{
		    private Scorer scorer;

			public sealed override void SetScorer(Scorer scorer)
			{
				this.scorer = scorer;
			}

			/// <exception cref="System.IO.IOException"></exception>
			public sealed override void Collect(int doc)
			{
				float score = this.scorer.Score();
				IsTrue(score == 1.0f);
				IsTrue(doc % 2 == 0);
				base.Collect(doc);
			}
		}

		private sealed class AnonCountingHitCollector5 : TestOmitTf.CountingHitCollector
		{
		    /// <exception cref="System.IO.IOException"></exception>
			public sealed override void Collect(int doc)
			{
				base.Collect(doc);
			}
		}

		public class CountingHitCollector : Collector
		{
			internal static int count = 0;

			internal static int sum = 0;

			private int docBase = -1;

			public CountingHitCollector()
			{
				count = 0;
				sum = 0;
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override void SetScorer(Scorer scorer)
			{
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override void Collect(int doc)
			{
				count++;
				sum += doc + docBase;
			}

			// use it to avoid any possibility of being merged away
			public static int GetCount()
			{
				return count;
			}

			public static int GetSum()
			{
				return sum;
			}

			public override void SetNextReader(AtomicReaderContext context)
			{
				docBase = context.docBase;
			}

			public override bool AcceptsDocsOutOfOrder
			{
			    get { return true; }
			}
		}

		/// <summary>test that when freqs are omitted, that totalTermFreq and sumTotalTermFreq are -1
		/// 	</summary>
		[Test]
		public virtual void TestStats()
		{
			Directory dir = NewDirectory();
			RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, NewIndexWriterConfig(
				TEST_VERSION_CURRENT, new MockAnalyzer(Random())));
			Lucene.Net.Documents.Document doc = new Lucene.Net.Documents.Document
				();
			FieldType ft = new FieldType(TextField.TYPE_NOT_STORED);
			ft.IndexOptions = (FieldInfo.IndexOptions.DOCS_ONLY);
			ft.Freeze();
			Field f = NewField("foo", "bar", ft);
			doc.Add(f);
			iw.AddDocument(doc);
			IndexReader ir = iw.Reader;
			iw.Dispose();
			AssertEquals(-1, ir.TotalTermFreq(new Term("foo", new BytesRef
				("bar"))));
			AssertEquals(-1, ir.GetSumTotalTermFreq("foo"));
			ir.Dispose();
			dir.Dispose();
		}
	}
}
