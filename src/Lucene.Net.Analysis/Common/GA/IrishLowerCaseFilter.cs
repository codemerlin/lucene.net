/*
 *
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 *
*/

using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Support;

namespace Lucene.Net.Analysis.GA
{
	/// <summary>
	/// Normalises token text to lower case, handling t-prothesis
	/// and n-eclipsis (i.e., that 'nAthair' should become 'n-athair')
	/// </summary>
	public sealed class IrishLowerCaseFilter : TokenFilter
	{
		private readonly CharTermAttribute termAtt;

		/// <summary>Create an IrishLowerCaseFilter that normalises Irish token text.</summary>
		/// <remarks>Create an IrishLowerCaseFilter that normalises Irish token text.</remarks>
		public IrishLowerCaseFilter(TokenStream @in) : base(@in)
		{
			termAtt = AddAttribute<CharTermAttribute>();
		}

		/// <exception cref="System.IO.IOException"></exception>
		public override bool IncrementToken()
		{
			if (input.IncrementToken())
			{
				char[] chArray = termAtt.Buffer;
				int chLen = termAtt.Length;
				int idx = 0;
				if (chLen > 1 && (chArray[0] == 'n' || chArray[0] == 't') && IsUpperVowel(chArray
					[1]))
				{
					chArray = termAtt.ResizeBuffer(chLen + 1);
					for (int i = chLen; i > 1; i--)
					{
						chArray[i] = chArray[i - 1];
					}
					chArray[1] = '-';
					termAtt.SetLength(chLen + 1);
					idx = 2;
					chLen = chLen + 1;
				}
				for (int i_1 = idx; i_1 < chLen; )
				{
					i_1 += Character.ToChars(System.Char.ToLower(chArray[i_1]), chArray, i_1);
				}
				return true;
			}
			else
			{
				return false;
			}
		}

		private bool IsUpperVowel(int v)
		{
			switch (v)
			{
				case 'A':
				case 'E':
				case 'I':
				case 'O':
				case 'U':
				case '\u00c1':
				case '\u00c9':
				case '\u00cd':
				case '\u00d3':
				case '\u00da':
				{
					// vowels with acute accent (fada)
					return true;
				}

				default:
				{
					return false;
					break;
				}
			}
		}
	}
}
