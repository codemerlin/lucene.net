/* 
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.TestFramework.Util;
using Lucene.Net.Util;
using NUnit.Framework;
using Attribute = Lucene.Net.Util.Attribute;
using FlagsAttribute = Lucene.Net.Analysis.Tokenattributes.FlagsAttribute;

namespace Lucene.Net.Analysis
{	
    [TestFixture]
	public class TestToken:LuceneTestCase
	{
        [Test]
		public virtual void TestCtor()
        {
            Token t = new Token();
            char[] content = "hello".toCharArray();
            t.CopyBuffer(content, 0, content.Length);
            assertNotSame(t.Buffer, content);
            assertEquals(0, t.StartOffset);
            assertEquals(0, t.EndOffset);
            assertEquals("hello", t.ToString());
            assertEquals("word", t.Type);
            assertEquals(0, t.Flags);

            t = new Token(6, 22);
            t.CopyBuffer(content, 0, content.Length);
            assertEquals("hello", t.ToString());
            assertEquals("hello", t.ToString());
            assertEquals(6, t.StartOffset);
            assertEquals(22, t.EndOffset);
            assertEquals("word", t.Type);
            assertEquals(0, t.Flags);

            t = new Token(6, 22, 7);
            t.CopyBuffer(content, 0, content.Length);
            assertEquals("hello", t.ToString());
            assertEquals("hello", t.ToString());
            assertEquals(6, t.StartOffset);
            assertEquals(22, t.EndOffset);
            assertEquals("word", t.Type);
            assertEquals(7, t.Flags);

            t = new Token(6, 22, "junk");
            t.CopyBuffer(content, 0, content.Length);
            assertEquals("hello", t.ToString());
            assertEquals("hello", t.ToString());
            assertEquals(6, t.StartOffset);
            assertEquals(22, t.EndOffset);
            assertEquals("junk", t.Type);
            assertEquals(0, t.Flags);
        }
		
        [Test]
		public virtual void TestResize()
        {
            Token t = new Token();
			char[] content = "hello".ToCharArray();
            t.CopyBuffer(content, 0, content.Length);
            for (int i = 0; i < 2000; i++)
            {
                t.ResizeBuffer(i);
                assertTrue(i <= t.Buffer.Length);
                assertEquals("hello", t.ToString());
            }
        }
		
        [Test]
		public virtual void TestGrow()
        {
            Token t = new Token();
            StringBuilder buf = new StringBuilder("ab");
            for (int i = 0; i < 20; i++)
            {
                char[] content = buf.toString().toCharArray();
                t.CopyBuffer(content, 0, content.Length);
                assertEquals(buf.Length, t.Length);
                assertEquals(buf.toString(), t.toString());
                buf.Append(buf.toString());
            }
            assertEquals(1048576, t.Length);

            // now as a string, second variant
            t = new Token();
            buf = new StringBuilder("ab");
            for (int i = 0; i < 20; i++)
            {
                t.SetEmpty().Append(buf);
                String content = buf.toString();
                assertEquals(content.Length, t.Length);
                assertEquals(content, t.toString());
                buf.append(content);
            }
            assertEquals(1048576, t.Length);

            // Test for slow growth to a long term
            t = new Token();
            buf = new StringBuilder("a");
            for (int i = 0; i < 20000; i++)
            {
                t.SetEmpty().Append(buf);
                String content = buf.toString();
                assertEquals(content.Length, t.Length);
                assertEquals(content, t.toString());
                buf.append("a");
            }
            assertEquals(20000, t.Length);

            // Test for slow growth to a long term
            t = new Token();
            buf = new StringBuilder("a");
            for (int i = 0; i < 20000; i++)
            {
                t.SetEmpty().Append(buf);
                String content = buf.toString();
                assertEquals(content.Length, t.Length);
                assertEquals(content, t.toString());
                buf.append("a");
            }
            assertEquals(20000, t.Length);
        }

        [Test]
		public virtual void TestToString()
        {
            char[] b = {'a', 'l', 'o', 'h', 'a'};
            Token t = new Token("", 0, 5);
            t.CopyBuffer(b, 0, 5);
            assertEquals("aloha", t.toString());

            t.SetEmpty().Append("hi there");
            assertEquals("hi there", t.toString());
        }

        [Test]
		public virtual void TestTermBufferEquals()
        {
            Token t1a = new Token();
            char[] content1a = "hello".toCharArray();
            t1a.CopyBuffer(content1a, 0, 5);
            Token t1b = new Token();
            char[] content1b = "hello".toCharArray();
            t1b.CopyBuffer(content1b, 0, 5);
            Token t2 = new Token();
            char[] content2 = "hello2".toCharArray();
            t2.CopyBuffer(content2, 0, 6);
            assertTrue(t1a.equals(t1b));
            assertFalse(t1a.equals(t2));
            assertFalse(t2.equals(t1b));
        }

        [Test]
		public virtual void TestMixedStringArray()
        {
            Token t = new Token("hello", 0, 5);
            assertEquals(t.Length, 5);
            assertEquals(t.toString(), "hello");
            t.SetEmpty().Append("hello2");
            assertEquals(t.Length, 6);
            assertEquals(t.toString(), "hello2");
            t.CopyBuffer("hello3".toCharArray(), 0, 6);
            assertEquals(t.toString(), "hello3");

            char[] buffer = t.Buffer;
            buffer[1] = 'o';
            assertEquals(t.toString(), "hollo3");
        }

        [Test]
		public virtual void TestClone()
        {
            Token t = new Token(0, 5);
            char[] content = "hello".toCharArray();
            t.CopyBuffer(content, 0, 5);
            char[] buf = t.Buffer;
            Token copy = AssertCloneIsEqual(t);
            assertEquals(t.toString(), copy.toString());
            assertNotSame(buf, copy.Buffer);

            BytesRef pl = new BytesRef(new sbyte[] {1, 2, 3, 4});
            t.Payload = pl;
            copy = AssertCloneIsEqual(t);
            assertEquals(pl, copy.Payload);
            assertNotSame(pl, copy.Payload);
        }

        [Test]
		public virtual void TestCopyTo()
        {
            Token t = new Token();
            Token copy = AssertCopyIsEqual(t);
            assertEquals("", t.toString());
            assertEquals("", copy.toString());

            t = new Token(0, 5);
            char[] content = "hello".toCharArray();
            t.CopyBuffer(content, 0, 5);
            char[] buf = t.Buffer;
            copy = AssertCopyIsEqual(t);
            assertEquals(t.toString(), copy.toString());
            assertNotSame(buf, copy.Buffer);

            BytesRef pl = new BytesRef(new sbyte[] {1, 2, 3, 4});
            t.Payload = pl;
            copy = AssertCopyIsEqual(t);
            assertEquals(pl, copy.Payload);
            assertNotSame(pl, copy.Payload);
        }

        public interface ISenselessAttribute : IAttribute {}

        public class SenselessAttribute : Attribute, ISenselessAttribute
        {
            public override void CopyTo(Attribute target) 
            { }

            public override void Clear() 
            { }

            public override bool Equals(object other)
            {
                return other is SenselessAttribute;
            }

            public override int GetHashCode()
            {
                return 0;
            }
        }

//        [Test]
//          public void testTokenAttributeFactory() {
//            //TODO MockTokenizer
//    //TokenStream ts = new MockTokenizer(Token.TOKEN_ATTRIBUTE_FACTORY, new StringReader("foo bar"), MockTokenizer.WHITESPACE, false, MockTokenizer.DEFAULT_MAX_TOKEN_LENGTH);
//            
//            TokenStream ts = new WhitespaceTokenizer(Analysis.Token.TOKEN_ATTRIBUTE_FACTORY, new StringReader("foo, bar"));
//    
//    assertTrue("SenselessAttribute is not implemented by SenselessAttributeImpl",
//      ts.addAttribute(SenselessAttribute.class) instanceof SenselessAttributeImpl);
//    
//    assertTrue("CharTermAttribute is not implemented by Token",
//      ts.addAttribute(CharTermAttribute.class) instanceof Token);
//    assertTrue("OffsetAttribute is not implemented by Token",
//      ts.addAttribute(OffsetAttribute.class) instanceof Token);
//    assertTrue("FlagsAttribute is not implemented by Token",
//      ts.addAttribute(FlagsAttribute.class) instanceof Token);
//    assertTrue("PayloadAttribute is not implemented by Token",
//      ts.addAttribute(PayloadAttribute.class) instanceof Token);
//    assertTrue("PositionIncrementAttribute is not implemented by Token", 
//      ts.addAttribute(PositionIncrementAttribute.class) instanceof Token);
//    assertTrue("TypeAttribute is not implemented by Token",
//      ts.addAttribute(TypeAttribute.class) instanceof Token);
//  }
//
        [Test]
        public void TestTokenAttributeFactory()
        {
			TokenStream ts = new MockTokenizer(Token.TOKEN_ATTRIBUTE_FACTORY, new StringReader
				("foo bar"), MockTokenizer.WHITESPACE, false, MockTokenizer.DEFAULT_MAX_TOKEN_LENGTH
				);
            IsTrue(ts.AddAttribute<ISenselessAttribute>() is SenselessAttribute,
                          "TypeAttribute is not implemented by SenselessAttributeImpl");

            IsTrue(ts.AddAttribute<ICharTermAttribute>() is Token, "TermAttribute is not implemented by Token");
            IsTrue(ts.AddAttribute<IOffsetAttribute>() is Token, "OffsetAttribute is not implemented by Token");
            IsTrue(ts.AddAttribute<IFlagsAttribute>() is Token, "FlagsAttribute is not implemented by Token");
            IsTrue(ts.AddAttribute<IPayloadAttribute>() is Token, "PayloadAttribute is not implemented by Token");
            IsTrue(ts.AddAttribute<IPositionIncrementAttribute>() is Token, "PositionIncrementAttribute is not implemented by Token");
            IsTrue(ts.AddAttribute<ITypeAttribute>() is Token, "TypeAttribute is not implemented by Token");
        }
//        [Test]
		public virtual void TestAttributeReflection()
		{
			Token t = new Token("foobar", 6, 22, 8);
			TestUtil.AssertAttributeReflection(t, new AnonAttributeDictionary());
		}

		private sealed class AnonAttributeDictionary : Dictionary<string, object>
		{
			public AnonAttributeDictionary()
			{
				{
					this[typeof(CharTermAttribute).FullName + "#term"] =  "foobar";
					this[typeof(ITermToBytesRefAttribute).FullName + "#bytes"] =  new BytesRef("foobar");
					this[typeof(OffsetAttribute).FullName + "#startOffset"] =  6;
					this[typeof(OffsetAttribute).FullName + "#endOffset"] =  22;
					this[typeof(PositionIncrementAttribute).FullName + "#positionIncrement"] =  1;
					this[typeof(PositionLengthAttribute).FullName + "#positionLength"] =  1;
					this[typeof(PayloadAttribute).FullName + "#payload"] =  null;
					this[typeof(TypeAttribute).FullName + "#type"] =  TypeAttribute.DEFAULT_TYPE;
					this[typeof(FlagsAttribute).FullName + "#flags"] =  8;
				}
			}
		}

		public static T AssertCloneIsEqual<T>(T att) where T:CharTermAttribute
        {
            var clone = att.Clone();
            AreEqual(att, clone, "Clone must be equal");
            AreEqual(att.GetHashCode(), clone.GetHashCode(), "Clone's hashcode must be equal");
            return (T)clone;
        }

		public static T AssertCopyIsEqual<T>(T att) where T:CharTermAttribute
        {
            var copy = Activator.CreateInstance(att.GetType());
            att.CopyTo((Attribute) copy);
            AreEqual(att, copy, "Copied instance must be equal");
            AreEqual(att.GetHashCode(), copy.GetHashCode(), "Copied instance's hashcode must be equal");
            return (T) copy;
        }

        public TestToken()
        {
        }

        public TestToken(System.String name)
            : base(name)
        {
        }
	}
}