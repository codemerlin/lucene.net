/*
 * This code is derived from MyJavaLibrary (http://somelinktomycoollibrary)
 * 
 * If this is an open source Java library, include the proper license and copyright attributions here!
 */

using System;
using Org.Apache.Lucene.Expressions;
using Org.Apache.Lucene.Expressions.JS;
using Org.Apache.Lucene.Search;
using Org.Apache.Lucene.Util;
using Sharpen;

namespace Org.Apache.Lucene.Expressions
{
	/// <summary>Tests validation of bindings</summary>
	public class TestExpressionValidation : LuceneTestCase
	{
		/// <exception cref="System.Exception"></exception>
		public virtual void TestValidExternals()
		{
			SimpleBindings bindings = new SimpleBindings();
			bindings.Add(new SortField("valid0", SortField.Type.INT));
			bindings.Add(new SortField("valid1", SortField.Type.INT));
			bindings.Add(new SortField("valid2", SortField.Type.INT));
			bindings.Add(new SortField("_score", SortField.Type.SCORE));
			bindings.Add("valide0", JavascriptCompiler.Compile("valid0 - valid1 + valid2 + _score"
				));
			bindings.Validate();
			bindings.Add("valide1", JavascriptCompiler.Compile("valide0 + valid0"));
			bindings.Validate();
			bindings.Add("valide2", JavascriptCompiler.Compile("valide0 * valide1"));
			bindings.Validate();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestInvalidExternal()
		{
			SimpleBindings bindings = new SimpleBindings();
			bindings.Add(new SortField("valid", SortField.Type.INT));
			bindings.Add("invalid", JavascriptCompiler.Compile("badreference"));
			try
			{
				bindings.Validate();
				NUnit.Framework.Assert.Fail("didn't get expected exception");
			}
			catch (ArgumentException expected)
			{
				NUnit.Framework.Assert.IsTrue(expected.Message.Contains("Invalid reference"));
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestInvalidExternal2()
		{
			SimpleBindings bindings = new SimpleBindings();
			bindings.Add(new SortField("valid", SortField.Type.INT));
			bindings.Add("invalid", JavascriptCompiler.Compile("valid + badreference"));
			try
			{
				bindings.Validate();
				NUnit.Framework.Assert.Fail("didn't get expected exception");
			}
			catch (ArgumentException expected)
			{
				NUnit.Framework.Assert.IsTrue(expected.Message.Contains("Invalid reference"));
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestSelfRecursion()
		{
			SimpleBindings bindings = new SimpleBindings();
			bindings.Add("cycle0", JavascriptCompiler.Compile("cycle0"));
			try
			{
				bindings.Validate();
				NUnit.Framework.Assert.Fail("didn't get expected exception");
			}
			catch (ArgumentException expected)
			{
				NUnit.Framework.Assert.IsTrue(expected.Message.Contains("Cycle detected"));
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCoRecursion()
		{
			SimpleBindings bindings = new SimpleBindings();
			bindings.Add("cycle0", JavascriptCompiler.Compile("cycle1"));
			bindings.Add("cycle1", JavascriptCompiler.Compile("cycle0"));
			try
			{
				bindings.Validate();
				NUnit.Framework.Assert.Fail("didn't get expected exception");
			}
			catch (ArgumentException expected)
			{
				NUnit.Framework.Assert.IsTrue(expected.Message.Contains("Cycle detected"));
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCoRecursion2()
		{
			SimpleBindings bindings = new SimpleBindings();
			bindings.Add("cycle0", JavascriptCompiler.Compile("cycle1"));
			bindings.Add("cycle1", JavascriptCompiler.Compile("cycle2"));
			bindings.Add("cycle2", JavascriptCompiler.Compile("cycle0"));
			try
			{
				bindings.Validate();
				NUnit.Framework.Assert.Fail("didn't get expected exception");
			}
			catch (ArgumentException expected)
			{
				NUnit.Framework.Assert.IsTrue(expected.Message.Contains("Cycle detected"));
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCoRecursion3()
		{
			SimpleBindings bindings = new SimpleBindings();
			bindings.Add("cycle0", JavascriptCompiler.Compile("100"));
			bindings.Add("cycle1", JavascriptCompiler.Compile("cycle0 + cycle2"));
			bindings.Add("cycle2", JavascriptCompiler.Compile("cycle0 + cycle1"));
			try
			{
				bindings.Validate();
				NUnit.Framework.Assert.Fail("didn't get expected exception");
			}
			catch (ArgumentException expected)
			{
				NUnit.Framework.Assert.IsTrue(expected.Message.Contains("Cycle detected"));
			}
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCoRecursion4()
		{
			SimpleBindings bindings = new SimpleBindings();
			bindings.Add("cycle0", JavascriptCompiler.Compile("100"));
			bindings.Add("cycle1", JavascriptCompiler.Compile("100"));
			bindings.Add("cycle2", JavascriptCompiler.Compile("cycle1 + cycle0 + cycle3"));
			bindings.Add("cycle3", JavascriptCompiler.Compile("cycle0 + cycle1 + cycle2"));
			try
			{
				bindings.Validate();
				NUnit.Framework.Assert.Fail("didn't get expected exception");
			}
			catch (ArgumentException expected)
			{
				NUnit.Framework.Assert.IsTrue(expected.Message.Contains("Cycle detected"));
			}
		}
	}
}
