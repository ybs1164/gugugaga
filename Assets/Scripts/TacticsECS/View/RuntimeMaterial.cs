using UnityEngine;

namespace TacticsECS
{
    /// <summary>
    /// 런타임에 프리미티브용 단색 머티리얼을 만들어주는 헬퍼.
    /// URP(Lit, "_BaseColor")와 Built-in(Standard, "_Color") 둘 다 대응한다.
    /// </summary>
    public static class RuntimeMaterial
    {
        private static Shader _shader;
        private static bool _resolved;

        private static void Resolve()
        {
            if (_resolved) return;
            _shader = Shader.Find("Universal Render Pipeline/Lit");
            if (_shader == null) _shader = Shader.Find("Standard");
            if (_shader == null) _shader = Shader.Find("Sprites/Default");
            _resolved = true;
        }

        public static Material CreateColored(Color color)
        {
            Resolve();
            var mat = new Material(_shader);
            SetColor(mat, color);
            return mat;
        }

        public static void SetColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }
    }
}
