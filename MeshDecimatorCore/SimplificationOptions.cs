namespace MeshDecimatorCore
{
    /// <summary>
    /// Options for mesh simplification algorithms.
    /// Based on UnityMeshSimplifier SimplificationOptions.
    /// </summary>
    public struct SimplificationOptions
    {
        /// <summary>
        /// Default simplification options.
        /// </summary>
        public static readonly SimplificationOptions Default = new SimplificationOptions
        {
            PreserveBorderEdges = false,
            PreserveUVSeamEdges = false,
            PreserveUVFoldoverEdges = false,
            PreserveSurfaceCurvature = false,
            EnableSmartLink = true,
            VertexLinkDistance = double.Epsilon,
            MaxIterationCount = 100,
            Aggressiveness = 7.0
        };

        /// <summary>
        /// If enabled, border edges (open mesh boundaries) will not be collapsed.
        /// Default value: false
        /// </summary>
        public bool PreserveBorderEdges;

        /// <summary>
        /// If enabled, UV seam edges will not be collapsed, preventing texture
        /// discontinuity artifacts.
        /// An UV seam edge is a border edge that is duplicated in two distinct
        /// triangles with different UV coordinates (cut for texturing purposes).
        /// This is relevant only if EnableSmarkLink is set to true.
        /// If EnableSmartLink is set to false the UV seam edges are always treated as boders.
        /// Default value: false
        /// </summary>
        public bool PreserveUVSeamEdges;

        /// <summary>
        /// If enabled, UV foldover edges will not be collapsed.
        /// An UV foldover edge is a border edge that is duplicated in two distinct
        /// triangles with same UV coordinates (likely normal-related vertex duplication).
        /// This is relevant only if EnableSmarkLink is set to true.
        /// If EnableSmartLink is set to false the UV foldover edges are always treated as boders.
        /// Default value: false
        /// </summary>
        public bool PreserveUVFoldoverEdges;

        /// <summary>
        /// If enabled, an additional curvature penalty is applied during
        /// error calculation to better preserve surface shape.
        /// Default value: false
        /// </summary>
        public bool PreserveSurfaceCurvature;

        /// <summary>
        /// If enabled, border vertices at the same position are linked together
        /// as seam or foldover edges instead of being treated as borders.
        /// This prevents holes while still allowing decimation of shared edges.
        /// Default value: true
        /// </summary>
        public bool EnableSmartLink;

        /// <summary>
        /// The maximum distance between two vertices to be linked together
        /// when smart linking is enabled.
        /// Default value: double.Epsilon
        /// </summary>
        public double VertexLinkDistance;

        /// <summary>
        /// The maximum number of iterations for the decimation algorithm.
        /// Default value: 100
        /// </summary>
        public int MaxIterationCount;

        /// <summary>
        /// The aggressiveness of the decimation algorithm.
        /// Higher values result in faster decimation with potentially lower quality.
        /// Default value: 7.0
        /// </summary>
        public double Aggressiveness;
    }
}
