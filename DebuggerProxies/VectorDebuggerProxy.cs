using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.Vectors.DebuggerProxies
{
	/// <summary>
	/// DEbugger proxy for <see cref="Vector"/>.
	/// </summary>
	internal class VectorDebuggerProxy
	{
		private Vector vector;

		public VectorDebuggerProxy(Vector vector)
		{
			if (vector == null) throw new ArgumentNullException("vector");

			this.vector = vector;
		}

		public double[] Items
		{
			get
			{
				return vector.array;
			}
		}
	}
}
