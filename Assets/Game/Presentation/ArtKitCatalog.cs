using System;
using UnityEngine;

namespace Emberfield.Presentation
{
    [Serializable]
    public sealed class ArtKitEntry
    {
        public string Id;
        public Mesh Lod0;
        public Mesh Lod1;
    }

    public sealed class ArtKitCatalog : ScriptableObject
    {
        public string SourceVersion;
        public Material Material;
        public ArtKitEntry[] Units = Array.Empty<ArtKitEntry>();
        public ArtKitEntry[] Buildings = Array.Empty<ArtKitEntry>();
        public ArtKitEntry[] Resources = Array.Empty<ArtKitEntry>();
    }
}
