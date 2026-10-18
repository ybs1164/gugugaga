using System;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 표 팩 zip 하나를 고른다 — 안드로이드는 시스템 파일 고르기(Assets/Plugins/Android/TablePackPicker.java)가 앱 캐시로 복사한 경로,
    /// PC·에디터는 파일 열기 창의 경로를 onPicked로 넘긴다(취소하면 빈 문자열). 안드로이드는 결과가 UnitySendMessage로 오므로
    /// 이름이 정해진 GameObject 하나를 씬 사이에도 남겨 둔다.
    /// </summary>
    public class TablePackPicker : MonoBehaviour
    {
        private const string ObjectName = "TablePackPicker";
        private Action<string> _onPicked;

        public static void Open(string dialogTitle, Action<string> onPicked)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var go = GameObject.Find(ObjectName);
            if (go == null) { go = new GameObject(ObjectName); DontDestroyOnLoad(go); }
            var picker = go.GetComponent<TablePackPicker>();
            if (picker == null) picker = go.AddComponent<TablePackPicker>();
            picker._onPicked = onPicked;
            string dest = System.IO.Path.Combine(Application.temporaryCachePath, "import.zip");
            using (var plugin = new AndroidJavaClass("com.ybs1164.gugugaga.TablePackPicker"))
                plugin.CallStatic("open", ObjectName, nameof(OnPicked), dest);
#else
            onPicked?.Invoke(StandaloneFileDialog.OpenFilePanel(dialogTitle, "", "zip"));
#endif
        }

        /// <summary>안드로이드 플러그인이 부른다.</summary>
        private void OnPicked(string path)
        {
            var callback = _onPicked;
            _onPicked = null;
            callback?.Invoke(path ?? string.Empty);
        }
    }
}
