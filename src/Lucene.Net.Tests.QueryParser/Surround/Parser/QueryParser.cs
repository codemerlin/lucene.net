/*
 * This code is derived from MyJavaLibrary (http://somelinktomycoollibrary)
 * 
 * If this is an open source Java library, include the proper license and copyright attributions here!
 */

using System;
using System.Collections.Generic;
using System.IO;
using Org.Apache.Lucene.Queryparser.Surround.Parser;
using Org.Apache.Lucene.Queryparser.Surround.Query;
using Sharpen;

namespace Org.Apache.Lucene.Queryparser.Surround.Parser
{
	/// <summary>This class is generated by JavaCC.</summary>
	/// <remarks>
	/// This class is generated by JavaCC.  The only method that clients should need
	/// to call is
	/// <see cref="Parse(string)">parse()</see>
	/// .
	/// <p>This parser generates queries that make use of position information
	/// (Span queries). It provides positional operators (<code>w</code> and
	/// <code>n</code>) that accept a numeric distance, as well as boolean
	/// operators (<code>and</code>, <code>or</code>, and <code>not</code>,
	/// wildcards (<code>*</code> and <code>?</code>), quoting (with
	/// <code>"</code>), and boosting (via <code>^</code>).</p>
	/// <p>The operators (W, N, AND, OR, NOT) can be expressed lower-cased or
	/// upper-cased, and the non-unary operators (everything but NOT) support
	/// both infix <code>(a AND b AND c)</code> and prefix <code>AND(a, b,
	/// c)</code> notation. </p>
	/// <p>The W and N operators express a positional relationship among their
	/// operands.  N is ordered, and W is unordered.  The distance is 1 by
	/// default, meaning the operands are adjacent, or may be provided as a
	/// prefix from 2-99.  So, for example, 3W(a, b) means that terms a and b
	/// must appear within three positions of each other, or in other words, up
	/// to two terms may appear between a and b.  </p>
	/// </remarks>
	public class QueryParser : QueryParserConstants
	{
		internal readonly int minimumPrefixLength = 3;

		internal readonly int minimumCharsInTrunc = 3;

		internal readonly string truncationErrorMessage = "Too unrestrictive truncation: ";

		internal readonly string boostErrorMessage = "Cannot handle boost value: ";

		internal readonly char truncator = '*';

		internal readonly char anyChar = '?';

		internal readonly char quote = '"';

		internal readonly char fieldOperator = ':';

		internal readonly char comma = ',';

		internal readonly char carat = '^';

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public static SrndQuery Parse(string query)
		{
			Org.Apache.Lucene.Queryparser.Surround.Parser.QueryParser parser = new Org.Apache.Lucene.Queryparser.Surround.Parser.QueryParser
				();
			return parser.Parse2(query);
		}

		public QueryParser() : this(new FastCharStream(new StringReader(string.Empty)))
		{
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public virtual SrndQuery Parse2(string query)
		{
			ReInit(new FastCharStream(new StringReader(query)));
			try
			{
				return TopSrndQuery();
			}
			catch (TokenMgrError tme)
			{
				throw new ParseException(tme.Message);
			}
		}

		protected internal virtual SrndQuery GetFieldsQuery(SrndQuery q, AList<string> fieldNames
			)
		{
			return new Org.Apache.Lucene.Queryparser.Surround.Query.FieldsQuery(q, fieldNames
				, fieldOperator);
		}

		protected internal virtual SrndQuery GetOrQuery(IList<SrndQuery> queries, bool infix
			, Token orToken)
		{
			return new Org.Apache.Lucene.Queryparser.Surround.Query.OrQuery(queries, infix, orToken
				.image);
		}

		protected internal virtual SrndQuery GetAndQuery(IList<SrndQuery> queries, bool infix
			, Token andToken)
		{
			return new Org.Apache.Lucene.Queryparser.Surround.Query.AndQuery(queries, infix, 
				andToken.image);
		}

		protected internal virtual SrndQuery GetNotQuery(IList<SrndQuery> queries, Token 
			notToken)
		{
			return new Org.Apache.Lucene.Queryparser.Surround.Query.NotQuery(queries, notToken
				.image);
		}

		protected internal static int GetOpDistance(string distanceOp)
		{
			return distanceOp.Length == 1 ? 1 : System.Convert.ToInt32(Sharpen.Runtime.Substring
				(distanceOp, 0, distanceOp.Length - 1));
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		protected internal static void CheckDistanceSubQueries(DistanceQuery distq, string
			 opName)
		{
			string m = distq.DistanceSubQueryNotAllowed();
			if (m != null)
			{
				throw new ParseException("Operator " + opName + ": " + m);
			}
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		protected internal virtual SrndQuery GetDistanceQuery(IList<SrndQuery> queries, bool
			 infix, Token dToken, bool ordered)
		{
			DistanceQuery dq = new DistanceQuery(queries, infix, GetOpDistance(dToken.image), 
				dToken.image, ordered);
			CheckDistanceSubQueries(dq, dToken.image);
			return dq;
		}

		protected internal virtual SrndQuery GetTermQuery(string term, bool quoted)
		{
			return new SrndTermQuery(term, quoted);
		}

		protected internal virtual bool AllowedSuffix(string suffixed)
		{
			return (suffixed.Length - 1) >= minimumPrefixLength;
		}

		protected internal virtual SrndQuery GetPrefixQuery(string prefix, bool quoted)
		{
			return new SrndPrefixQuery(prefix, quoted, truncator);
		}

		protected internal virtual bool AllowedTruncation(string truncated)
		{
			int nrNormalChars = 0;
			for (int i = 0; i < truncated.Length; i++)
			{
				char c = truncated[i];
				if ((c != truncator) && (c != anyChar))
				{
					nrNormalChars++;
				}
			}
			return nrNormalChars >= minimumCharsInTrunc;
		}

		protected internal virtual SrndQuery GetTruncQuery(string truncated)
		{
			return new SrndTruncQuery(truncated, truncator, anyChar);
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public SrndQuery TopSrndQuery()
		{
			SrndQuery q;
			q = FieldsQuery();
			Jj_consume_token(0);
			{
				if (true)
				{
					return q;
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public SrndQuery FieldsQuery()
		{
			SrndQuery q;
			AList<string> fieldNames;
			fieldNames = OptionalFields();
			q = OrQuery();
			{
				if (true)
				{
					return (fieldNames == null) ? q : GetFieldsQuery(q, fieldNames);
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public AList<string> OptionalFields()
		{
			Token fieldName;
			AList<string> fieldNames = null;
			while (true)
			{
				if (Jj_2_1(2))
				{
				}
				else
				{
					goto label_1_break;
				}
				// to the colon
				fieldName = Jj_consume_token(TERM);
				Jj_consume_token(COLON);
				if (fieldNames == null)
				{
					fieldNames = new AList<string>();
				}
				fieldNames.AddItem(fieldName.image);
label_1_continue: ;
			}
label_1_break: ;
			{
				if (true)
				{
					return fieldNames;
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public SrndQuery OrQuery()
		{
			SrndQuery q;
			AList<SrndQuery> queries = null;
			Token oprt = null;
			q = AndQuery();
			while (true)
			{
				switch ((jj_ntk == -1) ? Jj_ntk() : jj_ntk)
				{
					case OR:
					{
						break;
					}

					default:
					{
						jj_la1[0] = jj_gen;
						goto label_2_break;
						break;
					}
				}
				oprt = Jj_consume_token(OR);
				if (queries == null)
				{
					queries = new AList<SrndQuery>();
					queries.AddItem(q);
				}
				q = AndQuery();
				queries.AddItem(q);
label_2_continue: ;
			}
label_2_break: ;
			{
				if (true)
				{
					return (queries == null) ? q : GetOrQuery(queries, true, oprt);
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public SrndQuery AndQuery()
		{
			SrndQuery q;
			AList<SrndQuery> queries = null;
			Token oprt = null;
			q = NotQuery();
			while (true)
			{
				switch ((jj_ntk == -1) ? Jj_ntk() : jj_ntk)
				{
					case AND:
					{
						break;
					}

					default:
					{
						jj_la1[1] = jj_gen;
						goto label_3_break;
						break;
					}
				}
				oprt = Jj_consume_token(AND);
				if (queries == null)
				{
					queries = new AList<SrndQuery>();
					queries.AddItem(q);
				}
				q = NotQuery();
				queries.AddItem(q);
label_3_continue: ;
			}
label_3_break: ;
			{
				if (true)
				{
					return (queries == null) ? q : GetAndQuery(queries, true, oprt);
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public SrndQuery NotQuery()
		{
			SrndQuery q;
			AList<SrndQuery> queries = null;
			Token oprt = null;
			q = NQuery();
			while (true)
			{
				switch ((jj_ntk == -1) ? Jj_ntk() : jj_ntk)
				{
					case NOT:
					{
						break;
					}

					default:
					{
						jj_la1[2] = jj_gen;
						goto label_4_break;
						break;
					}
				}
				oprt = Jj_consume_token(NOT);
				if (queries == null)
				{
					queries = new AList<SrndQuery>();
					queries.AddItem(q);
				}
				q = NQuery();
				queries.AddItem(q);
label_4_continue: ;
			}
label_4_break: ;
			{
				if (true)
				{
					return (queries == null) ? q : GetNotQuery(queries, oprt);
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public SrndQuery NQuery()
		{
			SrndQuery q;
			AList<SrndQuery> queries;
			Token dt;
			q = WQuery();
			while (true)
			{
				switch ((jj_ntk == -1) ? Jj_ntk() : jj_ntk)
				{
					case N:
					{
						break;
					}

					default:
					{
						jj_la1[3] = jj_gen;
						goto label_5_break;
						break;
					}
				}
				dt = Jj_consume_token(N);
				queries = new AList<SrndQuery>();
				queries.AddItem(q);
				q = WQuery();
				queries.AddItem(q);
				q = GetDistanceQuery(queries, true, dt, false);
label_5_continue: ;
			}
label_5_break: ;
			{
				if (true)
				{
					return q;
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public SrndQuery WQuery()
		{
			SrndQuery q;
			AList<SrndQuery> queries;
			Token wt;
			q = PrimaryQuery();
			while (true)
			{
				switch ((jj_ntk == -1) ? Jj_ntk() : jj_ntk)
				{
					case W:
					{
						break;
					}

					default:
					{
						jj_la1[4] = jj_gen;
						goto label_6_break;
						break;
					}
				}
				wt = Jj_consume_token(W);
				queries = new AList<SrndQuery>();
				queries.AddItem(q);
				q = PrimaryQuery();
				queries.AddItem(q);
				q = GetDistanceQuery(queries, true, wt, true);
label_6_continue: ;
			}
label_6_break: ;
			{
				if (true)
				{
					return q;
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public SrndQuery PrimaryQuery()
		{
			SrndQuery q;
			switch ((jj_ntk == -1) ? Jj_ntk() : jj_ntk)
			{
				case LPAREN:
				{
					Jj_consume_token(LPAREN);
					q = FieldsQuery();
					Jj_consume_token(RPAREN);
					break;
				}

				case OR:
				case AND:
				case W:
				case N:
				{
					q = PrefixOperatorQuery();
					break;
				}

				case TRUNCQUOTED:
				case QUOTED:
				case SUFFIXTERM:
				case TRUNCTERM:
				case TERM:
				{
					q = SimpleTerm();
					break;
				}

				default:
				{
					jj_la1[5] = jj_gen;
					Jj_consume_token(-1);
					throw new ParseException();
				}
			}
			OptionalWeights(q);
			{
				if (true)
				{
					return q;
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public SrndQuery PrefixOperatorQuery()
		{
			Token oprt;
			IList<SrndQuery> queries;
			switch ((jj_ntk == -1) ? Jj_ntk() : jj_ntk)
			{
				case OR:
				{
					oprt = Jj_consume_token(OR);
					queries = FieldsQueryList();
					if (true)
					{
						return GetOrQuery(queries, false, oprt);
					}
					break;
				}

				case AND:
				{
					oprt = Jj_consume_token(AND);
					queries = FieldsQueryList();
					if (true)
					{
						return GetAndQuery(queries, false, oprt);
					}
					break;
				}

				case N:
				{
					oprt = Jj_consume_token(N);
					queries = FieldsQueryList();
					if (true)
					{
						return GetDistanceQuery(queries, false, oprt, false);
					}
					break;
				}

				case W:
				{
					oprt = Jj_consume_token(W);
					queries = FieldsQueryList();
					if (true)
					{
						return GetDistanceQuery(queries, false, oprt, true);
					}
					break;
				}

				default:
				{
					jj_la1[6] = jj_gen;
					Jj_consume_token(-1);
					throw new ParseException();
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public IList<SrndQuery> FieldsQueryList()
		{
			SrndQuery q;
			AList<SrndQuery> queries = new AList<SrndQuery>();
			Jj_consume_token(LPAREN);
			q = FieldsQuery();
			queries.AddItem(q);
			while (true)
			{
				Jj_consume_token(COMMA);
				q = FieldsQuery();
				queries.AddItem(q);
				switch ((jj_ntk == -1) ? Jj_ntk() : jj_ntk)
				{
					case COMMA:
					{
						break;
					}

					default:
					{
						jj_la1[7] = jj_gen;
						goto label_7_break;
						break;
					}
				}
label_7_continue: ;
			}
label_7_break: ;
			Jj_consume_token(RPAREN);
			{
				if (true)
				{
					return queries;
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public SrndQuery SimpleTerm()
		{
			Token term;
			switch ((jj_ntk == -1) ? Jj_ntk() : jj_ntk)
			{
				case TERM:
				{
					term = Jj_consume_token(TERM);
					if (true)
					{
						return GetTermQuery(term.image, false);
					}
					break;
				}

				case QUOTED:
				{
					term = Jj_consume_token(QUOTED);
					if (true)
					{
						return GetTermQuery(Sharpen.Runtime.Substring(term.image, 1, term.image.Length - 
							1), true);
					}
					break;
				}

				case SUFFIXTERM:
				{
					term = Jj_consume_token(SUFFIXTERM);
					if (!AllowedSuffix(term.image))
					{
						{
							if (true)
							{
								throw new ParseException(truncationErrorMessage + term.image);
							}
						}
					}
					if (true)
					{
						return GetPrefixQuery(Sharpen.Runtime.Substring(term.image, 0, term.image.Length 
							- 1), false);
					}
					break;
				}

				case TRUNCTERM:
				{
					term = Jj_consume_token(TRUNCTERM);
					if (!AllowedTruncation(term.image))
					{
						{
							if (true)
							{
								throw new ParseException(truncationErrorMessage + term.image);
							}
						}
					}
					if (true)
					{
						return GetTruncQuery(term.image);
					}
					break;
				}

				case TRUNCQUOTED:
				{
					term = Jj_consume_token(TRUNCQUOTED);
					if ((term.image.Length - 3) < minimumPrefixLength)
					{
						{
							if (true)
							{
								throw new ParseException(truncationErrorMessage + term.image);
							}
						}
					}
					if (true)
					{
						return GetPrefixQuery(Sharpen.Runtime.Substring(term.image, 1, term.image.Length 
							- 2), true);
					}
					break;
				}

				default:
				{
					jj_la1[8] = jj_gen;
					Jj_consume_token(-1);
					throw new ParseException();
				}
			}
			throw new Error("Missing return statement in function");
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		public void OptionalWeights(SrndQuery q)
		{
			Token weight = null;
			while (true)
			{
				switch ((jj_ntk == -1) ? Jj_ntk() : jj_ntk)
				{
					case CARAT:
					{
						break;
					}

					default:
					{
						jj_la1[9] = jj_gen;
						goto label_8_break;
						break;
					}
				}
				Jj_consume_token(CARAT);
				weight = Jj_consume_token(NUMBER);
				float f;
				try
				{
					f = float.ValueOf(weight.image);
				}
				catch (Exception floatExc)
				{
					{
						if (true)
						{
							throw new ParseException(boostErrorMessage + weight.image + " (" + floatExc + ")"
								);
						}
					}
				}
				if (f <= 0.0)
				{
					{
						if (true)
						{
							throw new ParseException(boostErrorMessage + weight.image);
						}
					}
				}
				q.SetWeight(f * q.GetWeight());
label_8_continue: ;
			}
label_8_break: ;
		}

		private bool Jj_2_1(int xla)
		{
			jj_la = xla;
			jj_lastpos = jj_scanpos = token;
			try
			{
				return !Jj_3_1();
			}
			catch (QueryParser.LookaheadSuccess)
			{
				return true;
			}
			finally
			{
				Jj_save(0, xla);
			}
		}

		private bool Jj_3_1()
		{
			if (Jj_scan_token(TERM))
			{
				return true;
			}
			if (Jj_scan_token(COLON))
			{
				return true;
			}
			return false;
		}

		/// <summary>Generated Token Manager.</summary>
		/// <remarks>Generated Token Manager.</remarks>
		public QueryParserTokenManager token_source;

		/// <summary>Current token.</summary>
		/// <remarks>Current token.</remarks>
		public Token token;

		/// <summary>Next token.</summary>
		/// <remarks>Next token.</remarks>
		public Token jj_nt;

		private int jj_ntk;

		private Token jj_scanpos;

		private Token jj_lastpos;

		private int jj_la;

		private int jj_gen;

		private readonly int[] jj_la1 = new int[10];

		private static int[] jj_la1_0;

		static QueryParser()
		{
			Jj_la1_init_0();
		}

		private static void Jj_la1_init_0()
		{
			jj_la1_0 = new int[] { unchecked((int)(0x100)), unchecked((int)(0x200)), unchecked(
				(int)(0x400)), unchecked((int)(0x1000)), unchecked((int)(0x800)), unchecked((int
				)(0x7c3b00)), unchecked((int)(0x1b00)), unchecked((int)(0x8000)), unchecked((int
				)(0x7c0000)), unchecked((int)(0x20000)) };
		}

		private readonly QueryParser.JJCalls[] jj_2_rtns = new QueryParser.JJCalls[1];

		private bool jj_rescan = false;

		private int jj_gc = 0;

		/// <summary>Constructor with user supplied CharStream.</summary>
		/// <remarks>Constructor with user supplied CharStream.</remarks>
		public QueryParser(CharStream stream)
		{
			token_source = new QueryParserTokenManager(stream);
			token = new Token();
			jj_ntk = -1;
			jj_gen = 0;
			for (int i = 0; i < 10; i++)
			{
				jj_la1[i] = -1;
			}
			for (int i_1 = 0; i_1 < jj_2_rtns.Length; i_1++)
			{
				jj_2_rtns[i_1] = new QueryParser.JJCalls();
			}
		}

		/// <summary>Reinitialise.</summary>
		/// <remarks>Reinitialise.</remarks>
		public virtual void ReInit(CharStream stream)
		{
			token_source.ReInit(stream);
			token = new Token();
			jj_ntk = -1;
			jj_gen = 0;
			for (int i = 0; i < 10; i++)
			{
				jj_la1[i] = -1;
			}
			for (int i_1 = 0; i_1 < jj_2_rtns.Length; i_1++)
			{
				jj_2_rtns[i_1] = new QueryParser.JJCalls();
			}
		}

		/// <summary>Constructor with generated Token Manager.</summary>
		/// <remarks>Constructor with generated Token Manager.</remarks>
		public QueryParser(QueryParserTokenManager tm)
		{
			token_source = tm;
			token = new Token();
			jj_ntk = -1;
			jj_gen = 0;
			for (int i = 0; i < 10; i++)
			{
				jj_la1[i] = -1;
			}
			for (int i_1 = 0; i_1 < jj_2_rtns.Length; i_1++)
			{
				jj_2_rtns[i_1] = new QueryParser.JJCalls();
			}
		}

		/// <summary>Reinitialise.</summary>
		/// <remarks>Reinitialise.</remarks>
		public virtual void ReInit(QueryParserTokenManager tm)
		{
			token_source = tm;
			token = new Token();
			jj_ntk = -1;
			jj_gen = 0;
			for (int i = 0; i < 10; i++)
			{
				jj_la1[i] = -1;
			}
			for (int i_1 = 0; i_1 < jj_2_rtns.Length; i_1++)
			{
				jj_2_rtns[i_1] = new QueryParser.JJCalls();
			}
		}

		/// <exception cref="Org.Apache.Lucene.Queryparser.Surround.Parser.ParseException"></exception>
		private Token Jj_consume_token(int kind)
		{
			Token oldToken;
			if ((oldToken = token).next != null)
			{
				token = token.next;
			}
			else
			{
				token = token.next = token_source.GetNextToken();
			}
			jj_ntk = -1;
			if (token.kind == kind)
			{
				jj_gen++;
				if (++jj_gc > 100)
				{
					jj_gc = 0;
					for (int i = 0; i < jj_2_rtns.Length; i++)
					{
						QueryParser.JJCalls c = jj_2_rtns[i];
						while (c != null)
						{
							if (c.gen < jj_gen)
							{
								c.first = null;
							}
							c = c.next;
						}
					}
				}
				return token;
			}
			token = oldToken;
			jj_kind = kind;
			throw GenerateParseException();
		}

		[System.Serializable]
		private sealed class LookaheadSuccess : Error
		{
		}

		private readonly QueryParser.LookaheadSuccess jj_ls = new QueryParser.LookaheadSuccess
			();

		private bool Jj_scan_token(int kind)
		{
			if (jj_scanpos == jj_lastpos)
			{
				jj_la--;
				if (jj_scanpos.next == null)
				{
					jj_lastpos = jj_scanpos = jj_scanpos.next = token_source.GetNextToken();
				}
				else
				{
					jj_lastpos = jj_scanpos = jj_scanpos.next;
				}
			}
			else
			{
				jj_scanpos = jj_scanpos.next;
			}
			if (jj_rescan)
			{
				int i = 0;
				Token tok = token;
				while (tok != null && tok != jj_scanpos)
				{
					i++;
					tok = tok.next;
				}
				if (tok != null)
				{
					Jj_add_error_token(kind, i);
				}
			}
			if (jj_scanpos.kind != kind)
			{
				return true;
			}
			if (jj_la == 0 && jj_scanpos == jj_lastpos)
			{
				throw jj_ls;
			}
			return false;
		}

		/// <summary>Get the next Token.</summary>
		/// <remarks>Get the next Token.</remarks>
		public Token GetNextToken()
		{
			if (token.next != null)
			{
				token = token.next;
			}
			else
			{
				token = token.next = token_source.GetNextToken();
			}
			jj_ntk = -1;
			jj_gen++;
			return token;
		}

		/// <summary>Get the specific Token.</summary>
		/// <remarks>Get the specific Token.</remarks>
		public Token GetToken(int index)
		{
			Token t = token;
			for (int i = 0; i < index; i++)
			{
				if (t.next != null)
				{
					t = t.next;
				}
				else
				{
					t = t.next = token_source.GetNextToken();
				}
			}
			return t;
		}

		private int Jj_ntk()
		{
			if ((jj_nt = token.next) == null)
			{
				return (jj_ntk = (token.next = token_source.GetNextToken()).kind);
			}
			else
			{
				return (jj_ntk = jj_nt.kind);
			}
		}

		private IList<int[]> jj_expentries = new AList<int[]>();

		private int[] jj_expentry;

		private int jj_kind = -1;

		private int[] jj_lasttokens = new int[100];

		private int jj_endpos;

		private void Jj_add_error_token(int kind, int pos)
		{
			if (pos >= 100)
			{
				return;
			}
			if (pos == jj_endpos + 1)
			{
				jj_lasttokens[jj_endpos++] = kind;
			}
			else
			{
				if (jj_endpos != 0)
				{
					jj_expentry = new int[jj_endpos];
					for (int i = 0; i < jj_endpos; i++)
					{
						jj_expentry[i] = jj_lasttokens[i];
					}
					for (Iterator<object> it = jj_expentries.Iterator(); it.HasNext(); )
					{
						int[] oldentry = (int[])(it.Next());
						if (oldentry.Length == jj_expentry.Length)
						{
							for (int i_1 = 0; i_1 < jj_expentry.Length; i_1++)
							{
								if (oldentry[i_1] != jj_expentry[i_1])
								{
									goto jj_entries_loop_continue;
								}
							}
							jj_expentries.AddItem(jj_expentry);
							goto jj_entries_loop_break;
						}
jj_entries_loop_continue: ;
					}
jj_entries_loop_break: ;
					if (pos != 0)
					{
						jj_lasttokens[(jj_endpos = pos) - 1] = kind;
					}
				}
			}
		}

		/// <summary>Generate ParseException.</summary>
		/// <remarks>Generate ParseException.</remarks>
		public virtual ParseException GenerateParseException()
		{
			jj_expentries.Clear();
			bool[] la1tokens = new bool[24];
			if (jj_kind >= 0)
			{
				la1tokens[jj_kind] = true;
				jj_kind = -1;
			}
			for (int i = 0; i < 10; i++)
			{
				if (jj_la1[i] == jj_gen)
				{
					for (int j = 0; j < 32; j++)
					{
						if ((jj_la1_0[i] & (1 << j)) != 0)
						{
							la1tokens[j] = true;
						}
					}
				}
			}
			for (int i_1 = 0; i_1 < 24; i_1++)
			{
				if (la1tokens[i_1])
				{
					jj_expentry = new int[1];
					jj_expentry[0] = i_1;
					jj_expentries.AddItem(jj_expentry);
				}
			}
			jj_endpos = 0;
			Jj_rescan_token();
			Jj_add_error_token(0, 0);
			int[][] exptokseq = new int[jj_expentries.Count][];
			for (int i_2 = 0; i_2 < jj_expentries.Count; i_2++)
			{
				exptokseq[i_2] = jj_expentries[i_2];
			}
			return new ParseException(token, exptokseq, tokenImage);
		}

		/// <summary>Enable tracing.</summary>
		/// <remarks>Enable tracing.</remarks>
		public void Enable_tracing()
		{
		}

		/// <summary>Disable tracing.</summary>
		/// <remarks>Disable tracing.</remarks>
		public void Disable_tracing()
		{
		}

		private void Jj_rescan_token()
		{
			jj_rescan = true;
			for (int i = 0; i < 1; i++)
			{
				try
				{
					QueryParser.JJCalls p = jj_2_rtns[i];
					do
					{
						if (p.gen > jj_gen)
						{
							jj_la = p.arg;
							jj_lastpos = jj_scanpos = p.first;
							switch (i)
							{
								case 0:
								{
									Jj_3_1();
									break;
								}
							}
						}
						p = p.next;
					}
					while (p != null);
				}
				catch (QueryParser.LookaheadSuccess)
				{
				}
			}
			jj_rescan = false;
		}

		private void Jj_save(int index, int xla)
		{
			QueryParser.JJCalls p = jj_2_rtns[index];
			while (p.gen > jj_gen)
			{
				if (p.next == null)
				{
					p = p.next = new QueryParser.JJCalls();
					break;
				}
				p = p.next;
			}
			p.gen = jj_gen + xla - jj_la;
			p.first = token;
			p.arg = xla;
		}

		internal sealed class JJCalls
		{
			internal int gen;

			internal Token first;

			internal int arg;

			internal QueryParser.JJCalls next;
		}
	}
}
