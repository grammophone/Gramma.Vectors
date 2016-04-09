using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Grammophone.Vectors
{
	/// <summary>
	/// Extension methods for collections containing <see cref="SparseVector"/> elements.
	/// </summary>
	public static class SparseVectorExtensions
	{
		#region Extension methods

		/// <summary>
		/// Compute the sum of a collection of vectors.
		/// </summary>
		/// <param name="vectors">The vectors to sum.</param>
		/// <param name="seed">Optional seed vector. If not null, the result will be accumulated on this vector.</param>
		/// <returns>Returns the sum of the vectors.</returns>
		public static SparseVector Sum(this IEnumerable<SparseVector> vectors, SparseVector seed = null)
		{
			if (vectors == null) throw new ArgumentNullException("vectors");

			if (seed == null) seed = new SparseVector();

			foreach (var vector in vectors)
			{
				seed.AddInPlace(vector);
			}

			return seed;
		}

		/// <summary>
		/// Computes the sum of the sequence of SparseVector items 
		/// that are obtained by invoking a transform function on each element 
		/// of the input sequence.
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
		/// </returns>
		public static SparseVector Sum<T>(this IEnumerable<T> source, Func<T, SparseVector> selector, SparseVector seed = null)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			if (seed == null) seed = new SparseVector();

			foreach (var sourceItem in source)
			{
				SparseVector addedVector = selector(sourceItem);

				seed.AddInPlace(addedVector);
			}

			return seed;
		}

		/// <summary>
		/// Computes the sum of the sequence of SparseVector items 
		/// that are obtained by invoking a transform function on each element 
		/// of the input sequence in parallel.
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
		/// </returns>
		public static SparseVector Sum<T>(this ParallelQuery<T> source, Func<T, SparseVector> selector, SparseVector seed = null)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (selector == null) throw new ArgumentNullException("selector");

			if (seed == null) seed = new SparseVector();

			return source.Aggregate(
				seed, (accumulator, item) => accumulator != null ? accumulator.AddInPlace(selector(item)) : accumulator = selector(item).Clone());
		}

		/// <summary>
		/// Compute the sum of a collection of vectors in parallel.
		/// </summary>
		/// <param name="source">The vectors to sum.</param>
		/// <param name="seed">Optional seed vector. If not null, the result will be accumulated on this vector.</param>
		/// <returns>Returns the sum of the vectors.</returns>
		public static SparseVector Sum(this ParallelQuery<SparseVector> source, SparseVector seed = null)
		{
			if (source == null) throw new ArgumentNullException("source");

			if (seed == null) seed = new SparseVector();

			return source.Aggregate(
				seed, (accumulator, added) => accumulator != null ? accumulator.AddInPlace(added) : added.Clone());
		}

		#endregion
	}

}
