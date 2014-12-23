﻿using Lucene.Net.Store;
using Lucene.Net.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lucene.Net.Codecs.Lucene41
{
    internal sealed class Lucene41SkipWriter : MultiLevelSkipListWriter
    {
        // private boolean DEBUG = Lucene41PostingsReader.DEBUG;

        private int[] lastSkipDoc;
        private long[] lastSkipDocPointer;
        private long[] lastSkipPosPointer;
        private long[] lastSkipPayPointer;
        private int[] lastPayloadByteUpto;

        private readonly IndexOutput docOut;
        private readonly IndexOutput posOut;
        private readonly IndexOutput payOut;

        private int curDoc;
        private long curDocPointer;
        private long curPosPointer;
        private long curPayPointer;
        private int curPosBufferUpto;
        private int curPayloadByteUpto;
        private bool fieldHasPositions;
        private bool fieldHasOffsets;
        private bool fieldHasPayloads;

        public Lucene41SkipWriter(int maxSkipLevels, int blockSize, int docCount, IndexOutput docOut, IndexOutput posOut, IndexOutput payOut)
            : base(blockSize, 8, maxSkipLevels, docCount)
        {
            this.docOut = docOut;
            this.posOut = posOut;
            this.payOut = payOut;

            lastSkipDoc = new int[maxSkipLevels];
            lastSkipDocPointer = new long[maxSkipLevels];
            if (posOut != null)
            {
                lastSkipPosPointer = new long[maxSkipLevels];
                if (payOut != null)
                {
                    lastSkipPayPointer = new long[maxSkipLevels];
                }
                lastPayloadByteUpto = new int[maxSkipLevels];
            }
        }

        public void SetField(bool fieldHasPositions, bool fieldHasOffsets, bool fieldHasPayloads)
        {
            this.fieldHasPositions = fieldHasPositions;
            this.fieldHasOffsets = fieldHasOffsets;
            this.fieldHasPayloads = fieldHasPayloads;
        }

        public override void ResetSkip()
        {
            base.ResetSkip();
            Arrays.Fill(lastSkipDoc, 0);
            Arrays.Fill(lastSkipDocPointer, docOut.FilePointer);
            if (fieldHasPositions)
            {
                Arrays.Fill(lastSkipPosPointer, posOut.FilePointer);
                if (fieldHasPayloads)
                {
                    Arrays.Fill(lastPayloadByteUpto, 0);
                }
                if (fieldHasOffsets || fieldHasPayloads)
                {
                    Arrays.Fill(lastSkipPayPointer, payOut.FilePointer);
                }
            }
        }

        public void BufferSkip(int doc, int numDocs, long posFP, long payFP, int posBufferUpto, int payloadByteUpto)
        {
            this.curDoc = doc;
            this.curDocPointer = docOut.FilePointer;
            this.curPosPointer = posFP;
            this.curPayPointer = payFP;
            this.curPosBufferUpto = posBufferUpto;
            this.curPayloadByteUpto = payloadByteUpto;
            BufferSkip(numDocs);
        }

        protected override void WriteSkipData(int level, IndexOutput skipBuffer)
        {
            int delta = curDoc - lastSkipDoc[level];
            // if (DEBUG) {
            //   System.out.println("writeSkipData level=" + level + " lastDoc=" + curDoc + " delta=" + delta + " curDocPointer=" + curDocPointer);
            // }
            skipBuffer.WriteVInt(delta);
            lastSkipDoc[level] = curDoc;

            skipBuffer.WriteVInt((int)(curDocPointer - lastSkipDocPointer[level]));
            lastSkipDocPointer[level] = curDocPointer;

            if (fieldHasPositions)
            {
                // if (DEBUG) {
                //   System.out.println("  curPosPointer=" + curPosPointer + " curPosBufferUpto=" + curPosBufferUpto);
                // }
                skipBuffer.WriteVInt((int)(curPosPointer - lastSkipPosPointer[level]));
                lastSkipPosPointer[level] = curPosPointer;
                skipBuffer.WriteVInt(curPosBufferUpto);

                if (fieldHasPayloads)
                {
                    skipBuffer.WriteVInt(curPayloadByteUpto);
                }

                if (fieldHasOffsets || fieldHasPayloads)
                {
                    skipBuffer.WriteVInt((int)(curPayPointer - lastSkipPayPointer[level]));
                    lastSkipPayPointer[level] = curPayPointer;
                }
            }
        }
    }
}
