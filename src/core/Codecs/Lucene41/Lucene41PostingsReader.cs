﻿using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Support;
using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lucene.Net.Codecs.Lucene41
{
    public sealed class Lucene41PostingsReader : PostingsReaderBase
    {
        private readonly IndexInput docIn;
        private readonly IndexInput posIn;
        private readonly IndexInput payIn;

        private readonly ForUtil forUtil;

		private int version;
        public Lucene41PostingsReader(Directory dir, FieldInfos fieldInfos, SegmentInfo segmentInfo, IOContext ioContext, String segmentSuffix)
        {
            bool success = false;
            IndexInput docIn = null;
            IndexInput posIn = null;
            IndexInput payIn = null;
            try
            {
                docIn = dir.OpenInput(IndexFileNames.SegmentFileName(segmentInfo.name, segmentSuffix, Lucene41PostingsFormat.DOC_EXTENSION),
                                      ioContext);
				version = CodecUtil.CheckHeader(docIn, Lucene41PostingsWriter.DOC_CODEC, Lucene41PostingsWriter
					.VERSION_START, Lucene41PostingsWriter.VERSION_CURRENT);
                forUtil = new ForUtil(docIn);

                if (fieldInfos.HasProx)
                {
                    posIn = dir.OpenInput(IndexFileNames.SegmentFileName(segmentInfo.name, segmentSuffix, Lucene41PostingsFormat.POS_EXTENSION),
                                          ioContext);
					CodecUtil.CheckHeader(posIn, Lucene41PostingsWriter.POS_CODEC, version, version);

                    if (fieldInfos.HasPayloads || fieldInfos.HasOffsets)
                    {
                        payIn = dir.OpenInput(IndexFileNames.SegmentFileName(segmentInfo.name, segmentSuffix, Lucene41PostingsFormat.PAY_EXTENSION),
                                              ioContext);
						CodecUtil.CheckHeader(payIn, Lucene41PostingsWriter.PAY_CODEC, version, version);
                    }
                }

                this.docIn = docIn;
                this.posIn = posIn;
                this.payIn = payIn;
                success = true;
            }
            finally
            {
                if (!success)
                {
                    IOUtils.CloseWhileHandlingException((IDisposable)docIn, posIn, payIn);
                }
            }
        }

        public override void Init(IndexInput termsIn)
        {
            // Make sure we are talking to the matching postings writer
			CodecUtil.CheckHeader(termsIn, Lucene41PostingsWriter.TERMS_CODEC, Lucene41PostingsWriter
				.VERSION_START, Lucene41PostingsWriter.VERSION_CURRENT);
            int indexBlockSize = termsIn.ReadVInt();
            if (indexBlockSize != Lucene41PostingsFormat.BLOCK_SIZE)
            {
                throw new InvalidOperationException("index-time BLOCK_SIZE (" + indexBlockSize + ") != read-time BLOCK_SIZE (" + Lucene41PostingsFormat.BLOCK_SIZE + ")");
            }
        }

        internal static void ReadVIntBlock(IndexInput docIn, int[] docBuffer, int[] freqBuffer, int num, bool indexHasFreq)
        {
            if (indexHasFreq)
            {
                for (int i = 0; i < num; i++)
                {
                    int code = docIn.ReadVInt();
                    docBuffer[i] = Number.URShift(code, 1);
                    if ((code & 1) != 0)
                    {
                        freqBuffer[i] = 1;
                    }
                    else
                    {
                        freqBuffer[i] = docIn.ReadVInt();
                    }
                }
            }
            else
            {
                for (int i = 0; i < num; i++)
                {
                    docBuffer[i] = docIn.ReadVInt();
                }
            }
        }

        internal sealed class IntBlockTermState : BlockTermState
        {
            internal long docStartFP;
            internal long posStartFP;
            internal long payStartFP;
            internal long skipOffset;
            internal long lastPosBlockOffset;
            // docid when there is a single pulsed posting, otherwise -1
            // freq is always implicitly totalTermFreq in this case.
            internal int singletonDocID;

            // Only used by the "primary" TermState -- clones don't
            // copy this (basically they are "transient"):
            internal ByteArrayDataInput bytesReader;  // TODO: should this NOT be in the TermState...?
            internal byte[] bytes;

            public override object Clone()
            {
                IntBlockTermState other = new IntBlockTermState();
                other.CopyFrom(this);
                return other;
            }

            public override void CopyFrom(TermState _other)
            {
                base.CopyFrom(_other);
                IntBlockTermState other = (IntBlockTermState)_other;
                docStartFP = other.docStartFP;
                posStartFP = other.posStartFP;
                payStartFP = other.payStartFP;
                lastPosBlockOffset = other.lastPosBlockOffset;
                skipOffset = other.skipOffset;
                singletonDocID = other.singletonDocID;

                // Do not copy bytes, bytesReader (else TermState is
                // very heavy, ie drags around the entire block's
                // byte[]).  On seek back, if next() is in fact used
                // (rare!), they will be re-read from disk.
            }

            public override string ToString()
            {
                return base.ToString() + " docStartFP=" + docStartFP + " posStartFP=" + posStartFP + " payStartFP=" + payStartFP + " lastPosBlockOffset=" + lastPosBlockOffset + " singletonDocID=" + singletonDocID;
            }
        }

        public override BlockTermState NewTermState()
        {
            return new IntBlockTermState();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IOUtils.Close(docIn, posIn, payIn);
            }
        }

		public override void DecodeTerm(long[] longs, DataInput @in, FieldInfo fieldInfo, 
			BlockTermState _termState, bool absolute)
		{
			Lucene41PostingsWriter.IntBlockTermState termState = (Lucene41PostingsWriter.IntBlockTermState
				)_termState;
			bool fieldHasPositions = fieldInfo.IndexOptionsValue.GetValueOrDefault().CompareTo(FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS) >= 0;
            bool fieldHasOffsets = fieldInfo.IndexOptionsValue.GetValueOrDefault().CompareTo(FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS) >= 0;
			bool fieldHasPayloads = fieldInfo.HasPayloads;
			if (absolute)
			{
				termState.docStartFP = 0;
				termState.posStartFP = 0;
				termState.payStartFP = 0;
			}
			if (version < Lucene41PostingsWriter.VERSION_META_ARRAY)
			{
				// backward compatibility
				_decodeTerm(@in, fieldInfo, termState);
				return;
			}
			termState.docStartFP += longs[0];
			if (fieldHasPositions)
			{
				termState.posStartFP += longs[1];
				if (fieldHasOffsets || fieldHasPayloads)
				{
					termState.payStartFP += longs[2];
				}
			}
			if (termState.docFreq == 1)
			{
				termState.singletonDocID = @in.ReadVInt();
			}
			else
			{
				termState.singletonDocID = -1;
			}
			if (fieldHasPositions)
			{
				if (termState.totalTermFreq > Lucene41PostingsFormat.BLOCK_SIZE)
				{
					termState.lastPosBlockOffset = @in.ReadVLong();
				}
				else
				{
					termState.lastPosBlockOffset = -1;
				}
			}
			if (termState.docFreq > Lucene41PostingsFormat.BLOCK_SIZE)
			{
				termState.skipOffset = @in.ReadVLong();
			}
			else
			{
				termState.skipOffset = -1;
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void _decodeTerm(DataInput @in, FieldInfo fieldInfo, Lucene41PostingsWriter.IntBlockTermState
			 termState)
		{
            bool fieldHasPositions = fieldInfo.IndexOptionsValue.GetValueOrDefault().CompareTo(FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS) >= 0;
            bool fieldHasOffsets = fieldInfo.IndexOptionsValue.GetValueOrDefault().CompareTo(FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS) >= 0;
			bool fieldHasPayloads = fieldInfo.HasPayloads;
			if (termState.docFreq == 1)
			{
				termState.singletonDocID = @in.ReadVInt();
			}
			else
			{
				termState.singletonDocID = -1;
				termState.docStartFP += @in.ReadVLong();
			}
			if (fieldHasPositions)
			{
				termState.posStartFP += @in.ReadVLong();
				if (termState.totalTermFreq > Lucene41PostingsFormat.BLOCK_SIZE)
				{
					termState.lastPosBlockOffset = @in.ReadVLong();
				}
				else
				{
					termState.lastPosBlockOffset = -1;
				}
				if ((fieldHasPayloads || fieldHasOffsets) && termState.totalTermFreq >= Lucene41PostingsFormat
					.BLOCK_SIZE)
				{
					termState.payStartFP += @in.ReadVLong();
				}
			}
			if (termState.docFreq > Lucene41PostingsFormat.BLOCK_SIZE)
			{
				termState.skipOffset = @in.ReadVLong();
			}
			else
			{
				termState.skipOffset = -1;
			}
		}


        public override DocsEnum Docs(FieldInfo fieldInfo, BlockTermState termState, IBits liveDocs, DocsEnum reuse, int flags)
        {
            BlockDocsEnum docsEnum;
            if (reuse is BlockDocsEnum)
            {
                docsEnum = (BlockDocsEnum)reuse;
                if (!docsEnum.CanReuse(docIn, fieldInfo))
                {
                    docsEnum = new BlockDocsEnum(this, fieldInfo);
                }
            }
            else
            {
                docsEnum = new BlockDocsEnum(this, fieldInfo);
            }
            return docsEnum.Reset(liveDocs, (IntBlockTermState)termState, flags);
        }

        public override DocsAndPositionsEnum DocsAndPositions(FieldInfo fieldInfo, BlockTermState termState, IBits liveDocs, DocsAndPositionsEnum reuse, int flags)
        {
            bool indexHasOffsets = fieldInfo.IndexOptionsValue >= FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS;
            bool indexHasPayloads = fieldInfo.HasPayloads;

            if ((!indexHasOffsets || (flags & DocsAndPositionsEnum.FLAG_OFFSETS) == 0) &&
                (!indexHasPayloads || (flags & DocsAndPositionsEnum.FLAG_PAYLOADS) == 0))
            {
                BlockDocsAndPositionsEnum docsAndPositionsEnum;
                if (reuse is BlockDocsAndPositionsEnum)
                {
                    docsAndPositionsEnum = (BlockDocsAndPositionsEnum)reuse;
                    if (!docsAndPositionsEnum.CanReuse(docIn, fieldInfo))
                    {
                        docsAndPositionsEnum = new BlockDocsAndPositionsEnum(this, fieldInfo);
                    }
                }
                else
                {
                    docsAndPositionsEnum = new BlockDocsAndPositionsEnum(this, fieldInfo);
                }
                return docsAndPositionsEnum.Reset(liveDocs, (IntBlockTermState)termState);
            }
            else
            {
                EverythingEnum everythingEnum;
                if (reuse is EverythingEnum)
                {
                    everythingEnum = (EverythingEnum)reuse;
                    if (!everythingEnum.CanReuse(docIn, fieldInfo))
                    {
                        everythingEnum = new EverythingEnum(this, fieldInfo);
                    }
                }
                else
                {
                    everythingEnum = new EverythingEnum(this, fieldInfo);
                }
                return everythingEnum.Reset(liveDocs, (IntBlockTermState)termState, flags);
            }
        }

        internal sealed class BlockDocsEnum : DocsEnum
        {
            private readonly Lucene41PostingsReader parent;

            private readonly sbyte[] encoded;

            private readonly int[] docDeltaBuffer = new int[ForUtil.MAX_DATA_SIZE];
            private readonly int[] freqBuffer = new int[ForUtil.MAX_DATA_SIZE];

            private int docBufferUpto;

            private Lucene41SkipReader skipper;
            private bool skipped;

            internal readonly IndexInput startDocIn;

            internal IndexInput docIn;
            internal readonly bool indexHasFreq;
            internal readonly bool indexHasPos;
            internal readonly bool indexHasOffsets;
            internal readonly bool indexHasPayloads;

            private int docFreq;                              // number of docs in this posting list
            private long totalTermFreq;                       // sum of freqs in this posting list (or docFreq when omitted)
            private int docUpto;                              // how many docs we've read
            private int doc;                                  // doc we last read
            private int accum;                                // accumulator for doc deltas
            private int freq;                                 // freq we last read

            // Where this term's postings start in the .doc file:
            private long docTermStartFP;

            // Where this term's skip data starts (after
            // docTermStartFP) in the .doc file (or -1 if there is
            // no skip data for this term):
            private long skipOffset;

            // docID for next skip point, we won't use skipper if 
            // target docID is not larger than this
            private int nextSkipDoc;

            private IBits liveDocs;

            private bool needsFreq; // true if the caller actually needs frequencies
            private int singletonDocID; // docid when there is a single pulsed posting, otherwise -1

            public BlockDocsEnum(Lucene41PostingsReader parent, FieldInfo fieldInfo)
            {
                this.parent = parent;
                this.startDocIn = parent.docIn;
                this.docIn = null;
                indexHasFreq = fieldInfo.IndexOptionsValue >= FieldInfo.IndexOptions.DOCS_AND_FREQS;
                indexHasPos = fieldInfo.IndexOptionsValue >= FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS;
                indexHasOffsets = fieldInfo.IndexOptionsValue >= FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS;
                indexHasPayloads = fieldInfo.HasPayloads;
                encoded = new sbyte[ForUtil.MAX_ENCODED_SIZE];
            }

            public bool CanReuse(IndexInput docIn, FieldInfo fieldInfo)
            {
                return docIn == startDocIn &&
                  indexHasFreq == (fieldInfo.IndexOptionsValue >= FieldInfo.IndexOptions.DOCS_AND_FREQS) &&
                  indexHasPos == (fieldInfo.IndexOptionsValue >= FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS) &&
                  indexHasPayloads == fieldInfo.HasPayloads;
            }

            public DocsEnum Reset(IBits liveDocs, IntBlockTermState termState, int flags)
            {
                this.liveDocs = liveDocs;
                // if (DEBUG) {
                //   System.out.println("  FPR.reset: termState=" + termState);
                // }
                docFreq = termState.docFreq;
                totalTermFreq = indexHasFreq ? termState.totalTermFreq : docFreq;
                docTermStartFP = termState.docStartFP;
                skipOffset = termState.skipOffset;
                singletonDocID = termState.singletonDocID;
                if (docFreq > 1)
                {
                    if (docIn == null)
                    {
                        // lazy init
                        docIn = (IndexInput)startDocIn.Clone();
                    }
                    docIn.Seek(docTermStartFP);
                }

                doc = -1;
                this.needsFreq = (flags & DocsEnum.FLAG_FREQS) != 0;
                if (!indexHasFreq)
                {
                    Arrays.Fill(freqBuffer, 1);
                }
                accum = 0;
                docUpto = 0;
                nextSkipDoc = Lucene41PostingsFormat.BLOCK_SIZE - 1; // we won't skip if target is found in first block
                docBufferUpto = Lucene41PostingsFormat.BLOCK_SIZE;
                skipped = false;
                return this;
            }

            public override int Freq
            {
                get { return freq; }
            }

            public override int DocID
            {
                get { return doc; }
            }

            private void RefillDocs()
            {
                int left = docFreq - docUpto;
                //assert left > 0;

                if (left >= Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    // if (DEBUG) {
                    //   System.out.println("    fill doc block from fp=" + docIn.getFilePointer());
                    // }
                    parent.forUtil.ReadBlock(docIn, encoded, docDeltaBuffer);

                    if (indexHasFreq)
                    {
                        // if (DEBUG) {
                        //   System.out.println("    fill freq block from fp=" + docIn.getFilePointer());
                        // }
                        if (needsFreq)
                        {
                            parent.forUtil.ReadBlock(docIn, encoded, freqBuffer);
                        }
                        else
                        {
                            parent.forUtil.SkipBlock(docIn); // skip over freqs
                        }
                    }
                }
                else if (docFreq == 1)
                {
                    docDeltaBuffer[0] = singletonDocID;
                    freqBuffer[0] = (int)totalTermFreq;
                }
                else
                {
                    // Read vInts:
                    // if (DEBUG) {
                    //   System.out.println("    fill last vInt block from fp=" + docIn.getFilePointer());
                    // }
                    ReadVIntBlock(docIn, docDeltaBuffer, freqBuffer, left, indexHasFreq);
                }
                docBufferUpto = 0;
            }

            public override int NextDoc()
            {
                // if (DEBUG) {
                //   System.out.println("\nFPR.nextDoc");
                // }
                while (true)
                {
                    // if (DEBUG) {
                    //   System.out.println("  docUpto=" + docUpto + " (of df=" + docFreq + ") docBufferUpto=" + docBufferUpto);
                    // }

                    if (docUpto == docFreq)
                    {
                        // if (DEBUG) {
                        //   System.out.println("  return doc=END");
                        // }
                        return doc = NO_MORE_DOCS;
                    }
                    if (docBufferUpto == Lucene41PostingsFormat.BLOCK_SIZE)
                    {
                        RefillDocs();
                    }

                    // if (DEBUG) {
                    //   System.out.println("    accum=" + accum + " docDeltaBuffer[" + docBufferUpto + "]=" + docDeltaBuffer[docBufferUpto]);
                    // }
                    accum += docDeltaBuffer[docBufferUpto];
                    docUpto++;

                    if (liveDocs == null || liveDocs[accum])
                    {
                        doc = accum;
                        freq = freqBuffer[docBufferUpto];
                        docBufferUpto++;
                        // if (DEBUG) {
                        //   System.out.println("  return doc=" + doc + " freq=" + freq);
                        // }
                        return doc;
                    }
                    // if (DEBUG) {
                    //   System.out.println("  doc=" + accum + " is deleted; try next doc");
                    // }
                    docBufferUpto++;
                }
            }

            public override int Advance(int target)
            {
                // TODO: make frq block load lazy/skippable
                // if (DEBUG) {
                //   System.out.println("  FPR.advance target=" + target);
                // }

                // current skip docID < docIDs generated from current buffer <= next skip docID
                // we don't need to skip if target is buffered already
                if (docFreq > Lucene41PostingsFormat.BLOCK_SIZE && target > nextSkipDoc)
                {

                    // if (DEBUG) {
                    //   System.out.println("load skipper");
                    // }

                    if (skipper == null)
                    {
                        // Lazy init: first time this enum has ever been used for skipping
                        skipper = new Lucene41SkipReader((IndexInput)docIn.Clone(),
                                                      Lucene41PostingsWriter.maxSkipLevels,
                                                      Lucene41PostingsFormat.BLOCK_SIZE,
                                                      indexHasPos,
                                                      indexHasOffsets,
                                                      indexHasPayloads);
                    }

                    if (!skipped)
                    {
                        //assert skipOffset != -1;
                        // This is the first time this enum has skipped
                        // since reset() was called; load the skip data:
                        skipper.Init(docTermStartFP + skipOffset, docTermStartFP, 0, 0, docFreq);
                        skipped = true;
                    }

                    // always plus one to fix the result, since skip position in Lucene41SkipReader 
                    // is a little different from MultiLevelSkipListReader
                    int newDocUpto = skipper.SkipTo(target) + 1;

                    if (newDocUpto > docUpto)
                    {
                        // Skipper moved
                        // if (DEBUG) {
                        //   System.out.println("skipper moved to docUpto=" + newDocUpto + " vs current=" + docUpto + "; docID=" + skipper.getDoc() + " fp=" + skipper.getDocPointer());
                        // }
                        //assert newDocUpto % BLOCK_SIZE == 0 : "got " + newDocUpto;
                        docUpto = newDocUpto;

                        // Force to read next block
                        docBufferUpto = Lucene41PostingsFormat.BLOCK_SIZE;
                        accum = skipper.Doc;               // actually, this is just lastSkipEntry
                        docIn.Seek(skipper.DocPointer);    // now point to the block we want to search
                    }
                    // next time we call advance, this is used to 
                    // foresee whether skipper is necessary.
                    nextSkipDoc = skipper.NextSkipDoc;
                }
                if (docUpto == docFreq)
                {
                    return doc = NO_MORE_DOCS;
                }
                if (docBufferUpto == Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    RefillDocs();
                }

                // Now scan... this is an inlined/pared down version
                // of nextDoc():
                while (true)
                {
                    // if (DEBUG) {
                    //   System.out.println("  scan doc=" + accum + " docBufferUpto=" + docBufferUpto);
                    // }
                    accum += docDeltaBuffer[docBufferUpto];
                    docUpto++;

                    if (accum >= target)
                    {
                        break;
                    }
                    docBufferUpto++;
                    if (docUpto == docFreq)
                    {
                        return doc = NO_MORE_DOCS;
                    }
                }

                if (liveDocs == null || liveDocs[accum])
                {
                    // if (DEBUG) {
                    //   System.out.println("  return doc=" + accum);
                    // }
                    freq = freqBuffer[docBufferUpto];
                    docBufferUpto++;
                    return doc = accum;
                }
                else
                {
                    // if (DEBUG) {
                    //   System.out.println("  now do nextDoc()");
                    // }
                    docBufferUpto++;
                    return NextDoc();
                }
            }

            public override long Cost
            {
                get { return docFreq; }
            }
        }

        internal sealed class BlockDocsAndPositionsEnum : DocsAndPositionsEnum
        {
            private readonly Lucene41PostingsReader parent;

            private readonly sbyte[] encoded;

            private readonly int[] docDeltaBuffer = new int[ForUtil.MAX_DATA_SIZE];
            private readonly int[] freqBuffer = new int[ForUtil.MAX_DATA_SIZE];
            private readonly int[] posDeltaBuffer = new int[ForUtil.MAX_DATA_SIZE];

            private int docBufferUpto;
            private int posBufferUpto;

            private Lucene41SkipReader skipper;
            private bool skipped;

            internal readonly IndexInput startDocIn;

            internal IndexInput docIn;
            internal readonly IndexInput posIn;

            internal readonly bool indexHasOffsets;
            internal readonly bool indexHasPayloads;

            private int docFreq;                              // number of docs in this posting list
            private long totalTermFreq;                       // number of positions in this posting list
            private int docUpto;                              // how many docs we've read
            private int doc;                                  // doc we last read
            private int accum;                                // accumulator for doc deltas
            private int freq;                                 // freq we last read
            private int position;                             // current position

            // how many positions "behind" we are; nextPosition must
            // skip these to "catch up":
            private int posPendingCount;

            // Lazy pos seek: if != -1 then we must seek to this FP
            // before reading positions:
            private long posPendingFP;

            // Where this term's postings start in the .doc file:
            private long docTermStartFP;

            // Where this term's postings start in the .pos file:
            private long posTermStartFP;

            // Where this term's payloads/offsets start in the .pay
            // file:
            private long payTermStartFP;

            // File pointer where the last (vInt encoded) pos delta
            // block is.  We need this to know whether to bulk
            // decode vs vInt decode the block:
            private long lastPosBlockFP;

            // Where this term's skip data starts (after
            // docTermStartFP) in the .doc file (or -1 if there is
            // no skip data for this term):
            private long skipOffset;

            private int nextSkipDoc;

            private IBits liveDocs;
            private int singletonDocID; // docid when there is a single pulsed posting, otherwise -1

            public BlockDocsAndPositionsEnum(Lucene41PostingsReader parent, FieldInfo fieldInfo)
            {
                this.parent = parent;
                this.startDocIn = parent.docIn;
                this.docIn = null;
                this.posIn = (IndexInput)parent.posIn.Clone();
                encoded = new sbyte[ForUtil.MAX_ENCODED_SIZE];
                indexHasOffsets = fieldInfo.IndexOptionsValue >= FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS;
                indexHasPayloads = fieldInfo.HasPayloads;
            }

            public bool CanReuse(IndexInput docIn, FieldInfo fieldInfo)
            {
                return docIn == startDocIn &&
                  indexHasOffsets == (fieldInfo.IndexOptionsValue >= FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS) &&
                  indexHasPayloads == fieldInfo.HasPayloads;
            }

            public DocsAndPositionsEnum Reset(IBits liveDocs, IntBlockTermState termState)
            {
                this.liveDocs = liveDocs;
                // if (DEBUG) {
                //   System.out.println("  FPR.reset: termState=" + termState);
                // }
                docFreq = termState.docFreq;
                docTermStartFP = termState.docStartFP;
                posTermStartFP = termState.posStartFP;
                payTermStartFP = termState.payStartFP;
                skipOffset = termState.skipOffset;
                totalTermFreq = termState.totalTermFreq;
                singletonDocID = termState.singletonDocID;
                if (docFreq > 1)
                {
                    if (docIn == null)
                    {
                        // lazy init
                        docIn = (IndexInput)startDocIn.Clone();
                    }
                    docIn.Seek(docTermStartFP);
                }
                posPendingFP = posTermStartFP;
                posPendingCount = 0;
                if (termState.totalTermFreq < Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    lastPosBlockFP = posTermStartFP;
                }
                else if (termState.totalTermFreq == Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    lastPosBlockFP = -1;
                }
                else
                {
                    lastPosBlockFP = posTermStartFP + termState.lastPosBlockOffset;
                }

                doc = -1;
                accum = 0;
                docUpto = 0;
                nextSkipDoc = Lucene41PostingsFormat.BLOCK_SIZE - 1;
                docBufferUpto = Lucene41PostingsFormat.BLOCK_SIZE;
                skipped = false;
                return this;
            }

            public override int Freq
            {
                get { return freq; }
            }

            public override int DocID
            {
                get { return doc; }
            }

            private void RefillDocs()
            {
                int left = docFreq - docUpto;
                //assert left > 0;

                if (left >= Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    // if (DEBUG) {
                    //   System.out.println("    fill doc block from fp=" + docIn.getFilePointer());
                    // }
                    parent.forUtil.ReadBlock(docIn, encoded, docDeltaBuffer);
                    // if (DEBUG) {
                    //   System.out.println("    fill freq block from fp=" + docIn.getFilePointer());
                    // }
                    parent.forUtil.ReadBlock(docIn, encoded, freqBuffer);
                }
                else if (docFreq == 1)
                {
                    docDeltaBuffer[0] = singletonDocID;
                    freqBuffer[0] = (int)totalTermFreq;
                }
                else
                {
                    // Read vInts:
                    // if (DEBUG) {
                    //   System.out.println("    fill last vInt doc block from fp=" + docIn.getFilePointer());
                    // }
                    ReadVIntBlock(docIn, docDeltaBuffer, freqBuffer, left, true);
                }
                docBufferUpto = 0;
            }

            private void RefillPositions()
            {
                // if (DEBUG) {
                //   System.out.println("      refillPositions");
                // }
                if (posIn.FilePointer == lastPosBlockFP)
                {
                    // if (DEBUG) {
                    //   System.out.println("        vInt pos block @ fp=" + posIn.getFilePointer() + " hasPayloads=" + indexHasPayloads + " hasOffsets=" + indexHasOffsets);
                    // }
                    int count = (int)(totalTermFreq % Lucene41PostingsFormat.BLOCK_SIZE);
                    int payloadLength = 0;
                    for (int i = 0; i < count; i++)
                    {
                        int code = posIn.ReadVInt();
                        if (indexHasPayloads)
                        {
                            if ((code & 1) != 0)
                            {
                                payloadLength = posIn.ReadVInt();
                            }
                            posDeltaBuffer[i] = Number.URShift(code, 1);
                            if (payloadLength != 0)
                            {
                                posIn.Seek(posIn.FilePointer + payloadLength);
                            }
                        }
                        else
                        {
                            posDeltaBuffer[i] = code;
                        }
                        if (indexHasOffsets)
                        {
                            if ((posIn.ReadVInt() & 1) != 0)
                            {
                                // offset length changed
                                posIn.ReadVInt();
                            }
                        }
                    }
                }
                else
                {
                    // if (DEBUG) {
                    //   System.out.println("        bulk pos block @ fp=" + posIn.getFilePointer());
                    // }
                    parent.forUtil.ReadBlock(posIn, encoded, posDeltaBuffer);
                }
            }

            public override int NextDoc()
            {
                // if (DEBUG) {
                //   System.out.println("  FPR.nextDoc");
                // }
                while (true)
                {
                    // if (DEBUG) {
                    //   System.out.println("    docUpto=" + docUpto + " (of df=" + docFreq + ") docBufferUpto=" + docBufferUpto);
                    // }
                    if (docUpto == docFreq)
                    {
                        return doc = NO_MORE_DOCS;
                    }
                    if (docBufferUpto == Lucene41PostingsFormat.BLOCK_SIZE)
                    {
                        RefillDocs();
                    }
                    // if (DEBUG) {
                    //   System.out.println("    accum=" + accum + " docDeltaBuffer[" + docBufferUpto + "]=" + docDeltaBuffer[docBufferUpto]);
                    // }
                    accum += docDeltaBuffer[docBufferUpto];
                    freq = freqBuffer[docBufferUpto];
                    posPendingCount += freq;
                    docBufferUpto++;
                    docUpto++;

                    if (liveDocs == null || liveDocs[accum])
                    {
                        doc = accum;
                        position = 0;
                        // if (DEBUG) {
                        //   System.out.println("    return doc=" + doc + " freq=" + freq + " posPendingCount=" + posPendingCount);
                        // }
                        return doc;
                    }
                    // if (DEBUG) {
                    //   System.out.println("    doc=" + accum + " is deleted; try next doc");
                    // }
                }
            }

            public override int Advance(int target)
            {
                // TODO: make frq block load lazy/skippable
                // if (DEBUG) {
                //   System.out.println("  FPR.advance target=" + target);
                // }

                if (docFreq > Lucene41PostingsFormat.BLOCK_SIZE && target > nextSkipDoc)
                {
                    // if (DEBUG) {
                    //   System.out.println("    try skipper");
                    // }
                    if (skipper == null)
                    {
                        // Lazy init: first time this enum has ever been used for skipping
                        // if (DEBUG) {
                        //   System.out.println("    create skipper");
                        // }
                        skipper = new Lucene41SkipReader((IndexInput)docIn.Clone(),
                                                      Lucene41PostingsWriter.maxSkipLevels,
                                                      Lucene41PostingsFormat.BLOCK_SIZE,
                                                      true,
                                                      indexHasOffsets,
                                                      indexHasPayloads);
                    }

                    if (!skipped)
                    {
                        //assert skipOffset != -1;
                        // This is the first time this enum has skipped
                        // since reset() was called; load the skip data:
                        // if (DEBUG) {
                        //   System.out.println("    init skipper");
                        // }
                        skipper.Init(docTermStartFP + skipOffset, docTermStartFP, posTermStartFP, payTermStartFP, docFreq);
                        skipped = true;
                    }

                    int newDocUpto = skipper.SkipTo(target) + 1;

                    if (newDocUpto > docUpto)
                    {
                        // Skipper moved
                        // if (DEBUG) {
                        //   System.out.println("    skipper moved to docUpto=" + newDocUpto + " vs current=" + docUpto + "; docID=" + skipper.getDoc() + " fp=" + skipper.getDocPointer() + " pos.fp=" + skipper.getPosPointer() + " pos.bufferUpto=" + skipper.getPosBufferUpto());
                        // }

                        //assert newDocUpto % BLOCK_SIZE == 0 : "got " + newDocUpto;
                        docUpto = newDocUpto;

                        // Force to read next block
                        docBufferUpto = Lucene41PostingsFormat.BLOCK_SIZE;
                        accum = skipper.Doc;
                        docIn.Seek(skipper.DocPointer);
                        posPendingFP = skipper.PosPointer;
                        posPendingCount = skipper.PosBufferUpto;
                    }
                    nextSkipDoc = skipper.NextSkipDoc;
                }
                if (docUpto == docFreq)
                {
                    return doc = NO_MORE_DOCS;
                }
                if (docBufferUpto == Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    RefillDocs();
                }

                // Now scan... this is an inlined/pared down version
                // of nextDoc():
                while (true)
                {
                    // if (DEBUG) {
                    //   System.out.println("  scan doc=" + accum + " docBufferUpto=" + docBufferUpto);
                    // }
                    accum += docDeltaBuffer[docBufferUpto];
                    freq = freqBuffer[docBufferUpto];
                    posPendingCount += freq;
                    docBufferUpto++;
                    docUpto++;

                    if (accum >= target)
                    {
                        break;
                    }
                    if (docUpto == docFreq)
                    {
                        return doc = NO_MORE_DOCS;
                    }
                }

                if (liveDocs == null || liveDocs[accum])
                {
                    // if (DEBUG) {
                    //   System.out.println("  return doc=" + accum);
                    // }
                    position = 0;
                    return doc = accum;
                }
                else
                {
                    // if (DEBUG) {
                    //   System.out.println("  now do nextDoc()");
                    // }
                    return NextDoc();
                }
            }

            // TODO: in theory we could avoid loading frq block
            // when not needed, ie, use skip data to load how far to
            // seek the pos pointer ... instead of having to load frq
            // blocks only to sum up how many positions to skip
            private void SkipPositions()
            {
                // Skip positions now:
                int toSkip = posPendingCount - freq;
                // if (DEBUG) {
                //   System.out.println("      FPR.skipPositions: toSkip=" + toSkip);
                // }

                int leftInBlock = Lucene41PostingsFormat.BLOCK_SIZE - posBufferUpto;
                if (toSkip < leftInBlock)
                {
                    posBufferUpto += toSkip;
                    // if (DEBUG) {
                    //   System.out.println("        skip w/in block to posBufferUpto=" + posBufferUpto);
                    // }
                }
                else
                {
                    toSkip -= leftInBlock;
                    while (toSkip >= Lucene41PostingsFormat.BLOCK_SIZE)
                    {
                        // if (DEBUG) {
                        //   System.out.println("        skip whole block @ fp=" + posIn.getFilePointer());
                        // }
                        //assert posIn.getFilePointer() != lastPosBlockFP;
                        parent.forUtil.SkipBlock(posIn);
                        toSkip -= Lucene41PostingsFormat.BLOCK_SIZE;
                    }
                    RefillPositions();
                    posBufferUpto = toSkip;
                    // if (DEBUG) {
                    //   System.out.println("        skip w/in block to posBufferUpto=" + posBufferUpto);
                    // }
                }

                position = 0;
            }

            public override int NextPosition()
            {
                // if (DEBUG) {
                //   System.out.println("    FPR.nextPosition posPendingCount=" + posPendingCount + " posBufferUpto=" + posBufferUpto);
                // }
                if (posPendingFP != -1)
                {
                    // if (DEBUG) {
                    //   System.out.println("      seek to pendingFP=" + posPendingFP);
                    // }
                    posIn.Seek(posPendingFP);
                    posPendingFP = -1;

                    // Force buffer refill:
                    posBufferUpto = Lucene41PostingsFormat.BLOCK_SIZE;
                }

                if (posPendingCount > freq)
                {
                    SkipPositions();
                    posPendingCount = freq;
                }

                if (posBufferUpto == Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    RefillPositions();
                    posBufferUpto = 0;
                }
                position += posDeltaBuffer[posBufferUpto++];
                posPendingCount--;
                // if (DEBUG) {
                //   System.out.println("      return pos=" + position);
                // }
                return position;
            }

            public override int StartOffset
            {
                get { return -1; }
            }

            public override int EndOffset
            {
                get { return -1; }
            }

            public override BytesRef Payload
            {
                get { return null; }
            }

            public override long Cost
            {
                get { return docFreq; }
            }
        }

        internal sealed class EverythingEnum : DocsAndPositionsEnum
        {
            private readonly Lucene41PostingsReader parent;

            private readonly sbyte[] encoded;

            private readonly int[] docDeltaBuffer = new int[ForUtil.MAX_DATA_SIZE];
            private readonly int[] freqBuffer = new int[ForUtil.MAX_DATA_SIZE];
            private readonly int[] posDeltaBuffer = new int[ForUtil.MAX_DATA_SIZE];

            private readonly int[] payloadLengthBuffer;
            private readonly int[] offsetStartDeltaBuffer;
            private readonly int[] offsetLengthBuffer;

            private sbyte[] payloadBytes;
            private int payloadByteUpto;
            private int payloadLength;

            private int lastStartOffset;
            private int startOffset;
            private int endOffset;

            private int docBufferUpto;
            private int posBufferUpto;

            private Lucene41SkipReader skipper;
            private bool skipped;

            internal readonly IndexInput startDocIn;

            internal IndexInput docIn;
            internal readonly IndexInput posIn;
            internal readonly IndexInput payIn;
            internal readonly BytesRef payload;

            internal readonly bool indexHasOffsets;
            internal readonly bool indexHasPayloads;

            private int docFreq;                              // number of docs in this posting list
            private long totalTermFreq;                       // number of positions in this posting list
            private int docUpto;                              // how many docs we've read
            private int doc;                                  // doc we last read
            private int accum;                                // accumulator for doc deltas
            private int freq;                                 // freq we last read
            private int position;                             // current position

            // how many positions "behind" we are; nextPosition must
            // skip these to "catch up":
            private int posPendingCount;

            // Lazy pos seek: if != -1 then we must seek to this FP
            // before reading positions:
            private long posPendingFP;

            // Lazy pay seek: if != -1 then we must seek to this FP
            // before reading payloads/offsets:
            private long payPendingFP;

            // Where this term's postings start in the .doc file:
            private long docTermStartFP;

            // Where this term's postings start in the .pos file:
            private long posTermStartFP;

            // Where this term's payloads/offsets start in the .pay
            // file:
            private long payTermStartFP;

            // File pointer where the last (vInt encoded) pos delta
            // block is.  We need this to know whether to bulk
            // decode vs vInt decode the block:
            private long lastPosBlockFP;

            // Where this term's skip data starts (after
            // docTermStartFP) in the .doc file (or -1 if there is
            // no skip data for this term):
            private long skipOffset;

            private int nextSkipDoc;

            private IBits liveDocs;

            private bool needsOffsets; // true if we actually need offsets
            private bool needsPayloads; // true if we actually need payloads
            private int singletonDocID; // docid when there is a single pulsed posting, otherwise -1

            public EverythingEnum(Lucene41PostingsReader parent, FieldInfo fieldInfo)
            {
                this.parent = parent;
                this.startDocIn = parent.docIn;
                this.docIn = null;
                this.posIn = (IndexInput)parent.posIn.Clone();
                this.payIn = (IndexInput)parent.payIn.Clone();
                encoded = new sbyte[ForUtil.MAX_ENCODED_SIZE];
                indexHasOffsets = fieldInfo.IndexOptionsValue >= FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS;
                if (indexHasOffsets)
                {
                    offsetStartDeltaBuffer = new int[ForUtil.MAX_DATA_SIZE];
                    offsetLengthBuffer = new int[ForUtil.MAX_DATA_SIZE];
                }
                else
                {
                    offsetStartDeltaBuffer = null;
                    offsetLengthBuffer = null;
                    startOffset = -1;
                    endOffset = -1;
                }

                indexHasPayloads = fieldInfo.HasPayloads;
                if (indexHasPayloads)
                {
                    payloadLengthBuffer = new int[ForUtil.MAX_DATA_SIZE];
                    payloadBytes = new sbyte[128];
                    payload = new BytesRef();
                }
                else
                {
                    payloadLengthBuffer = null;
                    payloadBytes = null;
                    payload = null;
                }
            }

            public bool CanReuse(IndexInput docIn, FieldInfo fieldInfo)
            {
                return docIn == startDocIn &&
                  indexHasOffsets == (fieldInfo.IndexOptionsValue >= FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS) &&
                  indexHasPayloads == fieldInfo.HasPayloads;
            }

            public EverythingEnum Reset(IBits liveDocs, IntBlockTermState termState, int flags)
            {
                this.liveDocs = liveDocs;
                // if (DEBUG) {
                //   System.out.println("  FPR.reset: termState=" + termState);
                // }
                docFreq = termState.docFreq;
                docTermStartFP = termState.docStartFP;
                posTermStartFP = termState.posStartFP;
                payTermStartFP = termState.payStartFP;
                skipOffset = termState.skipOffset;
                totalTermFreq = termState.totalTermFreq;
                singletonDocID = termState.singletonDocID;
                if (docFreq > 1)
                {
                    if (docIn == null)
                    {
                        // lazy init
                        docIn = (IndexInput)startDocIn.Clone();
                    }
                    docIn.Seek(docTermStartFP);
                }
                posPendingFP = posTermStartFP;
                payPendingFP = payTermStartFP;
                posPendingCount = 0;
                if (termState.totalTermFreq < Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    lastPosBlockFP = posTermStartFP;
                }
                else if (termState.totalTermFreq == Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    lastPosBlockFP = -1;
                }
                else
                {
                    lastPosBlockFP = posTermStartFP + termState.lastPosBlockOffset;
                }

                this.needsOffsets = (flags & DocsAndPositionsEnum.FLAG_OFFSETS) != 0;
                this.needsPayloads = (flags & DocsAndPositionsEnum.FLAG_PAYLOADS) != 0;

                doc = -1;
                accum = 0;
                docUpto = 0;
                nextSkipDoc = Lucene41PostingsFormat.BLOCK_SIZE - 1;
                docBufferUpto = Lucene41PostingsFormat.BLOCK_SIZE;
                skipped = false;
                return this;
            }

            public override int Freq
            {
                get { return freq; }
            }

            public override int DocID
            {
                get { return doc; }
            }

            private void RefillDocs()
            {
                int left = docFreq - docUpto;
                //assert left > 0;

                if (left >= Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    // if (DEBUG) {
                    //   System.out.println("    fill doc block from fp=" + docIn.getFilePointer());
                    // }
                    parent.forUtil.ReadBlock(docIn, encoded, docDeltaBuffer);
                    // if (DEBUG) {
                    //   System.out.println("    fill freq block from fp=" + docIn.getFilePointer());
                    // }
                    parent.forUtil.ReadBlock(docIn, encoded, freqBuffer);
                }
                else if (docFreq == 1)
                {
                    docDeltaBuffer[0] = singletonDocID;
                    freqBuffer[0] = (int)totalTermFreq;
                }
                else
                {
                    // if (DEBUG) {
                    //   System.out.println("    fill last vInt doc block from fp=" + docIn.getFilePointer());
                    // }
                    ReadVIntBlock(docIn, docDeltaBuffer, freqBuffer, left, true);
                }
                docBufferUpto = 0;
            }

            private void RefillPositions()
            {
                // if (DEBUG) {
                //   System.out.println("      refillPositions");
                // }
                if (posIn.FilePointer == lastPosBlockFP)
                {
                    // if (DEBUG) {
                    //   System.out.println("        vInt pos block @ fp=" + posIn.getFilePointer() + " hasPayloads=" + indexHasPayloads + " hasOffsets=" + indexHasOffsets);
                    // }
                    int count = (int)(totalTermFreq % Lucene41PostingsFormat.BLOCK_SIZE);
                    int payloadLength = 0;
                    int offsetLength = 0;
                    payloadByteUpto = 0;
                    for (int i = 0; i < count; i++)
                    {
                        int code = posIn.ReadVInt();
                        if (indexHasPayloads)
                        {
                            if ((code & 1) != 0)
                            {
                                payloadLength = posIn.ReadVInt();
                            }
                            // if (DEBUG) {
                            //   System.out.println("        i=" + i + " payloadLen=" + payloadLength);
                            // }
                            payloadLengthBuffer[i] = payloadLength;
                            posDeltaBuffer[i] = Number.URShift(code, 1);
                            if (payloadLength != 0)
                            {
                                if (payloadByteUpto + payloadLength > payloadBytes.Length)
                                {
                                    payloadBytes = ArrayUtil.Grow(payloadBytes, payloadByteUpto + payloadLength);
                                }
                                //System.out.println("          read payload @ pos.fp=" + posIn.getFilePointer());
                                posIn.ReadBytes(payloadBytes, payloadByteUpto, payloadLength);
                                payloadByteUpto += payloadLength;
                            }
                        }
                        else
                        {
                            posDeltaBuffer[i] = code;
                        }

                        if (indexHasOffsets)
                        {
                            // if (DEBUG) {
                            //   System.out.println("        i=" + i + " read offsets from posIn.fp=" + posIn.getFilePointer());
                            // }
                            int deltaCode = posIn.ReadVInt();
                            if ((deltaCode & 1) != 0)
                            {
                                offsetLength = posIn.ReadVInt();
                            }
                            offsetStartDeltaBuffer[i] = Number.URShift(deltaCode, 1);
                            offsetLengthBuffer[i] = offsetLength;
                            // if (DEBUG) {
                            //   System.out.println("          startOffDelta=" + offsetStartDeltaBuffer[i] + " offsetLen=" + offsetLengthBuffer[i]);
                            // }
                        }
                    }
                    payloadByteUpto = 0;
                }
                else
                {
                    // if (DEBUG) {
                    //   System.out.println("        bulk pos block @ fp=" + posIn.getFilePointer());
                    // }
                    parent.forUtil.ReadBlock(posIn, encoded, posDeltaBuffer);

                    if (indexHasPayloads)
                    {
                        // if (DEBUG) {
                        //   System.out.println("        bulk payload block @ pay.fp=" + payIn.getFilePointer());
                        // }
                        if (needsPayloads)
                        {
                            parent.forUtil.ReadBlock(payIn, encoded, payloadLengthBuffer);
                            int numBytes = payIn.ReadVInt();
                            // if (DEBUG) {
                            //   System.out.println("        " + numBytes + " payload bytes @ pay.fp=" + payIn.getFilePointer());
                            // }
                            if (numBytes > payloadBytes.Length)
                            {
                                payloadBytes = ArrayUtil.Grow(payloadBytes, numBytes);
                            }
                            payIn.ReadBytes(payloadBytes, 0, numBytes);
                        }
                        else
                        {
                            // this works, because when writing a vint block we always force the first length to be written
                            parent.forUtil.SkipBlock(payIn); // skip over lengths
                            int numBytes = payIn.ReadVInt(); // read length of payloadBytes
                            payIn.Seek(payIn.FilePointer + numBytes); // skip over payloadBytes
                        }
                        payloadByteUpto = 0;
                    }

                    if (indexHasOffsets)
                    {
                        // if (DEBUG) {
                        //   System.out.println("        bulk offset block @ pay.fp=" + payIn.getFilePointer());
                        // }
                        if (needsOffsets)
                        {
                            parent.forUtil.ReadBlock(payIn, encoded, offsetStartDeltaBuffer);
                            parent.forUtil.ReadBlock(payIn, encoded, offsetLengthBuffer);
                        }
                        else
                        {
                            // this works, because when writing a vint block we always force the first length to be written
                            parent.forUtil.SkipBlock(payIn); // skip over starts
                            parent.forUtil.SkipBlock(payIn); // skip over lengths
                        }
                    }
                }
            }

            public override int NextDoc()
            {
                // if (DEBUG) {
                //   System.out.println("  FPR.nextDoc");
                // }
                while (true)
                {
                    // if (DEBUG) {
                    //   System.out.println("    docUpto=" + docUpto + " (of df=" + docFreq + ") docBufferUpto=" + docBufferUpto);
                    // }
                    if (docUpto == docFreq)
                    {
                        return doc = NO_MORE_DOCS;
                    }
                    if (docBufferUpto == Lucene41PostingsFormat.BLOCK_SIZE)
                    {
                        RefillDocs();
                    }
                    // if (DEBUG) {
                    //   System.out.println("    accum=" + accum + " docDeltaBuffer[" + docBufferUpto + "]=" + docDeltaBuffer[docBufferUpto]);
                    // }
                    accum += docDeltaBuffer[docBufferUpto];
                    freq = freqBuffer[docBufferUpto];
                    posPendingCount += freq;
                    docBufferUpto++;
                    docUpto++;

                    if (liveDocs == null || liveDocs[accum])
                    {
                        doc = accum;
                        // if (DEBUG) {
                        //   System.out.println("    return doc=" + doc + " freq=" + freq + " posPendingCount=" + posPendingCount);
                        // }
                        position = 0;
                        lastStartOffset = 0;
                        return doc;
                    }

                    // if (DEBUG) {
                    //   System.out.println("    doc=" + accum + " is deleted; try next doc");
                    // }
                }
            }

            public override int Advance(int target)
            {
                // TODO: make frq block load lazy/skippable
                // if (DEBUG) {
                //   System.out.println("  FPR.advance target=" + target);
                // }

                if (docFreq > Lucene41PostingsFormat.BLOCK_SIZE && target > nextSkipDoc)
                {

                    // if (DEBUG) {
                    //   System.out.println("    try skipper");
                    // }

                    if (skipper == null)
                    {
                        // Lazy init: first time this enum has ever been used for skipping
                        // if (DEBUG) {
                        //   System.out.println("    create skipper");
                        // }
                        skipper = new Lucene41SkipReader((IndexInput)docIn.Clone(),
                                                      Lucene41PostingsWriter.maxSkipLevels,
                                                      Lucene41PostingsFormat.BLOCK_SIZE,
                                                      true,
                                                      indexHasOffsets,
                                                      indexHasPayloads);
                    }

                    if (!skipped)
                    {
                        //assert skipOffset != -1;
                        // This is the first time this enum has skipped
                        // since reset() was called; load the skip data:
                        // if (DEBUG) {
                        //   System.out.println("    init skipper");
                        // }
                        skipper.Init(docTermStartFP + skipOffset, docTermStartFP, posTermStartFP, payTermStartFP, docFreq);
                        skipped = true;
                    }

                    int newDocUpto = skipper.SkipTo(target) + 1;

                    if (newDocUpto > docUpto)
                    {
                        // Skipper moved
                        // if (DEBUG) {
                        //   System.out.println("    skipper moved to docUpto=" + newDocUpto + " vs current=" + docUpto + "; docID=" + skipper.getDoc() + " fp=" + skipper.getDocPointer() + " pos.fp=" + skipper.getPosPointer() + " pos.bufferUpto=" + skipper.getPosBufferUpto() + " pay.fp=" + skipper.getPayPointer() + " lastStartOffset=" + lastStartOffset);
                        // }
                        //assert newDocUpto % BLOCK_SIZE == 0 : "got " + newDocUpto;
                        docUpto = newDocUpto;

                        // Force to read next block
                        docBufferUpto = Lucene41PostingsFormat.BLOCK_SIZE;
                        accum = skipper.Doc;
                        docIn.Seek(skipper.DocPointer);
                        posPendingFP = skipper.PosPointer;
                        payPendingFP = skipper.PayPointer;
                        posPendingCount = skipper.PosBufferUpto;
                        lastStartOffset = 0; // new document
                        payloadByteUpto = skipper.PayloadByteUpto;
                    }
                    nextSkipDoc = skipper.NextSkipDoc;
                }
                if (docUpto == docFreq)
                {
                    return doc = NO_MORE_DOCS;
                }
                if (docBufferUpto == Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    RefillDocs();
                }

                // Now scan:
                while (true)
                {
                    // if (DEBUG) {
                    //   System.out.println("  scan doc=" + accum + " docBufferUpto=" + docBufferUpto);
                    // }
                    accum += docDeltaBuffer[docBufferUpto];
                    freq = freqBuffer[docBufferUpto];
                    posPendingCount += freq;
                    docBufferUpto++;
                    docUpto++;

                    if (accum >= target)
                    {
                        break;
                    }
                    if (docUpto == docFreq)
                    {
                        return doc = NO_MORE_DOCS;
                    }
                }

                if (liveDocs == null || liveDocs[accum])
                {
                    // if (DEBUG) {
                    //   System.out.println("  return doc=" + accum);
                    // }
                    position = 0;
                    lastStartOffset = 0;
                    return doc = accum;
                }
                else
                {
                    // if (DEBUG) {
                    //   System.out.println("  now do nextDoc()");
                    // }
                    return NextDoc();
                }
            }

            // TODO: in theory we could avoid loading frq block
            // when not needed, ie, use skip data to load how far to
            // seek the pos pointer ... instead of having to load frq
            // blocks only to sum up how many positions to skip
            private void SkipPositions()
            {
                // Skip positions now:
                int toSkip = posPendingCount - freq;
                // if (DEBUG) {
                //   System.out.println("      FPR.skipPositions: toSkip=" + toSkip);
                // }

                int leftInBlock = Lucene41PostingsFormat.BLOCK_SIZE - posBufferUpto;
                if (toSkip < leftInBlock)
                {
                    int end = posBufferUpto + toSkip;
                    while (posBufferUpto < end)
                    {
                        if (indexHasPayloads)
                        {
                            payloadByteUpto += payloadLengthBuffer[posBufferUpto];
                        }
                        posBufferUpto++;
                    }
                    // if (DEBUG) {
                    //   System.out.println("        skip w/in block to posBufferUpto=" + posBufferUpto);
                    // }
                }
                else
                {
                    toSkip -= leftInBlock;
                    while (toSkip >= Lucene41PostingsFormat.BLOCK_SIZE)
                    {
                        // if (DEBUG) {
                        //   System.out.println("        skip whole block @ fp=" + posIn.getFilePointer());
                        // }
                        //assert posIn.getFilePointer() != lastPosBlockFP;
                        parent.forUtil.SkipBlock(posIn);

                        if (indexHasPayloads)
                        {
                            // Skip payloadLength block:
                            parent.forUtil.SkipBlock(payIn);

                            // Skip payloadBytes block:
                            int numBytes = payIn.ReadVInt();
                            payIn.Seek(payIn.FilePointer + numBytes);
                        }

                        if (indexHasOffsets)
                        {
                            parent.forUtil.SkipBlock(payIn);
                            parent.forUtil.SkipBlock(payIn);
                        }
                        toSkip -= Lucene41PostingsFormat.BLOCK_SIZE;
                    }
                    RefillPositions();
                    payloadByteUpto = 0;
                    posBufferUpto = 0;
                    while (posBufferUpto < toSkip)
                    {
                        if (indexHasPayloads)
                        {
                            payloadByteUpto += payloadLengthBuffer[posBufferUpto];
                        }
                        posBufferUpto++;
                    }
                    // if (DEBUG) {
                    //   System.out.println("        skip w/in block to posBufferUpto=" + posBufferUpto);
                    // }
                }

                position = 0;
                lastStartOffset = 0;
            }

            public override int NextPosition()
            {
                // if (DEBUG) {
                //   System.out.println("    FPR.nextPosition posPendingCount=" + posPendingCount + " posBufferUpto=" + posBufferUpto + " payloadByteUpto=" + payloadByteUpto)// ;
                // }
                if (posPendingFP != -1)
                {
                    // if (DEBUG) {
                    //   System.out.println("      seek pos to pendingFP=" + posPendingFP);
                    // }
                    posIn.Seek(posPendingFP);
                    posPendingFP = -1;

                    if (payPendingFP != -1)
                    {
                        // if (DEBUG) {
                        //   System.out.println("      seek pay to pendingFP=" + payPendingFP);
                        // }
                        payIn.Seek(payPendingFP);
                        payPendingFP = -1;
                    }

                    // Force buffer refill:
                    posBufferUpto = Lucene41PostingsFormat.BLOCK_SIZE;
                }

                if (posPendingCount > freq)
                {
                    SkipPositions();
                    posPendingCount = freq;
                }

                if (posBufferUpto == Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    RefillPositions();
                    posBufferUpto = 0;
                }
                position += posDeltaBuffer[posBufferUpto];

                if (indexHasPayloads)
                {
                    payloadLength = payloadLengthBuffer[posBufferUpto];
                    payload.bytes = payloadBytes;
                    payload.offset = payloadByteUpto;
                    payload.length = payloadLength;
                    payloadByteUpto += payloadLength;
                }

                if (indexHasOffsets)
                {
                    startOffset = lastStartOffset + offsetStartDeltaBuffer[posBufferUpto];
                    endOffset = startOffset + offsetLengthBuffer[posBufferUpto];
                    lastStartOffset = startOffset;
                }

                posBufferUpto++;
                posPendingCount--;
                // if (DEBUG) {
                //   System.out.println("      return pos=" + position);
                // }
                return position;
            }

            public override int StartOffset
            {
                get { return startOffset; }
            }

            public override int EndOffset
            {
                get { return endOffset; }
            }

            public override BytesRef Payload
            {
                get
                {
                    // if (DEBUG) {
                    //   System.out.println("    FPR.getPayload payloadLength=" + payloadLength + " payloadByteUpto=" + payloadByteUpto);
                    // }
                    if (payloadLength == 0)
                    {
                        return null;
                    }
                    else
                    {
                        return payload;
                    }
                }
            }

            public override long Cost
            {
                get { return docFreq; }
            }
        }
		public override long RamBytesUsed()
		{
			return 0;
		}
		public override void CheckIntegrity()
		{
			if (version >= Lucene41PostingsWriter.VERSION_CHECKSUM)
			{
				if (docIn != null)
				{
					CodecUtil.ChecksumEntireFile(docIn);
				}
				if (posIn != null)
				{
					CodecUtil.ChecksumEntireFile(posIn);
				}
				if (payIn != null)
				{
					CodecUtil.ChecksumEntireFile(payIn);
				}
			}
		}
    }
}
