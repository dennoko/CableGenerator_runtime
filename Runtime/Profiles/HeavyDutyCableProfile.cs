using UnityEngine;

namespace CableGeneratorRuntime
{
    /// <summary>
    /// 太丸断面プロファイル（オプションで凹凸付き）。
    /// </summary>
    [CreateAssetMenu(fileName = "HeavyDutyCableProfile", menuName = "Cable Generator/Heavy Duty Cable Profile")]
    public class HeavyDutyCableProfile : CableProfile
    {
        [Tooltip("ケーブルの半径")]
        public float radius = 0.05f;

        [Tooltip("円周の分割数")]
        [Range(6, 64)]
        public int segments = 16;

        [Tooltip("凹凸のリブの数（0で滑らかな円）")]
        [Range(0, 24)]
        public int ribCount = 0;

        [Tooltip("リブの深さ（半径に対する割合）")]
        [Range(0f, 0.3f)]
        public float ribDepth = 0.1f;

        public override Vector2[] GetVertices()
        {
            int count = segments + 1;
            var verts = new Vector2[count];

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float r = radius;

                if (ribCount > 0)
                {
                    float ribPhase = Mathf.Sin(angle * ribCount);
                    r -= ribDepth * radius * (ribPhase * 0.5f + 0.5f);
                }

                verts[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            }

            return verts;
        }

        public override Vector2[] GetNormals()
        {
            int count = segments + 1;
            var norms = new Vector2[count];

            if (ribCount > 0)
            {
                var verts = GetVertices();
                for (int i = 0; i < count; i++)
                {
                    norms[i] = verts[i].normalized;
                }
            }
            else
            {
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    norms[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }

            return norms;
        }

        public override float[] GetUCoords()
        {
            int count = segments + 1;
            var us = new float[count];

            for (int i = 0; i <= segments; i++)
                us[i] = (float)i / segments;

            return us;
        }
    }
}
