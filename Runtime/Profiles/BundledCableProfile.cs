using UnityEngine;

namespace CableGeneratorRuntime
{
    /// <summary>
    /// 任意の本数の丸ケーブルを横一列に並べた断面プロファイル。
    /// データセンターなどで整然と並べられたケーブル束の見た目を再現する。
    /// </summary>
    [CreateAssetMenu(fileName = "BundledCableProfile", menuName = "Cable Generator/Bundled Cable Profile")]
    public class BundledCableProfile : CableProfile
    {
        [Tooltip("ケーブルの本数")]
        [Range(1, 24)]
        public int wireCount = 5;

        [Tooltip("各ケーブルの半径")]
        public float wireRadius = 0.01f;

        [Tooltip("隣接するケーブル間の隙間")]
        public float gap = 0.002f;

        [Tooltip("各ケーブルの円周分割数")]
        [Range(4, 32)]
        public int segments = 8;

        float WirePitch => wireRadius * 2f + gap;

        float BundleHalfWidth => WirePitch * (wireCount - 1) * 0.5f;

        public override Vector2[] GetVertices()
        {
            int vertsPerWire = segments + 1;
            int totalVerts = vertsPerWire * wireCount;
            var verts = new Vector2[totalVerts];

            float halfWidth = BundleHalfWidth;
            float pitch = WirePitch;

            for (int w = 0; w < wireCount; w++)
            {
                float cx = -halfWidth + pitch * w;
                int baseIdx = w * vertsPerWire;

                for (int i = 0; i <= segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    verts[baseIdx + i] = new Vector2(
                        cx + Mathf.Cos(angle) * wireRadius,
                        Mathf.Sin(angle) * wireRadius
                    );
                }
            }

            return verts;
        }

        public override Vector2[] GetNormals()
        {
            int vertsPerWire = segments + 1;
            int totalVerts = vertsPerWire * wireCount;
            var norms = new Vector2[totalVerts];

            for (int w = 0; w < wireCount; w++)
            {
                int baseIdx = w * vertsPerWire;

                for (int i = 0; i <= segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    norms[baseIdx + i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }

            return norms;
        }

        public override int GetVerticesPerLoop() => segments + 1;

        public override float[] GetUCoords()
        {
            int vertsPerWire = segments + 1;
            int totalVerts = vertsPerWire * wireCount;
            var us = new float[totalVerts];

            float uPerWire = 1f / wireCount;

            for (int w = 0; w < wireCount; w++)
            {
                int baseIdx = w * vertsPerWire;
                float uStart = uPerWire * w;

                for (int i = 0; i <= segments; i++)
                    us[baseIdx + i] = uStart + (float)i / segments * uPerWire;
            }

            return us;
        }
    }
}
