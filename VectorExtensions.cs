using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Gramma.Vectors
{
	/// <summary>
	/// Extension methods for collections containing <see cref="Vector"/> elements.
	/// </summary>
	public static class VectorExtensions
	{
		#region Extension methods

		/// <summary>
		/// Compute the sum of a collection of vectors.
		/// If the collection is empty, null is returned.
		/// </summary>
		/// <param name="vectors">The vectors to sum.</param>
		/// <param name="seed">Optional seed vector. If not null, the result will be accumulated on this vector.</param>
		/// <returns>Returns the sum of the vectors or <paramref name="seed"/> if the collection is empty.</returns>
		public static Vector Sum(this IEnumerable<Vector> vectors, Vector seed = null)
		{
			foreach (var vector in vectors)
			{
				if (seed == null)
					seed = vector.Clone();
				else
					seed.AddInPlace(vector);
			}

			return seed;
		}

		/// <summary>
		/// Computes the sum of the sequence of Vector items 
		/// that are obtained by invoking a transform function on each element 
		/// of the input sequence.
		/// If the collection is empty, null is returned.
		/// </summary>
		/// <typeparam name="T">The type of the element of the source sequence.</typeparam>
		/// <param name="source">The source sequence.</param>
		/// <param name="selector">
		/// The function that transforms each source sequence element to a Vector.
		/// </param>
		/// <param name="seed">Optional seed vector. If not null, the result will be accumulated on this vector.</param>
		/// <returns>
		/// Returns the sum of the vectors produced by
		/// the transforming function applied to each source element.
		/// If the collection is empty, <paramref name="seed"/> is returned.
		/// </returns>
		public static Vector Sum<T>(this IEnumerable<T> source, Func<T, Vector> selector, Vector seed = null)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			foreach (var sourceItem in source)
			{
				Vector addedVector = selector(sourceItem);

				if (seed == null)
					seed = addedVector.Clone();
				else
					seed.AddInPlace(addedVector);
			}

			return seed;
		}

		/// <summary>
		/// Computes the sum of the sequence of Vector items 
		/// that are obtained by invoking a transform function on each element 
		/// of the input sequence in parallel.
		/// If the collection is empty, null is returned.
		/// </summary>
		/// <typeparam name="T">The type of the element of the source sequence.</typeparam>
		/// <param name="source">The source sequence.</param>
		/// <param name="selector">
		/// The function that transforms each source sequence element to a Vector.
		/// </param>
		/// <param name="seed">Optional seed vector. If not null, the result will be accumulated on this vector.</param>
		/// <returns>
		/// Returns the sum of the vectors produced by
		/// the transforming function applied to each source element.
		/// If the collection is empty, <paramref name="seed"/> is returned.
		/// </returns>
		public static Vector Sum<T>(this ParallelQuery<T> source, Func<T, Vector> selector, Vector seed = null)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			return source.Aggregate(seed, (accumulator, item) => accumulator != null ? accumulator.AddInPlace(selector(item)) : selector(item).Clone());
		}

		/// <summary>
		/// Compute the sum of a collection of vectors in parallel.
		/// If the collection is empty, null is returned.
		/// </summary>
		/// <param name="source">The vectors to sum.</param>
		/// <param name="seed">Optional seed vector. If not null, the result will be accumulated on this vector.</param>
		/// <returns>Returns the sum of the vectors or <paramref name="seed"/> if the collection is empty.</returns>
		public static Vector Sum(this ParallelQuery<Vector> source, Vector seed = null)
		{
			if (source == null) throw new ArgumentNullException("source");

			return source.Aggregate(seed, (accumulator, added) => accumulator != null ? accumulator.AddInPlace(added) : added.Clone());
		}

		#endregion
	}
}
