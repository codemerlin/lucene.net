﻿using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Support;
using Lucene.Net.Util;
using Lucene.Net.Util.Packed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lucene.Net.Codecs.Lucene41
{
    public sealed class Lucene41PostingsWriter : PostingsWriterBase
    {
        internal const int maxSkipLevels = 10;

        internal const string TERMS_CODEC = "Lucene41PostingsWriterTerms";
        internal const string DOC_CODEC = "Lucene41PostingsWriterDoc";
        internal const string POS_CODEC = "Lucene41PostingsWriterPos";
        internal const string PAY_CODEC = "Lucene41PostingsWriterPay";

        // Increment version to change it
        internal const int VERSION_START = 0;
		internal const int VERSION_META_ARRAY = 1;
		internal const int VERSION_CHECKSUM = 2;
		internal const int VERSION_CURRENT = VERSION_CHECKSUM;

        internal readonly IndexOutput docOut;
        internal readonly IndexOutput posOut;
        internal readonly IndexOutput payOut;

		internal static readonly Lucene41PostingsWriter.IntBlockTermState emptyState = new 
			Lucene41PostingsWriter.IntBlockTermState();

		internal Lucene41PostingsWriter.IntBlockTermState lastState;
        private bool fieldHasFreqs;
        private bool fieldHasPositions;
        private bool fieldHasOffsets;
        private bool fieldHasPayloads;

        // Holds starting file pointers for each term:
		private long docStartFP;
		private long posStartFP;
		private long payStartFP;

        internal readonly int[] docDeltaBuffer;
        internal readonly int[] freqBuffer;
        private int docBufferUpto;

        internal readonly int[] posDeltaBuffer;
        internal readonly int[] payloadLengthBuffer;
        internal readonly int[] offsetStartDeltaBuffer;
        internal readonly int[] offsetLengthBuffer;
        private int posBufferUpto;

        private sbyte[] payloadBytes;
        private int payloadByteUpto;

        private int lastBlockDocID;
        private long lastBlockPosFP;
        private long lastBlockPayFP;
        private int lastBlockPosBufferUpto;
        private int lastBlockPayloadByteUpto;

        private int lastDocID;
        private int lastPosition;
        private int lastStartOffset;
        private int docCount;

        internal readonly sbyte[] encoded;

        private readonly ForUtil forUtil;
        private readonly Lucene41SkipWriter skipWriter;

        public Lucene41PostingsWriter(SegmentWriteState state, float acceptableOverheadRatio)
            : base()
        {


            docOut = state.directory.CreateOutput(IndexFileNames.SegmentFileName(state.segmentInfo.name, state.segmentSuffix, Lucene41PostingsFormat.DOC_EXTENSION),
                                                  state.context);
            IndexOutput posOut = null;
            IndexOutput payOut = null;
            bool success = false;
            try
            {
                CodecUtil.WriteHeader(docOut, DOC_CODEC, VERSION_CURRENT);
                forUtil = new ForUtil(acceptableOverheadRatio, docOut);
                if (state.fieldInfos.HasProx)
                {
                    posDeltaBuffer = new int[ForUtil.MAX_DATA_SIZE];
                    posOut = state.directory.CreateOutput(IndexFileNames.SegmentFileName(state.segmentInfo.name, state.segmentSuffix, Lucene41PostingsFormat.POS_EXTENSION),
                                                          state.context);
                    CodecUtil.WriteHeader(posOut, POS_CODEC, VERSION_CURRENT);

                    if (state.fieldInfos.HasPayloads)
                    {
                        payloadBytes = new sbyte[128];
                        payloadLengthBuffer = new int[ForUtil.MAX_DATA_SIZE];
                    }
                    else
                    {
                        payloadBytes = null;
                        payloadLengthBuffer = null;
                    }

                    if (state.fieldInfos.HasOffsets)
                    {
                        offsetStartDeltaBuffer = new int[ForUtil.MAX_DATA_SIZE];
                        offsetLengthBuffer = new int[ForUtil.MAX_DATA_SIZE];
                    }
                    else
                    {
                        offsetStartDeltaBuffer = null;
                        offsetLengthBuffer = null;
                    }

                    if (state.fieldInfos.HasPayloads || state.fieldInfos.HasOffsets)
                    {
                        payOut = state.directory.CreateOutput(IndexFileNames.SegmentFileName(state.segmentInfo.name, state.segmentSuffix, Lucene41PostingsFormat.PAY_EXTENSION),
                                                              state.context);
                        CodecUtil.WriteHeader(payOut, PAY_CODEC, VERSION_CURRENT);
                    }
                }
                else
                {
                    posDeltaBuffer = null;
                    payloadLengthBuffer = null;
                    offsetStartDeltaBuffer = null;
                    offsetLengthBuffer = null;
                    payloadBytes = null;
                }
                this.payOut = payOut;
                this.posOut = posOut;
                success = true;
            }
            finally
            {
                if (!success)
                {
                    IOUtils.CloseWhileHandlingException((IDisposable)docOut, posOut, payOut);
                }
            }

            docDeltaBuffer = new int[ForUtil.MAX_DATA_SIZE];
            freqBuffer = new int[ForUtil.MAX_DATA_SIZE];

            // TODO: should we try skipping every 2/4 blocks...?
            skipWriter = new Lucene41SkipWriter(maxSkipLevels,
                                             Lucene41PostingsFormat.BLOCK_SIZE,
                                             state.segmentInfo.DocCount,
                                             docOut,
                                             posOut,
                                             payOut);

            encoded = new sbyte[ForUtil.MAX_ENCODED_SIZE];
        }

        public Lucene41PostingsWriter(SegmentWriteState state)
            : this(state, PackedInts.COMPACT)
        {
        }

		internal sealed class IntBlockTermState : BlockTermState
		{
			internal long docStartFP = 0;

			internal long posStartFP = 0;

			internal long payStartFP = 0;

			internal long skipOffset = -1;

			internal long lastPosBlockOffset = -1;

			internal int singletonDocID = -1;

			// docid when there is a single pulsed posting, otherwise -1
			// freq is always implicitly totalTermFreq in this case.
			public override object Clone()
			{
				var other = new IntBlockTermState();
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
			}

			public override string ToString()
			{
				return base.ToString() + " docStartFP=" + docStartFP + " posStartFP=" + posStartFP
					 + " payStartFP=" + payStartFP + " lastPosBlockOffset=" + lastPosBlockOffset + " singletonDocID="
					 + singletonDocID;
			}
		}
		public override BlockTermState NewTermState()
		{
			return new Lucene41PostingsWriter.IntBlockTermState();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override void Init(IndexOutput termsOut)
		{
			CodecUtil.WriteHeader(termsOut, TERMS_CODEC, VERSION_CURRENT);
			termsOut.WriteVInt(Lucene41PostingsFormat.BLOCK_SIZE);
		}
		public override int SetField(FieldInfo fieldInfo)
        {
            FieldInfo.IndexOptions indexOptions = fieldInfo.IndexOptionsValue.GetValueOrDefault();
            fieldHasFreqs = indexOptions >= FieldInfo.IndexOptions.DOCS_AND_FREQS;
            fieldHasPositions = indexOptions >= FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS;
            fieldHasOffsets = indexOptions >= FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS;
            fieldHasPayloads = fieldInfo.HasPayloads;
            skipWriter.SetField(fieldHasPositions, fieldHasOffsets, fieldHasPayloads);
			lastState = emptyState;
			if (fieldHasPositions)
			{
				if (fieldHasPayloads || fieldHasOffsets)
				{
					return 3;
				}
				else
				{
					// doc + pos + pay FP
					return 2;
				}
			}
			else
			{
				// doc + pos FP
				return 1;
			}
        }

        public override void StartTerm()
        {
            docStartFP = docOut.FilePointer;
            if (fieldHasPositions)
            {
                posStartFP = posOut.FilePointer;
                if (fieldHasPayloads || fieldHasOffsets)
                {
                    payStartFP = payOut.FilePointer;
                }
            }
            lastDocID = 0;
            lastBlockDocID = -1;
            // if (DEBUG) {
            //   System.out.println("FPW.startTerm startFP=" + docTermStartFP);
            // }
            skipWriter.ResetSkip();
        }

        public override void StartDoc(int docID, int termDocFreq)
        {
            // if (DEBUG) {
            //   System.out.println("FPW.startDoc docID["+docBufferUpto+"]=" + docID);
            // }
            // Have collected a block of docs, and get a new doc. 
            // Should write skip data as well as postings list for
            // current block.
            if (lastBlockDocID != -1 && docBufferUpto == 0)
            {
                // if (DEBUG) {
                //   System.out.println("  bufferSkip at writeBlock: lastDocID=" + lastBlockDocID + " docCount=" + (docCount-1));
                // }
                skipWriter.BufferSkip(lastBlockDocID, docCount, lastBlockPosFP, lastBlockPayFP, lastBlockPosBufferUpto, lastBlockPayloadByteUpto);
            }

            int docDelta = docID - lastDocID;

            if (docID < 0 || (docCount > 0 && docDelta <= 0))
            {
                throw new CorruptIndexException("docs out of order (" + docID + " <= " + lastDocID + " ) (docOut: " + docOut + ")");
            }

            docDeltaBuffer[docBufferUpto] = docDelta;
            // if (DEBUG) {
            //   System.out.println("  docDeltaBuffer[" + docBufferUpto + "]=" + docDelta);
            // }
            if (fieldHasFreqs)
            {
                freqBuffer[docBufferUpto] = termDocFreq;
            }
            docBufferUpto++;
            docCount++;

            if (docBufferUpto == Lucene41PostingsFormat.BLOCK_SIZE)
            {
                // if (DEBUG) {
                //   System.out.println("  write docDelta block @ fp=" + docOut.getFilePointer());
                // }
                forUtil.WriteBlock(docDeltaBuffer, encoded, docOut);
                if (fieldHasFreqs)
                {
                    // if (DEBUG) {
                    //   System.out.println("  write freq block @ fp=" + docOut.getFilePointer());
                    // }
                    forUtil.WriteBlock(freqBuffer, encoded, docOut);
                }
                // NOTE: don't set docBufferUpto back to 0 here;
                // finishDoc will do so (because it needs to see that
                // the block was filled so it can save skip data)
            }


            lastDocID = docID;
            lastPosition = 0;
            lastStartOffset = 0;
        }

        public override void AddPosition(int position, BytesRef payload, int startOffset, int endOffset)
        {
            // if (DEBUG) {
            //   System.out.println("FPW.addPosition pos=" + position + " posBufferUpto=" + posBufferUpto + (fieldHasPayloads ? " payloadByteUpto=" + payloadByteUpto: ""));
            // }
            posDeltaBuffer[posBufferUpto] = position - lastPosition;
            if (fieldHasPayloads)
            {
                if (payload == null || payload.length == 0)
                {
                    // no payload
                    payloadLengthBuffer[posBufferUpto] = 0;
                }
                else
                {
                    payloadLengthBuffer[posBufferUpto] = payload.length;
                    if (payloadByteUpto + payload.length > payloadBytes.Length)
                    {
                        payloadBytes = ArrayUtil.Grow(payloadBytes, payloadByteUpto + payload.length);
                    }
                    Array.Copy(payload.bytes, payload.offset, payloadBytes, payloadByteUpto, payload.length);
                    payloadByteUpto += payload.length;
                }
            }

            if (fieldHasOffsets)
            {
                //assert startOffset >= lastStartOffset;
                //assert endOffset >= startOffset;
                offsetStartDeltaBuffer[posBufferUpto] = startOffset - lastStartOffset;
                offsetLengthBuffer[posBufferUpto] = endOffset - startOffset;
                lastStartOffset = startOffset;
            }

            posBufferUpto++;
            lastPosition = position;
            if (posBufferUpto == Lucene41PostingsFormat.BLOCK_SIZE)
            {
                // if (DEBUG) {
                //   System.out.println("  write pos bulk block @ fp=" + posOut.getFilePointer());
                // }
                forUtil.WriteBlock(posDeltaBuffer, encoded, posOut);

                if (fieldHasPayloads)
                {
                    forUtil.WriteBlock(payloadLengthBuffer, encoded, payOut);
                    payOut.WriteVInt(payloadByteUpto);
                    payOut.WriteBytes(payloadBytes, 0, payloadByteUpto);
                    payloadByteUpto = 0;
                }
                if (fieldHasOffsets)
                {
                    forUtil.WriteBlock(offsetStartDeltaBuffer, encoded, payOut);
                    forUtil.WriteBlock(offsetLengthBuffer, encoded, payOut);
                }
                posBufferUpto = 0;
            }
        }

        public override void FinishDoc()
        {
            // Since we don't know df for current term, we had to buffer
            // those skip data for each block, and when a new doc comes, 
            // write them to skip file.
            if (docBufferUpto == Lucene41PostingsFormat.BLOCK_SIZE)
            {
                lastBlockDocID = lastDocID;
                if (posOut != null)
                {
                    if (payOut != null)
                    {
                        lastBlockPayFP = payOut.FilePointer;
                    }
                    lastBlockPosFP = posOut.FilePointer;
                    lastBlockPosBufferUpto = posBufferUpto;
                    lastBlockPayloadByteUpto = payloadByteUpto;
                }
                // if (DEBUG) {
                //   System.out.println("  docBufferUpto="+docBufferUpto+" now get lastBlockDocID="+lastBlockDocID+" lastBlockPosFP=" + lastBlockPosFP + " lastBlockPosBufferUpto=" +  lastBlockPosBufferUpto + " lastBlockPayloadByteUpto=" + lastBlockPayloadByteUpto);
                // }
                docBufferUpto = 0;
            }
        }

        private class PendingTerm
        {
            public readonly long docStartFP;
            public readonly long posStartFP;
            public readonly long payStartFP;
            public readonly long skipOffset;
            public readonly long lastPosBlockOffset;
            public readonly int singletonDocID;

            public PendingTerm(long docStartFP, long posStartFP, long payStartFP, long skipOffset, long lastPosBlockOffset, int singletonDocID)
            {
                this.docStartFP = docStartFP;
                this.posStartFP = posStartFP;
                this.payStartFP = payStartFP;
                this.skipOffset = skipOffset;
                this.lastPosBlockOffset = lastPosBlockOffset;
                this.singletonDocID = singletonDocID;
            }
        }

        private readonly IList<PendingTerm> pendingTerms = new List<PendingTerm>();

		public override void FinishTerm(BlockTermState _state)
        {
			Lucene41PostingsWriter.IntBlockTermState state = (Lucene41PostingsWriter.IntBlockTermState
				)_state;
            //assert stats.docFreq > 0;

            // TODO: wasteful we are counting this (counting # docs
            // for this term) in two places?
            //assert stats.docFreq == docCount: stats.docFreq + " vs " + docCount;

            // if (DEBUG) {
            //   System.out.println("FPW.finishTerm docFreq=" + stats.docFreq);
            // }

            // if (DEBUG) {
            //   if (docBufferUpto > 0) {
            //     System.out.println("  write doc/freq vInt block (count=" + docBufferUpto + ") at fp=" + docOut.getFilePointer() + " docTermStartFP=" + docTermStartFP);
            //   }
            // }

            // docFreq == 1, don't write the single docid/freq to a separate file along with a pointer to it.
            int singletonDocID;
			if (state.docFreq == 1)
            {
                // pulse the singleton docid into the term dictionary, freq is implicitly totalTermFreq
                singletonDocID = docDeltaBuffer[0];
            }
            else
            {
                singletonDocID = -1;
                // vInt encode the remaining doc deltas and freqs:
                for (int i = 0; i < docBufferUpto; i++)
                {
                    int docDelta = docDeltaBuffer[i];
                    int freq = freqBuffer[i];
                    if (!fieldHasFreqs)
                    {
                        docOut.WriteVInt(docDelta);
                    }
                    else if (freqBuffer[i] == 1)
                    {
                        docOut.WriteVInt((docDelta << 1) | 1);
                    }
                    else
                    {
                        docOut.WriteVInt(docDelta << 1);
                        docOut.WriteVInt(freq);
                    }
                }
            }

            long lastPosBlockOffset;

            if (fieldHasPositions)
            {
                // if (DEBUG) {
                //   if (posBufferUpto > 0) {
                //     System.out.println("  write pos vInt block (count=" + posBufferUpto + ") at fp=" + posOut.getFilePointer() + " posTermStartFP=" + posTermStartFP + " hasPayloads=" + fieldHasPayloads + " hasOffsets=" + fieldHasOffsets);
                //   }
                // }

                // totalTermFreq is just total number of positions(or payloads, or offsets)
                // associated with current term.
                //assert stats.totalTermFreq != -1;
				if (state.totalTermFreq > Lucene41PostingsFormat.BLOCK_SIZE)
                {
                    // record file offset for last pos in last block
					lastPosBlockOffset = posOut.FilePointer - posStartFP;
                }
                else
                {
                    lastPosBlockOffset = -1;
                }
                if (posBufferUpto > 0)
                {
                    // TODO: should we send offsets/payloads to
                    // .pay...?  seems wasteful (have to store extra
                    // vLong for low (< BLOCK_SIZE) DF terms = vast vast
                    // majority)

                    // vInt encode the remaining positions/payloads/offsets:
                    int lastPayloadLength = -1;  // force first payload length to be written
                    int lastOffsetLength = -1;   // force first offset length to be written
                    int payloadBytesReadUpto = 0;
                    for (int i = 0; i < posBufferUpto; i++)
                    {
                        int posDelta = posDeltaBuffer[i];
                        if (fieldHasPayloads)
                        {
                            int payloadLength = payloadLengthBuffer[i];
                            if (payloadLength != lastPayloadLength)
                            {
                                lastPayloadLength = payloadLength;
                                posOut.WriteVInt((posDelta << 1) | 1);
                                posOut.WriteVInt(payloadLength);
                            }
                            else
                            {
                                posOut.WriteVInt(posDelta << 1);
                            }

                            // if (DEBUG) {
                            //   System.out.println("        i=" + i + " payloadLen=" + payloadLength);
                            // }

                            if (payloadLength != 0)
                            {
                                // if (DEBUG) {
                                //   System.out.println("          write payload @ pos.fp=" + posOut.getFilePointer());
                                // }
                                posOut.WriteBytes(payloadBytes, payloadBytesReadUpto, payloadLength);
                                payloadBytesReadUpto += payloadLength;
                            }
                        }
                        else
                        {
                            posOut.WriteVInt(posDelta);
                        }

                        if (fieldHasOffsets)
                        {
                            // if (DEBUG) {
                            //   System.out.println("          write offset @ pos.fp=" + posOut.getFilePointer());
                            // }
                            int delta = offsetStartDeltaBuffer[i];
                            int length = offsetLengthBuffer[i];
                            if (length == lastOffsetLength)
                            {
                                posOut.WriteVInt(delta << 1);
                            }
                            else
                            {
                                posOut.WriteVInt(delta << 1 | 1);
                                posOut.WriteVInt(length);
                                lastOffsetLength = length;
                            }
                        }
                    }

                    if (fieldHasPayloads)
                    {
                        //assert payloadBytesReadUpto == payloadByteUpto;
                        payloadByteUpto = 0;
                    }
                }
                // if (DEBUG) {
                //   System.out.println("  totalTermFreq=" + stats.totalTermFreq + " lastPosBlockOffset=" + lastPosBlockOffset);
                // }
            }
            else
            {
                lastPosBlockOffset = -1;
            }

            long skipOffset;
            if (docCount > Lucene41PostingsFormat.BLOCK_SIZE)
            {
				skipOffset = skipWriter.WriteSkip(docOut) - docStartFP;

                // if (DEBUG) {
                //   System.out.println("skip packet " + (docOut.getFilePointer() - (docTermStartFP + skipOffset)) + " bytes");
                // }
            }
            else
            {
                skipOffset = -1;
                // if (DEBUG) {
                //   System.out.println("  no skip: docCount=" + docCount);
                // }
            }

			state.docStartFP = docStartFP;
			state.posStartFP = posStartFP;
			state.payStartFP = payStartFP;
			state.singletonDocID = singletonDocID;
			state.skipOffset = skipOffset;
			state.lastPosBlockOffset = lastPosBlockOffset;
            docBufferUpto = 0;
            posBufferUpto = 0;
            lastDocID = 0;
            docCount = 0;
        }

		/// <exception cref="System.IO.IOException"></exception>
		public override void EncodeTerm(long[] longs, DataOutput @out, FieldInfo fieldInfo
			, BlockTermState _state, bool absolute)
		{
			Lucene41PostingsWriter.IntBlockTermState state = (Lucene41PostingsWriter.IntBlockTermState
				)_state;
			if (absolute)
			{
				lastState = emptyState;
			}
			longs[0] = state.docStartFP - lastState.docStartFP;
			if (fieldHasPositions)
			{
				longs[1] = state.posStartFP - lastState.posStartFP;
				if (fieldHasPayloads || fieldHasOffsets)
				{
					longs[2] = state.payStartFP - lastState.payStartFP;
				}
			}
			if (state.singletonDocID != -1)
			{
				@out.WriteVInt(state.singletonDocID);
			}
			if (fieldHasPositions)
			{
				if (state.lastPosBlockOffset != -1)
				{
					@out.WriteVLong(state.lastPosBlockOffset);
				}
			}
			if (state.skipOffset != -1)
			{
				@out.WriteVLong(state.skipOffset);
			}
			lastState = state;
		}

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IOUtils.Close(docOut, posOut, payOut);
            }
        }
    }
}
