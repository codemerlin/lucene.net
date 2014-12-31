/*
 * This code is derived from MyJavaLibrary (http://somelinktomycoollibrary)
 * 
 * If this is an open source Java library, include the proper license and copyright attributions here!
 */

using Lucene.Net.Util;


namespace Lucene.Net.Util
{
	public class TestBroadWord : LuceneTestCase
	{
		private void TstRank(long x)
		{
			AreEqual("rank(" + x + ")", long.BitCount(x), BroadWord.BitCount
				(x));
		}

		public virtual void TestRank1()
		{
			TstRank(0L);
			TstRank(1L);
			TstRank(3L);
			TstRank(unchecked((long)(0x100L)));
			TstRank(unchecked((long)(0x300L)));
			TstRank(unchecked((long)(0x8000000000000001L)));
		}

		private void TstSelect(long x, int r, int exp)
		{
			AreEqual("selectNaive(" + x + "," + r + ")", exp, BroadWord
				.SelectNaive(x, r));
			AreEqual("select(" + x + "," + r + ")", exp, BroadWord.Select
				(x, r));
		}

		public virtual void TestSelectFromZero()
		{
			TstSelect(0L, 1, 72);
		}

		public virtual void TestSelectSingleBit()
		{
			for (int i = 0; i < 64; i++)
			{
				TstSelect((1L << i), 1, i);
			}
		}

		public virtual void TestSelectTwoBits()
		{
			for (int i = 0; i < 64; i++)
			{
				for (int j = i + 1; j < 64; j++)
				{
					long x = (1L << i) | (1L << j);
					//System.out.println(getName() + " i: " + i + " j: " + j);
					TstSelect(x, 1, i);
					TstSelect(x, 2, j);
					TstSelect(x, 3, 72);
				}
			}
		}

		public virtual void TestSelectThreeBits()
		{
			for (int i = 0; i < 64; i++)
			{
				for (int j = i + 1; j < 64; j++)
				{
					for (int k = j + 1; k < 64; k++)
					{
						long x = (1L << i) | (1L << j) | (1L << k);
						TstSelect(x, 1, i);
						TstSelect(x, 2, j);
						TstSelect(x, 3, k);
						TstSelect(x, 4, 72);
					}
				}
			}
		}

		public virtual void TestSelectAllBits()
		{
			for (int i = 0; i < 64; i++)
			{
				TstSelect(unchecked((long)(0xFFFFFFFFFFFFFFFFL)), i + 1, i);
			}
		}

		public virtual void TestPerfSelectAllBitsBroad()
		{
			for (int j = 0; j < 100000; j++)
			{
				// 1000000 for real perf test
				for (int i = 0; i < 64; i++)
				{
					AreEqual(i, BroadWord.Select(unchecked((long)(0xFFFFFFFFFFFFFFFFL
						)), i + 1));
				}
			}
		}

		public virtual void TestPerfSelectAllBitsNaive()
		{
			for (int j = 0; j < 10000; j++)
			{
				// real perftest: 1000000
				for (int i = 0; i < 64; i++)
				{
					AreEqual(i, BroadWord.SelectNaive(unchecked((long)(0xFFFFFFFFFFFFFFFFL
						)), i + 1));
				}
			}
		}

		public virtual void TestSmalleru_87_01()
		{
			// 0 <= arguments < 2 ** (k-1), k=8, see paper
			for (long i = unchecked((long)(0x0L)); i <= unchecked((long)(0x7FL)); i++)
			{
				for (long j = unchecked((long)(0x0L)); i <= unchecked((long)(0x7FL)); i++)
				{
					long ii = i * BroadWord.L8_L;
					long jj = j * BroadWord.L8_L;
					AreEqual(ToStringUtils.LongHex(ii) + " < " + ToStringUtils
						.LongHex(jj), ToStringUtils.LongHex((i < j) ? (unchecked((long)(0x80L)) * BroadWord
						.L8_L) : unchecked((long)(0x0L))), ToStringUtils.LongHex(BroadWord.SmallerUpTo7_8
						(ii, jj)));
				}
			}
		}

		public virtual void TestSmalleru_8_01()
		{
			// 0 <= arguments < 2 ** k, k=8, see paper
			for (long i = unchecked((long)(0x0L)); i <= unchecked((long)(0xFFL)); i++)
			{
				for (long j = unchecked((long)(0x0L)); i <= unchecked((long)(0xFFL)); i++)
				{
					long ii = i * BroadWord.L8_L;
					long jj = j * BroadWord.L8_L;
					AreEqual(ToStringUtils.LongHex(ii) + " < " + ToStringUtils
						.LongHex(jj), ToStringUtils.LongHex((i < j) ? (unchecked((long)(0x80L)) * BroadWord
						.L8_L) : unchecked((long)(0x0L))), ToStringUtils.LongHex(BroadWord.Smalleru_8(ii
						, jj)));
				}
			}
		}

		public virtual void TestNotEquals0_8()
		{
			// 0 <= arguments < 2 ** k, k=8, see paper
			for (long i = unchecked((long)(0x0L)); i <= unchecked((long)(0xFFL)); i++)
			{
				long ii = i * BroadWord.L8_L;
				AreEqual(ToStringUtils.LongHex(ii) + " <> 0", ToStringUtils
					.LongHex((i != 0L) ? (unchecked((long)(0x80L)) * BroadWord.L8_L) : unchecked((long
					)(0x0L))), ToStringUtils.LongHex(BroadWord.NotEquals0_8(ii)));
			}
		}
	}
}
