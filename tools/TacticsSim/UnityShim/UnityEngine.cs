using System;
using System.IO;

// 시뮬레이터 전용 UnityEngine 대체: 게임 로직이 쓰는 만큼만 구현한다(동작은 Unity와 같게).
namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }

    public class Object { }
    public class TextAsset : Object { public string text; public byte[] bytes; }

    public static class Resources
    {
        /// <summary>Assets/Resources 폴더(시뮬레이터 시작 때 정한다).</summary>
        public static string Root = "Assets/Resources";
        public static T Load<T>(string path) where T : Object
        {
            if (typeof(T) != typeof(TextAsset)) return null;
            foreach (var ext in new[] { ".csv", ".txt", ".bytes", ".json" })
            {
                var file = Path.Combine(Root, path + ext);
                if (File.Exists(file)) return new TextAsset { text = File.ReadAllText(file), bytes = File.ReadAllBytes(file) } as T;
            }
            return null;
        }
    }

    public static class Application { public static string version = "sim"; public static bool isPlaying => false; }

    public static class Debug
    {
        public static bool Quiet = true;
        public static void Log(object m) { if (!Quiet) Console.Error.WriteLine(m); }
        public static void LogWarning(object m) { if (!Quiet) Console.Error.WriteLine("W " + m); }
        public static void LogError(object m) => Console.Error.WriteLine("E " + m);
    }

    public static class Mathf
    {
        public static int Abs(int v) => Math.Abs(v);
        public static float Abs(float v) => Math.Abs(v);
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(params int[] v) { int m = v[0]; foreach (var x in v) if (x > m) m = x; return m; }
        public static float Max(params float[] v) { float m = v[0]; foreach (var x in v) if (x > m) m = x; return m; }
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(params int[] v) { int m = v[0]; foreach (var x in v) if (x < m) m = x; return m; }
        public static float Min(params float[] v) { float m = v[0]; foreach (var x in v) if (x < m) m = x; return m; }
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static int RoundToInt(float f) => (int)Math.Round(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Exp(float f) => (float)Math.Exp(f);
        public static float Log(float f) => (float)Math.Log(f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8f);

        // 고전 Perlin(0..1 범위). Unity의 Mathf.PerlinNoise와 비트 단위로 같지는 않아 맵 모양이 Unity와 조금 다를 수 있다
        // (학습은 맵을 무작위로 만들므로 상관없다).
        private static readonly int[] P = BuildPermutation();
        private static int[] BuildPermutation()
        {
            int[] p = { 151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,190,6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,153,101,155,167,43,172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241,81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,157,184,84,204,176,115,121,50,45,127,4,150,254,138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180 };
            var r = new int[512];
            for (int i = 0; i < 512; i++) r[i] = p[i & 255];
            return r;
        }
        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Grad(int h, float x, float y) { h &= 7; float u = h < 4 ? x : y, v = h < 4 ? y : x; return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -2f * v : 2f * v); }
        private static float Mix(float a, float b, float t) => a + (b - a) * t;
        public static float PerlinNoise(float x, float y)
        {
            int xi = (int)Math.Floor(x) & 255, yi = (int)Math.Floor(y) & 255;
            x -= (float)Math.Floor(x); y -= (float)Math.Floor(y);
            float u = Fade(x), v = Fade(y);
            int a = P[xi] + yi, b = P[xi + 1] + yi;
            float n = Mix(Mix(Grad(P[a], x, y), Grad(P[b], x - 1, y), u), Mix(Grad(P[a + 1], x, y - 1), Grad(P[b + 1], x - 1, y - 1), u), v);
            return Clamp01(n * 0.5f + 0.5f);
        }
    }

    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => new Vector2Int(0, 0);
        public static Vector2Int one => new Vector2Int(1, 1);
        public static Vector2Int up => new Vector2Int(0, 1);
        public static Vector2Int down => new Vector2Int(0, -1);
        public static Vector2Int left => new Vector2Int(-1, 0);
        public static Vector2Int right => new Vector2Int(1, 0);
        public int sqrMagnitude => x * x + y * y;
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new Vector2Int(a.x - b.x, a.y - b.y);
        public static Vector2Int operator -(Vector2Int a) => new Vector2Int(-a.x, -a.y);
        public static Vector2Int operator *(Vector2Int a, int k) => new Vector2Int(a.x * k, a.y * k);
        public static Vector2Int operator *(int k, Vector2Int a) => new Vector2Int(a.x * k, a.y * k);
        public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);
        public static implicit operator Vector2(Vector2Int v) => new Vector2(v.x, v.y);
        public bool Equals(Vector2Int o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is Vector2Int v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
        public override string ToString() => $"({x}, {y})";
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float k) => new Vector2(a.x * k, a.y * k);
        public static Vector2 operator /(Vector2 a, float k) => new Vector2(a.x / k, a.y / k);
        public override string ToString() => $"({x:F2}, {y:F2})";
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float k) => new Vector3(a.x * k, a.y * k, a.z * k);
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color magenta => new Color(1, 0, 1);
    }

    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string s, out Color c)
        {
            c = default;
            if (string.IsNullOrEmpty(s) || s[0] != '#' || (s.Length != 7 && s.Length != 9)) return false;
            try
            {
                int V(int i) => Convert.ToInt32(s.Substring(i, 2), 16);
                c = new Color(V(1) / 255f, V(3) / 255f, V(5) / 255f, s.Length == 9 ? V(7) / 255f : 1f);
                return true;
            }
            catch { return false; }
        }
    }
}
