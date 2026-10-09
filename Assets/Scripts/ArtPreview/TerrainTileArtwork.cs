using UnityEngine;

namespace Origins
{
    public enum TerrainArtKind { Forest, Meadow, Plains, Hills, Mountain, Desert, Coast, Ocean, Swamp, Tundra }

    // Artwork metadata only; these types do not change the strategy's terrain rules.
    public sealed class TerrainTileArtwork : MonoBehaviour
    {
        public TerrainArtKind kind;
        public string displayName;
        public float density=.25f;
        public int decorItems=200, triangles, treeTriangles, generationSeed;
        public int majorFeatures, minDecor=150, maxDecor=250;
        public bool animatedWater;
        public Transform trees;
    }

}
