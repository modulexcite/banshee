//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// Copyright (C) Lluis Sanchez Gual, 2004
//

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Mono.CodeGeneration
{
	public class CodeArgumentReference: CodeValueReference
	{
		Type type;
		int argNum;
		string name;
		
		public CodeArgumentReference (Type type, int argNum, string name)
		{
			if (type == typeof(void))
				throw new InvalidOperationException ("Can't create a variable of type void");
			this.type = type;
			this.argNum = argNum;
			this.name = name;		
		}
		
		public override void Generate (ILGenerator gen)
		{
			switch (argNum) {
				case 0: gen.Emit (OpCodes.Ldarg_0); break;
				case 1: gen.Emit (OpCodes.Ldarg_1); break;
				case 2: gen.Emit (OpCodes.Ldarg_2); break;
				case 3: gen.Emit (OpCodes.Ldarg_3); break;
				default: gen.Emit (OpCodes.Ldarg, argNum); break;
			}
		}
		
		public override void GenerateSet (ILGenerator gen, CodeExpression value)
		{
			value.Generate (gen);
			CodeGenerationHelper.GenerateSafeConversion (gen, type, value.GetResultType ());
			gen.Emit (OpCodes.Starg, argNum);
		}
		
		public override void PrintCode (CodeWriter cp)
		{
			cp.Write (name);
		}
		
		public override Type GetResultType ()
		{
			return type;
		}
	}
}
