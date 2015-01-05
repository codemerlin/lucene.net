using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;

namespace Lucene.Net.Tests.Expressions.JS
{
	/// <summary>Tests customing the function map</summary>
	public class TestCustomFunctions : LuceneTestCase
	{
		private static double DELTA = 0.0000001;

		/// <summary>empty list of methods</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestEmpty()
		{
			IDictionary<string, MethodInfo> functions = Collections.EmptyMap();
			try
			{
				JavascriptCompiler.Compile("sqrt(20)", functions, GetType().GetClassLoader());
				NUnit.Framework.Assert.Fail();
			}
			catch (ArgumentException e)
			{
				NUnit.Framework.Assert.IsTrue(e.Message.Contains("Unrecognized method"));
			}
		}

		/// <summary>using the default map explicitly</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestDefaultList()
		{
			IDictionary<string, MethodInfo> functions = JavascriptCompiler.DEFAULT_FUNCTIONS;
			Expression expr = JavascriptCompiler.Compile("sqrt(20)", functions, GetType().GetClassLoader
				());
			NUnit.Framework.Assert.AreEqual(Math.Sqrt(20), expr.Evaluate(0, null), DELTA);
		}

		public static double ZeroArgMethod()
		{
			return 5;
		}

		/// <summary>tests a method with no arguments</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestNoArgMethod()
		{
			IDictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
			functions.Put("foo", GetType().GetMethod("zeroArgMethod"));
			Expression expr = JavascriptCompiler.Compile("foo()", functions, GetType().GetClassLoader
				());
			NUnit.Framework.Assert.AreEqual(5, expr.Evaluate(0, null), DELTA);
		}

		public static double OneArgMethod(double arg1)
		{
			return 3 + arg1;
		}

		/// <summary>tests a method with one arguments</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestOneArgMethod()
		{
			IDictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
			functions.Put("foo", GetType().GetMethod("oneArgMethod", typeof(double)));
			Expression expr = JavascriptCompiler.Compile("foo(3)", functions, GetType().GetClassLoader
				());
			NUnit.Framework.Assert.AreEqual(6, expr.Evaluate(0, null), DELTA);
		}

		public static double ThreeArgMethod(double arg1, double arg2, double arg3)
		{
			return arg1 + arg2 + arg3;
		}

		/// <summary>tests a method with three arguments</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestThreeArgMethod()
		{
			IDictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
			functions.Put("foo", GetType().GetMethod("threeArgMethod", typeof(double), typeof(
				double), typeof(double)));
			Expression expr = JavascriptCompiler.Compile("foo(3, 4, 5)", functions, GetType()
				.GetClassLoader());
			NUnit.Framework.Assert.AreEqual(12, expr.Evaluate(0, null), DELTA);
		}

		/// <summary>tests a map with 2 functions</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestTwoMethods()
		{
			IDictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
			functions.Put("foo", GetType().GetMethod("zeroArgMethod"));
			functions.Put("bar", GetType().GetMethod("oneArgMethod", typeof(double)));
			Expression expr = JavascriptCompiler.Compile("foo() + bar(3)", functions, GetType
				().GetClassLoader());
			NUnit.Framework.Assert.AreEqual(11, expr.Evaluate(0, null), DELTA);
		}

		public static string BogusReturnType()
		{
			return "bogus!";
		}

		/// <summary>wrong return type: must be double</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestWrongReturnType()
		{
			IDictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
			functions.Put("foo", GetType().GetMethod("bogusReturnType"));
			try
			{
				JavascriptCompiler.Compile("foo()", functions, GetType().GetClassLoader());
				NUnit.Framework.Assert.Fail();
			}
			catch (ArgumentException e)
			{
				NUnit.Framework.Assert.IsTrue(e.Message.Contains("does not return a double"));
			}
		}

		public static double BogusParameterType(string s)
		{
			return 0;
		}

		/// <summary>wrong param type: must be doubles</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestWrongParameterType()
		{
			IDictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
			functions.Put("foo", GetType().GetMethod("bogusParameterType", typeof(string)));
			try
			{
				JavascriptCompiler.Compile("foo(2)", functions, GetType().GetClassLoader());
				NUnit.Framework.Assert.Fail();
			}
			catch (ArgumentException e)
			{
				NUnit.Framework.Assert.IsTrue(e.Message.Contains("must take only double parameters"
					));
			}
		}

		public virtual double NonStaticMethod()
		{
			return 0;
		}

		/// <summary>wrong modifiers: must be static</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestWrongNotStatic()
		{
			IDictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
			functions.Put("foo", GetType().GetMethod("nonStaticMethod"));
			try
			{
				JavascriptCompiler.Compile("foo()", functions, GetType().GetClassLoader());
				NUnit.Framework.Assert.Fail();
			}
			catch (ArgumentException e)
			{
				NUnit.Framework.Assert.IsTrue(e.Message.Contains("is not static"));
			}
		}

		internal static double NonPublicMethod()
		{
			return 0;
		}

		/// <summary>wrong modifiers: must be public</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestWrongNotPublic()
		{
			IDictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
			functions.Put("foo", Sharpen.Runtime.GetDeclaredMethod(GetType(), "nonPublicMethod"
				));
			try
			{
				JavascriptCompiler.Compile("foo()", functions, GetType().GetClassLoader());
				NUnit.Framework.Assert.Fail();
			}
			catch (ArgumentException e)
			{
				NUnit.Framework.Assert.IsTrue(e.Message.Contains("is not public"));
			}
		}

		internal class NestedNotPublic
		{
			public static double Method()
			{
				return 0;
			}
		}

		/// <summary>wrong class modifiers: class containing method is not public</summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestWrongNestedNotPublic()
		{
			IDictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
			functions.Put("foo", typeof(TestCustomFunctions.NestedNotPublic).GetMethod("method"
				));
			try
			{
				JavascriptCompiler.Compile("foo()", functions, GetType().GetClassLoader());
				NUnit.Framework.Assert.Fail();
			}
			catch (ArgumentException e)
			{
				NUnit.Framework.Assert.IsTrue(e.Message.Contains("is not public"));
			}
		}

		/// <summary>Classloader that can be used to create a fake static class that has one method returning a static var
		/// 	</summary>
		internal sealed class Loader : ClassLoader, Opcodes
		{
			protected Loader(ClassLoader parent) : base(parent)
			{
			}

			public Type CreateFakeClass()
			{
				string className = typeof(TestCustomFunctions).FullName + "$Foo";
				ClassWriter classWriter = new ClassWriter(ClassWriter.COMPUTE_FRAMES | ClassWriter
					.COMPUTE_MAXS);
				classWriter.Visit(Opcodes.V1_5, ACC_PUBLIC | ACC_SUPER | ACC_FINAL | ACC_SYNTHETIC
					, className.Replace('.', '/'), null, Type.GetInternalName(typeof(object)), null);
				Method m = Method.GetMethod("void <init>()");
				GeneratorAdapter constructor = new GeneratorAdapter(ACC_PRIVATE | ACC_SYNTHETIC, 
					m, null, null, classWriter);
				constructor.LoadThis();
				constructor.LoadArgs();
				constructor.InvokeConstructor(Type.GetType(typeof(object)), m);
				constructor.ReturnValue();
				constructor.EndMethod();
				GeneratorAdapter gen = new GeneratorAdapter(ACC_STATIC | ACC_PUBLIC | ACC_SYNTHETIC
					, Method.GetMethod("double bar()"), null, null, classWriter);
				gen.Push(2.0);
				gen.ReturnValue();
				gen.EndMethod();
				byte[] bc = classWriter.ToByteArray();
				return DefineClass(className, bc, 0, bc.Length);
			}
		}

		/// <summary>
		/// uses this test with a different classloader and tries to
		/// register it using the default classloader, which should fail
		/// </summary>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestClassLoader()
		{
			ClassLoader thisLoader = GetType().GetClassLoader();
			TestCustomFunctions.Loader childLoader = new TestCustomFunctions.Loader(thisLoader
				);
			Type fooClass = childLoader.CreateFakeClass();
			MethodInfo barMethod = fooClass.GetMethod("bar");
			IDictionary<string, MethodInfo> functions = Sharpen.Collections.SingletonMap("bar"
				, barMethod);
			NUnit.Framework.Assert.AreNotSame(thisLoader, fooClass.GetClassLoader());
			NUnit.Framework.Assert.AreNotSame(thisLoader, barMethod.DeclaringType.GetClassLoader
				());
			// this should pass:
			Expression expr = JavascriptCompiler.Compile("bar()", functions, childLoader);
			NUnit.Framework.Assert.AreEqual(2.0, expr.Evaluate(0, null), DELTA);
			// use our classloader, not the foreign one, which should fail!
			try
			{
				JavascriptCompiler.Compile("bar()", functions, thisLoader);
				NUnit.Framework.Assert.Fail();
			}
			catch (ArgumentException e)
			{
				NUnit.Framework.Assert.IsTrue(e.Message.Contains("is not declared by a class which is accessible by the given parent ClassLoader"
					));
			}
			// mix foreign and default functions
			IDictionary<string, MethodInfo> mixedFunctions = new Dictionary<string, MethodInfo
				>(JavascriptCompiler.DEFAULT_FUNCTIONS);
			mixedFunctions.PutAll(functions);
			expr = JavascriptCompiler.Compile("bar()", mixedFunctions, childLoader);
			NUnit.Framework.Assert.AreEqual(2.0, expr.Evaluate(0, null), DELTA);
			expr = JavascriptCompiler.Compile("sqrt(20)", mixedFunctions, childLoader);
			NUnit.Framework.Assert.AreEqual(Math.Sqrt(20), expr.Evaluate(0, null), DELTA);
			// use our classloader, not the foreign one, which should fail!
			try
			{
				JavascriptCompiler.Compile("bar()", mixedFunctions, thisLoader);
				NUnit.Framework.Assert.Fail();
			}
			catch (ArgumentException e)
			{
				NUnit.Framework.Assert.IsTrue(e.Message.Contains("is not declared by a class which is accessible by the given parent ClassLoader"
					));
			}
		}

		internal static string MESSAGE = "This should not happen but it happens";

		public class StaticThrowingException
		{
			public static double Method()
			{
				throw new ArithmeticException(MESSAGE);
			}
		}

		/// <summary>the method throws an exception.</summary>
		/// <remarks>the method throws an exception. We should check the stack trace that it contains the source code of the expression as file name.
		/// 	</remarks>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestThrowingException()
		{
			IDictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
			functions.Put("foo", typeof(TestCustomFunctions.StaticThrowingException).GetMethod
				("method"));
			string source = "3 * foo() / 5";
			Expression expr = JavascriptCompiler.Compile(source, functions, GetType().GetClassLoader
				());
			try
			{
				expr.Evaluate(0, null);
				NUnit.Framework.Assert.Fail();
			}
			catch (ArithmeticException e)
			{
				NUnit.Framework.Assert.AreEqual(MESSAGE, e.Message);
				StringWriter sw = new StringWriter();
				PrintWriter pw = new PrintWriter(sw);
				Sharpen.Runtime.PrintStackTrace(e, pw);
				pw.Flush();
				NUnit.Framework.Assert.IsTrue(sw.ToString().Contains("JavascriptCompiler$CompiledExpression.evaluate("
					 + source + ")"));
			}
		}

		/// <summary>test that namespaces work with custom expressions.</summary>
		/// <remarks>test that namespaces work with custom expressions.</remarks>
		/// <exception cref="System.Exception"></exception>
		public virtual void TestNamespaces()
		{
			IDictionary<string, MethodInfo> functions = new Dictionary<string, MethodInfo>();
			functions.Put("foo.bar", GetType().GetMethod("zeroArgMethod"));
			string source = "foo.bar()";
			Expression expr = JavascriptCompiler.Compile(source, functions, GetType().GetClassLoader
				());
			NUnit.Framework.Assert.AreEqual(5, expr.Evaluate(0, null), DELTA);
		}
	}
}
