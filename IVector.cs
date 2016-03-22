using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gramma.Vectors
{
	/// <summary>
	/// Represents common functionality present to all vector implementations.
	/// </summary>
	public interface IVector
	{
		#region Properties

		/// <summary>
		/// Get the value at an index.
		/// </summary>
		/// <param name="index">The index of the vector's value.</param>
		/// <returns>The vector's value at the index.</returns>
		double this[int index] { get; }

		/// <summary>
		/// Get the squared Euclidean norm of the vector.
		/// </summary>
		double Norm2 { get; }

		#endregion

		#region Methods

		/// <summary>
		/// Vector addition.
		/// </summary>
		/// <param name="summand">If null, it is treated like zero.</param>
		IVector Add(IVector summand);

		/// <summary>
		/// Vector subtraction.
		/// </summary>
		/// <param name="subtracted">If null, it is treated like zero.</param>
		IVector Subtract(IVector subtracted);

		/// <summary>
		/// Scalar multiplication.
		/// </summary>
		IVector Scale(double coefficient);

		/// <summary>
		/// Scalar division.
		/// </summary>
		IVector Divide(double coefficient);

		/// <summary>
		/// Inner product.
		/// </summary>
		/// <param name="multiplicand">If null, it is treated like zero.</param>
		double InnerProduct(IVector multiplicand);

		/// <summary>
		/// Negation.
		/// </summary>
		IVector Negate();

		/// <summary>
		/// In-place vector addition.
		/// </summary>
		/// <param name="summand">If null, it is treated like zero.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		IVector AddInPlace(IVector summand);

		/// <summary>
		/// In-place vector subtraction.
		/// </summary>
		/// <param name="subtracted">If null, it is treated like zero.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		IVector SubtractInPlace(IVector subtracted);

		/// <summary>
		/// In-place scalar multiplication.
		/// </summary>
		/// <param name="coefficient">The scaling coefficient.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		IVector ScaleInPlace(double coefficient);

		/// <summary>
		/// In-place scalar division.
		/// </summary>
		/// <param name="coefficient">The division coefficient.</param>
		/// <returns>Returns the vetor itself, altered.</returns>
		IVector DivideInPlace(double coefficient);

		/// <summary>
		/// In-place negation.
		/// </summary>
		/// <returns>Returns the vetor itself, altered.</returns>
		IVector NegateInPlace();

		/// <summary>
		/// Create an exact copy of the vector.
		/// </summary>
		/// <returns>Returns a copy of the vector.</returns>
		IVector Clone();

		#endregion
	}
}
