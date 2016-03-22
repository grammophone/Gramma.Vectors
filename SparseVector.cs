using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Gramma.Vectors
{
	/// <summary>
	/// A vector which is efficient for sparse entries.
	/// </summary>
	/// <remarks>
	/// Designed to be open-ended, there is no explicit dimensionality.
	/// All vectors are compatible in the defined operators, 
	/// the implicit dimensionality of two vectors in an operator is 
	/// extended to be the largest of the two.
	/// </remarks>
	[Serializable]
	[DebuggerTypeProxy(typeof(DebuggerProxies.SparseVectorDebuggerProxy))]
	public class SparseVector : IEnumerable<SparseVector.Entry>, IVector, IDeserializationCallback
	{
		#region Delegates definition

		/// <summary>
		/// A function that represents a matrix by its effect on an <paramref name="input"/>
		/// vector when multiplied by it. It is expected that the process is linear.
		/// </summary>
		/// <param name="input">The input vector.</param>
		/// <returns>
		/// Returns the result of the multiplication of the implied matrix
		/// with the <paramref name="input"/> vector.
		/// </returns>
		public delegate SparseVector Tensor(SparseVector input);

		#endregion

		#region Auxilliary types

		/// <summary>
		/// An entry of the <see cref="SparseVector"/>.
		/// </summary>
		[Serializable]
		[DebuggerDisplay("Index = {Index}, Value = {Value}")]
		public class Entry : IComparable<Entry>
		{
			#region Fields
			
			/// <summary>
			/// The index of the entry.
			/// </summary>
			public int Index { get; internal set; }
			
			/// <summary>
			/// The value of the entry.
			/// </summary>
			public double Value { get; internal set; }

			internal Entry NextEntry;

			#endregion

			#region Construction

      /// <summary>
      /// Create.
      /// </summary>
			public Entry(int index, double value)
			{
				this.Index = index;
				this.Value = value;
			}

			#endregion

			#region IComparable<Entry> Members

			public int CompareTo(Entry other)
			{
				if (this.Index == other.Index) return 0;
				else if (this.Index < other.Index) return -1;
				else return 1;
			}

			#endregion
		}

		internal class Enumerator : IEnumerator<Entry>
		{
			#region Private fields

			private Entry currentEntry;

			private SparseVector vector;

			private bool beforeFirst;

			#endregion

			#region Construction

			public Enumerator(SparseVector vector)
			{
				this.vector = vector;
				this.currentEntry = null;
				this.beforeFirst = true;
			}

			#endregion

			#region IEnumerator<Entry> Members

			public Entry Current
			{
				get 
				{
					if (this.vector == null)
						throw new InvalidOperationException("The enumerator has been disposed.");

					if (this.beforeFirst)
						throw new InvalidOperationException(
							"The enumerator is positioned before the first element");

					if (this.currentEntry == null)
						throw new InvalidOperationException(
							"The enumerator is positioned after the last element");

					return this.currentEntry;
				}
			}

			#endregion

			#region IDisposable Members

			public void Dispose()
			{
				this.vector = null;
				this.currentEntry = null;
			}

			#endregion

			#region IEnumerator Members

			object System.Collections.IEnumerator.Current
			{
				get 
				{
					return this.Current;
				}
			}

			public bool MoveNext()
			{
				if (this.vector == null)
					throw new InvalidOperationException("The enumerator has been disposed.");

				if (this.beforeFirst)
				{
					this.currentEntry = vector.firstEntry;
					this.beforeFirst = false;
				}
				else
				{
					this.currentEntry = this.currentEntry.NextEntry;
				}

				return this.currentEntry != null;
			}

			public void Reset()
			{
				this.beforeFirst = true;
			}

			#endregion
		}

		#endregion

		#region Private fields

		private bool isNorm2Known = false;

		private double norm2 = 0.0;

		[NonSerialized]
		private Dictionary<int, Entry> dictionary = new Dictionary<int, Entry>();

		[NonSerialized]
    private bool isDictionaryBuilt = false;

		private Entry firstEntry = null;

		#endregion

		#region Construction

		/// <summary>
		/// Create a sparse vector by specifying its entries.
		/// The entries must be given in ascending order.
		/// </summary>
		/// <param name="entries">The entries of the vector.</param>
		public SparseVector(IEnumerable<Entry> entries)
		{
			if (entries == null) throw new ArgumentNullException("entries");

			Entry currentEntry = null;

			foreach (var entry in entries)
			{
				if (currentEntry == null)
				{
					firstEntry = entry;
				}
				else
				{
					if (currentEntry.Index >= entry.Index)
						throw new ArgumentException(
							"Entries are not given in ascending Index order.", 
							"entries");

					currentEntry.NextEntry = entry;
				}

				currentEntry = entry;
			}
		}

		/// <summary>
		/// Create a sparse vector by specifying its entries.
		/// The entries must be given in ascending order.
		/// </summary>
		/// <param name="entries">The entries of the vector.</param>
		public SparseVector(Entry[] entries)
		{
			if (entries == null) throw new ArgumentNullException("entries");

			Entry currentEntry = null;

			for (int i = 0; i < entries.Length; i++)
			{
				var entry = entries[i];

				if (currentEntry == null)
				{
					firstEntry = entry;
				}
				else
				{
					if (currentEntry.Index >= entry.Index)
						throw new ArgumentException(
							"Entries are not given in ascending Index order.",
							"entries");

					currentEntry.NextEntry = entry;
				}

				currentEntry = entry;
			}
		}

		/// <summary>
		/// CReate a zero sparse vector.
		/// </summary>
		public SparseVector()
		{
			
		}

		private SparseVector(Entry firstEntry)
		{
			this.firstEntry = firstEntry;

		}

		#endregion

		#region Properties

		/// <summary>
		/// Get the value at an index. The index doesn't have to be
		/// among the vector's entries. Complexity is about O(1) (hashing).
		/// </summary>
		/// <param name="index">The index of the vector's value.</param>
		/// <returns>The vector's value at the index.</returns>
		public double this[int index]
		{
			get
			{
        lock (dictionary)
        {
          if (!this.isDictionaryBuilt) BuildDictionary();
        }

				Entry entry;

				if (!this.dictionary.TryGetValue(index, out entry)) return 0.0;

				return entry.Value;
			}
		}

		/// <summary>
		/// Get the squared Euclidean norm of the vector.
		/// </summary>
		public double Norm2
		{
			get
			{
				if (!this.isNorm2Known)
				{
					double acc = 0.0;
					for (var entry = firstEntry; entry != null; entry = entry.NextEntry)
					{
						double value = entry.Value;
						acc += value * value;
					}

					this.norm2 = acc;
					this.isNorm2Known = true;
				}

				return this.norm2;
			}
		}

		internal Entry FirstEntry
		{
			get
			{
				return firstEntry;
			}
		}

		#endregion

		#region Operators

		/// <summary>
		/// Add two sparse vectors and produce a sparse vector.
		/// </summary>
		public static SparseVector operator+(SparseVector v1, SparseVector v2)
		{
			if (v1 == null) throw new ArgumentNullException("v1");
			if (v2 == null) throw new ArgumentNullException("v2");

			Entry resultRootEntry = null;

			Entry resultEntry = null;

			Entry currentEntry = null;

			for (Entry entry1 = v1.firstEntry, entry2 = v2.firstEntry; entry1 != null || entry2 != null; )
			{
				if (entry1 != null && entry2 != null)
				{
					if (entry1.Index == entry2.Index)
					{
						resultEntry = 
							new Entry(entry1.Index, entry1.Value + entry2.Value);

						entry1 = entry1.NextEntry;
						entry2 = entry2.NextEntry;
					}
					else
					{
						if (entry1.Index < entry2.Index)
						{
							resultEntry =
								new Entry(entry1.Index, entry1.Value);

							entry1 = entry1.NextEntry;
						}
						else
						{
							resultEntry =
								new Entry(entry2.Index, entry2.Value);

							entry2 = entry2.NextEntry;
						}
					}
        }
        else if (entry1 != null)
        {
					resultEntry = new Entry(entry1.Index, entry1.Value);
					entry1 = entry1.NextEntry;
        }
        else
        {
          resultEntry = new Entry(entry2.Index, entry2.Value);
          entry2 = entry2.NextEntry;
        }

				if (currentEntry == null)
				{
					resultRootEntry = resultEntry;
				}
				else
				{
					currentEntry.NextEntry = resultEntry;
				}

				currentEntry = resultEntry;
			}

			return new SparseVector(resultRootEntry);
		}

		/// <summary>
		/// Substract two sparse vectors and produce a sparse vector.
		/// </summary>
		public static SparseVector operator -(SparseVector v1, SparseVector v2)
		{
      if (v1 == null) throw new ArgumentNullException("v1");
      if (v2 == null) throw new ArgumentNullException("v2");

      Entry resultRootEntry = null;

      Entry resultEntry = null;

      Entry currentEntry = null;

      for (Entry entry1 = v1.firstEntry, entry2 = v2.firstEntry; entry1 != null || entry2 != null; )
      {
        if (entry1 != null && entry2 != null)
        {
          if (entry1.Index == entry2.Index)
          {
            resultEntry =
              new Entry(entry1.Index, entry1.Value - entry2.Value);

            entry1 = entry1.NextEntry;
            entry2 = entry2.NextEntry;
          }
          else
          {
            if (entry1.Index < entry2.Index)
            {
              resultEntry =
                new Entry(entry1.Index, entry1.Value);

              entry1 = entry1.NextEntry;
            }
            else
            {
              resultEntry =
                new Entry(entry2.Index, -entry2.Value);

              entry2 = entry2.NextEntry;
            }
          }
        }
        else if (entry1 != null)
        {
					resultEntry = new Entry(entry1.Index, entry1.Value);
					entry1 = entry1.NextEntry;
				}
        else
        {
          resultEntry = new Entry(entry2.Index, -entry2.Value);
          entry2 = entry2.NextEntry;
        }

        if (currentEntry == null)
        {
          resultRootEntry = resultEntry;
        }
        else
        {
          currentEntry.NextEntry = resultEntry;
        }

        currentEntry = resultEntry;
      }

      return new SparseVector(resultRootEntry);
    }

		/// <summary>
		/// Dot product of two sparse vectors.
		/// </summary>
		public static double operator *(SparseVector v1, SparseVector v2)
		{
			if (v1 == null) throw new ArgumentNullException("v1");
			if (v2 == null) throw new ArgumentNullException("v2");

			double result = 0.0;

			for (Entry entry1 = v1.firstEntry, entry2 = v2.firstEntry; entry1 != null && entry2 != null; )
			{
				if (entry1.Index == entry2.Index)
				{
					result += entry1.Value * entry2.Value;

					entry1 = entry1.NextEntry;
					entry2 = entry2.NextEntry;
				}
				else if (entry1.Index < entry2.Index)
				{
					entry1 = entry1.NextEntry;
				}
				else
				{
					entry2 = entry2.NextEntry;
				}
			}

			return result;
		}

		/// <summary>
		/// Dot product of a sparse vector and a dense vector.
		/// </summary>
		/// <param name="v1">The sparse vector.</param>
		/// <param name="v2">The dense vector.</param>
		/// <returns>Returns the inner product.</returns>
		public static double operator *(SparseVector v1, Vector v2)
		{
			if (v1 == null) throw new ArgumentNullException("v1");
			if (v2 == null) throw new ArgumentNullException("v2");

			double sum = 0.0;

			for (Entry entry = v1.FirstEntry; entry != null; entry = entry.NextEntry)
			{
				sum += entry.Value * v2[entry.Index];
			}

			return sum;
		}

		/// <summary>
		/// Dot product of a sparse vector and a dense vector.
		/// </summary>
		/// <param name="v1">The dense vector.</param>
		/// <param name="v2">The sparse vector.</param>
		/// <returns>Returns the inner product.</returns>
		public static double operator *(Vector v1, SparseVector v2)
		{
			return v2 * v1;
		}

		/// <summary>
		/// Inner product of a sparse vector and a generic vector.
		/// </summary>
		/// <param name="v1">The sparse vector.</param>
		/// <param name="v2">the generic vector.</param>
		/// <returns>Returns the inner product value.</returns>
		public static double operator *(SparseVector v1, IVector v2)
		{
			if (v2 == null) throw new ArgumentNullException("v2");

			var v2Sparse = v2 as SparseVector;

			if (v2Sparse != null) return v1 * v2;

			var v2Dense = v2 as Vector;

			if (v2Dense != null) return v1 * v2;

			throw new ArgumentException("vector type of v2 is not supported.", "v2");
		}

		/// <summary>
		/// Inner product of a sparse vector and a generic vector.
		/// </summary>
		/// <param name="v1">the generic vector.</param>
		/// <param name="v2">The sparse vector.</param>
		/// <returns>Returns the inner product value.</returns>
		public static double operator *(IVector v1, SparseVector v2)
		{
			return v2 * v1;
		}

		/// <summary>
		/// Product of a sparse vector by a scalar producing a sparse vector.
		/// </summary>
		public static SparseVector operator *(double a, SparseVector v)
		{
			if (v == null) throw new ArgumentNullException("v");

			if (a == 0.0) return new SparseVector();

			Entry firstEntry = null;

			Entry currentEntry = null;

			for (Entry entry = v.firstEntry; entry != null; entry = entry.NextEntry)
			{
				Entry resultEntry =
					new Entry(entry.Index, a * entry.Value);

				if (currentEntry == null)
					firstEntry = resultEntry;
				else
					currentEntry.NextEntry = resultEntry;

				currentEntry = resultEntry;
			}

			return new SparseVector(firstEntry);
		}

		/// <summary>
		/// Product of a sparse vector by a scalar producing a sparse vector.
		/// </summary>
		public static SparseVector operator *(SparseVector v, double a)
		{
			return a * v;
		}

		/// <summary>
		/// Division of a sparse vector by a scalar producing a sparse vector.
		/// </summary>
		public static SparseVector operator /(SparseVector v, double a)
		{
			if (v == null) throw new ArgumentNullException("v");

			if (a == 0.0) throw new DivideByZeroException();

			Entry firstEntry = null;

			Entry currentEntry = null;

			for (Entry entry = v.firstEntry; entry != null; entry = entry.NextEntry)
			{
				Entry resultEntry =
					new Entry(entry.Index, entry.Value / a);

				if (currentEntry == null)
					firstEntry = resultEntry;
				else
					currentEntry.NextEntry = resultEntry;

				currentEntry = resultEntry;
			}

			return new SparseVector(firstEntry);
		}

    /// <summary>
    /// Unary negation of a vector.
    /// </summary>
    public static SparseVector operator -(SparseVector v)
    {
      if (v == null) throw new ArgumentNullException("v");

      Entry firstEntry = null;

      Entry currentEntry = null;

      for (Entry entry = v.firstEntry; entry != null; entry = entry.NextEntry)
      {
        Entry resultEntry =
          new Entry(entry.Index, -entry.Value);

        if (currentEntry == null)
          firstEntry = resultEntry;
        else
          currentEntry.NextEntry = resultEntry;

        currentEntry = resultEntry;
      }

      return new SparseVector(firstEntry);
    }

    #endregion

		#region Public methods

		/// <summary>
		/// In-place addition of sparse vector.
		/// </summary>
		/// <param name="v2">The added vector. Null is treated like zero.</param>
		/// <returns>Returns the vector itself, altered.</returns>
		public SparseVector AddInPlace(SparseVector v2)
		{
			if (v2 == null) return this;

			Entry insertedEntry = null;

			Entry currentEntry = null;

			lock (this.dictionary)
			{
				isNorm2Known = false;

				if (this.isDictionaryBuilt)
				{
					this.isDictionaryBuilt = false;
					this.dictionary.Clear();
				}

				for (Entry entry1 = this.firstEntry, entry2 = v2.firstEntry; entry1 != null || entry2 != null; )
				{
					if (entry1 != null && entry2 != null)
					{
						if (entry1.Index == entry2.Index)
						{
							entry1.Value += entry2.Value;

							currentEntry = entry1;

							entry1 = entry1.NextEntry;
							entry2 = entry2.NextEntry;

							continue;
						}
						else
						{
							if (entry1.Index < entry2.Index)
							{
								currentEntry = entry1;

								entry1 = entry1.NextEntry;

								continue;
							}
							else
							{
								insertedEntry =
									new Entry(entry2.Index, entry2.Value);

								insertedEntry.NextEntry = entry1;

								entry2 = entry2.NextEntry;
							}
						}
					}
					else if (entry1 != null)
					{
						break;
					}
					else
					{
						insertedEntry = new Entry(entry2.Index, entry2.Value);

						entry2 = entry2.NextEntry;
					}

					if (currentEntry == null)
					{
						this.firstEntry = insertedEntry;
					}
					else
					{
						currentEntry.NextEntry = insertedEntry;
					}

					currentEntry = insertedEntry;
				}

				return this;
			}
		}

		/// <summary>
		/// In-place subtraction of sparse vector.
		/// </summary>
		/// <param name="v2">The subtracted vector. Null is treaded like zero.</param>
		/// <returns>Returns the vector itself, altered.</returns>
		public SparseVector SubtractInPlace(SparseVector v2)
		{
			if (v2 == null) return this;

			Entry insertedEntry = null;

			Entry currentEntry = null;

			lock (this.dictionary)
			{
				isNorm2Known = false;

				if (this.isDictionaryBuilt)
				{
					this.isDictionaryBuilt = false;
					this.dictionary.Clear();
				}

				for (Entry entry1 = this.firstEntry, entry2 = v2.firstEntry; entry1 != null || entry2 != null; )
				{
					if (entry1 != null && entry2 != null)
					{
						if (entry1.Index == entry2.Index)
						{
							entry1.Value -= entry2.Value;

							currentEntry = entry1;

							entry1 = entry1.NextEntry;
							entry2 = entry2.NextEntry;

							continue;
						}
						else
						{
							if (entry1.Index < entry2.Index)
							{
								currentEntry = entry1;

								entry1 = entry1.NextEntry;

								continue;
							}
							else
							{
								insertedEntry =
									new Entry(entry2.Index, -entry2.Value);

								insertedEntry.NextEntry = entry1;

								entry2 = entry2.NextEntry;
							}
						}
					}
					else if (entry1 != null)
					{
						break;
					}
					else
					{
						insertedEntry = new Entry(entry2.Index, -entry2.Value);

						entry2 = entry2.NextEntry;
					}

					if (currentEntry == null)
					{
						this.firstEntry = insertedEntry;
					}
					else
					{
						currentEntry.NextEntry = insertedEntry;
					}

					currentEntry = insertedEntry;
				}

				return this;
			}
		}

		/// <summary>
		/// In-place scalar multiplication.
		/// </summary>
		/// <param name="coefficient">The scaling coefficient.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		public SparseVector ScaleInPlace(double coefficient)
		{
			lock (this.dictionary)
			{
				isNorm2Known = false;

				if (this.isDictionaryBuilt)
				{
					this.isDictionaryBuilt = false;
					this.dictionary.Clear();
				}

				if (coefficient == 0.0)
				{
					this.firstEntry = null;
					return this;
				}

				for (var entry = this.firstEntry; entry != null; entry = entry.NextEntry)
				{
					entry.Value *= coefficient;
				}
			}

			return this;
		}

		/// <summary>
		/// In-place scalar division.
		/// </summary>
		/// <param name="coefficient">The division coefficient.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		public SparseVector DivideInPlace(double coefficient)
		{
			if (coefficient == 0.0) throw new ArgumentException("coefficient must not be zero.", "coefficient");

			lock (this.dictionary)
			{
				isNorm2Known = false;

				if (this.isDictionaryBuilt)
				{
					this.isDictionaryBuilt = false;
					this.dictionary.Clear();
				}

				for (var entry = this.firstEntry; entry != null; entry = entry.NextEntry)
				{
					entry.Value /= coefficient;
				}
			}

			return this;
		}

		/// <summary>
		/// In-place negation.
		/// </summary>
		/// <returns>Returns the vetor itself, altered.</returns>
		public SparseVector NegateInPlace()
		{
			lock (this.dictionary)
			{
				if (this.isDictionaryBuilt)
				{
					this.isDictionaryBuilt = false;
					this.dictionary.Clear();
				}

				for (var entry = this.firstEntry; entry != null; entry = entry.NextEntry)
				{
					entry.Value = -entry.Value;
				}
			}

			return this;
		}

		/// <summary>
		/// In-place vector addition.
		/// </summary>
		/// <param name="summand">If null, it is treated like zero.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		public SparseVector AddInPlace(Vector summand)
		{
			if (summand == null) return this;

			Debug.WriteLine("In-place adding a dense vector to a sparse vector.");

			lock (this.dictionary)
			{
				isNorm2Known = false;

				int lastIndex = 0;

				var currentEntry = this.firstEntry;

				if (this.isDictionaryBuilt)
				{
					this.isDictionaryBuilt = false;
					this.dictionary.Clear();
				}

				for (var entry = this.firstEntry; entry != null; entry = entry.NextEntry)
				{
					for (int i = lastIndex; i < entry.Index; i++)
					{
						var insertedEntry = new Entry(i, summand[i]);

						if (currentEntry == null)
						{
							firstEntry = insertedEntry;
						}
						else
						{
							currentEntry.NextEntry = insertedEntry;
						}

						currentEntry = insertedEntry;
					}

					entry.Value += summand[entry.Index];

					lastIndex = entry.Index + 1;

					if (currentEntry != null)
					{
						currentEntry.NextEntry = entry;
					}

					currentEntry = entry;
				}

				for (int i = lastIndex; i < summand.Length; i++)
				{
					var insertedEntry = new Entry(i, summand[i]);

					if (currentEntry == null)
					{
						this.firstEntry = insertedEntry;
					}
					else
					{
						currentEntry.NextEntry = insertedEntry;
					}

					currentEntry = insertedEntry;
				}

			}

			return this;
		}

		/// <summary>
		/// In-place vector addition.
		/// </summary>
		/// <param name="summand">If null, it is treated like zero.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		SparseVector AddInPlace(IVector summand)
		{
			if (summand == null) return this;

			var sparseSummand = summand as SparseVector;

			if (sparseSummand != null) return AddInPlace(sparseSummand);

			var denseSummand = summand as Vector;

			if (denseSummand != null) return AddInPlace(denseSummand);

			throw new ArgumentException("summand is not a supported vector type.", "summand");
		}

		/// <summary>
		/// In-place vector subtraction.
		/// </summary>
		/// <param name="subtracted">If null, it is treated like zero.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		public SparseVector SubtractInPlace(Vector subtracted)
		{
			if (subtracted == null) return this;

			Debug.WriteLine("In-place subtracting a dense vector from a sparse vector.");

			lock (this.dictionary)
			{
				int lastIndex = 0;

				var currentEntry = this.firstEntry;

				if (this.isDictionaryBuilt)
				{
					this.isDictionaryBuilt = false;
					this.dictionary.Clear();
				}

				for (var entry = this.firstEntry; entry != null; entry = entry.NextEntry)
				{
					for (int i = lastIndex; i < entry.Index; i++)
					{
						var insertedEntry = new Entry(i, -subtracted[i]);

						if (currentEntry == null)
						{
							firstEntry = insertedEntry;
						}
						else
						{
							currentEntry.NextEntry = insertedEntry;
						}

						currentEntry = insertedEntry;
					}

					entry.Value -= subtracted[entry.Index];

					lastIndex = entry.Index + 1;

					if (currentEntry != null)
					{
						currentEntry.NextEntry = entry;
					}

					currentEntry = entry;
				}

				for (int i = lastIndex; i < subtracted.Length; i++)
				{
					var insertedEntry = new Entry(i, -subtracted[i]);

					if (currentEntry == null)
					{
						this.firstEntry = insertedEntry;
					}
					else
					{
						currentEntry.NextEntry = insertedEntry;
					}

					currentEntry = insertedEntry;
				}

			}

			return this;
		}

		/// <summary>
		/// In-place vector subtraction.
		/// </summary>
		/// <param name="subtracted">If null, it is treated like zero.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		public SparseVector SubtractInPlace(IVector subtracted)
		{
			if (subtracted == null) return this;

			var sparseSubtracted = subtracted as SparseVector;

			if (sparseSubtracted != null) return SubtractInPlace(sparseSubtracted);

			var denseSubtracted = subtracted as Vector;

			if (denseSubtracted != null) return SubtractInPlace(denseSubtracted);

			throw new ArgumentException("subtracted is not a supported vector type.", "subtracted");
		}

		/// <summary>
		/// Create an exact copy of the vector.
		/// </summary>
		/// <returns>Returns a copy of the vector.</returns>
		public SparseVector Clone()
		{
			return new SparseVector(this);
		}

		#endregion

		#region IEnumerable<Entry> Members

		public IEnumerator<SparseVector.Entry> GetEnumerator()
		{
			return new Enumerator(this);
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return new Enumerator(this);
		}

		#endregion

    #region Private methods

    private void BuildDictionary()
    {
      for (var entry = this.firstEntry; entry != null; entry = entry.NextEntry)
      {
        this.dictionary[entry.Index] = entry;
      }

      this.isDictionaryBuilt = true;
    }

    #endregion

		#region IVector Members

		IVector IVector.Add(IVector summand)
		{
			if (summand == null) return new SparseVector(this);

			var sparseSummand = summand as SparseVector;

			if (sparseSummand != null) return this + sparseSummand;

			var denseSummand = summand as Vector;

			if (denseSummand != null) return this + denseSummand;

			throw new ArgumentException("summand is not a supported vector type.", "summand");
		}

		IVector IVector.Subtract(IVector subtracted)
		{
			if (subtracted == null) return new SparseVector(this);

			var sparseSubtracted = subtracted as SparseVector;

			if (sparseSubtracted != null) return this - sparseSubtracted;

			var denseSubtracted = subtracted as Vector;

			if (denseSubtracted != null) return this - denseSubtracted;

			throw new ArgumentException("subtracted is not a supported vector type.", "subtracted");
		}

		IVector IVector.Scale(double coefficient)
		{
			return coefficient * this;
		}

		IVector IVector.Divide(double coefficient)
		{
			return this / coefficient;
		}

		double IVector.InnerProduct(IVector multiplicand)
		{
			if (multiplicand == null) return 0.0;

			var sparseMultiplicant = multiplicand as SparseVector;

			if (sparseMultiplicant != null) return this * sparseMultiplicant;

			var denseMultiplicant = multiplicand as Vector;

			if (denseMultiplicant != null) return this * denseMultiplicant;

			throw new ArgumentException("multiplicant is not a supported vector type.", "multiplicant");
		}

		IVector IVector.Negate()
		{
			return -this;
		}

		IVector IVector.AddInPlace(IVector summand)
		{
			return AddInPlace(summand);
		}

		IVector IVector.SubtractInPlace(IVector subtracted)
		{
			return SubtractInPlace(subtracted);
		}

		IVector IVector.ScaleInPlace(double coefficient)
		{
			return ScaleInPlace(coefficient);
		}

		IVector IVector.DivideInPlace(double coefficient)
		{
			return DivideInPlace(coefficient);
		}

		IVector IVector.NegateInPlace()
		{
			return NegateInPlace();
		}

		IVector IVector.Clone()
		{
			return this.Clone();
		}

		#endregion

		#region IDeserializationCallback Members

		void IDeserializationCallback.OnDeserialization(object sender)
		{
			this.dictionary = new Dictionary<int, Entry>();
		}

		#endregion
	}

}
