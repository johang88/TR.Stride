using Stride.Core;
using Stride.Rendering.Images;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TR.Stride.Rendering
{
    [DataContract("ToneMapFilmicOperator")]
    [Display("Filmic")]
    public class ToneMapFilmicOperator : ToneMapCommonOperator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToneMapReinhardOperator"/> class.
        /// </summary>
        public ToneMapFilmicOperator()
            : base("ToneMapFilmicOperatorShader")
        {
        }
    }
}