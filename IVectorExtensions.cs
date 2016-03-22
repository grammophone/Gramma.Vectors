using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Gramma.Vectors
{
	/// <summary>
	/// Extension methods for <see cref="IVector"/>.
	/// </summary>
	public static class IVectorExtensions
	{
		/// <summary>
		/// Compute the sum of a collection of vectors.
		/// If the collection is empty, null is returned.
		/// </summary>
		/// <param name="vectors">The collection of vectors to be summed.</param>
		/// <param name="seed">Optional seed vector. If not null, the result will be accumulated on this vector.</param>
		/// <returns>Returns <paramref name="seed"/> if the collection is empty, else returns the sum.</returns>
		public static IVector Sum(this IEnumerable<IVector> vectors, IVector seed = null)
		{
			if (vectors == null) throw new ArgumentNullException("vectors");

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
		/// Computes the sum of the sequence of IVector items 
		/// that are obtained by invoking a transform function on each element 
		/// of the input sequence. If the collection is empty, null is returned.
		/// </summary>
		/// <typeparam name="T">The type of the element of the source sequence.</typeparam>
		/// <param name="source">The source sequence.</param>
		/// <param name="selector">
		/// The function that transforms each source sequence element to a SparseVector.
		/// </param>
		/// <param name="seed">Optional seed vector. If not null, the result will be accumulated on this vector.</param>
		/// <returns>
		/// Returns the sum of the vectors produced by
		/// the transforming function applied to each source element.
		/// If the collection is empty, <paramref name="seed"/> is returned.
		/// </returns>
		public static IVector Sum<T>(this IEnumerable<T> source, Func<T, IVector> selector, IVector seed = null)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			foreach (var sourceItem in source)
			{
				var vector = selector(sourceItem);

				if (seed == null)
					seed = vector.Clone();
				else
					seed.AddInPlace(vector);
			}

			return seed;
		}

		/// <summary>
		/// Computes the sum of the sequence of SparseVector items 
		/// that are obtained by invoking a transform function on each element 
		/// of the input sequence in parallel.
		/// Returns null if the collection is empty.
		/// </summary>
		/// <typeparam name="T">The type of the element of the source sequence.</typeparam>
		/// <param name="source">The source sequence.</param>
		/// <param name="selector">
		/// The function that transforms each source sequence element to a SparseVector.
		/// </param>
		/// <param name="seed">Optional seed vector. If not null, the result will be accumulated on this vector.</param>
		/// <returns>
		/// Returns the sum of the vectors produced by
		/// the transforming function applied to each source element,
		/// or <paramref name="seed"/> if the collection is empty.
		/// </returns>
		public static IVector Sum<T>(this ParallelQuery<T> source, Func<T, IVector> selector, IVector seed = null)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			return source.Aggregate(seed, (accumulator, item) => accumulator != null ? accumulator.AddInPlace(selector(item)) : selector(item).Clone());
		}

		/// <summary>
		/// Compute the sum of a collection of vectors in parallel.
		/// The collection must be non-empty.
		/// Returns null if the collection is empty.
		/// </summary>
		/// <param name="source">The vectors to sum.</param>
		/// <param name="seed">Optional seed vector. If not null, the result will be accumulated on this vector.</param>
		/// <returns>Returns the sum of the vectors or <paramref name="seed"/> if the collection is empty.</returns>
		public static IVector Sum(this ParallelQuery<IVector> source, IVector seed = null)
		{
			if (source == null) throw new ArgumentNullException("source");

			return source.Aggregate(seed, (accumulator, added) => accumulator != null ? accumulator.AddInPlace(added) : added.Clone());
		}

	}
}
