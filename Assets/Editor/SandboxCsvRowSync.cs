using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TacticsECS.EditorTools
{
    /// <summary>
    /// 샌드박스 HUD 프리팹(Assets/Prefabs/UI/SandboxHud.prefab)의 CSV 목록에 GameDataLoader.SandboxCsvPaths 중 줄이 없는 표를 더한다 —
    /// 마지막 줄을 복제해 그 아래(줄 간격 그대로)에 놓고 목록 높이를 늘린다. 글자는 SandboxHud.SetCsvSources가 채운다. 표를 새로 만들었을 때 한 번 돌린다.
    /// 사용: unity run . -- -logFile Logs/sandbox_rows.log -executeMethod TacticsECS.EditorTools.SandboxCsvRowSync.Run
    /// </summary>
    public static class SandboxCsvRowSync
    {
        private const string PrefabPath = "Assets/Prefabs/UI/SandboxHud.prefab";

        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var content = (RectTransform)root.transform.Find("Canvas/CsvFiles/Viewport/Content");
                var rows = content.Cast<RectTransform>().OrderByDescending(r => r.anchoredPosition.y).ToList();
                float step = rows.Count > 1 ? rows[rows.Count - 2].anchoredPosition.y - rows[rows.Count - 1].anchoredPosition.y : 40f;
                int added = 0;
                foreach (var key in GameDataLoader.SandboxCsvPaths)
                {
                    string name = key.Replace('/', '_');
                    if (content.Find(name) != null) continue;
                    var last = content.Cast<RectTransform>().OrderBy(r => r.anchoredPosition.y).First();
                    var row = (RectTransform)Object.Instantiate(last.gameObject, content).transform;
                    row.name = name;
                    row.anchoredPosition = last.anchoredPosition - new Vector2(0f, step);
                    content.sizeDelta += new Vector2(0f, step);
                    added++;
                }
                if (added > 0) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[SandboxCsvRowSync] added {added} row(s)");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
