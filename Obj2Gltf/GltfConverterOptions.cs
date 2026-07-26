using System;
using System.Text;

namespace SilentWave.Obj2Gltf
{
    public class GltfConverterOptions
    {
        /// <summary>
        /// obj and mtl files' text encoding
        /// </summary>
        public Encoding ObjEncoding { get; set; }

        /// <summary>
        /// Default is false
        /// </summary>
        public bool RemoveDegenerateFaces { get; set; } = false;
        
        /// <summary>
        /// Default is false
        /// </summary>
        public bool DeleteOriginals { get; set; } = false;

        /// <summary>
        /// Marks every output material with the KHR_materials_unlit extension, so viewers render
        /// the base color texture as-is without applying PBR lighting. Useful for photogrammetry
        /// content where lighting is already baked into the textures. Default is false.
        /// </summary>
        public bool UnlitMaterials { get; set; } = false;
    }
}
