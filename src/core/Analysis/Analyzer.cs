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
using System.IO;
using Lucene.Net.Store;
using Lucene.Net.Support;
using Lucene.Net.Util;

namespace Lucene.Net.Analysis
{
    /// <summary>
    /// An Analyzer builds TokenStreams, which analyze text.  It thus represents a
    /// policy for extracting index terms from text.
    /// <p/>
    /// Typical implementations first build a Tokenizer, which breaks the stream of
    /// characters from the Reader into raw Tokens.  One or more TokenFilters may
    /// then be applied to the output of the Tokenizer.
    /// </summary>
    public abstract class Analyzer : IDisposable
    {
        private readonly ReuseStrategy reuseStrategy;
        private bool isDisposed;

        public Analyzer(): this(new GlobalReuseStrategy())
        {
        }

        public Analyzer(ReuseStrategy reuseStrategy)
        {
            this.reuseStrategy = reuseStrategy;
        }

        public abstract TokenStreamComponents CreateComponents(string fieldName, TextReader reader);

        /// <summary>Creates a TokenStream which tokenizes all the text in the provided
        /// Reader.  Must be able to handle null field name for
        /// backward compatibility.
        /// </summary>
        public TokenStream TokenStream(String fieldName, TextReader reader)
        {
            TokenStreamComponents components = reuseStrategy.GetReusableComponents(fieldName);
            System.IO.TextReader r = InitReader(fieldName, reader);

            if (components == null)
            {
                components = CreateComponents(fieldName, reader);
                reuseStrategy.SetReusableComponents(fieldName, components);
            }
            else
            {
                components.SetReader(reader);
            }

            return components.TokenStream;
        }

        public TokenStream TokenStream(string fieldName, string text)
        {
            TokenStreamComponents components = reuseStrategy.GetReusableComponents(fieldName);
            ReusableStringReader strReader = (components == null || components.reusableStringReader
                 == null) ? new ReusableStringReader() : components.reusableStringReader;
            strReader.SetValue(text);
            TextReader r = InitReader(fieldName, strReader);
            if (components == null)
            {
                components = CreateComponents(fieldName, r);
                reuseStrategy.SetReusableComponents(fieldName, components);
            }
            else
            {
                components.SetReader(r);
            }
            components.reusableStringReader = strReader;
            return components.TokenStream;
        }
        public virtual TextReader InitReader(String fieldName, System.IO.TextReader reader)
        {
            return reader;
        }


        /// <summary> Invoked before indexing a Fieldable instance if
        /// terms have already been added to that field.  This allows custom
        /// analyzers to place an automatic position increment gap between
        /// Fieldable instances using the same field name.  The default value
        /// position increment gap is 0.  With a 0 position increment gap and
        /// the typical default token position increment of 1, all terms in a field,
        /// including across Fieldable instances, are in successive positions, allowing
        /// exact PhraseQuery matches, for instance, across Fieldable instance boundaries.
        /// 
        /// </summary>
        /// <param name="fieldName">Fieldable name being indexed.
        /// </param>
        /// <returns> position increment gap, added to the next token emitted from <see cref="TokenStream(String,System.IO.TextReader)" />
        /// </returns>
        public virtual int GetPositionIncrementGap(String fieldName)
        {
            return 0;
        }

        /// <summary> Just like <see cref="GetPositionIncrementGap" />, except for
        /// Token offsets instead.  By default this returns 1 for
        /// tokenized fields and, as if the fields were joined
        /// with an extra space character, and 0 for un-tokenized
        /// fields.  This method is only called if the field
        /// produced at least one token for indexing.
        /// 
        /// </summary>
        /// <param name="fieldName">the field just indexed
        /// </param>
        /// <returns> offset gap, added to the next token emitted from <see cref="TokenStream(String,System.IO.TextReader)" />
        /// </returns>
        public virtual int GetOffsetGap(string fieldName)
        {
            return 1;
        }

        /// <summary>
        /// Returns the used
        /// <see cref="ReuseStrategy">ReuseStrategy</see>
        /// .
        /// </summary>
        public ReuseStrategy GetReuseStrategy()
        {
            return reuseStrategy;
        }

        public virtual void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed) return;

            if (disposing)
            {
                reuseStrategy.Dispose();
            }
            isDisposed = true;
        }

        public class TokenStreamComponents
        {
            protected readonly Tokenizer source;
            protected readonly TokenStream sink;

            [System.NonSerialized]
            internal ReusableStringReader reusableStringReader;
            public TokenStreamComponents(Tokenizer source, TokenStream result)
            {
                this.source = source;
                this.sink = result;
            }

            public TokenStreamComponents(Tokenizer source)
            {
                this.source = source;
                this.sink = source;
            }

            public virtual void SetReader(System.IO.TextReader reader)
            {
                source.Reader = reader;
            }

            public TokenStream TokenStream
            {
                get
                {
                    return sink;
                }
            }

            public Tokenizer Tokenizer
            {
                get
                {
                    return source;
                }
            }
        }

        public abstract class ReuseStrategy : IDisposable
        {
            private CloseableThreadLocal<Object> storedValue = new CloseableThreadLocal<Object>();

            public ReuseStrategy()
            {
            }

            public abstract TokenStreamComponents GetReusableComponents(string fieldName);

            public abstract void SetReusableComponents(string fieldName, TokenStreamComponents components);

            protected Object StoredValue
            {
                get
                {
                    if (storedValue == null)
                        throw new AlreadyClosedException("this Analyzer is disposed");

                    return storedValue.Get();
                }
                set
                {
                    if (storedValue == null)
                        throw new AlreadyClosedException("this Analyzer is disposed");

                    storedValue.Set(value);
                }
            }

            public void Dispose()
            {
                if (storedValue != null)
                {
                    storedValue.Dispose();
                    storedValue = null;
                }
            }
        }

        public static readonly Analyzer.ReuseStrategy GLOBAL_REUSE_STRATEGY = new GlobalReuseStrategy();
        [Obsolete(@"This implementation class will be hidden in Lucene 5.0. Use Analyzer.GLOBAL_REUSE_STRATEGY instead!")]
        public sealed class GlobalReuseStrategy : ReuseStrategy
        {
            public GlobalReuseStrategy()
            {
            }

            public override TokenStreamComponents GetReusableComponents(string fieldName)
            {
                return (TokenStreamComponents)StoredValue;
            }

            public override void SetReusableComponents(string fieldName, TokenStreamComponents components)
            {
                StoredValue = components;
            }
        }

        /// <summary>
        /// A predefined
        /// <see cref="ReuseStrategy">ReuseStrategy</see>
        /// that reuses components per-field by
        /// maintaining a Map of TokenStreamComponent per field name.
        /// </summary>
        public static readonly Analyzer.ReuseStrategy PER_FIELD_REUSE_STRATEGY = new Analyzer.PerFieldReuseStrategy();

        /// <summary>
        /// Implementation of
        /// <see cref="ReuseStrategy">ReuseStrategy</see>
        /// that reuses components per-field by
        /// maintaining a Map of TokenStreamComponent per field name.
        /// </summary>
        [Obsolete(@"This implementation class will be hidden in Lucene 5.0. Use Analyzer.PER_FIELD_REUSE_STRATEGY instead!")]
        public sealed class PerFieldReuseStrategy : ReuseStrategy
        {
            
            [Obsolete(@"Don't create instances of this class, use Analyzer.PER_FIELD_REUSE_STRATEGY")]
            public PerFieldReuseStrategy()
            {
            }

            public override TokenStreamComponents GetReusableComponents(string fieldName)
            {
                var componentsPerField = (HashMap<string, TokenStreamComponents>)StoredValue;

                return componentsPerField != null ? componentsPerField[fieldName] : null;
            }

            public override void SetReusableComponents(string fieldName, TokenStreamComponents components)
            {
                var componentsPerField = (HashMap<string, TokenStreamComponents>)StoredValue;

                if (componentsPerField == null)
                {
                    componentsPerField = new HashMap<string, TokenStreamComponents>();
                    StoredValue = componentsPerField;
                }

                componentsPerField[fieldName] = components;
            }
        }
    }
}