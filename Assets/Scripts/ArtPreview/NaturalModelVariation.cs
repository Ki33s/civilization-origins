using UnityEngine;

namespace Origins
{
    // Shared by the artwork generator and the game map; one transform per complete model.
    public struct NaturalModelVariation
    {
        public float yaw, scale;
        public static NaturalModelVariation Sample(System.Random random)
        {
            return new NaturalModelVariation { yaw=Mathf.Min(359.99997f,(float)random.NextDouble()*360f), scale=.5f+(float)random.NextDouble() };
        }
        public void Apply(Transform model)
        {
            model.localRotation=Quaternion.Euler(0,yaw,0);
            model.localScale*=scale;
        }
        public Matrix4x4 Matrix(Vector3 position,Quaternion groundRotation,float baseScale=1)
        {
            return Matrix4x4.TRS(position,groundRotation*Quaternion.Euler(0,yaw,0),Vector3.one*(baseScale*scale));
        }
    }
}
