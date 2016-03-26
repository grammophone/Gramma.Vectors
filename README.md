# Gramma.Vectors
This .NET library provides dense and sparse vectors and matrices defined as abstract tensors, meaning linear functions operating on vectors. This abstraction permits expressing very large matrices and corresponding large problems, without having to store their elements in memory. This freedom of expression allows the matrix contents to be specified under any sparsity or algorithmic pattern or to be partitioned across various storage media or the cloud. In addition, since a tensor is simply a specification of a function, this also allows any suitable parallelization scheme to be adopted for the computation of an application of a tensor over a vector.

Both flavors of vectors, `Vector` and `SparseVector`, have math operators defined in order to offer clean vector expressions in code. The operators are addition, subtraction, negation, multiplication and division by scalar, inner and outer products. The dense `Vector` is also implicitly convertible from `Double[]`. Both types of vectors work with tensor types defined as delegate types `Vector.Tensor` and `SparseVector.Tensor` respectively.

Dense vector operations become parallelized when the dimension exceeds a certain threshold, which is 32768 by default and can be set by specifying the `parallelismDimensionThreshold` attribute in a `VectorsConfigurationSection` defined in the application's config file. The threshold can also be defined programmatically by setting the `Vector.ParallelismDimensionThreshold` static property.

The above are summarized in the following UML diagram.

![Vectors class diagram](http://s17.postimg.org/imk0wke4f/Vectors.png)

For example usage, please take a look at the [first example](https://github.com/grammophone/Gramma.Optimization/wiki/1.-A-simple-example) and the [second example](https://github.com/grammophone/Gramma.Optimization/wiki/2.-Another-example) of the [Gramma.Optimization](https://github.com/grammophone/Gramma.Optimization) library which relies on this one.

This library has no dependencies.
