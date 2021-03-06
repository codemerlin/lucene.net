﻿namespace org.apache.lucene.analysis.miscellaneous
{

	/*
	 * Licensed to the Apache Software Foundation (ASF) under one or more
	 * contributor license agreements.  See the NOTICE file distributed with
	 * this work for additional information regarding copyright ownership.
	 * The ASF licenses this file to You under the Apache License, Version 2.0
	 * (the "License"); you may not use this file except in compliance with
	 * the License.  You may obtain a copy of the License at
	 *
	 *     http://www.apache.org/licenses/LICENSE-2.0
	 *
	 * Unless required by applicable law or agreed to in writing, software
	 * distributed under the License is distributed on an "AS IS" BASIS,
	 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	 * See the License for the specific language governing permissions and
	 * limitations under the License.
	 */

	using KeywordAttribute = org.apache.lucene.analysis.tokenattributes.KeywordAttribute;

	/// <summary>
	/// Marks terms as keywords via the <seealso cref="KeywordAttribute"/>.
	/// </summary>
	/// <seealso cref= KeywordAttribute </seealso>
	public abstract class KeywordMarkerFilter : TokenFilter
	{

	  private readonly KeywordAttribute keywordAttr = addAttribute(typeof(KeywordAttribute));

	  /// <summary>
	  /// Creates a new <seealso cref="KeywordMarkerFilter"/> </summary>
	  /// <param name="in"> the input stream </param>
	  protected internal KeywordMarkerFilter(TokenStream @in) : base(@in)
	  {
	  }

//JAVA TO C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: @Override public final boolean incrementToken() throws java.io.IOException
	  public override bool incrementToken()
	  {
		if (input.incrementToken())
		{
		  if (Keyword)
		  {
			keywordAttr.Keyword = true;
		  }
		  return true;
		}
		else
		{
		  return false;
		}
	  }

	  protected internal abstract bool Keyword {get;}

	}

}