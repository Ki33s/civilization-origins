using UnityEngine;

namespace Origins
{
    public static class ContentLoader
    {
        public static GameContent Load()
        {
            var asset=Resources.Load<TextAsset>("EarlyEraContent");
            if(!asset) throw new System.InvalidOperationException("EarlyEraContent.json is missing");
            GameContent.Current=new GameContent(JsonUtility.FromJson<ContentRecords>(asset.text));
            return GameContent.Current;
        }
    }
}
