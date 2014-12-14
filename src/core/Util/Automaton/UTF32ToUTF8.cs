﻿using Lucene.Net.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lucene.Net.Util.Automaton
{
    public sealed class UTF32ToUTF8
    {
        private static readonly int[] startCodes = new int[] { 0, 128, 2048, 65536 };
        private static readonly int[] endCodes = new int[] { 127, 2047, 65535, 1114111 };

        static int[] MASKS = new int[32];
        static UTF32ToUTF8()
        {
            int v = 2;
            for (int i = 0; i < 32; i++)
            {
                MASKS[i] = v - 1;
                v *= 2;
            }
        }

        // Represents one of the N utf8 bytes that (in sequence)
        // define a code point.  value is the byte value; bits is
        // how many bits are "used" by utf8 at that byte
        private class UTF8Byte
        {
            internal int value;                                    // TODO: change to byte
            internal sbyte bits;
        }

        // Holds a single code point, as a sequence of 1-4 utf8 bytes:
        // TODO: maybe move to UnicodeUtil?
        private class UTF8Sequence
        {
            private UTF8Byte[] bytes;
            internal int len;

            public UTF8Sequence()
            {
                bytes = new UTF8Byte[4];
                for (int i = 0; i < 4; i++)
                {
                    bytes[i] = new UTF8Byte();
                }
            }

            public int ByteAt(int idx)
            {
                return bytes[idx].value;
            }

            public int NumBits(int idx)
            {
                return bytes[idx].bits;
            }

            public void Set(int code)
            {
                if (code < 128)
                {
                    // 0xxxxxxx
                    bytes[0].value = code;
                    bytes[0].bits = 7;
                    len = 1;
                }
                else if (code < 2048)
                {
                    // 110yyyxx 10xxxxxx
                    bytes[0].value = (6 << 5) | (code >> 6);
                    bytes[0].bits = 5;
                    SetRest(code, 1);
                    len = 2;
                }
                else if (code < 65536)
                {
                    // 1110yyyy 10yyyyxx 10xxxxxx
                    bytes[0].value = (14 << 4) | (code >> 12);
                    bytes[0].bits = 4;
                    SetRest(code, 2);
                    len = 3;
                }
                else
                {
                    // 11110zzz 10zzyyyy 10yyyyxx 10xxxxxx
                    bytes[0].value = (30 << 3) | (code >> 18);
                    bytes[0].bits = 3;
                    SetRest(code, 3);
                    len = 4;
                }
            }

            public void SetRest(int code, int numBytes)
            {
                for (int i = 0; i < numBytes; i++)
                {
                    bytes[numBytes - i].value = 128 | (code & MASKS[5]);
                    bytes[numBytes - i].bits = 6;
                    code = code >> 6;
                }
            }

            public override String ToString()
            {
                StringBuilder b = new StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    if (i > 0)
                    {
                        b.Append(' ');
                    }
                    b.Append(bytes[i].value.ToBinaryString());
                }
                return b.ToString();
            }
        }

        private readonly UTF8Sequence startUTF8 = new UTF8Sequence();
        private readonly UTF8Sequence endUTF8 = new UTF8Sequence();

        private readonly UTF8Sequence tmpUTF8a = new UTF8Sequence();
        private readonly UTF8Sequence tmpUTF8b = new UTF8Sequence();

        internal void ConvertOneEdge(State start, State end, int startCodePoint, int endCodePoint)
        {
            startUTF8.Set(startCodePoint);
            endUTF8.Set(endCodePoint);
            //System.out.println("start = " + startUTF8);
            //System.out.println("  end = " + endUTF8);
            Build(start, end, startUTF8, endUTF8, 0);
        }

        private void Build(State start, State end, UTF8Sequence startUTF8, UTF8Sequence endUTF8, int upto)
        {

            // Break into start, middle, end:
            if (startUTF8.ByteAt(upto) == endUTF8.ByteAt(upto))
            {
                // Degen case: lead with the same byte:
                if (upto == startUTF8.len - 1 && upto == endUTF8.len - 1)
                {
                    // Super degen: just single edge, one UTF8 byte:
                    start.AddTransition(new Transition(startUTF8.ByteAt(upto), endUTF8.ByteAt(upto), end));
                    return;
                }
                else
                {
                    //assert startUTF8.len > upto+1;
                    //assert endUTF8.len > upto+1;
                    State n = NewUTF8State();

                    // Single value leading edge
                    start.AddTransition(new Transition(startUTF8.ByteAt(upto), n));  // type=single

                    // Recurse for the rest
                    Build(n, end, startUTF8, endUTF8, 1 + upto);
                }
            }
            else if (startUTF8.len == endUTF8.len)
            {
                if (upto == startUTF8.len - 1)
                {
                    start.AddTransition(new Transition(startUTF8.ByteAt(upto), endUTF8.ByteAt(upto), end));        // type=startend
                }
                else
                {
                    Start(start, end, startUTF8, upto, false);
                    if (endUTF8.ByteAt(upto) - startUTF8.ByteAt(upto) > 1)
                    {
                        // There is a middle
                        All(start, end, startUTF8.ByteAt(upto) + 1, endUTF8.ByteAt(upto) - 1, startUTF8.len - upto - 1);
                    }
                    End(start, end, endUTF8, upto, false);
                }
            }
            else
            {

                // start
                Start(start, end, startUTF8, upto, true);

                // possibly middle, spanning multiple num bytes
                int byteCount = 1 + startUTF8.len - upto;
                int limit = endUTF8.len - upto;
                while (byteCount < limit)
                {
                    // wasteful: we only need first byte, and, we should
                    // statically encode this first byte:
                    tmpUTF8a.Set(startCodes[byteCount - 1]);
                    tmpUTF8b.Set(endCodes[byteCount - 1]);
                    All(start, end,
                        tmpUTF8a.ByteAt(0),
                        tmpUTF8b.ByteAt(0),
                        tmpUTF8a.len - 1);
                    byteCount++;
                }

                // end
                End(start, end, endUTF8, upto, true);
            }
        }

        private void Start(State start, State end, UTF8Sequence utf8, int upto, bool doAll)
        {
            if (upto == utf8.len - 1)
            {
                // Done recursing
                start.AddTransition(new Transition(utf8.ByteAt(upto), utf8.ByteAt(upto) | MASKS[utf8.NumBits(upto) - 1], end));  // type=start
            }
            else
            {
                State n = NewUTF8State();
                start.AddTransition(new Transition(utf8.ByteAt(upto), n));  // type=start
                Start(n, end, utf8, 1 + upto, true);
                int endCode = utf8.ByteAt(upto) | MASKS[utf8.NumBits(upto) - 1];
                if (doAll && utf8.ByteAt(upto) != endCode)
                {
                    All(start, end, utf8.ByteAt(upto) + 1, endCode, utf8.len - upto - 1);
                }
            }
        }

        private void End(State start, State end, UTF8Sequence utf8, int upto, bool doAll)
        {
            if (upto == utf8.len - 1)
            {
                // Done recursing
                start.AddTransition(new Transition(utf8.ByteAt(upto) & (~MASKS[utf8.NumBits(upto) - 1]), utf8.ByteAt(upto), end));   // type=end
            }
            else
            {
                int startCode;
                if (utf8.NumBits(upto) == 5)
                {
                    // special case -- avoid created unused edges (utf8
                    // doesn't accept certain byte sequences) -- there
                    // are other cases we could optimize too:
                    startCode = 194;
                }
                else
                {
                    startCode = utf8.ByteAt(upto) & (~MASKS[utf8.NumBits(upto) - 1]);
                }
                if (doAll && utf8.ByteAt(upto) != startCode)
                {
                    All(start, end, startCode, utf8.ByteAt(upto) - 1, utf8.len - upto - 1);
                }
                State n = NewUTF8State();
                start.AddTransition(new Transition(utf8.ByteAt(upto), n));  // type=end
                End(n, end, utf8, 1 + upto, true);
            }
        }

        private void All(State start, State end, int startCode, int endCode, int left)
        {
            if (left == 0)
            {
                start.AddTransition(new Transition(startCode, endCode, end));  // type=all
            }
            else
            {
                State lastN = NewUTF8State();
                start.AddTransition(new Transition(startCode, endCode, lastN));  // type=all
                while (left > 1)
                {
                    State n = NewUTF8State();
                    lastN.AddTransition(new Transition(128, 191, n));  // type=all*
                    left--;
                    lastN = n;
                }
                lastN.AddTransition(new Transition(128, 191, end)); // type = all*
            }
        }

        private State[] utf8States;
        private int utf8StateCount;

        public Automaton Convert(Automaton utf32)
        {
            if (utf32.IsSingleton())
            {
                utf32 = utf32.CloneExpanded();
            }

            State[] map = new State[utf32.GetNumberedStates().Length];
            List<State> pending = new List<State>();
            State utf32State = utf32.InitialState;
            pending.Add(utf32State);
            Automaton utf8 = new Automaton();
            utf8.Deterministic = false;

            State utf8State = utf8.InitialState;

            utf8States = new State[5];
            utf8StateCount = 0;
            utf8State.number = utf8StateCount;
            utf8States[utf8StateCount] = utf8State;
            utf8StateCount++;

            utf8State.Accept = utf32State.Accept;

            map[utf32State.number] = utf8State;

            while (pending.Count != 0)
            {
                utf32State = pending[pending.Count - 1];
                pending.RemoveAt(pending.Count - 1);
                utf8State = map[utf32State.number];
                for (int i = 0; i < utf32State.numTransitions; i++)
                {
                    Transition t = utf32State.transitionsArray[i];
                    State destUTF32 = t.to;
                    State destUTF8 = map[destUTF32.number];
                    if (destUTF8 == null)
                    {
                        destUTF8 = NewUTF8State();
                        destUTF8.Accept = destUTF32.Accept;
                        map[destUTF32.number] = destUTF8;
                        pending.Add(destUTF32);
                    }
                    ConvertOneEdge(utf8State, destUTF8, t.min, t.max);
                }
            }

            utf8.SetNumberedStates(utf8States, utf8StateCount);

            return utf8;
        }

        private State NewUTF8State()
        {
            State s = new State();
            if (utf8StateCount == utf8States.Length)
            {
                State[] newArray = new State[ArrayUtil.Oversize(1 + utf8StateCount, RamUsageEstimator.NUM_BYTES_OBJECT_REF)];
                Array.Copy(utf8States, 0, newArray, 0, utf8StateCount);
                utf8States = newArray;
            }
            utf8States[utf8StateCount] = s;
            s.number = utf8StateCount;
            utf8StateCount++;
            return s;
        }
    }
}
