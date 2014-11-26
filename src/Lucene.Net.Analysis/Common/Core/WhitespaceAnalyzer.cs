﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Version = Lucene.Net.Util.Version;

namespace Lucene.Net.Analysis.Core
{
    public sealed class WhitespaceAnalyzer : Analyzer
    {
        private readonly Version? matchVersion;

        public WhitespaceAnalyzer(Version? matchVersion)
        {
            this.matchVersion = matchVersion;
        }

        public override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            return new TokenStreamComponents(new WhitespaceTokenizer(matchVersion, reader));
        }
    }
}
