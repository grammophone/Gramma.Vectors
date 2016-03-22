using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.Vectors.DebuggerProxies
{
	/// <summary>
	/// Debug proxy for <see cref="SparseVector"/>.
	/// </summary>
	internal class SparseVectorDebuggerProxy
	{
		private SparseVector vector;

		public SparseVectorDebuggerProxy(SparseVector vector)
		{
			if (vector == null) throw new ArgumentNullException("vector");

			this.vector = vector;
		}

		public SparseVector.Entry[] Entries
		{
			get
			{
				return vector.ToArray();
			}
		}
	}
}
