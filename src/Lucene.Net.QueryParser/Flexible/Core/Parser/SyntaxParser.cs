/*
 * This code is derived from MyJavaLibrary (http://somelinktomycoollibrary)
 * 
 * If this is an open source Java library, include the proper license and copyright attributions here!
 */

using Lucene.Net.Queryparser.Flexible.Core.Nodes;
using Lucene.Net.Queryparser.Flexible.Core.Parser;
using Sharpen;

namespace Lucene.Net.Queryparser.Flexible.Core.Parser
{
	/// <summary>
	/// A parser needs to implement
	/// <see cref="SyntaxParser">SyntaxParser</see>
	/// interface
	/// </summary>
	public interface SyntaxParser
	{
		/// <param name="query">- query data to be parsed</param>
		/// <param name="field">- default field name</param>
		/// <returns>QueryNode tree</returns>
		/// <exception cref="Lucene.Net.Queryparser.Flexible.Core.QueryNodeParseException
		/// 	"></exception>
		QueryNode Parse(CharSequence query, CharSequence field);
	}
}
