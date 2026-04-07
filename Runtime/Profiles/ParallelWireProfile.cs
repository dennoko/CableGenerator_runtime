using UnityEngine;

namespace CableGeneratorRuntime
{
    /// <summary>
    /// 2本の円を並べたメガネ型断面プロファイル。
    /// </summary>
    [CreateAssetMenu(fileName = "ParallelWireProfile", menuName = "Cable Generator/Parallel Wire Profile")]
    public class ParallelWireProfile : CableProfile
    {
        [Tooltip("各ワイヤーの半径")]
        public float wireRadius = 0.02f;

        [Tooltip("2本のワイヤー中心間の距離")]
        public float spacing = 0.06f;

        [Tooltip("各円の分割数")]
        [Range(4, 32)]
        public int segments = 8;

        public override Vector2[] GetVertices()
        {
            int count = (segments + 1) * 2;
            var verts = new Vector2[count];

            float halfSpacing = spacing * 0.5f;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts[i] = new Vector2(
                    -halfSpacing + Mathf.Cos(angle) * wireRadius,
                    Mathf.Sin(angle) * wireRadius
                );
            }

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                verts[segments + 1 + i] = new Vector2(
                    halfSpacing + Mathf.Cos(angle) * wireRadius,
                    Mathf.Sin(angle) * wireRadius
                );
            }

            return verts;
        }

        public override Vector2[] GetNormals()
        {
            int count = (segments + 1) * 2;
            var norms = new Vector2[count];

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                var n = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                norms[i] = n;
            }

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                var n = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                norms[segments + 1 + i] = n;
            }

            return norms;
        }

        public override int GetVerticesPerLoop() => segments + 1;

        public override float[] GetUCoords()
        {
            int count = (segments + 1) * 2;
            var us = new float[count];

            for (int i = 0; i <= segments; i++)
                us[i] = (float)i / segments * 0.5f;

            for (int i = 0; i <= segments; i++)
                us[segments + 1 + i] = 0.5f + (float)i / segments * 0.5f;

            return us;
        }
    }
}
