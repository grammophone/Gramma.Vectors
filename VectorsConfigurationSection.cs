using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace Gramma.Vectors
{
	/// <summary>
	/// Definition for configuration section "vectorsConfiguration".
	/// </summary>
	public class VectorsConfigurationSection : ConfigurationSection
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public VectorsConfigurationSection()
		{
			this["parallelismDimensionThreshold"] = 32768;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// Specifies the minimum dimension size where the various
		/// operations are executed in parallel. Default is 32768.
		/// </summary>
		[ConfigurationProperty("parallelismDimensionThreshold")]
		public int ParallelismDimensionThreshold
		{
			get
			{
				return (int)this["parallelismDimensionThreshold"];
			}
			set
			{
				this["parallelismDimensionThreshold"] = value;
			}
		}

		#endregion
	}
}
