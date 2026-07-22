using System.Text.Json.Serialization;
using CommandLine;
using Obj2Tiles.Stages;

namespace Obj2Tiles;

public sealed class Options
{
    [Value(0, MetaName = "Input", Required = true, HelpText = "Input OBJ file.")]
    public string Input { get; set; } = null!;

    [Value(1, MetaName = "Output", Required = true, HelpText = "Output folder.")]
    public string Output { get; set; } = null!;

    [Option('s', "stage", Required = false, HelpText = "Stage to stop at (Decimation, Splitting, Tiling)", Default = Stage.Tiling)]
    public Stage StopAt { get; set; }

    [Option('p', "preset", Required = false, HelpText = "Applies a bundle of option defaults. 'legacy' sets --no-zsplit --no-octree --lod-texture-scale 1.0. 'standard' sets --octree --local --zsplit --lod-texture-scale 0.5 --decimation-mode Quality. Any of these options given explicitly on the command line take precedence over the preset.", Default = Preset.None)]
    public Preset Preset { get; set; }

    [Option('d', "divisions", Required = false, HelpText = "How many tiles divisions", Default = 2)]
    public int Divisions { get; set; }

    [Option('z', "zsplit", Required = false, HelpText = "Splits along z-axis too", Default = false)]
    public bool ZSplit { get; set; }

    [Option("no-zsplit", Required = false, HelpText = "Disables z-axis splitting, overriding --zsplit.", Default = false)]
    public bool NoZSplit { get; set; }

    [Option('l', "lods", Required = false, HelpText = "How many levels of details", Default = 3)]
    public int LODs { get; set; }

    [Option('m', "decimation-mode", Required = false, HelpText = "Decimation mode: Aggressive (max reduction, may distort UV seams), Standard (usually good results, may have distortions in certain cases), or Quality (best results but slower and less vertex reduction)", Default = DecimationMode.Standard)]
    public DecimationMode DecimationMode { get; set; } = DecimationMode.Standard;

    [Option('k', "keeptextures", Required = false, HelpText = "Keeps original textures", Default = false)]
    public bool KeepOriginalTextures { get; set; }

    [Option('g', "split-strategy", Required = false, HelpText = "Split strategy: AbsoluteCenter, VertexBaricenter, or VertexMedian (balanced tiles)", Default = SplitPointStrategy.VertexBaricenter)]
    public SplitPointStrategy SplitPointStrategy { get; set; } = SplitPointStrategy.VertexBaricenter;

    [Option("lat", Required = false, HelpText = "Latitude of the mesh", Default = null)]
    public double? Latitude { get; set; }

    [Option("lon", Required = false, HelpText = "Longitude of the mesh", Default = null)]
    public double? Longitude { get; set; }

    [Option("alt", Required = false, HelpText = "Altitude of the mesh (meters)", Default = 0)]
    public double Altitude { get; set; }

    [Option("scale", Required = false, HelpText = "Scale for data if using units other than meters ( 1200.0/3937.0 for survey ft)", Default = 1.0)]
    public double Scale { get; set; }

    [Option('e',"error", Required = false, HelpText = "Base error for root node. If omitted, it's auto-computed from the coarsest LOD using --error-estimation-mode/--error-factor.", Default = null)]
    public double? BaseError { get; set; }

    [Option("error-estimation-mode", Required = false, HelpText = "How to estimate geometric error: BoundingBoxDiagonal/AverageEdgeLength/MaximumEdgeLength derive each tile's error from that tile's own geometry (bounding-box diagonal, or average/maximum triangle edge length, times --error-factor). The Toplevel* variants instead derive a single value at the root from the coarsest LOD using the same metric, then halve it once per LOD subdivision.", Default = ErrorEstimationMode.AverageEdgeLength)]
    public ErrorEstimationMode ErrorEstimationMode { get; set; } = ErrorEstimationMode.AverageEdgeLength;

    [Option("error-factor", Required = false, HelpText = "Multiplier applied to the metric selected by --error-estimation-mode. If omitted, defaults to 0.1 for *BoundingBoxDiagonal modes, 0.5 for *AverageEdgeLength/*MaximumEdgeLength modes.", Default = null)]
    public double? ErrorFactor { get; set; }

    [Option("use-system-temp", Required = false, HelpText = "Uses the system temp folder", Default = false)]
    public bool UseSystemTempFolder { get; set; }

    [Option("keep-intermediate", Required = false, HelpText = "Keeps the intermediate files (do not cleanup)", Default = false)]
    public bool KeepIntermediateFiles { get; set; }

    [Option('t', "y-up-to-z-up", Required = false, HelpText = "Convert the upward Y-axis to the upward Z-axis, which is used in some situations where the upward axis may be the Y-axis or the Z-axis after the obj is exported.", Default = false)]
    public bool YUpToZUp { get; set; }

    [Option("local", Required = false, HelpText = "Local mode: no ECEF geo-referencing, uses identity matrix in tileset.json. Use this when you don't need to place the model on a globe.", Default = false)]
    public bool LocalMode { get; set; }

    [Option("octree", Required = false, HelpText = "Use octree spatial subdivision: each LOD gets one additional division level relative to the next coarser LOD, producing a proper tile hierarchy instead of same-count tiles per LOD.", Default = false)]
    public bool Octree { get; set; }

    [Option("no-octree", Required = false, HelpText = "Disables octree spatial subdivision, overriding --octree.", Default = false)]
    public bool NoOctree { get; set; }

    [Option("lod-texture-scale", Required = false, HelpText = "Per-LOD texture downscale factor. LOD-0 always keeps full resolution; each subsequent LOD multiplies the previous resolution by this factor. E.g. 0.5 gives LOD-1 at 1/2 resolution, LOD-2 at 1/4, etc. Default 0.5.", Default = 0.5)]
    public double LodTextureScale { get; set; }

    [JsonIgnore]
    public bool EffectiveZSplit => ZSplit && !NoZSplit;

    [JsonIgnore]
    public bool EffectiveOctree => Octree && !NoOctree;
}

public enum Stage
{
    Decimation,
    Splitting,
    Tiling
}

public enum Preset
{
    None,
    Legacy,
    Standard
}