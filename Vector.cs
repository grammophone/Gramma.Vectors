using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Threading;
using System.Globalization;
using System.Configuration;

namespace Grammophone.Vectors
{
	/// <summary>
	/// Algebraic vector with elements of type double.
	/// </summary>
	/// <remarks>
	/// Supports enumeration of elements as well as direct random access.
	/// Settings are optionally defined in application configuration section "vectorsConfiguration"
	/// which is described in <see cref="VectorsConfigurationSection"/>.
	/// </remarks>
	[Serializable]
	[DebuggerDisplay("Length = {Length}")]
	[DebuggerTypeProxy(typeof(DebuggerProxies.VectorDebuggerProxy))]
	public class Vector : IEnumerable<double>, ISerializable, IVector
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
		public delegate Vector Tensor(Vector input);

		#endregion

		#region Private fields

		internal double[] array;

		private bool isNorm2Known;

		private double norm2;

		private static int parallelismDimensionThreshold = 35768;

		private static int sqrtParallelismDimensionThreshold = 64;
		
		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="length">
		/// The number of elements of the vector.
		/// </param>
		public Vector(int length)
		{
			if (length < 0) throw new ArgumentException("Size must not be negative.", "length");

			this.array = new double[length];
			this.isNorm2Known = false;
		}

		/// <summary>
		/// Create from sparse vector.
		/// </summary>
		/// <param name="length">The length of the dense vector to be created.</param>
		/// <param name="sparseVector">The sparse vector used to initialize the dense vector.</param>
		public Vector(int length, SparseVector sparseVector)
			: this(length)
		{
			if (sparseVector == null) throw new ArgumentNullException("sparseVector");

			for (var entry = sparseVector.FirstEntry; entry != null; entry = entry.NextEntry)
			{
				this.array[entry.Index] = entry.Value;
			}
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="source">The source sequence to copy elements from.</param>
		public Vector(IEnumerable<double> source)
		{
			if (source == null) throw new ArgumentNullException("source");

			this.array = source.ToArray();
			this.isNorm2Known = false;
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="source">The source sequence to copy elements from.</param>
		public Vector(ICollection<double> source)
		{
			if (source == null) throw new ArgumentNullException("source");

			this.array = source.ToArray();
			this.isNorm2Known = false;
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="source">The source array to copy elements from.</param>
		public Vector(double[] source)
		{
			if (source == null) throw new ArgumentNullException("source");

			this.array = new double[source.Length];
			source.CopyTo(this.array, 0);
			
			this.isNorm2Known = false;
		}

		/// <summary>
		/// Constructor for serialization.
		/// </summary>
		protected Vector(SerializationInfo info, StreamingContext context)
		{
			if (info == null) throw new ArgumentNullException("info");

			this.isNorm2Known = false;

			this.array = (double[])info.GetValue("array", typeof(double[]));
		}

		/// <summary>
		/// Read application configuration section "vectorsConfiguration"
		/// to set <see cref="ParallelismDimensionThreshold"/>.
		/// </summary>
		static Vector()
		{
			var section = ConfigurationManager.GetSection("vectorsConfiguration") as VectorsConfigurationSection;

			if (section != null)
			{
				ParallelismDimensionThreshold = section.ParallelismDimensionThreshold;
			}

		}

		#endregion

		#region Public properties

		/// <summary>
		/// Represents the identity matrix.
		/// </summary>
		public static Tensor IdentityTensor
		{
			get
			{
				return v => v;
			}
		}

		/// <summary>
		/// Represents the zero matrix.
		/// </summary>
		public static Tensor ZeroTensor
		{
			get
			{
				return v => new Vector(v.Length);
			}
		}

		/// <summary>
		/// Specifies the minimum dimension size where the various
		/// operations are executed in parallel. Default is 32768.
		/// </summary>
		/// <remarks>
		/// This is typically used as a dimension thresold for vector operations parallelization.
		/// </remarks>
		public static int ParallelismDimensionThreshold
		{
			get
			{
				return parallelismDimensionThreshold;
			}
			set
			{
				parallelismDimensionThreshold = value;
				
				sqrtParallelismDimensionThreshold = 
					(int)Math.Ceiling(Math.Sqrt(parallelismDimensionThreshold));
			}
		}

		/// <summary>
		/// Returns an approximate sqare root of <see cref="ParallelismDimensionThreshold"/>.
		/// </summary>
		/// <remarks>
		/// This is typically used as a dimension thresold for matrix/tensor operations parallelization.
		/// </remarks>
		public static int SqrtParallelismDimensionThreshold
		{
			get
			{
				return sqrtParallelismDimensionThreshold;
			}
		}

		/// <summary>
		/// Access an element's value by index.
		/// </summary>
		/// <param name="index">Zero-based index.</param>
		public double this[int index]
		{
			get
			{
				return this.array[index];
			}
			set
			{
				this.array[index] = value;
				this.isNorm2Known = false;
			}
		}

		/// <summary>
		/// The number of elements of the vector.
		/// </summary>
		public int Length
		{
			get
			{
				return this.array.Length;
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
					this.norm2 = this * this;
					this.isNorm2Known = true;
				}

				return this.norm2;
			}
		}

		#endregion

		#region Operators

		/// <summary>
		/// Implicitly convert an array of double precision numbers to
		/// a Vector.
		/// </summary>
		public static implicit operator Vector(double[] array)
		{
			return new Vector(array);
		}

		/// <summary>
		/// Inner product.
		/// </summary>
		public static double operator *(Vector v1, Vector v2)
		{
			EnsureCompatibleVectors(v1, v2);

			double inner = 0.0;

			int count = v1.Length;

			unsafe
			{
				if (count < ParallelismDimensionThreshold)
				{
					fixed (double* pv1 = v1.array, pv2 = v2.array)
					{
						double* pv1i;
						double* pv2i;
						int i;
						for (i = 0, pv1i = pv1, pv2i = pv2; i < count; i++, pv1i++, pv2i++)
						{
							inner += (*pv1i) * (*pv2i);
						}
					}
				}
				else
				{
					SpinLock spinLock = new SpinLock();

					Parallel.ForEach(
						Partitioner.Create(0, count),
						() => 0.0,
						(range, loopState, accumulator) =>
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* pv1 = v1.array, pv2 = v2.array)
							{
								double* pv1i = &pv1[startIndex];
								double* pv2i = &pv2[startIndex];

								int i;

								for (
									i = startIndex, pv1i = &pv1[startIndex], pv2i = &pv2[startIndex]; 
									i < endIndex; 
									i++, pv1i++, pv2i++)
								{
									accumulator += (*pv1i) * (*pv2i);
								}
							}

							return accumulator;
						},
						localSum =>
						{
							bool lockAcquired = false;
							spinLock.Enter(ref lockAcquired);

							inner += localSum;

							if (lockAcquired) spinLock.Exit();
						}
					);

				}

			}

			return inner;
		}

		/// <summary>
		/// Inner product of a dense vector and a generic vector.
		/// </summary>
		/// <param name="v1">The dense vector.</param>
		/// <param name="v2">the generic vector.</param>
		/// <returns>Returns the inner product value.</returns>
		public static double operator *(Vector v1, IVector v2)
		{
			if (v2 == null) throw new ArgumentNullException("v2");

			var v2Dense = v2 as Vector;

			if (v2Dense != null) return v1 * v2Dense;

			var v2Sparse = v2 as SparseVector;

			if (v2Sparse != null) return v1 * v2Sparse;

			throw new ArgumentException("vector type of v2 is not supported.", "v2");
		}

		/// <summary>
		/// Inner product of a dense vector and a generic vector.
		/// </summary>
		/// <param name="v1">the generic vector.</param>
		/// <param name="v2">The dense vector.</param>
		/// <returns>Returns the inner product value.</returns>
		public static double operator *(IVector v1, Vector v2)
		{
			return v2 * v1;
		}

		/// <summary>
		/// Outer product between vectors v1 T * v2.
		/// </summary>
		public static Tensor operator %(Vector v1, Vector v2)
		{
			return GetTensor((i, j) => v1[i] * v2[j]);
		}

		/// <summary>
		/// Vector addition.
		/// </summary>
		public static Vector operator +(Vector v1, Vector v2)
		{
			EnsureCompatibleVectors(v1, v2);

			int count = v1.Length;

			var v = new Vector(count);

			unsafe
			{
				if (count < ParallelismDimensionThreshold)
				{
					fixed (double * pv = v.array, pv1 = v1.array, pv2 = v2.array)
					{
						double *pv1i;
						double *pv2i;
						double *pvi;
						int i;

						for (i = 0, pvi = pv, pv1i = pv1, pv2i = pv2; i < count; i++, pvi++, pv1i++, pv2i++)
						{
							*pvi = *pv1i + *pv2i;
						}
					}
				}
				else
				{
					var partitioner = Partitioner.Create(0, count);

					Parallel.ForEach(
						partitioner,
						range => 
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* pv = v.array, pv1 = v1.array, pv2 = v2.array)
							{
								double* pv1i;
								double* pv2i;
								double* pvi;
								int i;

								for (
									i = startIndex, pvi = &pv[startIndex], pv1i = &pv1[startIndex], pv2i = &pv2[startIndex]; 
									i < endIndex; 
									i++, pvi++, pv1i++, pv2i++)
								{
									*pvi = *pv1i + *pv2i;
								}
							}
						}
					);
				}
			}

			return v;
		}

		/// <summary>
		/// Add a dense vector to a sparse vector and produce a dense vector sum.
		/// </summary>
		/// <param name="v1">The sparse vector to add.</param>
		/// <param name="v2">The dense vector to add.</param>
		/// <returns>Returns the dense sum vector.</returns>
		public static Vector operator +(SparseVector v1, Vector v2)
		{
			if (v1 == null) throw new ArgumentNullException("v1");
			if (v2 == null) throw new ArgumentNullException("v2");

			var sum = v2.Clone();

			for (var entry = v1.FirstEntry; entry != null; entry = entry.NextEntry)
			{
				sum[entry.Index] += entry.Value;
			}

			return sum;
		}

		/// <summary>
		/// Add a dense vector to a sparse vector and produce a dense vector sum.
		/// </summary>
		/// <param name="v1">The dense vector to add.</param>
		/// <param name="v2">The sparse vector to add.</param>
		/// <returns>Returns the dense sum vector.</returns>
		public static Vector operator +(Vector v1, SparseVector v2)
		{
			return v2 + v1;
		}

		/// <summary>
		/// Add a generic vector to a dense vector.
		/// </summary>
		/// <param name="v1">The generic vector.</param>
		/// <param name="v2">The sparse vector.</param>
		/// <returns>Returns the sum as a dense vector.</returns>
		public static Vector operator +(Vector v1, IVector v2)
		{
			if (v2 == null) throw new ArgumentNullException("v2");

			var v2Dense = v2 as Vector;

			if (v2Dense != null) return v1 + v2Dense;

			var v2Sparse = v2 as SparseVector;

			if (v2Sparse != null) return v1 + v2Sparse;

			throw new ArgumentException("vector type of v2 is not supported.", "v2");
		}

		/// <summary>
		/// Add a generic vector to a dense vector.
		/// </summary>
		/// <param name="v1">The generic vector.</param>
		/// <param name="v2">The dense vector.</param>
		/// <returns>Returns the sum as a dense vector.</returns>
		public static Vector operator +(IVector v1, Vector v2)
		{
			return v2 + v1;
		}

		/// <summary>
		/// Vector subtraction.
		/// </summary>
		public static Vector operator -(Vector v1, Vector v2)
		{
			EnsureCompatibleVectors(v1, v2);

			int count = v1.Length;

			var v = new Vector(count);

			unsafe
			{
				if (count < ParallelismDimensionThreshold)
				{
					fixed (double* pv = v.array, pv1 = v1.array, pv2 = v2.array)
					{
						double* pv1i;
						double* pv2i;
						double* pvi;
						int i;

						for (i = 0, pvi = pv, pv1i = pv1, pv2i = pv2; i < count; i++, pvi++, pv1i++, pv2i++)
						{
							*pvi = *pv1i - *pv2i;
						}
					}
				}
				else
				{
					var partitioner = Partitioner.Create(0, count);

					Parallel.ForEach(
						partitioner,
						range =>
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* pv = v.array, pv1 = v1.array, pv2 = v2.array)
							{
								double* pv1i;
								double* pv2i;
								double* pvi;
								int i;

								for (
									i = startIndex, pvi = &pv[startIndex], pv1i = &pv1[startIndex], pv2i = &pv2[startIndex];
									i < endIndex;
									i++, pvi++, pv1i++, pv2i++)
								{
									*pvi = *pv1i - *pv2i;
								}
							}
						}
					);
				}
			}

			return v;
		}

		/// <summary>
		/// Subtract a sparse vector from a dense vector and produce a dense vector difference.
		/// </summary>
		/// <param name="v1">The dense vector.</param>
		/// <param name="v2">The subtracted sparse vector.</param>
		/// <returns>Returns the dense difference vector.</returns>
		public static Vector operator -(Vector v1, SparseVector v2)
		{
			if (v1 == null) throw new ArgumentNullException("v1");
			if (v2 == null) throw new ArgumentNullException("v2");

			var difference = v1.Clone();

			for (var entry = v2.FirstEntry; entry != null; entry = entry.NextEntry)
			{
				difference[entry.Index] -= entry.Value;
			}

			return difference;
		}

		/// <summary>
		/// Subtract a dense vector from a sparse vector.
		/// </summary>
		/// <param name="v1">The sparse vector.</param>
		/// <param name="v2">The dense vector to subtract.</param>
		/// <returns>Returns the dense difference vector.</returns>
		public static Vector operator -(SparseVector v1, Vector v2)
		{
			if (v2 == null) throw new ArgumentNullException("v2");

			return v1 + (-v2);
		}

		/// <summary>
		/// Subtract a generic vector from a dense vector.
		/// </summary>
		/// <param name="v1">The dense vector.</param>
		/// <param name="v2">The generic vector.</param>
		/// <returns>Returns the difference as a dense vector.</returns>
		public static Vector operator -(Vector v1, IVector v2)
		{
			if (v2 == null) throw new ArgumentNullException("v2");

			var v2Dense = v2 as Vector;

			if (v2Dense != null) return v1 - v2Dense;

			var v2Sparse = v2 as SparseVector;

			if (v2Sparse != null) return v1 - v2Sparse;

			throw new ArgumentException("vector type of v2 is not supported.", "v2");
		}

		/// <summary>
		/// Subtract a dense vector from a generic vector.
		/// </summary>
		/// <param name="v1">The generic vector.</param>
		/// <param name="v2">The dense vector.</param>
		/// <returns>Returns the difference as a dense vector.</returns>
		public static Vector operator -(IVector v1, Vector v2)
		{
			if (v1 == null) throw new ArgumentNullException("v1");

			var v1Dense = v1 as Vector;

			if (v1Dense != null) return v1Dense - v2;

			var v1Sparse = v1 as SparseVector;

			if (v1Sparse != null) return v1Sparse - v2;

			throw new ArgumentException("v1 vector type is not supported.", "v1");
		}

		/// <summary>
		/// Scalar multiplication.
		/// </summary>
		public static Vector operator *(double k, Vector v1)
		{
			if (v1 == null) throw new ArgumentNullException("v1");

			int count = v1.Length;

			var v = new Vector(count);

			unsafe
			{
				if (count < ParallelismDimensionThreshold)
				{
					fixed (double* pv = v.array, pv1 = v1.array)
					{
						int i;
						double* pvi;
						double* pv1i;

						for (i = 0, pvi = pv, pv1i = pv1; i < count; i++, pvi++, pv1i++)
						{
							*pvi = k * (*pv1i);
						}
					}
				}
				else
				{
					var partitioner = Partitioner.Create(0, count);

					Parallel.ForEach(
						partitioner,
						range =>
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* pv = v.array, pv1 = v1.array)
							{
								int i;
								double* pvi;
								double* pv1i;

								for (
									i = startIndex, pvi = &pv[startIndex], pv1i = &pv1[startIndex]; 
									i < endIndex; 
									i++, pvi++, pv1i++)
								{
									*pvi = k * (*pv1i);
								}
							}
						}
					);
				}
			}

			return v;
		}

		/// <summary>
		/// Scalar multiplication.
		/// </summary>
		public static Vector operator *(Vector v1, double k)
		{
			return k * v1;
		}

		/// <summary>
		/// Scalar division.
		/// </summary>
		public static Vector operator /(Vector v1, double k)
		{
			if (k == 0.0) throw new ArgumentException("Scalar k must not be zero.", "k");
			if (v1 == null) throw new ArgumentNullException("v1");

			int count = v1.Length;

			var v = new Vector(count);

			unsafe
			{
				if (count < ParallelismDimensionThreshold)
				{
					fixed (double* pv = v.array, pv1 = v1.array)
					{
						int i;
						double* pvi;
						double* pv1i;

						for (i = 0, pvi = pv, pv1i = pv1; i < count; i++, pvi++, pv1i++)
						{
							*pvi = (*pv1i) / k;
						}
					}
				}
				else
				{
					var partitioner = Partitioner.Create(0, count);

					Parallel.ForEach(
						partitioner,
						range =>
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* pv = v.array, pv1 = v1.array)
							{
								int i;
								double* pvi;
								double* pv1i;

								for (
									i = startIndex, pvi = &pv[startIndex], pv1i = &pv1[startIndex];
									i < endIndex;
									i++, pvi++, pv1i++)
								{
									*pvi = (*pv1i) / k;
								}
							}
						}
					);
				}
			}

			return v;
		}

		/// <summary>
		/// Negation.
		/// </summary>
		public static Vector operator -(Vector v1)
		{
			if (v1 == null) throw new ArgumentNullException("v1");

			int count = v1.Length;

			var v = new Vector(count);

			unsafe
			{
				if (count < ParallelismDimensionThreshold)
				{
					fixed (double* pv = v.array, pv1 = v1.array)
					{
						int i;
						double* pvi;
						double* pv1i;

						for (i = 0, pvi = pv, pv1i = pv1; i < count; i++, pvi++, pv1i++)
						{
							*pvi = -*pv1i;
						}
					}
				}
				else
				{
					var partitioner = Partitioner.Create(0, count);

					Parallel.ForEach(
						partitioner,
						range => 
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* pv = v.array, pv1 = v1.array)
							{
								int i;
								double* pvi;
								double* pv1i;

								for (
									i = startIndex, pvi = &pv[startIndex], pv1i = &pv1[startIndex]; 
									i < endIndex; 
									i++, pvi++, pv1i++)
								{
									*pvi = -*pv1i;
								}
							}
						}
					);
				}
			}

			return v;
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Get the underlying array of double floating point numbers
		/// supporting this vector. 
		/// </summary>
		/// <remarks>
		/// This is offered for fast direct access by C++ or 'unsafe' C#
		/// in order to serve critical performance scenarios.
		/// </remarks>
		public double[] GetArray()
		{
			return this.array;
		}

		/// <summary>
		/// Create a diagonal tensor having as elements the given diagonal.
		/// </summary>
		/// <param name="diagonal">The diagonal elements.</param>
		public static Tensor GetDiagonalTensor(Vector diagonal)
		{
			if (diagonal == null) throw new ArgumentNullException("diagonal");

			return delegate(Vector x)
			{
				if (x == null) throw new ArgumentNullException("x");

				int count = diagonal.Length;

				if (x.Length != count)
					throw new ArgumentException("Vector length is not compatible to tensor.", "x");

				var y = new Vector(count);

				unsafe
				{
					if (count < ParallelismDimensionThreshold)
					{
						fixed (double* px = x.array, py = y.array, pdiagonal = diagonal.array)
						{
							int i;
							double* pxi, pyi, pdiagonali;

							for (
								i = 0, pxi = px, pyi = py, pdiagonali = pdiagonal;
								i < count;
								i++, pxi++, pyi++, pdiagonali++)
							{
								*pyi = (*pxi) * (*pdiagonali);
							}
						}
					}
					else
					{
						var partitioner = Partitioner.Create(0, count);

						Parallel.ForEach(partitioner, range =>
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* px = x.array, py = y.array, pdiagonal = diagonal.array)
							{
								int i;
								double* pxi, pyi, pdiagonali;

								for (
									i = startIndex, pxi = &px[startIndex], pyi = &py[startIndex], pdiagonali = &pdiagonal[startIndex];
									i < endIndex;
									i++, pxi++, pyi++, pdiagonali++)
								{
									*pyi = (*pxi) * (*pdiagonali);
								}
							}
						});
					}
				}

				return y;
			};
		}

		/// <summary>
		/// Get a tensor by specifying each matrix element in functional form.
		/// </summary>
		/// <param name="elementFunction">
		/// A function with integer arguments i, j as the zero-based row and column indices
		/// respectively, and returning double. It represents a square matrix compatible
		/// with the vector that will be given to the resulting tensor.
		/// </param>
		/// <returns>Returns the Tensor as specified.</returns>
		/// <remarks>
		/// The action of the tensor is parallelized when the dimensions of the applied vector
		/// exceed the square root of <see cref="ParallelismDimensionThreshold"/>.
		/// </remarks>
		public static Tensor GetTensor(Func<int, int, double> elementFunction)
		{
			//return delegate(Vector x)
			//{
			//  if (x == null) throw new ArgumentNullException("x");

			//  Vector y;

			//  var range = Enumerable.Range(0, x.Length);

			//  if (x.Length < SqrtParallelismDimensionThreshold)
			//  {
			//    y = range.Select(i => range.Sum(j => elementFunction(i, j) * x[j]));
			//  }
			//  else
			//  {
			//    y = range.AsParallel().Select(i => range.Sum(j => elementFunction(i, j) * x[j]));
			//  }

			//  return y;

			//};

			return delegate(Vector x)
			{
				if (x == null) throw new ArgumentNullException("x");

				int xCount = x.Length;

				int rowsCount = xCount;

				Vector y = new Vector(rowsCount);

				unsafe
				{
					if (rowsCount * xCount < ParallelismDimensionThreshold)
					{
						fixed (double* px = x.array, py = y.array)
						{
							int i, j;
							double* pxi;
							double* pyi;
							double acc;

							for (i = 0, pyi = py; i < rowsCount; i++, pyi++)
							{
								for (j = 0, pxi = px, acc = 0.0; j < xCount; j++, pxi++)
								{
									acc += elementFunction(i, j) * (*pxi);
								}

								*pyi = acc;
							}

						}
					}
					else
					{
						var rowsPartitioner = Partitioner.Create(0, rowsCount);

						Parallel.ForEach(
							rowsPartitioner,
							range =>
							{
								int startRowIndex = range.Item1;
								int endRowIndex = range.Item2;

								fixed (double* px = x.array, py = y.array)
								{
									int i, j;
									double* pxi;
									double* pyi;
									double acc;

									for (i = startRowIndex, pyi = &py[startRowIndex]; i < endRowIndex; i++, pyi++)
									{
										for (j = 0, pxi = px, acc = 0.0; j < xCount; j++, pxi++)
										{
											acc += elementFunction(i, j) * (*pxi);
										}

										*pyi = acc;
									}

								}
							}
						);

					}
				}

				return y;

			};
		}

		/// <summary>
		/// Get a tensor by specifying the matrix explicitly.
		/// </summary>
		/// <param name="matrix">The matrix as an array of two dimensions.</param>
		/// <returns>Returns the Tensor as specified.</returns>
		/// <remarks>
		/// The action of the tensor is parallelized when the dimensions of the applied vector
		/// exceed the square root of <see cref="ParallelismDimensionThreshold"/>.
		/// </remarks>
		public static Tensor GetTensor(double[,] matrix)
		{
			return delegate(Vector x)
			{
				if (x == null) throw new ArgumentNullException("x");

				int xCount = x.Length;

				if (xCount != matrix.GetLength(1))
					throw new ArgumentException("vector is not compatible to tensor.", "x");

				int rowsCount = matrix.GetLength(0);

				Vector y = new Vector(rowsCount);

				unsafe
				{
					if (rowsCount * xCount < ParallelismDimensionThreshold)
					{
						fixed (double* px = x.array, py = y.array, pmatrix = matrix)
						{
							int i, j;
							double* pxi;
							double* pyi;
							double* pmatrixij;
							double acc;

							for (i = 0, pyi = py; i < rowsCount; i++, pyi++)
							{
								for (j = 0, pxi = px, acc = 0.0, pmatrixij = &pmatrix[i]; j < xCount; j++, pxi++, pmatrixij += rowsCount)
								{
									acc += (*pmatrixij) * (*pxi);
								}

								*pyi = acc;
							}

						}
					}
					else
					{
						var rowsPartitioner = Partitioner.Create(0, rowsCount);

						Parallel.ForEach(
							rowsPartitioner,
							range =>
							{
								int startRowIndex = range.Item1;
								int endRowIndex = range.Item2;

								fixed (double* px = x.array, py = y.array, pmatrix = matrix)
								{
									int i, j;
									double* pxi;
									double* pyi;
									double* pmatrixij;
									double acc;

									for (i = startRowIndex, pyi = &py[startRowIndex]; i < endRowIndex; i++, pyi++)
									{
										for (j = 0, pxi = px, acc = 0.0, pmatrixij = &pmatrix[i]; j < xCount; j++, pxi++, pmatrixij += rowsCount)
										{
											acc += (*pmatrixij) * (*pxi);
										}

										*pyi = acc;
									}

								}
							}
						);

					}
				}

				return y;

			};
		}

		/// <summary>
		/// Get a tensor by specifying the matrix explicitly.
		/// </summary>
		/// <param name="matrix">The matrix as an array of two dimensions.</param>
		/// <returns>Returns the Tensor as specified.</returns>
		/// <remarks>
		/// The action of the tensor is parallelized when the dimensions of the applied vector
		/// exceed the square root of <see cref="ParallelismDimensionThreshold"/>.
		/// </remarks>
		public static Tensor GetTensor(float[,] matrix)
		{
			return delegate(Vector x)
			{
				if (x == null) throw new ArgumentNullException("x");

				int xCount = x.Length;

				if (xCount != matrix.GetLength(1))
					throw new ArgumentException("vector is not compatible to tensor.", "x");

				int rowsCount = matrix.GetLength(0);

				Vector y = new Vector(rowsCount);

				unsafe
				{
					if (rowsCount * xCount < ParallelismDimensionThreshold)
					{
						fixed (double* px = x.array, py = y.array)
						fixed (float* pmatrix = matrix)
						{
							int i, j;
							double* pxi;
							double* pyi;
							float* pmatrixij;
							double acc;

							for (i = 0, pyi = py; i < rowsCount; i++, pyi++)
							{
								for (j = 0, pxi = px, acc = 0.0, pmatrixij = &pmatrix[i]; j < xCount; j++, pxi++, pmatrixij += rowsCount)
								{
									acc += (*pmatrixij) * (*pxi);
								}

								*pyi = acc;
							}

						}
					}
					else
					{
						var rowsPartitioner = Partitioner.Create(0, rowsCount);

						Parallel.ForEach(
							rowsPartitioner,
							range =>
							{
								int startRowIndex = range.Item1;
								int endRowIndex = range.Item2;

								fixed (double* px = x.array, py = y.array)
								fixed (float* pmatrix = matrix)
								{
									int i, j;
									double* pxi;
									double* pyi;
									float* pmatrixij;
									double acc;

									for (i = startRowIndex, pyi = &py[startRowIndex]; i < endRowIndex; i++, pyi++)
									{
										for (j = 0, pxi = px, acc = 0.0, pmatrixij = &pmatrix[i]; j < xCount; j++, pxi++, pmatrixij += rowsCount)
										{
											acc += (*pmatrixij) * (*pxi);
										}

										*pyi = acc;
									}

								}
							}
						);

					}
				}

				return y;
			};
		}

		/// <summary>
		/// Get the Vendermonde tensor with parameters specified as
		/// the elements of vector <paramref name="β"/>.
		/// </summary>
		/// <param name="β">The paramters of the Vandermonde matrix.</param>
		/// <returns>Returns the tensor as specified.</returns>
		public static Vector.Tensor GetVandermondeTensor(Vector β)
		{
			if (β == null) throw new ArgumentNullException("β");

			return delegate(Vector x)
			{
				if (x == null) throw new ArgumentNullException("x");

				if (x.Length != β.Length)
					throw new ArgumentException("Vector is not compatible to tensor.", "x");

				var y = new Vector(x.Length);

				if (x.Length < ParallelismDimensionThreshold)
				{
					for (int i = 0; i < x.Length; i++)
					{
						int j;
						double βij;
						double accumulator;

						for (accumulator = 0.0, βij = 1.0, j = 0; j < x.Length; j++, βij *= β[i])
						{
							accumulator += βij * x[j];
						}

						y[i] = accumulator;
					}
				}
				else
				{
					Parallel.ForEach(
						Partitioner.Create(0, x.Length),
						delegate(Tuple<int, int> range)
						{
							for (int i = range.Item1; i < range.Item2; i++)
							{
								int j;
								double βij;
								double accumulator;

								for (accumulator = 0.0, βij = 1.0, j = 0; j < x.Length; j++, βij *= β[i])
								{
									accumulator += βij * x[j];
								}

								y[i] = accumulator;
							}
						}
					);

				}

				return y;

			};
		}

		/// <summary>
		/// Create a copy of this vector.
		/// </summary>
		public Vector Clone()
		{
			var copy = new Vector(this.array);

			if (this.isNorm2Known)
			{
				copy.isNorm2Known = true;
				copy.norm2 = this.norm2;
			}

			return copy;
		}

		/// <summary>
		/// Add in-place another vector.
		/// </summary>
		/// <param name="v2">The summand.</param>
		/// <returns>Returns the vector itself, altered.</returns>
		public Vector AddInPlace(Vector v2)
		{
			if (v2 == null) return this;

			EnsureCompatibleVectors(this, v2);

			int count = this.Length;

			this.isNorm2Known = false;

			unsafe
			{
				if (count < ParallelismDimensionThreshold)
				{
					fixed (double* pv = this.array, pv2 = v2.array)
					{
						double* pv2i;
						double* pvi;
						int i;

						for (i = 0, pvi = pv, pv2i = pv2; i < count; i++, pvi++, pv2i++)
						{
							*pvi += *pv2i;
						}
					}
				}
				else
				{
					var partitioner = Partitioner.Create(0, count);

					Parallel.ForEach(
						partitioner,
						range =>
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* pv = this.array, pv2 = v2.array)
							{
								double* pv2i;
								double* pvi;
								int i;

								for (
									i = startIndex, pvi = &pv[startIndex], pv2i = &pv2[startIndex];
									i < endIndex;
									i++, pvi++, pv2i++)
								{
									*pvi += *pv2i;
								}
							}
						}
					);
				}
			}

			return this;
		}

		/// <summary>
		/// In-place vector addition.
		/// </summary>
		/// <param name="summand">If null, it is treated like zero.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		public Vector AddInPlace(IVector summand)
		{
			if (summand == null) return this;

			var denseSummand = summand as Vector;

			if (denseSummand != null) return AddInPlace(denseSummand);

			var sparseSummand = summand as SparseVector;

			if (sparseSummand != null) return AddInPlace(sparseSummand);

			throw new ArgumentException("summand is not a supported vector type.", "summand");
		}

		/// <summary>
		/// Subtract in-place another vector.
		/// </summary>
		/// <param name="v2">The subtracted vector.</param>
		/// <returns>Returns the vector itself, altered.</returns>
		public Vector SubtractInPlace(Vector v2)
		{
			if (v2 == null) return this;

			EnsureCompatibleVectors(this, v2);

			int count = this.Length;

			this.isNorm2Known = false;

			unsafe
			{
				if (count < ParallelismDimensionThreshold)
				{
					fixed (double* pv = this.array, pv2 = v2.array)
					{
						double* pv2i;
						double* pvi;
						int i;

						for (i = 0, pvi = pv, pv2i = pv2; i < count; i++, pvi++, pv2i++)
						{
							*pvi -= *pv2i;
						}
					}
				}
				else
				{
					var partitioner = Partitioner.Create(0, count);

					Parallel.ForEach(
						partitioner,
						range =>
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* pv = this.array, pv2 = v2.array)
							{
								double* pv2i;
								double* pvi;
								int i;

								for (
									i = startIndex, pvi = &pv[startIndex], pv2i = &pv2[startIndex];
									i < endIndex;
									i++, pvi++, pv2i++)
								{
									*pvi -= *pv2i;
								}
							}
						}
					);
				}
			}

			return this;
		}

		/// <summary>
		/// In-place vector subtraction.
		/// </summary>
		/// <param name="subtracted">If null, it is treated like zero.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		public Vector SubtractInPlace(IVector subtracted)
		{
			if (subtracted == null) return this;

			var denseSubtracted = subtracted as Vector;

			if (denseSubtracted != null) return SubtractInPlace(denseSubtracted);

			var sparseSubtracted = subtracted as SparseVector;

			if (sparseSubtracted != null) return SubtractInPlace(sparseSubtracted);

			throw new ArgumentException("subtracted is not a supported vector type.", "subtracted");
		}

		/// <summary>
		/// In-place scalar multiplication.
		/// </summary>
		/// <param name="k">The scaling coefficient.</param>
		/// <returns>Returns the vector itself, altered.</returns>
		public Vector ScaleInPlace(double k)
		{
			int count = this.Length;

			this.isNorm2Known = false;

			unsafe
			{
				if (count < ParallelismDimensionThreshold)
				{
					fixed (double* pv = this.array)
					{
						int i;
						double* pvi;

						for (i = 0, pvi = pv; i < count; i++, pvi++)
						{
							*pvi *= k;
						}
					}
				}
				else
				{
					var partitioner = Partitioner.Create(0, count);

					Parallel.ForEach(
						partitioner,
						range =>
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* pv = this.array)
							{
								int i;
								double* pvi;

								for (
									i = startIndex, pvi = &pv[startIndex];
									i < endIndex;
									i++, pvi++)
								{
									*pvi *= k;
								}
							}
						}
					);
				}
			}

			return this;
		}

		/// <summary>
		/// In-place scalar division.
		/// </summary>
		/// <param name="k">The dividing coefficient.</param>
		/// <returns>Returns the vector itself, altered.</returns>
		public Vector DivideInPlace(double k)
		{
			int count = this.Length;

			this.isNorm2Known = false;

			unsafe
			{
				if (count < ParallelismDimensionThreshold)
				{
					fixed (double* pv = this.array)
					{
						int i;
						double* pvi;

						for (i = 0, pvi = pv; i < count; i++, pvi++)
						{
							*pvi /= k;
						}
					}
				}
				else
				{
					var partitioner = Partitioner.Create(0, count);

					Parallel.ForEach(
						partitioner,
						range =>
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* pv = this.array)
							{
								int i;
								double* pvi;

								for (
									i = startIndex, pvi = &pv[startIndex];
									i < endIndex;
									i++, pvi++)
								{
									*pvi /= k;
								}
							}
						}
					);
				}
			}

			return this;
		}

		/// <summary>
		/// In-place negation.
		/// </summary>
		/// <returns>Returns the vector itself, altered.</returns>
		public Vector NegateInPlace()
		{
			int count = this.Length;

			this.isNorm2Known = false;

			unsafe
			{
				if (count < ParallelismDimensionThreshold)
				{
					fixed (double* pv = this.array)
					{
						int i;
						double* pvi;

						for (i = 0, pvi = pv; i < count; i++, pvi++)
						{
							*pvi = - *pvi;
						}
					}
				}
				else
				{
					var partitioner = Partitioner.Create(0, count);

					Parallel.ForEach(
						partitioner,
						range =>
						{
							int startIndex = range.Item1;
							int endIndex = range.Item2;

							fixed (double* pv = this.array)
							{
								int i;
								double* pvi;

								for (
									i = startIndex, pvi = &pv[startIndex];
									i < endIndex;
									i++, pvi++)
								{
									*pvi = - *pvi;
								}
							}
						}
					);
				}
			}

			return this;
		}

		/// <summary>
		/// In-place addition of a sparse vector.
		/// </summary>
		/// <param name="v2">The vector to be added.</param>
		/// <returns>Returns the vector itself, altered.</returns>
		public Vector AddInPlace(SparseVector v2)
		{
			if (v2 == null) return this;

			this.isNorm2Known = false;

			for (var entry = v2.FirstEntry; entry != null; entry = entry.NextEntry)
			{
				this.array[entry.Index] += entry.Value;
			}

			return this;
		}

		/// <summary>
		/// In-place subtraction of a sparse vector.
		/// </summary>
		/// <param name="v2">The vector to be subtracted.</param>
		/// <returns>Returns the vector itself, altered.</returns>
		public Vector SubtractInPlace(SparseVector v2)
		{
			if (v2 == null) return this;

			this.isNorm2Known = false;

			for (var entry = v2.FirstEntry; entry != null; entry = entry.NextEntry)
			{
				this.array[entry.Index] -= entry.Value;
			}

			return this;
		}

		public override string ToString()
		{
			var builder = new StringBuilder(array.Length * 20);

			builder.Append('(');

			for (int i = 0; i < array.Length - 1; i++)
			{
				builder.Append(array[i]);
				builder.Append(", ");
			}

			if (array.Length > 0)
			{
				builder.Append(array[array.Length - 1]);
			}

			builder.Append(')');

			return builder.ToString();
		}

		#endregion

		#region IEnumerable<double> Members

		public IEnumerator<double> GetEnumerator()
		{
			return this.array.AsEnumerable().GetEnumerator();
		}

		#endregion

		#region IEnumerable Members

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.array.GetEnumerator();
		}

		#endregion

		#region Private methods

		private static void EnsureCompatibleVectors(Vector v1, Vector v2)
		{
			if (v1 == null) throw new ArgumentNullException("v1");
			if (v2 == null) throw new ArgumentNullException("v2");
			if (v1.Length != v2.Length) throw new ArgumentException("Vectors must be of the same length.");
		}

		#endregion

		#region ISerializable Members

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("array", this.array);
		}

		#endregion

		#region IVector Members

		IVector IVector.Add(IVector summand)
		{
			return this + summand;
		}

		IVector IVector.Subtract(IVector subtracted)
		{
			return this - subtracted;
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
			return this * multiplicand;
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
			return Clone();
		}

		#endregion
	}

}
