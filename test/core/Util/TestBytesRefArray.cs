/*
 * This code is derived from MyJavaLibrary (http://somelinktomycoollibrary)
 * 
 * If this is an open source Java library, include the proper license and copyright attributions here!
 */

using System.Collections.Generic;
using Lucene.Net.Util;
using Sharpen;

namespace Lucene.Net.Util
{
	public class TestBytesRefArray : LuceneTestCase
	{
		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestAppend()
		{
			Random random = Random();
			BytesRefArray list = new BytesRefArray(Counter.NewCounter());
			IList<string> stringList = new AList<string>();
			for (int j = 0; j < 2; j++)
			{
				if (j > 0 && random.NextBoolean())
				{
					list.Clear();
					stringList.Clear();
				}
				int entries = AtLeast(500);
				BytesRef spare = new BytesRef();
				int initSize = list.Size();
				for (int i = 0; i < entries; i++)
				{
					string randomRealisticUnicodeString = TestUtil.RandomRealisticUnicodeString(random
						);
					spare.CopyChars(randomRealisticUnicodeString);
					NUnit.Framework.Assert.AreEqual(i + initSize, list.Append(spare));
					stringList.AddItem(randomRealisticUnicodeString);
				}
				for (int i_1 = 0; i_1 < entries; i_1++)
				{
					NUnit.Framework.Assert.IsNotNull(list.Get(spare, i_1));
					NUnit.Framework.Assert.AreEqual("entry " + i_1 + " doesn't match", stringList[i_1
						], spare.Utf8ToString());
				}
				// check random
				for (int i_2 = 0; i_2 < entries; i_2++)
				{
					int e = random.Next(entries);
					NUnit.Framework.Assert.IsNotNull(list.Get(spare, e));
					NUnit.Framework.Assert.AreEqual("entry " + i_2 + " doesn't match", stringList[e], 
						spare.Utf8ToString());
				}
				for (int i_3 = 0; i_3 < 2; i_3++)
				{
					BytesRefIterator iterator = list.Iterator();
					foreach (string @string in stringList)
					{
						NUnit.Framework.Assert.AreEqual(@string, iterator.Next().Utf8ToString());
					}
				}
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void TestSort()
		{
			Random random = Random();
			BytesRefArray list = new BytesRefArray(Counter.NewCounter());
			IList<string> stringList = new AList<string>();
			for (int j = 0; j < 2; j++)
			{
				if (j > 0 && random.NextBoolean())
				{
					list.Clear();
					stringList.Clear();
				}
				int entries = AtLeast(500);
				BytesRef spare = new BytesRef();
				int initSize = list.Size();
				for (int i = 0; i < entries; i++)
				{
					string randomRealisticUnicodeString = TestUtil.RandomRealisticUnicodeString(random
						);
					spare.CopyChars(randomRealisticUnicodeString);
					NUnit.Framework.Assert.AreEqual(initSize + i, list.Append(spare));
					stringList.AddItem(randomRealisticUnicodeString);
				}
				stringList.Sort();
				BytesRefIterator iter = list.Iterator(BytesRef.GetUTF8SortedAsUTF16Comparator());
				int i_1 = 0;
				while ((spare = iter.Next()) != null)
				{
					NUnit.Framework.Assert.AreEqual("entry " + i_1 + " doesn't match", stringList[i_1
						], spare.Utf8ToString());
					i_1++;
				}
				NUnit.Framework.Assert.IsNull(iter.Next());
				NUnit.Framework.Assert.AreEqual(i_1, stringList.Count);
			}
		}
	}
}
