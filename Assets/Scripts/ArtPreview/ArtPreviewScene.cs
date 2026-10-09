using UnityEngine;

namespace Origins
{
    // Prevent the gameplay bootstrap from running inside isolated art scenes.
    public sealed class ArtPreviewScene : MonoBehaviour { }
}
