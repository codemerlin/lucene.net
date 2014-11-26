/*
 * This code is derived from MyJavaLibrary (http://somelinktomycoollibrary)
 * 
 * If this is an open source Java library, include the proper license and copyright attributions here!
 */

using Lucene.Net.Queryparser.Xml;
using Lucene.Net.Search;
using Org.W3c.Dom;
using Sharpen;

namespace Lucene.Net.Queryparser.Xml.Builders
{
	/// <summary>
	/// Builder for
	/// <see cref="Lucene.Net.Search.DisjunctionMaxQuery">Lucene.Net.Search.DisjunctionMaxQuery
	/// 	</see>
	/// </summary>
	public class DisjunctionMaxQueryBuilder : QueryBuilder
	{
		private readonly QueryBuilder factory;

		public DisjunctionMaxQueryBuilder(QueryBuilder factory)
		{
			this.factory = factory;
		}

		/// <exception cref="Lucene.Net.Queryparser.Xml.ParserException"></exception>
		public virtual Query GetQuery(Element e)
		{
			float tieBreaker = DOMUtils.GetAttribute(e, "tieBreaker", 0.0f);
			DisjunctionMaxQuery dq = new DisjunctionMaxQuery(tieBreaker);
			dq.SetBoost(DOMUtils.GetAttribute(e, "boost", 1.0f));
			NodeList nl = e.GetChildNodes();
			for (int i = 0; i < nl.GetLength(); i++)
			{
				Node node = nl.Item(i);
				if (node is Element)
				{
					// all elements are disjuncts.
					Element queryElem = (Element)node;
					Query q = factory.GetQuery(queryElem);
					dq.Add(q);
				}
			}
			return dq;
		}
	}
}
