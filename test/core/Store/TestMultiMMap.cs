/*
 *
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Lucene.Net.Store
{
    [TestFixture]
	public class TestMultiMMap : LuceneTestCase
    {
		public override void SetUp()
		{
			base.SetUp();
			AssumeTrue("test requires a jre that supports unmapping", MMapDirectory.UNMAP_SUPPORTED
				);
		}
        [Test]
        public void TestDoesntExist()
        {
            Assert.Ignore("Need to port tests, but we don't really support MMapDirectories anyway");
        }
		public virtual void TestCloneSafety()
		{
			MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testCloneSafety"));
			IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
			io.WriteVInt(5);
			io.Close();
			IndexInput one = mmapDir.OpenInput("bytes", IOContext.DEFAULT);
			IndexInput two = ((IndexInput)one.Clone());
			IndexInput three = ((IndexInput)two.Clone());
			// clone of clone
			one.Close();
			try
			{
				one.ReadVInt();
				NUnit.Framework.Assert.Fail("Must throw AlreadyClosedException");
			}
			catch (AlreadyClosedException)
			{
			}
			// pass
			try
			{
				two.ReadVInt();
				NUnit.Framework.Assert.Fail("Must throw AlreadyClosedException");
			}
			catch (AlreadyClosedException)
			{
			}
			// pass
			try
			{
				three.ReadVInt();
				NUnit.Framework.Assert.Fail("Must throw AlreadyClosedException");
			}
			catch (AlreadyClosedException)
			{
			}
			// pass
			two.Close();
			three.Close();
			// test double close of master:
			one.Close();
			mmapDir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCloneClose()
		{
			MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testCloneClose"));
			IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
			io.WriteVInt(5);
			io.Close();
			IndexInput one = mmapDir.OpenInput("bytes", IOContext.DEFAULT);
			IndexInput two = ((IndexInput)one.Clone());
			IndexInput three = ((IndexInput)two.Clone());
			// clone of clone
			two.Close();
			NUnit.Framework.Assert.AreEqual(5, one.ReadVInt());
			try
			{
				two.ReadVInt();
				NUnit.Framework.Assert.Fail("Must throw AlreadyClosedException");
			}
			catch (AlreadyClosedException)
			{
			}
			// pass
			NUnit.Framework.Assert.AreEqual(5, three.ReadVInt());
			one.Close();
			three.Close();
			mmapDir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCloneSliceSafety()
		{
			MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testCloneSliceSafety"));
			IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
			io.WriteInt(1);
			io.WriteInt(2);
			io.Close();
			Directory.IndexInputSlicer slicer = mmapDir.CreateSlicer("bytes", NewIOContext(Random
				()));
			IndexInput one = slicer.OpenSlice("first int", 0, 4);
			IndexInput two = slicer.OpenSlice("second int", 4, 4);
			IndexInput three = ((IndexInput)one.Clone());
			// clone of clone
			IndexInput four = ((IndexInput)two.Clone());
			// clone of clone
			slicer.Close();
			try
			{
				one.ReadInt();
				NUnit.Framework.Assert.Fail("Must throw AlreadyClosedException");
			}
			catch (AlreadyClosedException)
			{
			}
			// pass
			try
			{
				two.ReadInt();
				NUnit.Framework.Assert.Fail("Must throw AlreadyClosedException");
			}
			catch (AlreadyClosedException)
			{
			}
			// pass
			try
			{
				three.ReadInt();
				NUnit.Framework.Assert.Fail("Must throw AlreadyClosedException");
			}
			catch (AlreadyClosedException)
			{
			}
			// pass
			try
			{
				four.ReadInt();
				NUnit.Framework.Assert.Fail("Must throw AlreadyClosedException");
			}
			catch (AlreadyClosedException)
			{
			}
			// pass
			one.Close();
			two.Close();
			three.Close();
			four.Close();
			// test double-close of slicer:
			slicer.Close();
			mmapDir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCloneSliceClose()
		{
			MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testCloneSliceClose"));
			IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
			io.WriteInt(1);
			io.WriteInt(2);
			io.Close();
			Directory.IndexInputSlicer slicer = mmapDir.CreateSlicer("bytes", NewIOContext(Random
				()));
			IndexInput one = slicer.OpenSlice("first int", 0, 4);
			IndexInput two = slicer.OpenSlice("second int", 4, 4);
			one.Close();
			try
			{
				one.ReadInt();
				NUnit.Framework.Assert.Fail("Must throw AlreadyClosedException");
			}
			catch (AlreadyClosedException)
			{
			}
			// pass
			NUnit.Framework.Assert.AreEqual(2, two.ReadInt());
			// reopen a new slice "one":
			one = slicer.OpenSlice("first int", 0, 4);
			NUnit.Framework.Assert.AreEqual(1, one.ReadInt());
			one.Close();
			two.Close();
			slicer.Close();
			mmapDir.Close();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestSeekZero()
		{
			for (int i = 0; i < 31; i++)
			{
				MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSeekZero"), null, 1 
					<< i);
				IndexOutput io = mmapDir.CreateOutput("zeroBytes", NewIOContext(Random()));
				io.Close();
				IndexInput ii = mmapDir.OpenInput("zeroBytes", NewIOContext(Random()));
				ii.Seek(0L);
				ii.Close();
				mmapDir.Close();
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestSeekSliceZero()
		{
			for (int i = 0; i < 31; i++)
			{
				MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSeekSliceZero"), null
					, 1 << i);
				IndexOutput io = mmapDir.CreateOutput("zeroBytes", NewIOContext(Random()));
				io.Close();
				Directory.IndexInputSlicer slicer = mmapDir.CreateSlicer("zeroBytes", NewIOContext
					(Random()));
				IndexInput ii = slicer.OpenSlice("zero-length slice", 0, 0);
				ii.Seek(0L);
				ii.Close();
				slicer.Close();
				mmapDir.Close();
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestSeekEnd()
		{
			for (int i = 0; i < 17; i++)
			{
				MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSeekEnd"), null, 1 <<
					 i);
				IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
				byte[] bytes = new byte[1 << i];
				Random().NextBytes(bytes);
				io.WriteBytes(bytes, bytes.Length);
				io.Close();
				IndexInput ii = mmapDir.OpenInput("bytes", NewIOContext(Random()));
				byte[] actual = new byte[1 << i];
				ii.ReadBytes(actual, 0, actual.Length);
				NUnit.Framework.Assert.AreEqual(new BytesRef(bytes), new BytesRef(actual));
				ii.Seek(1 << i);
				ii.Close();
				mmapDir.Close();
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestSeekSliceEnd()
		{
			for (int i = 0; i < 17; i++)
			{
				MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSeekSliceEnd"), null
					, 1 << i);
				IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
				byte[] bytes = new byte[1 << i];
				Random().NextBytes(bytes);
				io.WriteBytes(bytes, bytes.Length);
				io.Close();
				Directory.IndexInputSlicer slicer = mmapDir.CreateSlicer("bytes", NewIOContext(Random
					()));
				IndexInput ii = slicer.OpenSlice("full slice", 0, bytes.Length);
				byte[] actual = new byte[1 << i];
				ii.ReadBytes(actual, 0, actual.Length);
				NUnit.Framework.Assert.AreEqual(new BytesRef(bytes), new BytesRef(actual));
				ii.Seek(1 << i);
				ii.Close();
				slicer.Close();
				mmapDir.Close();
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestSeeking()
		{
			for (int i = 0; i < 10; i++)
			{
				MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSeeking"), null, 1 <<
					 i);
				IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
				byte[] bytes = new byte[1 << (i + 1)];
				// make sure we switch buffers
				Random().NextBytes(bytes);
				io.WriteBytes(bytes, bytes.Length);
				io.Close();
				IndexInput ii = mmapDir.OpenInput("bytes", NewIOContext(Random()));
				byte[] actual = new byte[1 << (i + 1)];
				// first read all bytes
				ii.ReadBytes(actual, 0, actual.Length);
				NUnit.Framework.Assert.AreEqual(new BytesRef(bytes), new BytesRef(actual));
				for (int sliceStart = 0; sliceStart < bytes.Length; sliceStart++)
				{
					for (int sliceLength = 0; sliceLength < bytes.Length - sliceStart; sliceLength++)
					{
						byte[] slice = new byte[sliceLength];
						ii.Seek(sliceStart);
						ii.ReadBytes(slice, 0, slice.Length);
						NUnit.Framework.Assert.AreEqual(new BytesRef(bytes, sliceStart, sliceLength), new 
							BytesRef(slice));
					}
				}
				ii.Close();
				mmapDir.Close();
			}
		}

		// note instead of seeking to offset and reading length, this opens slices at the 
		// the various offset+length and just does readBytes.
		/// <exception cref="System.Exception"></exception>
		public virtual void TestSlicedSeeking()
		{
			for (int i = 0; i < 10; i++)
			{
				MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSlicedSeeking"), null
					, 1 << i);
				IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
				byte[] bytes = new byte[1 << (i + 1)];
				// make sure we switch buffers
				Random().NextBytes(bytes);
				io.WriteBytes(bytes, bytes.Length);
				io.Close();
				IndexInput ii = mmapDir.OpenInput("bytes", NewIOContext(Random()));
				byte[] actual = new byte[1 << (i + 1)];
				// first read all bytes
				ii.ReadBytes(actual, 0, actual.Length);
				ii.Close();
				NUnit.Framework.Assert.AreEqual(new BytesRef(bytes), new BytesRef(actual));
				Directory.IndexInputSlicer slicer = mmapDir.CreateSlicer("bytes", NewIOContext(Random
					()));
				for (int sliceStart = 0; sliceStart < bytes.Length; sliceStart++)
				{
					for (int sliceLength = 0; sliceLength < bytes.Length - sliceStart; sliceLength++)
					{
						byte[] slice = new byte[sliceLength];
						IndexInput input = slicer.OpenSlice("bytesSlice", sliceStart, slice.Length);
						input.ReadBytes(slice, 0, slice.Length);
						input.Close();
						NUnit.Framework.Assert.AreEqual(new BytesRef(bytes, sliceStart, sliceLength), new 
							BytesRef(slice));
					}
				}
				slicer.Close();
				mmapDir.Close();
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestRandomChunkSizes()
		{
			int num = AtLeast(10);
			for (int i = 0; i < num; i++)
			{
				AssertChunking(Random(), TestUtil.NextInt(Random(), 20, 100));
			}
		}

		/// <exception cref="System.Exception"></exception>
		private void AssertChunking(Random random, int chunkSize)
		{
			FilePath path = CreateTempDir("mmap" + chunkSize);
			MMapDirectory mmapDir = new MMapDirectory(path, null, chunkSize);
			// we will map a lot, try to turn on the unmap hack
			if (MMapDirectory.UNMAP_SUPPORTED)
			{
				mmapDir.SetUseUnmap(true);
			}
			MockDirectoryWrapper dir = new MockDirectoryWrapper(random, mmapDir);
			RandomIndexWriter writer = new RandomIndexWriter(random, dir, NewIndexWriterConfig
				(TEST_VERSION_CURRENT, new MockAnalyzer(random)).SetMergePolicy(NewLogMergePolicy
				()));
			Lucene.Net.Document.Document doc = new Lucene.Net.Document.Document
				();
			Field docid = NewStringField("docid", "0", Field.Store.YES);
			Field junk = NewStringField("junk", string.Empty, Field.Store.YES);
			doc.Add(docid);
			doc.Add(junk);
			int numDocs = 100;
			for (int i = 0; i < numDocs; i++)
			{
				docid.SetStringValue(string.Empty + i);
				junk.SetStringValue(TestUtil.RandomUnicodeString(random));
				writer.AddDocument(doc);
			}
			IndexReader reader = writer.GetReader();
			writer.Close();
			int numAsserts = AtLeast(100);
			for (int i_1 = 0; i_1 < numAsserts; i_1++)
			{
				int docID = random.Next(numDocs);
				NUnit.Framework.Assert.AreEqual(string.Empty + docID, reader.Document(docID).Get(
					"docid"));
			}
			reader.Close();
			dir.Close();
		}
    }
}
