﻿using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ThreadState = Lucene.Net.Index.DocumentsWriterPerThreadPool.ThreadState;

namespace Lucene.Net.Index
{
    public abstract class FlushPolicy : ICloneable
    {
        protected LiveIndexWriterConfig indexWriterConfig;

		protected internal InfoStream infoStream;
        public abstract void OnDelete(DocumentsWriterFlushControl control, ThreadState state);

        public virtual void OnUpdate(DocumentsWriterFlushControl control, ThreadState state)
        {
            OnInsert(control, state);
            OnDelete(control, state);
        }

        public abstract void OnInsert(DocumentsWriterFlushControl control, ThreadState state);

		protected internal virtual void Init(LiveIndexWriterConfig indexWriterConfig)
        {
            lock (this)
            {
				this.indexWriterConfig = indexWriterConfig;
				infoStream = indexWriterConfig.InfoStream;
            }
        }

        protected ThreadState FindLargestNonPendingWriter(
            DocumentsWriterFlushControl control, ThreadState perThreadState)
        {
            //assert perThreadState.dwpt.getNumDocsInRAM() > 0;
            long maxRamSoFar = perThreadState.bytesUsed;
            // the dwpt which needs to be flushed eventually
            ThreadState maxRamUsingThreadState = perThreadState;
            //assert !perThreadState.flushPending : "DWPT should have flushed";
            IEnumerator<ThreadState> activePerThreadsIterator = control.AllActiveThreadStates;
            while (activePerThreadsIterator.MoveNext())
            {
                ThreadState next = activePerThreadsIterator.Current;
                if (!next.flushPending)
                {
                    long nextRam = next.bytesUsed;
                    if (nextRam > maxRamSoFar && next.dwpt.NumDocsInRAM > 0)
                    {
                        maxRamSoFar = nextRam;
                        maxRamUsingThreadState = next;
                    }
                }
            }
            //assert assertMessage("set largest ram consuming thread pending on lower watermark");
            return maxRamUsingThreadState;
        }

        private bool AssertMessage(String s)
        {
			if (infoStream.IsEnabled("FP"))
            {
				infoStream.Message("FP", s);
            }
            return true;
        }

        public object Clone()
        {
            FlushPolicy clone = (FlushPolicy)this.MemberwiseClone();
			// should not happen
            clone.indexWriterConfig = null;
			clone.infoStream = null;
            return clone;
        }
    }
}
