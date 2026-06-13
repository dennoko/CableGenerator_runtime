using UnityEngine;

namespace CableGeneratorRuntime
{
    /// <summary>
    /// スパイラルチューブ（ケーブル結束用の螺旋巻きチューブ）の断面プロファイル。
    /// 厚みを持つ C 字（環状セクター）断面を、スプライン距離に応じてねじることで、
    /// 隙間が螺旋状に巻かれた見た目を再現する。ねじれ量で螺旋のピッチを調整する。
    /// </summary>
    [CreateAssetMenu(fileName = "SpiralTubeProfile", menuName = "Cable Generator/Spiral Tube Profile")]
    public class SpiralTubeProfile : CableProfile, ITwistedProfile
    {
        [Tooltip("内半径（結束するケーブル束の半径相当）")]
        public float innerRadius = 0.02f;

        [Tooltip("チューブの肉厚（壁の厚み）")]
        public float wallThickness = 0.003f;

        [Tooltip("巻きの隙間角度（度）。大きいほど開いた螺旋になる。0で隙間なしの閉じた筒")]
        [Range(0f, 350f)]
        public float gapAngleDegrees = 90f;

        [Tooltip("円弧（C字）の分割数。大きいほど断面が滑らかになる")]
        [Range(3, 64)]
        public int segments = 24;

        [Tooltip("1メートルあたりの巻き数（螺旋のピッチ。負値で逆巻き）")]
        [Range(-20f, 20f)]
        public float twistTurnsPerMeter = 3f;

        public float GetTwistTurnsPerMeter() => twistTurnsPerMeter;

        public override Vector2[] GetVertices()
        {
            BuildLoop(out var verts, out _);
            return verts;
        }

        public override Vector2[] GetNormals()
        {
            BuildLoop(out _, out var norms);
            return norms;
        }

        public override float[] GetUCoords()
        {
            BuildLoop(out var verts, out _);
            var us = new float[verts.Length];

            float totalPerimeter = 0f;
            for (int i = 1; i < verts.Length; i++)
                totalPerimeter += Vector2.Distance(verts[i], verts[i - 1]);

            // 全周長ゼロ（退化）を避ける。
            if (totalPerimeter < 1e-6f) return us;

            float cumulative = 0f;
            for (int i = 1; i < verts.Length; i++)
            {
                cumulative += Vector2.Distance(verts[i], verts[i - 1]);
                us[i] = cumulative / totalPerimeter;
            }

            return us;
        }

        /// <summary>
        /// C 字断面を 1 本の閉じた頂点ループとして構築する。
        /// 外周アーク → 端キャップ → 内周アーク → 端キャップ の順に辿ることで、
        /// 単一の単純多角形（反時計回り）となり面の向きが外向きで一致する。
        /// キャップ部はコーナー頂点を複製し、外周/内周と異なる法線を与えてエッジを立てる。
        /// </summary>
        void BuildLoop(out Vector2[] verts, out Vector2[] norms)
        {
            int n = Mathf.Max(3, segments);
            float gap = Mathf.Clamp(gapAngleDegrees, 0f, 350f) * Mathf.Deg2Rad;
            float rIn = Mathf.Max(1e-4f, innerRadius);
            float rOut = rIn + Mathf.Max(1e-4f, wallThickness);

            float start = gap * 0.5f;
            float end = Mathf.PI * 2f - gap * 0.5f;
            float cover = end - start; // = 2π - gap（材料が存在する角度範囲）

            // 外周(n+1) + 端キャップ2 + 内周(n+1) + 端キャップ2 + 閉じ1
            int count = (n + 1) + 2 + (n + 1) + 2 + 1;
            verts = new Vector2[count];
            norms = new Vector2[count];
            int idx = 0;

            // 外周アーク: start → end（角度増加 = 外向き法線で正しい巻き順）
            for (int i = 0; i <= n; i++)
            {
                float a = start + cover * (i / (float)n);
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts[idx] = new Vector2(c * rOut, s * rOut);
                norms[idx] = new Vector2(c, s);
                idx++;
            }

            // 端キャップ（end 側）: 法線は隙間に向かう接線方向（+tangent）
            {
                float c = Mathf.Cos(end), s = Mathf.Sin(end);
                Vector2 capN = new Vector2(-s, c);
                verts[idx] = new Vector2(c * rOut, s * rOut); norms[idx] = capN; idx++;
                verts[idx] = new Vector2(c * rIn, s * rIn); norms[idx] = capN; idx++;
            }

            // 内周アーク: end → start（角度減少 = 内向き法線・穴側の正しい巻き順）
            for (int i = 0; i <= n; i++)
            {
                float a = end - cover * (i / (float)n);
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts[idx] = new Vector2(c * rIn, s * rIn);
                norms[idx] = new Vector2(-c, -s);
                idx++;
            }

            // 端キャップ（start 側）: 法線は隙間に向かう接線方向（-tangent）
            {
                float c = Mathf.Cos(start), s = Mathf.Sin(start);
                Vector2 capN = new Vector2(s, -c);
                verts[idx] = new Vector2(c * rIn, s * rIn); norms[idx] = capN; idx++;
                verts[idx] = new Vector2(c * rOut, s * rOut); norms[idx] = capN; idx++;
            }

            // 先頭（外周 start）へ閉じる。
            verts[idx] = verts[0];
            norms[idx] = norms[0];
        }
    }
}
