/* 
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;

using NUnit.Framework;

using SimpleAnalyzer = Lucene.Net.Analysis.SimpleAnalyzer;
using Document = Lucene.Net.Documents.Document;
using Field = Lucene.Net.Documents.Field;
using IndexWriter = Lucene.Net.Index.IndexWriter;
using QueryParser = Lucene.Net.QueryParsers.QueryParser;
using RAMDirectory = Lucene.Net.Store.RAMDirectory;
using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;

namespace Lucene.Net.Search
{
	
	/// <summary>Similarity unit test.</summary>
    [TestFixture]
	public class TestNot:LuceneTestCase
	{		
		[Test]
		public virtual void  TestNot_Renamed()
		{
			Directory store = NewDirectory();
			RandomIndexWriter writer = new RandomIndexWriter(Random(), store);
			
			Document d1 = new Document();
			d1.Add(NewTextField("field", "a b", Field.Store.YES));
			
			writer.AddDocument(d1);
			IndexReader reader = writer.GetReader();
			IndexSearcher searcher = NewSearcher(reader);
			BooleanQuery query = new BooleanQuery();
			query.Add(new TermQuery(new Term("field", "a")), BooleanClause.Occur.SHOULD);
			query.Add(new TermQuery(new Term("field", "b")), BooleanClause.Occur.MUST_NOT);
			ScoreDoc[] hits = searcher.Search(query, null, 1000).scoreDocs;
			NUnit.Framework.Assert.AreEqual(0, hits.Length);
			writer.Close();
			reader.Close();
			store.Close();
		}
	}
}