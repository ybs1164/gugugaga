using System.Collections;
using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 피해를 입었을 때 잠깐 떠올랐다 사라지는 숫자 라벨(월드 스페이스). 맞은 유닛(UnitView)의 자식이
    /// 아니라 독립된 오브젝트로 스폰된다(UnitView.ShowDamagePopup 참고) — 그래야 그 자리에서 죽어
    /// gameObject가 비활성화되어도(UnitView.Refresh) 라벨은 끊기지 않고 끝까지 애니메이션을 마친다.
    /// 구조(TextMesh + 폰트)는 Assets/Prefabs/UI/DamagePopup.prefab에 이미 만들어져 있다
    /// (UIPrefabSetup.GenerateDamagePopup) — 이 스크립트는 Play(text, color)로 받은 값을 띄우고
    /// 스스로 위치/투명도를 애니메이션한 뒤 자신을 파괴할 뿐이다.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.8f;
        [SerializeField] private float riseDistance = 0.6f;

        private TextMesh _text;

        public void Play(string text, Color color)
        {
            _text = GetComponent<TextMesh>();
            _text.text = text;
            _text.color = color;
            StartCoroutine(Animate(color));
        }

        private IEnumerator Animate(Color color)
        {
            var start = transform.position;
            var end = start + Vector3.up * riseDistance;
            float t = 0f;

            while (t < lifetime)
            {
                t += Time.deltaTime;
                float frac = Mathf.Clamp01(t / lifetime);
                transform.position = Vector3.Lerp(start, end, frac);
                color.a = 1f - frac;
                _text.color = color;
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
