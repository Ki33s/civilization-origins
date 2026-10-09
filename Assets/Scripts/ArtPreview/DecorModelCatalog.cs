using System;
using UnityEngine;

namespace Origins
{
    [Serializable] public sealed class DecorModelDefinition
    {
        public string id,label,category;
        public bool large;
        public GameObject prefab;
        public Mesh[] medium,far;
        public int triangles;
    }
    [CreateAssetMenu(menuName="Истоки/Модели декора")]
    public sealed class DecorModelCatalog : ScriptableObject
    {
        public DecorModelDefinition[] models;
        public DecorModelDefinition Get(string id) { foreach(var model in models) if(model.id==id) return model; return null; }
    }
}
