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
using Lucene.Net.Test.Analysis.TokenAttributes;
using Lucene.Net.Test.Analysis;
using NUnit.Framework;
using Document = Lucene.Net.Documents.Document;
using Field = Lucene.Net.Documents.Field;
using TermVector = Lucene.Net.Documents.Field.TermVector;
using IndexReader = Lucene.Net.Index.IndexReader;
using IndexWriter = Lucene.Net.Index.IndexWriter;
using Term = Lucene.Net.Index.Term;
using Directory = Lucene.Net.Store.Directory;
using RAMDirectory = Lucene.Net.Store.RAMDirectory;

namespace Lucene.Net.Analysis
{

    [TestFixture]
    public class TestCachingTokenFilter : BaseTokenStreamTestCase
    {
        private class AnonymousClassTokenStream : TokenStream
        {
            public AnonymousClassTokenStream(TestCachingTokenFilter enclosingInstance)
            {
                InitBlock(enclosingInstance);
            }

            private void InitBlock(TestCachingTokenFilter enclosingInstance)
            {
                this.enclosingInstance = enclosingInstance;
                termAtt = AddAttribute<ITermAttribute>();
                offsetAtt = AddAttribute<IOffsetAttribute>();
            }

            private TestCachingTokenFilter enclosingInstance;
            
            private int index = 0;
            private ITermAttribute termAtt;
            private IOffsetAttribute offsetAtt;

            public override bool IncrementToken()
            {
                if (index == enclosingInstance.tokens.Length)
                {
                    return false;
                }
                else
                {
                    ClearAttributes();
					this.termAtt.Append(this._enclosing.tokens[this.index++]);
                    offsetAtt.SetOffset(0, 0);
                    return true;
                }
            }

            protected override void Dispose(bool disposing)
            {
                // Do Nothing
            }
        }

        private string[] tokens = new string[] { "term1", "term2", "term3", "term2" };

        [Test]
        public virtual void TestCaching()
        {
            Directory dir = NewDirectory();
			RandomIndexWriter writer = new RandomIndexWriter(Random(), dir);
            Document doc = new Document();
            TokenStream stream = new AnonymousClassTokenStream(this);

            stream = new CachingTokenFilter(stream);

			doc.Add(new TextField("preanalyzed", stream));

            // 1) we consume all tokens twice before we add the doc to the index
            checkTokens(stream);
            stream.Reset();
            checkTokens(stream);

            // 2) now add the document to the index and verify if all tokens are indexed
            //    don't reset the stream here, the DocumentWriter should do that implicitly
            writer.AddDocument(doc);
            writer.Close();

			IndexReader reader = writer.GetReader();
			DocsAndPositionsEnum termPositions = MultiFields.GetTermPositionsEnum(reader, MultiFields
				.GetLiveDocs(reader), "preanalyzed", new BytesRef("term1"));
			NUnit.Framework.Assert.IsTrue(termPositions.NextDoc() != DocIdSetIterator.NO_MORE_DOCS
				);
            Assert.AreEqual(1, termPositions.Freq);
            Assert.AreEqual(0, termPositions.NextPosition());
			termPositions = MultiFields.GetTermPositionsEnum(reader, MultiFields.GetLiveDocs(
				reader), "preanalyzed", new BytesRef("term2"));
			NUnit.Framework.Assert.IsTrue(termPositions.NextDoc() != DocIdSetIterator.NO_MORE_DOCS
				);
            Assert.AreEqual(2, termPositions.Freq);
            Assert.AreEqual(1, termPositions.NextPosition());
            Assert.AreEqual(3, termPositions.NextPosition());
			termPositions = MultiFields.GetTermPositionsEnum(reader, MultiFields.GetLiveDocs(
				reader), "preanalyzed", new BytesRef("term3"));
			NUnit.Framework.Assert.IsTrue(termPositions.NextDoc() != DocIdSetIterator.NO_MORE_DOCS
				);
            Assert.AreEqual(1, termPositions.Freq);
            Assert.AreEqual(2, termPositions.NextPosition());
            reader.Close();
			writer.Close();
            // 3) reset stream and consume tokens again
            stream.Reset();
            checkTokens(stream);
        }

        private void checkTokens(TokenStream stream)
        {
            int count = 0;

			CharTermAttribute termAtt = stream.GetAttribute<CharTermAttribute>();
            Assert.IsNotNull(termAtt);
            while (stream.IncrementToken())
            {
                Assert.IsTrue(count < tokens.Length);
                Assert.AreEqual(tokens[count], termAtt.Term);
                count++;
            }

            Assert.AreEqual(tokens.Length, count);
        }
    }
}