using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.Vectors.ExtraExtensions
{
	/// <summary>
	/// Extensions for producing vectors via Select.
	/// They hide the generic Select methods of the base class library
	/// which return <see cref="IEnumerable{Double}"/>
	/// and they return <see cref="Vector"/> instead.
	/// </summary>
	public static class SelectExtensions
	{
		/// <summary>
		/// Project a collection to a vector using an element-by-element mapping.
		/// </summary>
		/// <typeparam name="T">The type of the elements in the source collection.</typeparam>
		/// <param name="source">The collection to project.</param>
		/// <param name="selector">The element-wise mapping function.</param>
		/// <returns>Returns the projection.</returns>
		public static Vector Select<T>(this IEnumerable<T> source, Func<T, double> selector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			IEnumerable<double> result = Enumerable.Select(source, selector);

			return new Vector(result);
		}

		/// <summary>
		/// Project a collection to a vector using an element-by-element mapping.
		/// </summary>
		/// <typeparam name="T">The type of the elements in the source collection.</typeparam>
		/// <param name="source">The collection to project.</param>
		/// <param name="selector">The mapping function having arguments the element and its index.</param>
		/// <returns>Returns the projection.</returns>
		public static Vector Select<T>(this IEnumerable<T> source, Func<T, int, double> selector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			IEnumerable<double> result = Enumerable.Select(source, selector);

			return new Vector(result);
		}

		/// <summary>
		/// Project a collection to a vector using an element-by-element mapping.
		/// </summary>
		/// <typeparam name="T">The type of the elements in the source collection.</typeparam>
		/// <param name="source">The collection to project.</param>
		/// <param name="selector">The element-wise mapping function.</param>
		/// <returns>Returns the projection.</returns>
		public static Vector Select<T>(this ParallelQuery<T> source, Func<T, double> selector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			ParallelQuery<double> result = ParallelEnumerable.Select(source.AsOrdered(), selector);

			return new Vector(result);
		}

		/// <summary>
		/// Project a collection to a vector using an element-by-element mapping.
		/// </summary>
		/// <typeparam name="T">The type of the elements in the source collection.</typeparam>
		/// <param name="source">The collection to project.</param>
		/// <param name="selector">The mapping function having arguments the element and its index.</param>
		/// <returns>Returns the projection.</returns>
		public static Vector Select<T>(this ParallelQuery<T> source, Func<T, int, double> selector)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			ParallelQuery<double> result = ParallelEnumerable.Select(source.AsOrdered(), selector);

			return new Vector(result);
		}

	}
}
