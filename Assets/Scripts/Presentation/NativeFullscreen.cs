using System;
using System.Collections;
using UnityEngine;

namespace Origins
{
    // Shared by the game and all preview scenes. Explicit capture arguments take precedence.
    public sealed class NativeFullscreen : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if(Application.isEditor) return;
            var go=new GameObject("Display settings"); DontDestroyOnLoad(go);
            go.AddComponent<NativeFullscreen>();
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-screen-fullscreen")<0) EnterFullscreen();
            yield return null; yield return null;
            Debug.Log("DISPLAY: "+Screen.width+"x"+Screen.height+" "+Screen.fullScreenMode+"; monitor "+Screen.currentResolution);
        }
        static void EnterFullscreen()
        {
            var native=Screen.currentResolution;
            Screen.SetResolution(native.width,native.height,FullScreenMode.FullScreenWindow);
        }
        void Update()
        {
            if(!Input.GetKeyDown(KeyCode.F11) && !(Input.GetKeyDown(KeyCode.Return) && (Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt)))) return;
            if(Screen.fullScreen) Screen.SetResolution(1280,720,FullScreenMode.Windowed);
            else EnterFullscreen();
        }
    }
}
