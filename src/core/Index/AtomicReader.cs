﻿using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Lucene.Net.Index
{
    public abstract class AtomicReader : IndexReader
    {
        private readonly AtomicReaderContext readerContext;

        protected AtomicReader()
            : base()
        {
            // .NET port: can't reference "this" until we're in the constructor, so not inline above like in java version.
            this.readerContext = new AtomicReaderContext(this);
        }

        public sealed override IndexReaderContext Context
        {
            get
            {
                EnsureOpen();
                return readerContext;
            }
        }

        // .NET Port: This is needed since Context can't change return type on override
        public AtomicReaderContext AtomicContext
        {
            get
            {
                EnsureOpen();
                return readerContext;
            }
        }

        [Obsolete(@"(4.0) use GetFieldInfos() and check FieldInfo.HasNorms() for the field instead.")]
        public bool HasNorms(string field)
        {
            EnsureOpen();
            // note: using normValues(field) != null would potentially cause i/o
            FieldInfo fi = FieldInfos.FieldInfo(field);
            return fi != null && fi.HasNorms;
        }

        public abstract Fields Fields { get; }

        public sealed override int DocFreq(Term term)
        {
            Fields fields = Fields;
            if (fields == null)
            {
                return 0;
            }
            Terms terms = fields.Terms(term.Field);
            if (terms == null)
            {
                return 0;
            }
            TermsEnum termsEnum = terms.Iterator(null);
			if (termsEnum.SeekExact(term.Bytes))
            {
                return termsEnum.DocFreq;
            }
            else
            {
                return 0;
            }
        }

        public sealed override long TotalTermFreq(Term term)
        {
            Fields fields = Fields;
            if (fields == null)
            {
                return 0;
            }
            Terms terms = fields.Terms(term.Field);
            if (terms == null)
            {
                return 0;
            }
            TermsEnum termsEnum = terms.Iterator(null);
			if (termsEnum.SeekExact(term.Bytes))
            {
                return termsEnum.TotalTermFreq;
            }
            else
            {
                return 0;
            }
        }

        public sealed override long GetSumDocFreq(String field)
        {
            Terms terms = Terms(field);
            if (terms == null)
            {
                return 0;
            }
            return terms.SumDocFreq;
        }

        public sealed override int GetDocCount(String field)
        {
            Terms terms = Terms(field);
            if (terms == null)
            {
                return 0;
            }
            return terms.DocCount;
        }

        public sealed override long GetSumTotalTermFreq(String field)
        {
            Terms terms = Terms(field);
            if (terms == null)
            {
                return 0;
            }
            return terms.SumTotalTermFreq;
        }

        /** This may return null if the field does not exist.*/
        public Terms Terms(String field)
        {
            Fields fields = Fields;
            if (fields == null)
            {
                return null;
            }
            return fields.Terms(field);
        }

        public DocsEnum TermDocsEnum(Term term)
        {
            if (term.Field == null)
                throw new IOException();
            if (term.Bytes == null)
                throw new IOException();
            Fields fields = Fields;
            if (fields != null)
            {
                Terms terms = fields.Terms(term.Field);
                if (terms != null)
                {
                    TermsEnum termsEnum = terms.Iterator(null);
                    if (termsEnum.SeekExact(term.Bytes, true))
                    {
                        return termsEnum.Docs(LiveDocs, null);
                    }
                }
            }
            return null;
        }
        
        public DocsAndPositionsEnum TermPositionsEnum(Term term)
        {
            if (term.Field == null)
                throw new IOException();
            if (term.Bytes == null)
                throw new IOException();

            Fields fields = Fields;
            if (fields != null)
            {
                Terms terms = fields.Terms(term.Field);
                if (terms != null)
                {
                    TermsEnum termsEnum = terms.Iterator(null);
                    if (termsEnum.SeekExact(term.Bytes, true))
                    {
                        return termsEnum.DocsAndPositions(LiveDocs, null);
                    }
                }
            }
            return null;
        }

        public abstract NumericDocValues GetNumericDocValues(String field);
        
        public abstract BinaryDocValues GetBinaryDocValues(String field);
        
        public abstract SortedDocValues GetSortedDocValues(String field);
        
        public abstract SortedSetDocValues GetSortedSetDocValues(String field);
        
		public abstract IBits GetDocsWithField(string field);
        public abstract NumericDocValues GetNormValues(String field);

        public abstract FieldInfos FieldInfos { get; }
        
        public abstract IBits LiveDocs { get; }
		public abstract void CheckIntegrity();
    }
}
