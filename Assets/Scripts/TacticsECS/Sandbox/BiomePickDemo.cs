using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TacticsECS
{
    /// <summary>
    /// 빌드 실행 인자 <c>-biomeDemo &lt;폴더&gt;</c>로만 켜지는 Sandbox 바이옴 선택 시연. 바이옴·종족 버튼 → 바이옴 드롭다운을 열고 목록 줄(Toggle)을
    /// 사람이 누르는 것과 같은 경로로 눌러 바이옴 두 개를 고른 뒤 "지형 생성"을 누르고, 단계마다 화면을 PNG로 남기고 끝낸다.
    /// 인자가 없으면 아무것도 만들지 않는다.
    /// </summary>
    public class BiomePickDemo : MonoBehaviour
    {
        private string _folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-biomeDemo");
            if (i < 0 || i + 1 >= args.Length) return;
            var go = new GameObject("BiomePickDemo");
            DontDestroyOnLoad(go);
            go.AddComponent<BiomePickDemo>()._folder = args[i + 1];
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(_folder);
            SandboxHud hud = null;
            while (hud == null) { yield return null; hud = FindFirstObjectByType<SandboxHud>(); }
            yield return new WaitForSeconds(1f);

            var canvas = hud.transform.Find("Canvas");
            var tab = canvas.Find("BiomeTab").gameObject;
            if (!tab.activeSelf) canvas.Find("Toolbar/바이옴종족Button").GetComponent<Button>().onClick.Invoke();
            var dropdown = tab.transform.Find("BiomeDropdown").GetComponent<Dropdown>();

            yield return Shot("01_tab_auto");
            dropdown.Show();
            yield return Shot("02_list_open");
            yield return PickRow(dropdown, 2);   // 첫 바이옴
            dropdown.Show();
            yield return Shot("03_first_picked");
            yield return PickRow(dropdown, 4);   // 셋째 바이옴
            yield return Shot("04_caption_two");
            dropdown.Show();
            yield return Shot("05_list_two_picked");
            dropdown.Hide();
            yield return new WaitForSeconds(0.5f);
            canvas.Find("Toolbar/지형생성Button").GetComponent<Button>().onClick.Invoke();
            yield return Shot("06_generated");
            Application.Quit();
        }

        /// <summary>펼친 목록에서 option 번째 줄의 Toggle을 켠다 — 마우스로 그 줄을 누른 것과 같다(Dropdown.OnSelectItem → 값 변경 → 목록 닫힘).</summary>
        private IEnumerator PickRow(Dropdown dropdown, int option)
        {
            dropdown.Show();
            yield return new WaitForSeconds(0.4f);
            var list = dropdown.transform.Find("Dropdown List");
            var rows = list.GetComponentsInChildren<Toggle>(false).Where(t => t.gameObject.activeInHierarchy).ToList();
            rows[option].isOn = true; // 바이옴 줄은 선택값(0번 요약 줄)이 아니라 늘 꺼져 있다
            yield return new WaitForSeconds(0.6f);
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForSeconds(0.6f);
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"));
            yield return null;
            yield return null;
        }
    }
}
