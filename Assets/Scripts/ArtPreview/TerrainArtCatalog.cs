using UnityEngine;

namespace Origins
{
    [CreateAssetMenu(menuName="Истоки/Набор графики местности")]
    public sealed class TerrainArtCatalog : ScriptableObject
    {
        public float defaultDensity=.25f;
        public int minDecorItems=150, maxDecorItems=250, generationSeed;
        public float hexRadius=4.5f;
        public GameObject forest, meadow, plains, hills;
        public GameObject mountain, desert, coast, ocean;
        public GameObject swamp, tundra;
        public int desertMinDecor=30, desertMaxDecor=60, coastMinDecor=40, coastMaxDecor=80;
    }
}
