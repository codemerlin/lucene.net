/*
 * This code is derived from MyJavaLibrary (http://somelinktomycoollibrary)
 * 
 * If this is an open source Java library, include the proper license and copyright attributions here!
 */

using System;
using System.Collections.Generic;
using System.Text;
using Lucene.Net.Document;
using Lucene.Net.Facet;
using Lucene.Net.Facet.Sortedset;
using Lucene.Net.Facet.Taxonomy;
using Lucene.Net.Index;
using Lucene.Net.Util;
using Sharpen;

namespace Lucene.Net.Facet
{
	/// <summary>Records per-dimension configuration.</summary>
	/// <remarks>
	/// Records per-dimension configuration.  By default a
	/// dimension is flat, single valued and does
	/// not require count for the dimension; use
	/// the setters in this class to change these settings for
	/// each dim.
	/// <p><b>NOTE</b>: this configuration is not saved into the
	/// index, but it's vital, and up to the application to
	/// ensure, that at search time the provided
	/// <code>FacetsConfig</code>
	/// matches what was used during indexing.
	/// </remarks>
	/// <lucene.experimental></lucene.experimental>
	public class FacetsConfig
	{
		/// <summary>
		/// Which Lucene field holds the drill-downs and ords (as
		/// doc values).
		/// </summary>
		/// <remarks>
		/// Which Lucene field holds the drill-downs and ords (as
		/// doc values).
		/// </remarks>
		public static readonly string DEFAULT_INDEX_FIELD_NAME = "$facets";

		private readonly IDictionary<string, FacetsConfig.DimConfig> fieldTypes = new ConcurrentHashMap
			<string, FacetsConfig.DimConfig>();

		private readonly IDictionary<string, string> assocDimTypes = new ConcurrentHashMap
			<string, string>();

		/// <summary>Holds the configuration for one dimension</summary>
		/// <lucene.experimental></lucene.experimental>
		public sealed class DimConfig
		{
			/// <summary>True if this dimension is hierarchical.</summary>
			/// <remarks>True if this dimension is hierarchical.</remarks>
			public bool hierarchical;

			/// <summary>True if this dimension is multi-valued.</summary>
			/// <remarks>True if this dimension is multi-valued.</remarks>
			public bool multiValued;

			/// <summary>
			/// True if the count/aggregate for the entire dimension
			/// is required, which is unusual (default is false).
			/// </summary>
			/// <remarks>
			/// True if the count/aggregate for the entire dimension
			/// is required, which is unusual (default is false).
			/// </remarks>
			public bool requireDimCount;

			/// <summary>
			/// Actual field where this dimension's facet labels
			/// should be indexed
			/// </summary>
			public string indexFieldName = DEFAULT_INDEX_FIELD_NAME;

			/// <summary>Default constructor.</summary>
			/// <remarks>Default constructor.</remarks>
			public DimConfig()
			{
			}
			// Used only for best-effort detection of app mixing
			// int/float/bytes in a single indexed field:
		}

		/// <summary>Default per-dimension configuration.</summary>
		/// <remarks>Default per-dimension configuration.</remarks>
		public static readonly FacetsConfig.DimConfig DEFAULT_DIM_CONFIG = new FacetsConfig.DimConfig
			();

		/// <summary>Default constructor.</summary>
		/// <remarks>Default constructor.</remarks>
		public FacetsConfig()
		{
		}

		/// <summary>Get the default configuration for new dimensions.</summary>
		/// <remarks>
		/// Get the default configuration for new dimensions.  Useful when
		/// the dimension is not known beforehand and may need different
		/// global default settings, like
		/// <code>
		/// multivalue =
		/// true
		/// </code>
		/// .
		/// </remarks>
		/// <returns>
		/// The default configuration to be used for dimensions that
		/// are not yet set in the
		/// <see cref="FacetsConfig">FacetsConfig</see>
		/// 
		/// </returns>
		protected internal virtual FacetsConfig.DimConfig GetDefaultDimConfig()
		{
			return DEFAULT_DIM_CONFIG;
		}

		/// <summary>Get the current configuration for a dimension.</summary>
		/// <remarks>Get the current configuration for a dimension.</remarks>
		public virtual FacetsConfig.DimConfig GetDimConfig(string dimName)
		{
			lock (this)
			{
				FacetsConfig.DimConfig ft = fieldTypes.Get(dimName);
				if (ft == null)
				{
					ft = GetDefaultDimConfig();
				}
				return ft;
			}
		}

		/// <summary>
		/// Pass
		/// <code>true</code>
		/// if this dimension is hierarchical
		/// (has depth &gt; 1 paths).
		/// </summary>
		public virtual void SetHierarchical(string dimName, bool v)
		{
			lock (this)
			{
				FacetsConfig.DimConfig ft = fieldTypes.Get(dimName);
				if (ft == null)
				{
					ft = new FacetsConfig.DimConfig();
					fieldTypes.Put(dimName, ft);
				}
				ft.hierarchical = v;
			}
		}

		/// <summary>
		/// Pass
		/// <code>true</code>
		/// if this dimension may have more than
		/// one value per document.
		/// </summary>
		public virtual void SetMultiValued(string dimName, bool v)
		{
			lock (this)
			{
				FacetsConfig.DimConfig ft = fieldTypes.Get(dimName);
				if (ft == null)
				{
					ft = new FacetsConfig.DimConfig();
					fieldTypes.Put(dimName, ft);
				}
				ft.multiValued = v;
			}
		}

		/// <summary>
		/// Pass
		/// <code>true</code>
		/// if at search time you require
		/// accurate counts of the dimension, i.e. how many
		/// hits have this dimension.
		/// </summary>
		public virtual void SetRequireDimCount(string dimName, bool v)
		{
			lock (this)
			{
				FacetsConfig.DimConfig ft = fieldTypes.Get(dimName);
				if (ft == null)
				{
					ft = new FacetsConfig.DimConfig();
					fieldTypes.Put(dimName, ft);
				}
				ft.requireDimCount = v;
			}
		}

		/// <summary>
		/// Specify which index field name should hold the
		/// ordinals for this dimension; this is only used by the
		/// taxonomy based facet methods.
		/// </summary>
		/// <remarks>
		/// Specify which index field name should hold the
		/// ordinals for this dimension; this is only used by the
		/// taxonomy based facet methods.
		/// </remarks>
		public virtual void SetIndexFieldName(string dimName, string indexFieldName)
		{
			lock (this)
			{
				FacetsConfig.DimConfig ft = fieldTypes.Get(dimName);
				if (ft == null)
				{
					ft = new FacetsConfig.DimConfig();
					fieldTypes.Put(dimName, ft);
				}
				ft.indexFieldName = indexFieldName;
			}
		}

		/// <summary>
		/// Returns map of field name to
		/// <see cref="DimConfig">DimConfig</see>
		/// .
		/// </summary>
		public virtual IDictionary<string, FacetsConfig.DimConfig> GetDimConfigs()
		{
			return fieldTypes;
		}

		private static void CheckSeen(ICollection<string> seenDims, string dim)
		{
			if (seenDims.Contains(dim))
			{
				throw new ArgumentException("dimension \"" + dim + "\" is not multiValued, but it appears more than once in this document"
					);
			}
			seenDims.AddItem(dim);
		}

		/// <summary>
		/// Translates any added
		/// <see cref="FacetField">FacetField</see>
		/// s into normal fields for indexing;
		/// only use this version if you did not add any taxonomy-based fields (
		/// <see cref="FacetField">FacetField</see>
		/// or
		/// <see cref="Lucene.Net.Facet.Taxonomy.AssociationFacetField">Lucene.Net.Facet.Taxonomy.AssociationFacetField
		/// 	</see>
		/// ).
		/// <p>
		/// <b>NOTE:</b> you should add the returned document to IndexWriter, not the
		/// input one!
		/// </summary>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual Lucene.Net.Document.Document Build(Lucene.Net.Document.Document
			 doc)
		{
			return Build(null, doc);
		}

		/// <summary>
		/// Translates any added
		/// <see cref="FacetField">FacetField</see>
		/// s into normal fields for indexing.
		/// <p>
		/// <b>NOTE:</b> you should add the returned document to IndexWriter, not the
		/// input one!
		/// </summary>
		/// <exception cref="System.IO.IOException"></exception>
		public virtual Lucene.Net.Document.Document Build(TaxonomyWriter taxoWriter
			, Lucene.Net.Document.Document doc)
		{
			// Find all FacetFields, collated by the actual field:
			IDictionary<string, IList<FacetField>> byField = new Dictionary<string, IList<FacetField
				>>();
			// ... and also all SortedSetDocValuesFacetFields:
			IDictionary<string, IList<SortedSetDocValuesFacetField>> dvByField = new Dictionary
				<string, IList<SortedSetDocValuesFacetField>>();
			// ... and also all AssociationFacetFields
			IDictionary<string, IList<AssociationFacetField>> assocByField = new Dictionary<string
				, IList<AssociationFacetField>>();
			ICollection<string> seenDims = new HashSet<string>();
			foreach (IndexableField field in doc.GetFields())
			{
				if (field.FieldType() == FacetField.TYPE)
				{
					FacetField facetField = (FacetField)field;
					FacetsConfig.DimConfig dimConfig = GetDimConfig(facetField.dim);
					if (dimConfig.multiValued == false)
					{
						CheckSeen(seenDims, facetField.dim);
					}
					string indexFieldName = dimConfig.indexFieldName;
					IList<FacetField> fields = byField.Get(indexFieldName);
					if (fields == null)
					{
						fields = new AList<FacetField>();
						byField.Put(indexFieldName, fields);
					}
					fields.AddItem(facetField);
				}
				if (field.FieldType() == SortedSetDocValuesFacetField.TYPE)
				{
					SortedSetDocValuesFacetField facetField = (SortedSetDocValuesFacetField)field;
					FacetsConfig.DimConfig dimConfig = GetDimConfig(facetField.dim);
					if (dimConfig.multiValued == false)
					{
						CheckSeen(seenDims, facetField.dim);
					}
					string indexFieldName = dimConfig.indexFieldName;
					IList<SortedSetDocValuesFacetField> fields = dvByField.Get(indexFieldName);
					if (fields == null)
					{
						fields = new AList<SortedSetDocValuesFacetField>();
						dvByField.Put(indexFieldName, fields);
					}
					fields.AddItem(facetField);
				}
				if (field.FieldType() == AssociationFacetField.TYPE)
				{
					AssociationFacetField facetField = (AssociationFacetField)field;
					FacetsConfig.DimConfig dimConfig = GetDimConfig(facetField.dim);
					if (dimConfig.multiValued == false)
					{
						CheckSeen(seenDims, facetField.dim);
					}
					if (dimConfig.hierarchical)
					{
						throw new ArgumentException("AssociationFacetField cannot be hierarchical (dim=\""
							 + facetField.dim + "\")");
					}
					if (dimConfig.requireDimCount)
					{
						throw new ArgumentException("AssociationFacetField cannot requireDimCount (dim=\""
							 + facetField.dim + "\")");
					}
					string indexFieldName = dimConfig.indexFieldName;
					IList<AssociationFacetField> fields = assocByField.Get(indexFieldName);
					if (fields == null)
					{
						fields = new AList<AssociationFacetField>();
						assocByField.Put(indexFieldName, fields);
					}
					fields.AddItem(facetField);
					// Best effort: detect mis-matched types in same
					// indexed field:
					string type;
					if (facetField is IntAssociationFacetField)
					{
						type = "int";
					}
					else
					{
						if (facetField is FloatAssociationFacetField)
						{
							type = "float";
						}
						else
						{
							type = "bytes";
						}
					}
					// NOTE: not thread safe, but this is just best effort:
					string curType = assocDimTypes.Get(indexFieldName);
					if (curType == null)
					{
						assocDimTypes.Put(indexFieldName, type);
					}
					else
					{
						if (!curType.Equals(type))
						{
							throw new ArgumentException("mixing incompatible types of AssocationFacetField ("
								 + curType + " and " + type + ") in indexed field \"" + indexFieldName + "\"; use FacetsConfig to change the indexFieldName for each dimension"
								);
						}
					}
				}
			}
			Lucene.Net.Document.Document result = new Lucene.Net.Document.Document
				();
			ProcessFacetFields(taxoWriter, byField, result);
			ProcessSSDVFacetFields(dvByField, result);
			ProcessAssocFacetFields(taxoWriter, assocByField, result);
			//System.out.println("add stored: " + addedStoredFields);
			foreach (IndexableField field_1 in doc.GetFields())
			{
				IndexableFieldType ft = field_1.FieldType();
				if (ft != FacetField.TYPE && ft != SortedSetDocValuesFacetField.TYPE && ft != AssociationFacetField
					.TYPE)
				{
					result.Add(field_1);
				}
			}
			return result;
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void ProcessFacetFields(TaxonomyWriter taxoWriter, IDictionary<string, IList
			<FacetField>> byField, Lucene.Net.Document.Document doc)
		{
			foreach (KeyValuePair<string, IList<FacetField>> ent in byField.EntrySet())
			{
				string indexFieldName = ent.Key;
				//System.out.println("  indexFieldName=" + indexFieldName + " fields=" + ent.getValue());
				IntsRef ordinals = new IntsRef(32);
				foreach (FacetField facetField in ent.Value)
				{
					FacetsConfig.DimConfig ft = GetDimConfig(facetField.dim);
					if (facetField.path.Length > 1 && ft.hierarchical == false)
					{
						throw new ArgumentException("dimension \"" + facetField.dim + "\" is not hierarchical yet has "
							 + facetField.path.Length + " components");
					}
					FacetLabel cp = new FacetLabel(facetField.dim, facetField.path);
					CheckTaxoWriter(taxoWriter);
					int ordinal = taxoWriter.AddCategory(cp);
					if (ordinals.length == ordinals.ints.Length)
					{
						ordinals.Grow(ordinals.length + 1);
					}
					ordinals.ints[ordinals.length++] = ordinal;
					//System.out.println("ords[" + (ordinals.length-1) + "]=" + ordinal);
					//System.out.println("  add cp=" + cp);
					if (ft.multiValued && (ft.hierarchical || ft.requireDimCount))
					{
						//System.out.println("  add parents");
						// Add all parents too:
						int parent = taxoWriter.GetParent(ordinal);
						while (parent > 0)
						{
							if (ordinals.ints.Length == ordinals.length)
							{
								ordinals.Grow(ordinals.length + 1);
							}
							ordinals.ints[ordinals.length++] = parent;
							parent = taxoWriter.GetParent(parent);
						}
						if (ft.requireDimCount == false)
						{
							// Remove last (dimension) ord:
							ordinals.length--;
						}
					}
					// Drill down:
					for (int i = 1; i <= cp.length; i++)
					{
						doc.Add(new StringField(indexFieldName, PathToString(cp.components, i), Field.Store
							.NO));
					}
				}
				// Facet counts:
				// DocValues are considered stored fields:
				doc.Add(new BinaryDocValuesField(indexFieldName, DedupAndEncode(ordinals)));
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void ProcessSSDVFacetFields(IDictionary<string, IList<SortedSetDocValuesFacetField
			>> byField, Lucene.Net.Document.Document doc)
		{
			//System.out.println("process SSDV: " + byField);
			foreach (KeyValuePair<string, IList<SortedSetDocValuesFacetField>> ent in byField
				.EntrySet())
			{
				string indexFieldName = ent.Key;
				//System.out.println("  field=" + indexFieldName);
				foreach (SortedSetDocValuesFacetField facetField in ent.Value)
				{
					FacetLabel cp = new FacetLabel(facetField.dim, facetField.label);
					string fullPath = PathToString(cp.components, cp.length);
					//System.out.println("add " + fullPath);
					// For facet counts:
					doc.Add(new SortedSetDocValuesField(indexFieldName, new BytesRef(fullPath)));
					// For drill-down:
					doc.Add(new StringField(indexFieldName, fullPath, Field.Store.NO));
					doc.Add(new StringField(indexFieldName, facetField.dim, Field.Store.NO));
				}
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		private void ProcessAssocFacetFields(TaxonomyWriter taxoWriter, IDictionary<string
			, IList<AssociationFacetField>> byField, Lucene.Net.Document.Document doc
			)
		{
			foreach (KeyValuePair<string, IList<AssociationFacetField>> ent in byField.EntrySet
				())
			{
				byte[] bytes = new byte[16];
				int upto = 0;
				string indexFieldName = ent.Key;
				foreach (AssociationFacetField field in ent.Value)
				{
					// NOTE: we don't add parents for associations
					CheckTaxoWriter(taxoWriter);
					FacetLabel label = new FacetLabel(field.dim, field.path);
					int ordinal = taxoWriter.AddCategory(label);
					if (upto + 4 > bytes.Length)
					{
						bytes = ArrayUtil.Grow(bytes, upto + 4);
					}
					// big-endian:
					bytes[upto++] = unchecked((byte)(ordinal >> 24));
					bytes[upto++] = unchecked((byte)(ordinal >> 16));
					bytes[upto++] = unchecked((byte)(ordinal >> 8));
					bytes[upto++] = unchecked((byte)ordinal);
					if (upto + field.assoc.length > bytes.Length)
					{
						bytes = ArrayUtil.Grow(bytes, upto + field.assoc.length);
					}
					System.Array.Copy(field.assoc.bytes, field.assoc.offset, bytes, upto, field.assoc
						.length);
					upto += field.assoc.length;
					// Drill down:
					for (int i = 1; i <= label.length; i++)
					{
						doc.Add(new StringField(indexFieldName, PathToString(label.components, i), Field.Store
							.NO));
					}
				}
				doc.Add(new BinaryDocValuesField(indexFieldName, new BytesRef(bytes, 0, upto)));
			}
		}

		/// <summary>
		/// Encodes ordinals into a BytesRef; expert: subclass can
		/// override this to change encoding.
		/// </summary>
		/// <remarks>
		/// Encodes ordinals into a BytesRef; expert: subclass can
		/// override this to change encoding.
		/// </remarks>
		protected internal virtual BytesRef DedupAndEncode(IntsRef ordinals)
		{
			Arrays.Sort(ordinals.ints, ordinals.offset, ordinals.length);
			byte[] bytes = new byte[5 * ordinals.length];
			int lastOrd = -1;
			int upto = 0;
			for (int i = 0; i < ordinals.length; i++)
			{
				int ord = ordinals.ints[ordinals.offset + i];
				// ord could be == lastOrd, so we must dedup:
				if (ord > lastOrd)
				{
					int delta;
					if (lastOrd == -1)
					{
						delta = ord;
					}
					else
					{
						delta = ord - lastOrd;
					}
					if ((delta & ~unchecked((int)(0x7F))) == 0)
					{
						bytes[upto] = unchecked((byte)delta);
						upto++;
					}
					else
					{
						if ((delta & ~unchecked((int)(0x3FFF))) == 0)
						{
							bytes[upto] = unchecked((byte)(unchecked((int)(0x80)) | ((delta & unchecked((int)
								(0x3F80))) >> 7)));
							bytes[upto + 1] = unchecked((byte)(delta & unchecked((int)(0x7F))));
							upto += 2;
						}
						else
						{
							if ((delta & ~unchecked((int)(0x1FFFFF))) == 0)
							{
								bytes[upto] = unchecked((byte)(unchecked((int)(0x80)) | ((delta & unchecked((int)
									(0x1FC000))) >> 14)));
								bytes[upto + 1] = unchecked((byte)(unchecked((int)(0x80)) | ((delta & unchecked((
									int)(0x3F80))) >> 7)));
								bytes[upto + 2] = unchecked((byte)(delta & unchecked((int)(0x7F))));
								upto += 3;
							}
							else
							{
								if ((delta & ~unchecked((int)(0xFFFFFFF))) == 0)
								{
									bytes[upto] = unchecked((byte)(unchecked((int)(0x80)) | ((delta & unchecked((int)
										(0xFE00000))) >> 21)));
									bytes[upto + 1] = unchecked((byte)(unchecked((int)(0x80)) | ((delta & unchecked((
										int)(0x1FC000))) >> 14)));
									bytes[upto + 2] = unchecked((byte)(unchecked((int)(0x80)) | ((delta & unchecked((
										int)(0x3F80))) >> 7)));
									bytes[upto + 3] = unchecked((byte)(delta & unchecked((int)(0x7F))));
									upto += 4;
								}
								else
								{
									bytes[upto] = unchecked((byte)(unchecked((int)(0x80)) | ((delta & unchecked((int)
										(0xF0000000))) >> 28)));
									bytes[upto + 1] = unchecked((byte)(unchecked((int)(0x80)) | ((delta & unchecked((
										int)(0xFE00000))) >> 21)));
									bytes[upto + 2] = unchecked((byte)(unchecked((int)(0x80)) | ((delta & unchecked((
										int)(0x1FC000))) >> 14)));
									bytes[upto + 3] = unchecked((byte)(unchecked((int)(0x80)) | ((delta & unchecked((
										int)(0x3F80))) >> 7)));
									bytes[upto + 4] = unchecked((byte)(delta & unchecked((int)(0x7F))));
									upto += 5;
								}
							}
						}
					}
					lastOrd = ord;
				}
			}
			return new BytesRef(bytes, 0, upto);
		}

		private void CheckTaxoWriter(TaxonomyWriter taxoWriter)
		{
			if (taxoWriter == null)
			{
				throw new InvalidOperationException("a non-null TaxonomyWriter must be provided when indexing FacetField or AssociationFacetField"
					);
			}
		}

		private const char DELIM_CHAR = '\u001F';

		private const char ESCAPE_CHAR = '\u001E';

		// Joins the path components together:
		// Escapes any occurrence of the path component inside the label:
		/// <summary>Turns a dim + path into an encoded string.</summary>
		/// <remarks>Turns a dim + path into an encoded string.</remarks>
		public static string PathToString(string dim, string[] path)
		{
			string[] fullPath = new string[1 + path.Length];
			fullPath[0] = dim;
			System.Array.Copy(path, 0, fullPath, 1, path.Length);
			return PathToString(fullPath, fullPath.Length);
		}

		/// <summary>Turns a dim + path into an encoded string.</summary>
		/// <remarks>Turns a dim + path into an encoded string.</remarks>
		public static string PathToString(string[] path)
		{
			return PathToString(path, path.Length);
		}

		/// <summary>
		/// Turns the first
		/// <code>length</code>
		/// elements of
		/// <code>path</code>
		/// into an encoded string.
		/// </summary>
		public static string PathToString(string[] path, int length)
		{
			if (length == 0)
			{
				return string.Empty;
			}
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < length; i++)
			{
				string s = path[i];
				if (s.Length == 0)
				{
					throw new ArgumentException("each path component must have length > 0 (got: \"\")"
						);
				}
				int numChars = s.Length;
				for (int j = 0; j < numChars; j++)
				{
					char ch = s[j];
					if (ch == DELIM_CHAR || ch == ESCAPE_CHAR)
					{
						sb.Append(ESCAPE_CHAR);
					}
					sb.Append(ch);
				}
				sb.Append(DELIM_CHAR);
			}
			// Trim off last DELIM_CHAR:
			sb.Length = sb.Length - 1;
			return sb.ToString();
		}

		/// <summary>
		/// Turns an encoded string (from a previous call to
		/// <see cref="PathToString(string[])">PathToString(string[])</see>
		/// ) back into the original
		/// <code>String[]</code>
		/// .
		/// </summary>
		public static string[] StringToPath(string s)
		{
			IList<string> parts = new AList<string>();
			int length = s.Length;
			if (length == 0)
			{
				return new string[0];
			}
			char[] buffer = new char[length];
			int upto = 0;
			bool lastEscape = false;
			for (int i = 0; i < length; i++)
			{
				char ch = s[i];
				if (lastEscape)
				{
					buffer[upto++] = ch;
					lastEscape = false;
				}
				else
				{
					if (ch == ESCAPE_CHAR)
					{
						lastEscape = true;
					}
					else
					{
						if (ch == DELIM_CHAR)
						{
							parts.AddItem(new string(buffer, 0, upto));
							upto = 0;
						}
						else
						{
							buffer[upto++] = ch;
						}
					}
				}
			}
			parts.AddItem(new string(buffer, 0, upto));
			return Sharpen.Collections.ToArray(!lastEscape, new string[parts.Count]);
		}
	}
}
