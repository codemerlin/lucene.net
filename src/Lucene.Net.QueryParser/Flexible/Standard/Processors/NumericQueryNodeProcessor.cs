/*
 * This code is derived from MyJavaLibrary (http://somelinktomycoollibrary)
 * 
 * If this is an open source Java library, include the proper license and copyright attributions here!
 */

using System.Collections.Generic;
using Lucene.Net.Document;
using Lucene.Net.Queryparser.Flexible.Core;
using Lucene.Net.Queryparser.Flexible.Core.Config;
using Lucene.Net.Queryparser.Flexible.Core.Messages;
using Lucene.Net.Queryparser.Flexible.Core.Nodes;
using Lucene.Net.Queryparser.Flexible.Core.Processors;
using Lucene.Net.Queryparser.Flexible.Messages;
using Lucene.Net.Queryparser.Flexible.Standard.Config;
using Lucene.Net.Queryparser.Flexible.Standard.Nodes;
using Sharpen;

namespace Lucene.Net.Queryparser.Flexible.Standard.Processors
{
	/// <summary>
	/// This processor is used to convert
	/// <see cref="Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode">Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode
	/// 	</see>
	/// s to
	/// <see cref="Lucene.Net.Queryparser.Flexible.Standard.Nodes.NumericRangeQueryNode
	/// 	">Lucene.Net.Queryparser.Flexible.Standard.Nodes.NumericRangeQueryNode</see>
	/// s. It looks for
	/// <see cref="Lucene.Net.Queryparser.Flexible.Standard.Config.StandardQueryConfigHandler.ConfigurationKeys.NUMERIC_CONFIG
	/// 	">Lucene.Net.Queryparser.Flexible.Standard.Config.StandardQueryConfigHandler.ConfigurationKeys.NUMERIC_CONFIG
	/// 	</see>
	/// set in the
	/// <see cref="Lucene.Net.Queryparser.Flexible.Core.Config.FieldConfig">Lucene.Net.Queryparser.Flexible.Core.Config.FieldConfig
	/// 	</see>
	/// of
	/// every
	/// <see cref="Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode">Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode
	/// 	</see>
	/// found. If
	/// <see cref="Lucene.Net.Queryparser.Flexible.Standard.Config.StandardQueryConfigHandler.ConfigurationKeys.NUMERIC_CONFIG
	/// 	">Lucene.Net.Queryparser.Flexible.Standard.Config.StandardQueryConfigHandler.ConfigurationKeys.NUMERIC_CONFIG
	/// 	</see>
	/// is found, it considers that
	/// <see cref="Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode">Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode
	/// 	</see>
	/// to be a numeric query and convert it to
	/// <see cref="Lucene.Net.Queryparser.Flexible.Standard.Nodes.NumericRangeQueryNode
	/// 	">Lucene.Net.Queryparser.Flexible.Standard.Nodes.NumericRangeQueryNode</see>
	/// with upper and lower inclusive and lower and
	/// upper equals to the value represented by the
	/// <see cref="Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode">Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode
	/// 	</see>
	/// converted
	/// to
	/// <see cref="Sharpen.Number">Sharpen.Number</see>
	/// . It means that <b>field:1</b> is converted to <b>field:[1
	/// TO 1]</b>. <br/>
	/// <br/>
	/// Note that
	/// <see cref="Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode">Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode
	/// 	</see>
	/// s children of a
	/// <see cref="Lucene.Net.Queryparser.Flexible.Core.Nodes.RangeQueryNode{T}">Lucene.Net.Queryparser.Flexible.Core.Nodes.RangeQueryNode&lt;T&gt;
	/// 	</see>
	/// are ignored.
	/// </summary>
	/// <seealso cref="Lucene.Net.Queryparser.Flexible.Standard.Config.StandardQueryConfigHandler.ConfigurationKeys.NUMERIC_CONFIG
	/// 	">Lucene.Net.Queryparser.Flexible.Standard.Config.StandardQueryConfigHandler.ConfigurationKeys.NUMERIC_CONFIG
	/// 	</seealso>
	/// <seealso cref="Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode">Lucene.Net.Queryparser.Flexible.Core.Nodes.FieldQueryNode
	/// 	</seealso>
	/// <seealso cref="Lucene.Net.Queryparser.Flexible.Standard.Config.NumericConfig
	/// 	">Lucene.Net.Queryparser.Flexible.Standard.Config.NumericConfig</seealso>
	/// <seealso cref="Lucene.Net.Queryparser.Flexible.Standard.Nodes.NumericQueryNode
	/// 	">Lucene.Net.Queryparser.Flexible.Standard.Nodes.NumericQueryNode</seealso>
	public class NumericQueryNodeProcessor : QueryNodeProcessorImpl
	{
		/// <summary>
		/// Constructs a
		/// <see cref="NumericQueryNodeProcessor">NumericQueryNodeProcessor</see>
		/// object.
		/// </summary>
		public NumericQueryNodeProcessor()
		{
		}

		// empty constructor
		/// <exception cref="Lucene.Net.Queryparser.Flexible.Core.QueryNodeException">
		/// 	</exception>
		protected internal override QueryNode PostProcessNode(QueryNode node)
		{
			if (node is FieldQueryNode && !(node.GetParent() is RangeQueryNode))
			{
				QueryConfigHandler config = GetQueryConfigHandler();
				if (config != null)
				{
					FieldQueryNode fieldNode = (FieldQueryNode)node;
					FieldConfig fieldConfig = config.GetFieldConfig(fieldNode.GetFieldAsString());
					if (fieldConfig != null)
					{
						NumericConfig numericConfig = fieldConfig.Get(StandardQueryConfigHandler.ConfigurationKeys
							.NUMERIC_CONFIG);
						if (numericConfig != null)
						{
							NumberFormat numberFormat = numericConfig.GetNumberFormat();
							string text = fieldNode.GetTextAsString();
							Number number = null;
							if (text.Length > 0)
							{
								try
								{
									number = numberFormat.Parse(text);
								}
								catch (ParseException e)
								{
									throw new QueryNodeParseException(new MessageImpl(QueryParserMessages.COULD_NOT_PARSE_NUMBER
										, fieldNode.GetTextAsString(), numberFormat.GetType().GetCanonicalName()), e);
								}
								switch (numericConfig.GetType())
								{
									case FieldType.NumericType.LONG:
									{
										number = number;
										break;
									}

									case FieldType.NumericType.INT:
									{
										number = number;
										break;
									}

									case FieldType.NumericType.DOUBLE:
									{
										number = number;
										break;
									}

									case FieldType.NumericType.FLOAT:
									{
										number = number;
									}
								}
							}
							else
							{
								throw new QueryNodeParseException(new MessageImpl(QueryParserMessages.NUMERIC_CANNOT_BE_EMPTY
									, fieldNode.GetFieldAsString()));
							}
							NumericQueryNode lowerNode = new NumericQueryNode(fieldNode.GetField(), number, numberFormat
								);
							NumericQueryNode upperNode = new NumericQueryNode(fieldNode.GetField(), number, numberFormat
								);
							return new NumericRangeQueryNode(lowerNode, upperNode, true, true, numericConfig);
						}
					}
				}
			}
			return node;
		}

		/// <exception cref="Lucene.Net.Queryparser.Flexible.Core.QueryNodeException">
		/// 	</exception>
		protected internal override QueryNode PreProcessNode(QueryNode node)
		{
			return node;
		}

		/// <exception cref="Lucene.Net.Queryparser.Flexible.Core.QueryNodeException">
		/// 	</exception>
		protected internal override IList<QueryNode> SetChildrenOrder(IList<QueryNode> children
			)
		{
			return children;
		}
	}
}
