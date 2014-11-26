/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

//using System;
//using System.Collections.Generic;
//using Lucene.Net.Analysis.Util;

//namespace Lucene.Net.Analysis.Compound
//{
//    /// <summary>
//    /// Factory for
//    /// <see cref="HyphenationCompoundWordTokenFilter">HyphenationCompoundWordTokenFilter
//    /// 	</see>
//    /// .
//    /// <p>
//    /// This factory accepts the following parameters:
//    /// <ul>
//    /// <li><code>hyphenator</code> (mandatory): path to the FOP xml hyphenation pattern.
//    /// See <a href="http://offo.sourceforge.net/hyphenation/">http://offo.sourceforge.net/hyphenation/</a>.
//    /// <li><code>encoding</code> (optional): encoding of the xml hyphenation file. defaults to UTF-8.
//    /// <li><code>dictionary</code> (optional): dictionary of words. defaults to no dictionary.
//    /// <li><code>minWordSize</code> (optional): minimal word length that gets decomposed. defaults to 5.
//    /// <li><code>minSubwordSize</code> (optional): minimum length of subwords. defaults to 2.
//    /// <li><code>maxSubwordSize</code> (optional): maximum length of subwords. defaults to 15.
//    /// <li><code>onlyLongestMatch</code> (optional): if true, adds only the longest matching subword
//    /// to the stream. defaults to false.
//    /// </ul>
//    /// <p>
//    /// <pre class="prettyprint">
//    /// &lt;fieldType name="text_hyphncomp" class="TextField" positionIncrementGap="100"&gt;
//    /// &lt;analyzer&gt;
//    /// &lt;tokenizer class="WhitespaceTokenizerFactory"/&gt;
//    /// &lt;filter class="HyphenationCompoundWordTokenFilterFactory" hyphenator="hyphenator.xml" encoding="UTF-8"
//    /// dictionary="dictionary.txt" minWordSize="5" minSubwordSize="2" maxSubwordSize="15" onlyLongestMatch="false"/&gt;
//    /// &lt;/analyzer&gt;
//    /// &lt;/fieldType&gt;</pre>
//    /// </summary>
//    /// <seealso cref="HyphenationCompoundWordTokenFilter">HyphenationCompoundWordTokenFilter
//    /// 	</seealso>
//    public class HyphenationCompoundWordTokenFilterFactory : TokenFilterFactory, IResourceLoaderAware
//    {
//        private CharArraySet dictionary;

//        private HyphenationTree hyphenator;

//        private readonly string dictFile;

//        private readonly string hypFile;

//        private readonly string encoding;

//        private readonly int minWordSize;

//        private readonly int minSubwordSize;

//        private readonly int maxSubwordSize;

//        private readonly bool onlyLongestMatch;

//        /// <summary>Creates a new HyphenationCompoundWordTokenFilterFactory</summary>
//        protected internal HyphenationCompoundWordTokenFilterFactory(IDictionary<string, 
//            string> args) : base(args)
//        {
//            AssureMatchVersion();
//            dictFile = Get(args, "dictionary");
//            encoding = Get(args, "encoding");
//            hypFile = Require(args, "hyphenator");
//            minWordSize = GetInt(args, "minWordSize", CompoundWordTokenFilterBase.DEFAULT_MIN_WORD_SIZE
//                );
//            minSubwordSize = GetInt(args, "minSubwordSize", CompoundWordTokenFilterBase.DEFAULT_MIN_SUBWORD_SIZE
//                );
//            maxSubwordSize = GetInt(args, "maxSubwordSize", CompoundWordTokenFilterBase.DEFAULT_MAX_SUBWORD_SIZE
//                );
//            onlyLongestMatch = GetBoolean(args, "onlyLongestMatch", false);
//            if (args.Any())
//            {
//                throw new ArgumentException("Unknown parameters: " + args);
//            }
//        }

//        /// <exception cref="System.IO.IOException"></exception>
//        public virtual void Inform(IResourceLoader loader)
//        {
//            InputStream stream = null;
//            try
//            {
//                if (dictFile != null)
//                {
//                    // the dictionary can be empty.
//                    dictionary = GetWordSet(loader, dictFile, false);
//                }
//                // TODO: Broken, because we cannot resolve real system id
//                // ResourceLoader should also supply method like ClassLoader to get resource URL
//                stream = loader.OpenResource(hypFile);
//                InputSource @is = new InputSource(stream);
//                @is.SetEncoding(encoding);
//                // if it's null let xml parser decide
//                @is.SetSystemId(hypFile);
//                hyphenator = HyphenationCompoundWordTokenFilter.GetHyphenationTree(@is);
//            }
//            finally
//            {
//                IOUtils.CloseWhileHandlingException(stream);
//            }
//        }

//        public override TokenStream Create(TokenStream input)
//        {
//            return new HyphenationCompoundWordTokenFilter(luceneMatchVersion, input, hyphenator
//                , dictionary, minWordSize, minSubwordSize, maxSubwordSize, onlyLongestMatch);
//        }
//    }
//}
