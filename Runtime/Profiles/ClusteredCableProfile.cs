using UnityEngine;

namespace CableGeneratorRuntime
{
    /// <summary>
    /// 複数のケーブルを束ねた断面プロファイル。
    /// 中心に1本、周囲に同心円状に配置し、揺らぎを加えて自然な束の見た目を再現する。
    /// </summary>
    [CreateAssetMenu(fileName = "ClusteredCableProfile", menuName = "Cable Generator/Clustered Cable Profile")]
    public class ClusteredCableProfile : CableProfile
    {
        [Tooltip("ケーブルの本数")]
        [Range(1, 37)]
        public int wireCount = 7;

        [Tooltip("各ケーブルの半径")]
        public float wireRadius = 0.008f;

        [Tooltip("各ケーブルの円周分割数")]
        [Range(4, 32)]
        public int segments = 6;

        [Tooltip("位置の揺らぎの強さ（0=きっちり配置、1=大きく崩れる）")]
        [Range(0f, 1f)]
        public float jitter = 0.3f;

        [Tooltip("半径の揺らぎの強さ（0=均一、1=最大±50%のばらつき）")]
        [Range(0f, 1f)]
        public float radiusJitter = 0.0f;

        [Tooltip("揺らぎのシード値（同じ値なら同じ配置になる）")]
        public int seed = 42;

        // キャッシュ
        Vector2[] cachedCenters;
        float[] cachedRadii;
        int cachedHash;

        void OnValidate()
        {
            InvalidateCache();
        }

        void InvalidateCache()
        {
            cachedCenters = null;
            cachedRadii = null;
            cachedHash = 0;
        }

        int ComputeParamHash()
        {
            unchecked
            {
                int h = wireCount * 397;
                h = h * 397 + wireRadius.GetHashCode();
                h = h * 397 + segments;
                h = h * 397 + jitter.GetHashCode();
                h = h * 397 + radiusJitter.GetHashCode();
                h = h * 397 + seed;
                return h;
            }
        }

        Vector2[] GetCachedCenters()
        {
            int h = ComputeParamHash();
            if (cachedCenters == null || cachedHash != h)
            {
                cachedCenters = ComputeWireCenters();
                cachedRadii = ComputeWireRadii(cachedCenters.Length);
                cachedHash = h;
            }
            return cachedCenters;
        }

        float[] GetCachedRadii()
        {
            GetCachedCenters(); // ensure cache is populated
            return cachedRadii;
        }

        public override Vector2[] GetVertices()
        {
            var centers = GetCachedCenters();
            var radii = GetCachedRadii();
            int vertsPerWire = segments + 1;
            int totalVerts = vertsPerWire * centers.Length;
            var verts = new Vector2[totalVerts];

            for (int w = 0; w < centers.Length; w++)
            {
                int baseIdx = w * vertsPerWire;
                float r = radii[w];
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (float)i / segments * Mathf.PI * 2f;
                    verts[baseIdx + i] = new Vector2(
                        centers[w].x + Mathf.Cos(angle) * r,
                        centers[w].y + Mathf.Sin(angle) * r
                    );
                }
            }

            return verts;
        }

        public override Vector2[] GetNormals()
        {
            var centers = GetCachedCenters();
            int vertsPerWire = segments + 1;
            int totalVerts = vertsPerWire * centers.Length;
            var norms = new Vector2[totalVerts];

            for (int w = 0; w < centers.Length; w++)
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
            var centers = GetCachedCenters();
            int vertsPerWire = segments + 1;
            int totalVerts = vertsPerWire * centers.Length;
            var us = new float[totalVerts];

            float uPerWire = 1f / centers.Length;

            for (int w = 0; w < centers.Length; w++)
            {
                int baseIdx = w * vertsPerWire;
                float uStart = uPerWire * w;
                for (int i = 0; i <= segments; i++)
                    us[baseIdx + i] = uStart + (float)i / segments * uPerWire;
            }

            return us;
        }

        /// <summary>
        /// 各ケーブルの半径を計算する。radiusJitter > 0 なら個別にばらつきを与える。
        /// </summary>
        float[] ComputeWireRadii(int count)
        {
            var radii = new float[count];

            if (radiusJitter <= 0f)
            {
                for (int i = 0; i < count; i++)
                    radii[i] = wireRadius;
                return radii;
            }

            // 位置揺らぎとは別のシード空間を使う（seed + 7919）
            var rng = new System.Random(seed + 7919);
            float maxVariation = wireRadius * radiusJitter * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float offset = ((float)rng.NextDouble() * 2f - 1f) * maxVariation;
                radii[i] = wireRadius + offset;
            }

            return radii;
        }

        /// <summary>
        /// 同心円パッキングで各ケーブルの中心座標を計算する。
        /// リング0 = 中心 (1本)、リング1 = 6本、リング2 = 12本、リング3 = 18本 ...
        /// </summary>
        Vector2[] ComputeWireCenters()
        {
            int count = Mathf.Max(1, wireCount);
            var centers = new Vector2[count];

            // 隣接ケーブル中心間の距離
            float pitch = wireRadius * 2.1f;

            // 同心円パッキングで配置
            int placed = 0;

            // 中心の1本
            if (placed < count)
                centers[placed++] = Vector2.zero;

            // リング1, 2, 3 ...
            int ring = 1;
            while (placed < count)
            {
                int spotsInRing = ring * 6;
                float ringRadius = pitch * ring;

                for (int i = 0; i < spotsInRing && placed < count; i++)
                {
                    float angle = (float)i / spotsInRing * Mathf.PI * 2f;
                    centers[placed++] = new Vector2(
                        Mathf.Cos(angle) * ringRadius,
                        Mathf.Sin(angle) * ringRadius
                    );
                }

                ring++;
            }

            // 揺らぎを適用（中心のケーブルは固定）
            if (jitter > 0f)
            {
                float maxOffset = wireRadius * jitter * 0.5f;
                var rng = new System.Random(seed);

                for (int i = 1; i < count; i++)
                {
                    float ox = ((float)rng.NextDouble() * 2f - 1f) * maxOffset;
                    float oy = ((float)rng.NextDouble() * 2f - 1f) * maxOffset;
                    centers[i] += new Vector2(ox, oy);
                }
            }

            // 全体の重心を原点に補正
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < count; i++)
                centroid += centers[i];
            centroid /= count;
            for (int i = 0; i < count; i++)
                centers[i] -= centroid;

            return centers;
        }
    }
}
