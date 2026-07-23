using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using CommandLine;
using CommandLine.Text;
using Obj2Tiles.Library;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Stages;
using Obj2Tiles.Stages.Model;

namespace Obj2Tiles
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            using var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true;
                with.HelpWriter = Console.Out;
            });

            var oResult = await parser.ParseArguments<Options>(args).WithParsedAsync(opts =>
            {
                ApplyPreset(opts, args);
                return Run(opts);
            });

            if (oResult.Tag == ParserResultType.NotParsed)
            {
                Console.WriteLine("Usage: obj2tiles [options]");
            }
        }

        private static void ApplyPreset(Options opts, string[] args)
        {
            if (opts.Preset == Preset.None) return;

            bool WasSpecified(params string[] flags) =>
                args.Any(a => flags.Contains(a) || Array.Exists(flags, f => a.StartsWith(f + "=")));

            switch (opts.Preset)
            {
                case Preset.Legacy:
                    if (!WasSpecified("--no-zsplit"))
                        opts.NoZSplit = true;
                    if (!WasSpecified("--no-octree"))
                        opts.NoOctree = true;
                    if (!WasSpecified("--lod-texture-scale"))
                        opts.LodTextureScale = 1.0;
                    break;

                case Preset.Standard:
                    if (!WasSpecified("-z", "--zsplit"))
                        opts.ZSplit = true;
                    if (!WasSpecified("--octree"))
                        opts.Octree = true;
                    if (!WasSpecified("--local"))
                        opts.LocalMode = true;
                    if (!WasSpecified("--lod-texture-scale"))
                        opts.LodTextureScale = 0.5;
                    if (!WasSpecified("-m", "--decimation-mode"))
                        opts.DecimationMode = DecimationMode.Quality;
                    if (!WasSpecified("--glb"))
                        opts.UseGlb = true;
                    break;
            }
        }

        private static async Task Run(Options opts)
        {
            Console.WriteLine();
            Console.WriteLine(" *** OBJ to Tiles ***");
            Console.WriteLine();

            if (!CheckOptions(opts)) return;

            opts.Output = Path.GetFullPath(opts.Output);
            opts.Input = Path.GetFullPath(opts.Input);

            Directory.CreateDirectory(opts.Output);

            var pipelineId = Guid.NewGuid().ToString();
            var sw = new Stopwatch();
            var swg = Stopwatch.StartNew();

            Func<string, string> createTempFolder = opts.UseSystemTempFolder
                ? s => CreateTempFolder(s, Path.GetTempPath())
                : s => CreateTempFolder(s, Path.Combine(opts.Output, ".temp"));

            string? destFolderDecimation = null;
            string? destFolderSplit = null;

            try
            {

                destFolderDecimation = opts.StopAt == Stage.Decimation
                    ? opts.Output
                    : createTempFolder($"{pipelineId}-obj2tiles-decimation");

                Console.WriteLine($" => Decimation stage with {opts.LODs} LODs");
                sw.Start();

                var decimateRes = await StagesFacade.Decimate(opts.Input, destFolderDecimation, opts.LODs, opts.DecimationMode);

                Console.WriteLine(" ?> Decimation stage done in {0}", sw.Elapsed);

                if (opts.StopAt == Stage.Decimation)
                    return;

                Console.WriteLine();
                Console.WriteLine(
                    $" => Splitting stage with {opts.Divisions} divisions {(opts.EffectiveZSplit ? "and Z-split" : "")}");

                destFolderSplit = opts.StopAt == Stage.Splitting
                    ? opts.Output
                    : createTempFolder($"{pipelineId}-obj2tiles-split");

                Console.WriteLine($" ?> Keep original textures: {opts.KeepOriginalTextures}, Split strategy: {opts.SplitPointStrategy}");

                var boundsMapper = await StagesFacade.Split(decimateRes.DestFiles, destFolderSplit, opts.Divisions,
                    opts.EffectiveZSplit, opts.KeepOriginalTextures, opts.SplitPointStrategy, opts.EffectiveOctree, (float)opts.LodTextureScale);

                Console.WriteLine(" ?> Splitting stage done in {0}", sw.Elapsed);

                if (opts.StopAt == Stage.Splitting)
                    return;

                var gpsCoords = opts.Latitude != null && opts.Longitude != null
                    ? new GpsCoords(opts.Latitude.Value, opts.Longitude.Value, opts.Altitude, opts.Scale, opts.YUpToZUp)
                    : null;

                Console.WriteLine();
                Console.WriteLine($" => Tiling stage {(gpsCoords != null ? $"with GPS coords {gpsCoords}" : "")}");

                var baseError = opts.BaseError;

                Console.WriteLine();
                Console.WriteLine(baseError.HasValue
                    ? $" => Tiling stage with baseError {baseError}"
                    : $" => Tiling stage with auto-computed baseError ({opts.ErrorEstimationMode})");

                sw.Restart();

                if (opts.LocalMode && (opts.Latitude != null || opts.Longitude != null))
                    Console.WriteLine(" !> Warning: --local overrides --lat/--lon. ECEF transform will not be applied.");

                StagesFacade.Tile(destFolderSplit, opts.Output, opts.LODs, opts.BaseError, boundsMapper, gpsCoords, opts.LocalMode, opts.EffectiveOctree,
                    opts.ErrorEstimationMode, opts.ErrorFactor, opts.EffectiveUseGlb);

                Console.WriteLine(" ?> Tiling stage done in {0}", sw.Elapsed);
            }
            catch (Exception ex)
            {
                Console.WriteLine(" !> Exception: {0}", ex.Message);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine(" => Pipeline completed in {0}", swg.Elapsed);

                var tmpFolder = Path.Combine(opts.Output, ".temp");

                if (opts.KeepIntermediateFiles)
                {
                    Console.WriteLine(
                        $" ?> Skipping cleanup, intermediate files are in '{tmpFolder}' with pipeline id '{pipelineId}'");

                    Console.WriteLine(" ?> You should delete this folder manually, it is only for debugging purposes");
                }
                else
                {

                    Console.WriteLine(" => Cleaning up");

                    if (destFolderDecimation != null && destFolderDecimation != opts.Output)
                        Directory.Delete(destFolderDecimation, true);

                    if (destFolderSplit != null && destFolderSplit != opts.Output)
                        Directory.Delete(destFolderSplit, true);

                    if (Directory.Exists(tmpFolder))
                        Directory.Delete(tmpFolder, true);

                    Console.WriteLine(" ?> Cleaning up ok");
                }
            }
        }

        private static bool CheckOptions(Options opts)
        {

            if (string.IsNullOrWhiteSpace(opts.Input))
            {
                Console.WriteLine(" !> Input file is required");
                return false;
            }

            if (!File.Exists(opts.Input))
            {
                Console.WriteLine(" !> Input file does not exist");
                return false;
            }

            if (string.IsNullOrWhiteSpace(opts.Output))
            {
                Console.WriteLine(" !> Output folder is required");
                return false;
            }

            if (opts.LODs < 1)
            {
                Console.WriteLine(" !> LODs must be at least 1");
                return false;
            }

            if (opts.Divisions < 0)
            {
                Console.WriteLine(" !> Divisions must be non-negative");
                return false;
            }

            return true;
        }


        private static string CreateTempFolder(string folderName, string baseFolder)
        {
            var tempFolder = Path.Combine(baseFolder, folderName);
            Directory.CreateDirectory(tempFolder);
            return tempFolder;
        }
    }
}