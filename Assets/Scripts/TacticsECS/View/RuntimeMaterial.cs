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

        public static Material CreateColored(Color color, Texture texture = null)
        {
            Resolve();
            var mat = new Material(_shader);
            SetColor(mat, color);
            if (texture != null) SetTexture(mat, texture);
            return mat;
        }

        public static void SetColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }

        /// <summary>유닛 모델의 겉감(바디 텍스처)을 한 번 지정한다. 색상(SetColor)은 이 텍스처 위에 곱해져 팀 색으로 틴트된다.</summary>
        public static void SetTexture(Material mat, Texture texture)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        }

        /// <summary>
        /// 평면 하나로 만든 표시물(체력바 Quad 등)용. 유닛은 이동/공격 방향에 맞춰 계속 회전하는데,
        /// Quad는 한쪽 면에만 그려지는 게 기본이라 그대로 두면 카메라 반대쪽을 보는 순간 사라진다.
        /// 컬링을 꺼서 어느 방향을 보고 있어도 항상 보이게 한다.
        /// </summary>
        public static void SetDoubleSided(Material mat)
        {
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        }
    }
}
