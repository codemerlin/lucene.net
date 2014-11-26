/*
 * This code is derived from MyJavaLibrary (http://somelinktomycoollibrary)
 * 
 * If this is an open source Java library, include the proper license and copyright attributions here!
 */

using System;
using System.Collections.Generic;
using System.IO;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Lucene.Net.Util.Packed;
using Sharpen;

namespace Lucene.Net.Util
{
	/// <summary>
	/// <see cref="Lucene.Net.Search.DocIdSet">Lucene.Net.Search.DocIdSet</see>
	/// implementation based on word-aligned hybrid encoding on
	/// words of 8 bits.
	/// <p>This implementation doesn't support random-access but has a fast
	/// <see cref="Lucene.Net.Search.DocIdSetIterator">Lucene.Net.Search.DocIdSetIterator
	/// 	</see>
	/// which can advance in logarithmic time thanks to
	/// an index.</p>
	/// <p>The compression scheme is simplistic and should work well with sparse and
	/// very dense doc id sets while being only slightly larger than a
	/// <see cref="FixedBitSet">FixedBitSet</see>
	/// for incompressible sets (overhead&lt;2% in the worst
	/// case) in spite of the index.</p>
	/// <p><b>Format</b>: The format is byte-aligned. An 8-bits word is either clean,
	/// meaning composed only of zeros or ones, or dirty, meaning that it contains
	/// between 1 and 7 bits set. The idea is to encode sequences of clean words
	/// using run-length encoding and to leave sequences of dirty words as-is.</p>
	/// <table>
	/// <tr><th>Token</th><th>Clean length+</th><th>Dirty length+</th><th>Dirty words</th></tr>
	/// <tr><td>1 byte</td><td>0-n bytes</td><td>0-n bytes</td><td>0-n bytes</td></tr>
	/// </table>
	/// <ul>
	/// <li><b>Token</b> encodes whether clean means full of zeros or ones in the
	/// first bit, the number of clean words minus 2 on the next 3 bits and the
	/// number of dirty words on the last 4 bits. The higher-order bit is a
	/// continuation bit, meaning that the number is incomplete and needs additional
	/// bytes to be read.</li>
	/// <li><b>Clean length+</b>: If clean length has its higher-order bit set,
	/// you need to read a
	/// <see cref="Lucene.Net.Store.DataInput.ReadVInt()">vint</see>
	/// , shift it by 3 bits on
	/// the left side and add it to the 3 bits which have been read in the token.</li>
	/// <li><b>Dirty length+</b> works the same way as <b>Clean length+</b> but
	/// on 4 bits and for the length of dirty words.</li>
	/// <li><b>Dirty words</b> are the dirty words, there are <b>Dirty length</b>
	/// of them.</li>
	/// </ul>
	/// <p>This format cannot encode sequences of less than 2 clean words and 0 dirty
	/// word. The reason is that if you find a single clean word, you should rather
	/// encode it as a dirty word. This takes the same space as starting a new
	/// sequence (since you need one byte for the token) but will be lighter to
	/// decode. There is however an exception for the first sequence. Since the first
	/// sequence may start directly with a dirty word, the clean length is encoded
	/// directly, without subtracting 2.</p>
	/// <p>There is an additional restriction on the format: the sequence of dirty
	/// words is not allowed to contain two consecutive clean words. This restriction
	/// exists to make sure no space is wasted and to make sure iterators can read
	/// the next doc ID by reading at most 2 dirty words.</p>
	/// </summary>
	/// <lucene.experimental></lucene.experimental>
	public sealed class WAH8DocIdSet : DocIdSet
	{
		private const int MIN_INDEX_INTERVAL = 8;

		/// <summary>Default index interval.</summary>
		/// <remarks>Default index interval.</remarks>
		public const int DEFAULT_INDEX_INTERVAL = 24;

		private static readonly MonotonicAppendingLongBuffer SINGLE_ZERO_BUFFER = new MonotonicAppendingLongBuffer
			(1, 64, PackedInts.COMPACT);

		private static Lucene.Net.Util.WAH8DocIdSet EMPTY = new Lucene.Net.Util.WAH8DocIdSet
			(new byte[0], 0, 1, SINGLE_ZERO_BUFFER, SINGLE_ZERO_BUFFER);

		static WAH8DocIdSet()
		{
			// Minimum index interval, intervals below this value can't guarantee anymore
			// that this set implementation won't be significantly larger than a FixedBitSet
			// The reason is that a single sequence saves at least one byte and an index
			// entry requires at most 8 bytes (2 ints) so there shouldn't be more than one
			// index entry every 8 sequences
			SINGLE_ZERO_BUFFER.Add(0L);
			SINGLE_ZERO_BUFFER.Freeze();
		}

		private sealed class _IComparer_97 : IComparer<WAH8DocIdSet.Iterator>
		{
			public _IComparer_97()
			{
			}

			public int Compare(WAH8DocIdSet.Iterator wi1, WAH8DocIdSet.Iterator wi2)
			{
				return wi1.@in.Length() - wi2.@in.Length();
			}
		}

		private static readonly IComparer<WAH8DocIdSet.Iterator> SERIALIZED_LENGTH_COMPARATOR
			 = new _IComparer_97();

		/// <summary>
		/// Same as
		/// <see cref="Intersect(System.Collections.Generic.ICollection{E}, int)">Intersect(System.Collections.Generic.ICollection&lt;E&gt;, int)
		/// 	</see>
		/// with the default index interval.
		/// </summary>
		public static Lucene.Net.Util.WAH8DocIdSet Intersect(ICollection<Lucene.Net.Util.WAH8DocIdSet
			> docIdSets)
		{
			return Intersect(docIdSets, DEFAULT_INDEX_INTERVAL);
		}

		/// <summary>Compute the intersection of the provided sets.</summary>
		/// <remarks>
		/// Compute the intersection of the provided sets. This method is much faster than
		/// computing the intersection manually since it operates directly at the byte level.
		/// </remarks>
		public static Lucene.Net.Util.WAH8DocIdSet Intersect(ICollection<Lucene.Net.Util.WAH8DocIdSet
			> docIdSets, int indexInterval)
		{
			switch (docIdSets.Count)
			{
				case 0:
				{
					throw new ArgumentException("There must be at least one set to intersect");
				}

				case 1:
				{
					return docIdSets.Iterator().Next();
				}
			}
			// The logic below is similar to ConjunctionScorer
			int numSets = docIdSets.Count;
			WAH8DocIdSet.Iterator[] iterators = new WAH8DocIdSet.Iterator[numSets];
			int i = 0;
			foreach (Lucene.Net.Util.WAH8DocIdSet set in docIdSets)
			{
				WAH8DocIdSet.Iterator it = ((WAH8DocIdSet.Iterator)set.Iterator());
				iterators[i++] = it;
			}
			Arrays.Sort(iterators, SERIALIZED_LENGTH_COMPARATOR);
			WAH8DocIdSet.WordBuilder builder = new WAH8DocIdSet.WordBuilder().SetIndexInterval
				(indexInterval);
			int wordNum = 0;
			while (true)
			{
				// Advance the least costly iterator first
				iterators[0].AdvanceWord(wordNum);
				wordNum = iterators[0].wordNum;
				if (wordNum == DocIdSetIterator.NO_MORE_DOCS)
				{
					break;
				}
				byte word = iterators[0].word;
				for (i = 1; i < numSets; ++i)
				{
					if (iterators[i].wordNum < wordNum)
					{
						iterators[i].AdvanceWord(wordNum);
					}
					if (iterators[i].wordNum > wordNum)
					{
						wordNum = iterators[i].wordNum;
						goto main_continue;
					}
					//HM:revisit 
					//assert iterators[i].wordNum == wordNum;
					word &= iterators[i].word;
					if (word == 0)
					{
						// There are common words, but they don't share any bit
						++wordNum;
						goto main_continue;
					}
				}
				// Found a common word
				//HM:revisit 
				//assert word != 0;
				builder.AddWord(wordNum, word);
				++wordNum;
main_continue: ;
			}
main_break: ;
			return builder.Build();
		}

		/// <summary>
		/// Same as
		/// <see cref="Union(System.Collections.Generic.ICollection{E}, int)">Union(System.Collections.Generic.ICollection&lt;E&gt;, int)
		/// 	</see>
		/// with the default index interval.
		/// </summary>
		public static Lucene.Net.Util.WAH8DocIdSet Union(ICollection<Lucene.Net.Util.WAH8DocIdSet
			> docIdSets)
		{
			return Union(docIdSets, DEFAULT_INDEX_INTERVAL);
		}

		/// <summary>Compute the union of the provided sets.</summary>
		/// <remarks>
		/// Compute the union of the provided sets. This method is much faster than
		/// computing the union manually since it operates directly at the byte level.
		/// </remarks>
		public static Lucene.Net.Util.WAH8DocIdSet Union(ICollection<Lucene.Net.Util.WAH8DocIdSet
			> docIdSets, int indexInterval)
		{
			switch (docIdSets.Count)
			{
				case 0:
				{
					return EMPTY;
				}

				case 1:
				{
					return docIdSets.Iterator().Next();
				}
			}
			// The logic below is very similar to DisjunctionScorer
			int numSets = docIdSets.Count;
			PriorityQueue<WAH8DocIdSet.Iterator> iterators = new _PriorityQueue_186(numSets);
			foreach (Lucene.Net.Util.WAH8DocIdSet set in docIdSets)
			{
				WAH8DocIdSet.Iterator iterator = ((WAH8DocIdSet.Iterator)set.Iterator());
				iterator.NextWord();
				iterators.Add(iterator);
			}
			WAH8DocIdSet.Iterator top = iterators.Top();
			if (top.wordNum == int.MaxValue)
			{
				return EMPTY;
			}
			int wordNum = top.wordNum;
			byte word = top.word;
			WAH8DocIdSet.WordBuilder builder = new WAH8DocIdSet.WordBuilder().SetIndexInterval
				(indexInterval);
			while (true)
			{
				top.NextWord();
				iterators.UpdateTop();
				top = iterators.Top();
				if (top.wordNum == wordNum)
				{
					word |= top.word;
				}
				else
				{
					builder.AddWord(wordNum, word);
					if (top.wordNum == int.MaxValue)
					{
						break;
					}
					wordNum = top.wordNum;
					word = top.word;
				}
			}
			return builder.Build();
		}

		private sealed class _PriorityQueue_186 : PriorityQueue<WAH8DocIdSet.Iterator>
		{
			public _PriorityQueue_186(int baseArg1) : base(baseArg1)
			{
			}

			protected internal override bool LessThan(WAH8DocIdSet.Iterator a, WAH8DocIdSet.Iterator
				 b)
			{
				return a.wordNum < b.wordNum;
			}
		}

		internal static int WordNum(int docID)
		{
			//HM:revisit 
			//assert docID >= 0;
			return (int)(((uint)docID) >> 3);
		}

		/// <summary>Word-based builder.</summary>
		/// <remarks>Word-based builder.</remarks>
		internal class WordBuilder
		{
			internal readonly GrowableByteArrayDataOutput @out;

			internal readonly GrowableByteArrayDataOutput dirtyWords;

			internal int clean;

			internal int lastWordNum;

			internal int numSequences;

			internal int indexInterval;

			internal int cardinality;

			internal bool reverse;

			public WordBuilder()
			{
				@out = new GrowableByteArrayDataOutput(1024);
				dirtyWords = new GrowableByteArrayDataOutput(128);
				clean = 0;
				lastWordNum = -1;
				numSequences = 0;
				indexInterval = DEFAULT_INDEX_INTERVAL;
				cardinality = 0;
			}

			/// <summary>Set the index interval.</summary>
			/// <remarks>
			/// Set the index interval. Smaller index intervals improve performance of
			/// <see cref="Lucene.Net.Search.DocIdSetIterator.Advance(int)">Lucene.Net.Search.DocIdSetIterator.Advance(int)
			/// 	</see>
			/// but make the
			/// <see cref="Lucene.Net.Search.DocIdSet">Lucene.Net.Search.DocIdSet</see>
			/// larger. An index interval <code>i</code> makes the index add an overhead
			/// which is at most <code>4/i</code>, but likely much less.The default index
			/// interval is <code>8</code>, meaning the index has an overhead of at most
			/// 50%. To disable indexing, you can pass
			/// <see cref="int.MaxValue">int.MaxValue</see>
			/// as an
			/// index interval.
			/// </remarks>
			public virtual WAH8DocIdSet.WordBuilder SetIndexInterval(int indexInterval)
			{
				if (indexInterval < MIN_INDEX_INTERVAL)
				{
					throw new ArgumentException("indexInterval must be >= " + MIN_INDEX_INTERVAL);
				}
				this.indexInterval = indexInterval;
				return this;
			}

			/// <exception cref="System.IO.IOException"></exception>
			internal virtual void WriteHeader(bool reverse, int cleanLength, int dirtyLength)
			{
				int cleanLengthMinus2 = cleanLength - 2;
				//HM:revisit 
				//assert cleanLengthMinus2 >= 0;
				//HM:revisit 
				//assert dirtyLength >= 0;
				int token = ((cleanLengthMinus2 & unchecked((int)(0x03))) << 4) | (dirtyLength & 
					unchecked((int)(0x07)));
				if (reverse)
				{
					token |= 1 << 7;
				}
				if (cleanLengthMinus2 > unchecked((int)(0x03)))
				{
					token |= 1 << 6;
				}
				if (dirtyLength > unchecked((int)(0x07)))
				{
					token |= 1 << 3;
				}
				@out.WriteByte(unchecked((byte)token));
				if (cleanLengthMinus2 > unchecked((int)(0x03)))
				{
					@out.WriteVInt((int)(((uint)cleanLengthMinus2) >> 2));
				}
				if (dirtyLength > unchecked((int)(0x07)))
				{
					@out.WriteVInt((int)(((uint)dirtyLength) >> 3));
				}
			}

			private bool SequenceIsConsistent()
			{
				for (int i = 1; i < dirtyWords.length; ++i)
				{
				}
				//HM:revisit 
				//assert dirtyWords.bytes[i-1] != 0 || dirtyWords.bytes[i] != 0;
				//HM:revisit 
				//assert dirtyWords.bytes[i-1] != (byte) 0xFF || dirtyWords.bytes[i] != (byte) 0xFF;
				return true;
			}

			internal virtual void WriteSequence()
			{
				//HM:revisit 
				//assert sequenceIsConsistent();
				try
				{
					WriteHeader(reverse, clean, dirtyWords.length);
				}
				catch (IOException cannotHappen)
				{
					throw new Exception(cannotHappen);
				}
				@out.WriteBytes(dirtyWords.bytes, 0, dirtyWords.length);
				dirtyWords.length = 0;
				++numSequences;
			}

			internal virtual void AddWord(int wordNum, byte word)
			{
				//HM:revisit 
				//assert wordNum > lastWordNum;
				//HM:revisit 
				//assert word != 0;
				if (!reverse)
				{
					if (lastWordNum == -1)
					{
						clean = 2 + wordNum;
						// special case for the 1st sequence
						dirtyWords.WriteByte(word);
					}
					else
					{
						switch (wordNum - lastWordNum)
						{
							case 1:
							{
								if (word == unchecked((byte)unchecked((int)(0xFF))) && dirtyWords.bytes[dirtyWords
									.length - 1] == unchecked((byte)unchecked((int)(0xFF))))
								{
									--dirtyWords.length;
									WriteSequence();
									reverse = true;
									clean = 2;
								}
								else
								{
									dirtyWords.WriteByte(word);
								}
								break;
							}

							case 2:
							{
								dirtyWords.WriteByte(unchecked((byte)0));
								dirtyWords.WriteByte(word);
								break;
							}

							default:
							{
								WriteSequence();
								clean = wordNum - lastWordNum - 1;
								dirtyWords.WriteByte(word);
								break;
							}
						}
					}
				}
				else
				{
					switch (wordNum - lastWordNum)
					{
						case 1:
						{
							//HM:revisit 
							//assert lastWordNum >= 0;
							if (word == unchecked((byte)unchecked((int)(0xFF))))
							{
								if (dirtyWords.length == 0)
								{
									++clean;
								}
								else
								{
									if (dirtyWords.bytes[dirtyWords.length - 1] == unchecked((byte)unchecked((int)(0xFF
										))))
									{
										--dirtyWords.length;
										WriteSequence();
										clean = 2;
									}
									else
									{
										dirtyWords.WriteByte(word);
									}
								}
							}
							else
							{
								dirtyWords.WriteByte(word);
							}
							break;
						}

						case 2:
						{
							dirtyWords.WriteByte(unchecked((byte)0));
							dirtyWords.WriteByte(word);
							break;
						}

						default:
						{
							WriteSequence();
							reverse = false;
							clean = wordNum - lastWordNum - 1;
							dirtyWords.WriteByte(word);
							break;
						}
					}
				}
				lastWordNum = wordNum;
				cardinality += BitUtil.BitCount(word);
			}

			/// <summary>
			/// Build a new
			/// <see cref="WAH8DocIdSet">WAH8DocIdSet</see>
			/// .
			/// </summary>
			public virtual WAH8DocIdSet Build()
			{
				if (cardinality == 0)
				{
					//HM:revisit 
					//assert lastWordNum == -1;
					return EMPTY;
				}
				WriteSequence();
				byte[] data = Arrays.CopyOf(@out.bytes, @out.length);
				// Now build the index
				int valueCount = (numSequences - 1) / indexInterval + 1;
				MonotonicAppendingLongBuffer indexPositions;
				MonotonicAppendingLongBuffer indexWordNums;
				if (valueCount <= 1)
				{
					indexPositions = indexWordNums = SINGLE_ZERO_BUFFER;
				}
				else
				{
					int pageSize = 128;
					int initialPageCount = (valueCount + pageSize - 1) / pageSize;
					MonotonicAppendingLongBuffer positions = new MonotonicAppendingLongBuffer(initialPageCount
						, pageSize, PackedInts.COMPACT);
					MonotonicAppendingLongBuffer wordNums = new MonotonicAppendingLongBuffer(initialPageCount
						, pageSize, PackedInts.COMPACT);
					positions.Add(0L);
					wordNums.Add(0L);
					WAH8DocIdSet.Iterator it = new WAH8DocIdSet.Iterator(data, cardinality, int.MaxValue
						, SINGLE_ZERO_BUFFER, SINGLE_ZERO_BUFFER);
					//HM:revisit 
					//assert it.in.getPosition() == 0;
					//HM:revisit 
					//assert it.wordNum == -1;
					for (int i = 1; i < valueCount; ++i)
					{
						// skip indexInterval sequences
						for (int j = 0; j < indexInterval; ++j)
						{
							bool readSequence = it.ReadSequence();
							//HM:revisit 
							//assert readSequence;
							it.SkipDirtyBytes();
						}
						int position = it.@in.GetPosition();
						int wordNum = it.wordNum;
						positions.Add(position);
						wordNums.Add(wordNum + 1);
					}
					positions.Freeze();
					wordNums.Freeze();
					indexPositions = positions;
					indexWordNums = wordNums;
				}
				return new WAH8DocIdSet(data, cardinality, indexInterval, indexPositions, indexWordNums
					);
			}
		}

		/// <summary>
		/// A builder for
		/// <see cref="WAH8DocIdSet">WAH8DocIdSet</see>
		/// s.
		/// </summary>
		public sealed class Builder : WAH8DocIdSet.WordBuilder
		{
			private int lastDocID;

			private int wordNum;

			private int word;

			/// <summary>Sole constructor</summary>
			public Builder() : base()
			{
				lastDocID = -1;
				wordNum = -1;
				word = 0;
			}

			/// <summary>Add a document to this builder.</summary>
			/// <remarks>Add a document to this builder. Documents must be added in order.</remarks>
			public WAH8DocIdSet.Builder Add(int docID)
			{
				if (docID <= lastDocID)
				{
					throw new ArgumentException("Doc ids must be added in-order, got " + docID + " which is <= lastDocID="
						 + lastDocID);
				}
				int wordNum = WordNum(docID);
				if (this.wordNum == -1)
				{
					this.wordNum = wordNum;
					word = 1 << (docID & unchecked((int)(0x07)));
				}
				else
				{
					if (wordNum == this.wordNum)
					{
						word |= 1 << (docID & unchecked((int)(0x07)));
					}
					else
					{
						AddWord(this.wordNum, unchecked((byte)word));
						this.wordNum = wordNum;
						word = 1 << (docID & unchecked((int)(0x07)));
					}
				}
				lastDocID = docID;
				return this;
			}

			/// <summary>
			/// Add the content of the provided
			/// <see cref="Lucene.Net.Search.DocIdSetIterator">Lucene.Net.Search.DocIdSetIterator
			/// 	</see>
			/// .
			/// </summary>
			/// <exception cref="System.IO.IOException"></exception>
			public WAH8DocIdSet.Builder Add(DocIdSetIterator disi)
			{
				for (int doc = disi.NextDoc(); doc != DocIdSetIterator.NO_MORE_DOCS; doc = disi.NextDoc
					())
				{
					Add(doc);
				}
				return this;
			}

			public override WAH8DocIdSet.WordBuilder SetIndexInterval(int indexInterval)
			{
				return (WAH8DocIdSet.Builder)base.SetIndexInterval(indexInterval);
			}

			public override WAH8DocIdSet Build()
			{
				if (this.wordNum != -1)
				{
					AddWord(wordNum, unchecked((byte)word));
				}
				return base.Build();
			}
		}

		private readonly byte[] data;

		private readonly int cardinality;

		private readonly int indexInterval;

		private readonly MonotonicAppendingLongBuffer positions;

		private readonly MonotonicAppendingLongBuffer wordNums;

		internal WAH8DocIdSet(byte[] data, int cardinality, int indexInterval, MonotonicAppendingLongBuffer
			 positions, MonotonicAppendingLongBuffer wordNums)
		{
			// where the doc IDs are stored
			// index for advance(int)
			// wordNums[i] starts at the sequence at positions[i]
			this.data = data;
			this.cardinality = cardinality;
			this.indexInterval = indexInterval;
			this.positions = positions;
			this.wordNums = wordNums;
		}

		public override bool IsCacheable()
		{
			return true;
		}

		public override DocIdSetIterator Iterator()
		{
			return new WAH8DocIdSet.Iterator(data, cardinality, indexInterval, positions, wordNums
				);
		}

		internal static int ReadCleanLength(ByteArrayDataInput @in, int token)
		{
			int len = ((int)(((uint)token) >> 4)) & unchecked((int)(0x07));
			int startPosition = @in.GetPosition();
			if ((len & unchecked((int)(0x04))) != 0)
			{
				len = (len & unchecked((int)(0x03))) | (@in.ReadVInt() << 2);
			}
			if (startPosition != 1)
			{
				len += 2;
			}
			return len;
		}

		internal static int ReadDirtyLength(ByteArrayDataInput @in, int token)
		{
			int len = token & unchecked((int)(0x0F));
			if ((len & unchecked((int)(0x08))) != 0)
			{
				len = (len & unchecked((int)(0x07))) | (@in.ReadVInt() << 3);
			}
			return len;
		}

		internal class Iterator : DocIdSetIterator
		{
			internal static int IndexThreshold(int cardinality, int indexInterval)
			{
				// Short sequences encode for 3 words (2 clean words and 1 dirty byte),
				// don't advance if we are going to read less than 3 x indexInterval
				// sequences
				long indexThreshold = 3L * 3 * indexInterval;
				return (int)Math.Min(int.MaxValue, indexThreshold);
			}

			internal readonly ByteArrayDataInput @in;

			internal readonly int cardinality;

			internal readonly int indexInterval;

			internal readonly MonotonicAppendingLongBuffer positions;

			internal readonly MonotonicAppendingLongBuffer wordNums;

			internal readonly int indexThreshold;

			internal int allOnesLength;

			internal int dirtyLength;

			internal int wordNum;

			internal byte word;

			internal int bitList;

			internal int sequenceNum;

			internal int docID;

			internal Iterator(byte[] data, int cardinality, int indexInterval, MonotonicAppendingLongBuffer
				 positions, MonotonicAppendingLongBuffer wordNums)
			{
				// byte offset
				// current word
				// list of bits set in the current word
				// in which sequence are we?
				this.@in = new ByteArrayDataInput(data);
				this.cardinality = cardinality;
				this.indexInterval = indexInterval;
				this.positions = positions;
				this.wordNums = wordNums;
				wordNum = -1;
				word = 0;
				bitList = 0;
				sequenceNum = -1;
				docID = -1;
				indexThreshold = IndexThreshold(cardinality, indexInterval);
			}

			internal virtual bool ReadSequence()
			{
				if (@in.Eof())
				{
					wordNum = int.MaxValue;
					return false;
				}
				int token = @in.ReadByte() & unchecked((int)(0xFF));
				if ((token & (1 << 7)) == 0)
				{
					int cleanLength = ReadCleanLength(@in, token);
					wordNum += cleanLength;
				}
				else
				{
					allOnesLength = ReadCleanLength(@in, token);
				}
				dirtyLength = ReadDirtyLength(@in, token);
				//HM:revisit 
				//assert in.length() - in.getPosition() >= dirtyLength : in.getPosition() + " " + in.length() + " " + dirtyLength;
				++sequenceNum;
				return true;
			}

			internal virtual void SkipDirtyBytes(int count)
			{
				//HM:revisit 
				//assert count >= 0;
				//HM:revisit 
				//assert count <= allOnesLength + dirtyLength;
				wordNum += count;
				if (count <= allOnesLength)
				{
					allOnesLength -= count;
				}
				else
				{
					count -= allOnesLength;
					allOnesLength = 0;
					@in.SkipBytes(count);
					dirtyLength -= count;
				}
			}

			internal virtual void SkipDirtyBytes()
			{
				wordNum += allOnesLength + dirtyLength;
				@in.SkipBytes(dirtyLength);
				allOnesLength = 0;
				dirtyLength = 0;
			}

			internal virtual void NextWord()
			{
				if (allOnesLength > 0)
				{
					word = unchecked((byte)unchecked((int)(0xFF)));
					++wordNum;
					--allOnesLength;
					return;
				}
				if (dirtyLength > 0)
				{
					word = @in.ReadByte();
					++wordNum;
					--dirtyLength;
					if (word != 0)
					{
						return;
					}
					if (dirtyLength > 0)
					{
						word = @in.ReadByte();
						++wordNum;
						--dirtyLength;
						//HM:revisit 
						//assert word != 0; // never more than one consecutive 0
						return;
					}
				}
				if (ReadSequence())
				{
					NextWord();
				}
			}

			internal virtual int ForwardBinarySearch(int targetWordNum)
			{
				// advance forward and double the window at each step
				int indexSize = (int)wordNums.Size();
				int lo = sequenceNum / indexInterval;
				int hi = lo + 1;
				//HM:revisit 
				//assert sequenceNum == -1 || wordNums.get(lo) <= wordNum;
				//HM:revisit 
				//assert lo + 1 == wordNums.size() || wordNums.get(lo + 1) > wordNum;
				while (true)
				{
					if (hi >= indexSize)
					{
						hi = indexSize - 1;
						break;
					}
					else
					{
						if (wordNums.Get(hi) >= targetWordNum)
						{
							break;
						}
					}
					int newLo = hi;
					hi += (hi - lo) << 1;
					lo = newLo;
				}
				// we found a window containing our target, let's binary search now
				while (lo <= hi)
				{
					int mid = (int)(((uint)(lo + hi)) >> 1);
					int midWordNum = (int)wordNums.Get(mid);
					if (midWordNum <= targetWordNum)
					{
						lo = mid + 1;
					}
					else
					{
						hi = mid - 1;
					}
				}
				//HM:revisit 
				//assert wordNums.get(hi) <= targetWordNum;
				//HM:revisit 
				//assert hi+1 == wordNums.size() || wordNums.get(hi + 1) > targetWordNum;
				return hi;
			}

			internal virtual void AdvanceWord(int targetWordNum)
			{
				//HM:revisit 
				//assert targetWordNum > wordNum;
				int delta = targetWordNum - wordNum;
				if (delta <= allOnesLength + dirtyLength + 1)
				{
					SkipDirtyBytes(delta - 1);
				}
				else
				{
					SkipDirtyBytes();
					//HM:revisit 
					//assert dirtyLength == 0;
					if (delta > indexThreshold)
					{
						// use the index
						int i = ForwardBinarySearch(targetWordNum);
						int position = (int)positions.Get(i);
						if (position > @in.GetPosition())
						{
							// if the binary search returned a backward offset, don't move
							wordNum = (int)wordNums.Get(i) - 1;
							@in.SetPosition(position);
							sequenceNum = i * indexInterval - 1;
						}
					}
					while (true)
					{
						if (!ReadSequence())
						{
							return;
						}
						delta = targetWordNum - wordNum;
						if (delta <= allOnesLength + dirtyLength + 1)
						{
							if (delta > 1)
							{
								SkipDirtyBytes(delta - 1);
							}
							break;
						}
						SkipDirtyBytes();
					}
				}
				NextWord();
			}

			public override int DocID()
			{
				return docID;
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override int NextDoc()
			{
				if (bitList != 0)
				{
					// there are remaining bits in the current word
					docID = (wordNum << 3) | ((bitList & unchecked((int)(0x0F))) - 1);
					bitList = (int)(((uint)bitList) >> 4);
					return docID;
				}
				NextWord();
				if (wordNum == int.MaxValue)
				{
					return docID = NO_MORE_DOCS;
				}
				bitList = BitUtil.BitList(word);
				//HM:revisit 
				//assert bitList != 0;
				docID = (wordNum << 3) | ((bitList & unchecked((int)(0x0F))) - 1);
				bitList = (int)(((uint)bitList) >> 4);
				return docID;
			}

			/// <exception cref="System.IO.IOException"></exception>
			public override int Advance(int target)
			{
				//HM:revisit 
				//assert target > docID;
				int targetWordNum = WordNum(target);
				if (targetWordNum > wordNum)
				{
					AdvanceWord(targetWordNum);
					bitList = BitUtil.BitList(word);
				}
				return SlowAdvance(target);
			}

			public override long Cost()
			{
				return cardinality;
			}
		}

		/// <summary>
		/// Return the number of documents in this
		/// <see cref="Lucene.Net.Search.DocIdSet">Lucene.Net.Search.DocIdSet</see>
		/// in constant time.
		/// </summary>
		public int Cardinality()
		{
			return cardinality;
		}

		/// <summary>Return the memory usage of this class in bytes.</summary>
		/// <remarks>Return the memory usage of this class in bytes.</remarks>
		public long RamBytesUsed()
		{
			return RamUsageEstimator.AlignObjectSize(3 * RamUsageEstimator.NUM_BYTES_OBJECT_REF
				 + 2 * RamUsageEstimator.NUM_BYTES_INT) + RamUsageEstimator.SizeOf(data) + positions
				.RamBytesUsed() + wordNums.RamBytesUsed();
		}
	}
}
