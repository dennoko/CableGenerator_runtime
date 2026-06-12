using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace CableGeneratorRuntime
{
    [RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
    [ExecuteAlways]
    public class CableGenerator : MonoBehaviour
    {
        [Header("断面プロファイル")]
        public CableProfile profile;

        [Header("メッシュ設定")]
        [Tooltip("スプライン方向の分割数（滑らかさ）")]
        [Range(2, 256)]
        public int resolution = 32;

        [Tooltip("UVタイリングスケール")]
        public float uvTiling = 1f;

        SplineContainer splineContainer;
        MeshFilter meshFilter;
        Mesh generatedMesh;

        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<int> tris = new List<int>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Vector2> uv2s = new List<Vector2>();

        void OnEnable()
        {
            splineContainer = GetComponent<SplineContainer>();
            meshFilter = GetComponent<MeshFilter>();

            Spline.Changed += OnSplineChanged;

            RebuildMesh();
        }

        void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
        }

        void OnDestroy()
        {
            if (generatedMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(generatedMesh);
                else
                    DestroyImmediate(generatedMesh);
                generatedMesh = null;
            }
        }

        void OnValidate()
        {
            if (splineContainer != null)
                RebuildMesh();
        }

        void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            if (splineContainer == null) return;

            for (int i = 0; i < splineContainer.Splines.Count; i++)
            {
                if (splineContainer.Splines[i] == spline)
                {
                    RebuildMesh();
                    return;
                }
            }
        }

        public void RebuildMesh()
        {
            if (splineContainer == null || profile == null) return;
            if (splineContainer.Splines.Count == 0) return;

            var spline = splineContainer.Splines[0];
            if (spline.Count < 2) return;

            if (generatedMesh == null)
            {
                generatedMesh = new Mesh();
                generatedMesh.name = "CableGeneratorMesh";
                generatedMesh.MarkDynamic();
            }

            generatedMesh.Clear();
            verts.Clear();
            tris.Clear();
            normals.Clear();
            uvs.Clear();
            uv2s.Clear();

            Vector2[] profileVerts = profile.GetVertices();
            Vector2[] profileNormals = profile.GetNormals();
            float[] profileUs = profile.GetUCoords();
            int profileVertCount = profileVerts.Length;
            int vertsPerLoop = profile.GetVerticesPerLoop();

            if (profileVertCount == 0) return;

            float splineLength = SplineUtility.CalculateLength(spline, splineContainer.transform.localToWorldMatrix);
            if (splineLength < 0.0001f) return;

            // --- UV1 パッキングレイアウトを事前計算 ---
            float[] perimeters = profile.GetPerimeters();
            int loopCount = profileVertCount / vertsPerLoop;
            var uv1Layout = CalculateUV1Layout(perimeters, loopCount, splineLength);

            // --- 頂点生成 ---
            Vector3 prevUp = Vector3.up;

            // ねじれ対応プロファイルはスプライン距離に比例して断面を回転させる
            float twistTurnsPerMeter = (profile is ITwistedProfile twisted)
                ? twisted.GetTwistTurnsPerMeter() : 0f;

            for (int i = 0; i <= resolution; i++)
            {
                float t = (float)i / resolution;

                SplineUtility.Evaluate(spline, t, out float3 pos, out float3 tangent, out float3 up);

                Vector3 position = splineContainer.transform.TransformPoint((Vector3)pos);
                Vector3 tan = splineContainer.transform.TransformDirection(math.normalize(tangent));
                Vector3 upDir = splineContainer.transform.TransformDirection(math.normalize(up));

                if (tan.sqrMagnitude < 0.0001f)
                    tan = Vector3.forward;

                if (Vector3.Dot(upDir, upDir) < 0.0001f)
                    upDir = prevUp;

                Vector3 right = Vector3.Cross(upDir, tan).normalized;
                if (right.sqrMagnitude < 0.0001f)
                {
                    right = Vector3.Cross(Vector3.up, tan).normalized;
                    if (right.sqrMagnitude < 0.0001f)
                        right = Vector3.Cross(Vector3.right, tan).normalized;
                }

                upDir = Vector3.Cross(tan, right).normalized;
                prevUp = upDir;

                Vector3 localPos = splineContainer.transform.InverseTransformPoint(position);
                Vector3 localRight = splineContainer.transform.InverseTransformDirection(right);
                Vector3 localUp = splineContainer.transform.InverseTransformDirection(upDir);

                float v = (splineLength * t) * uvTiling;

                float twistAngle = twistTurnsPerMeter * (Mathf.PI * 2f) * splineLength * t;
                float twistCos = Mathf.Cos(twistAngle);
                float twistSin = Mathf.Sin(twistAngle);

                for (int j = 0; j < profileVertCount; j++)
                {
                    float px = profileVerts[j].x * twistCos - profileVerts[j].y * twistSin;
                    float py = profileVerts[j].x * twistSin + profileVerts[j].y * twistCos;

                    Vector3 vertPos = localPos
                        + localRight * px
                        + localUp * py;
                    verts.Add(vertPos);

                    float nx = profileNormals[j].x * twistCos - profileNormals[j].y * twistSin;
                    float ny = profileNormals[j].x * twistSin + profileNormals[j].y * twistCos;

                    Vector3 norm = (localRight * nx + localUp * ny).normalized;
                    normals.Add(norm);

                    uvs.Add(new Vector2(profileUs[j], v));

                    // UV1: パッキングされたライトマップUV
                    int loopIdx = j / vertsPerLoop;
                    int localJ = j % vertsPerLoop;
                    // 閉じ頂点(末尾)はUV1上で先頭と重ならないよう、
                    // 1つ手前の外側にわずかにオフセット
                    float localU;
                    if (localJ == vertsPerLoop - 1)
                        localU = (float)(localJ - 1) / (vertsPerLoop - 1) + 0.5f / (vertsPerLoop - 1);
                    else
                        localU = (float)localJ / (vertsPerLoop - 1);
                    uv2s.Add(CalculateUV1(uv1Layout, loopIdx, localU, t, i));
                }
            }

            // --- 三角形インデックス生成（ループ境界をスキップ）---
            for (int i = 0; i < resolution; i++)
            {
                int ringStart = i * profileVertCount;
                int nextRingStart = (i + 1) * profileVertCount;

                for (int j = 0; j < profileVertCount - 1; j++)
                {
                    if ((j + 1) % vertsPerLoop == 0)
                        continue;

                    int a = ringStart + j;
                    int b = ringStart + j + 1;
                    int c = nextRingStart + j;
                    int d = nextRingStart + j + 1;

                    tris.Add(a);
                    tris.Add(b);
                    tris.Add(c);

                    tris.Add(b);
                    tris.Add(d);
                    tris.Add(c);
                }
            }

            generatedMesh.SetVertices(verts);
            generatedMesh.SetNormals(normals);
            generatedMesh.SetUVs(0, uvs);
            generatedMesh.SetUVs(1, uv2s);
            generatedMesh.SetTriangles(tris, 0);
            generatedMesh.RecalculateBounds();

            meshFilter.sharedMesh = generatedMesh;
        }

        // ====== UV1 パッキング ======

        struct StripInfo
        {
            public int loopIndex;
            public int splitIndex;
            public int splitCount;
            public float realWidth;   // 実寸の周長
            public float realHeight;  // 実寸の短冊高さ (splineLength / splits)
            // パッキング後の位置
            public float packU;
            public float packV;
            public float packW;
            public float packH;
        }

        struct UV1Layout
        {
            public StripInfo[] strips;
            public int[] loopStripStart;  // loopIndex → strips配列中の開始インデックス
            public int[] loopSplitCount;  // loopIndex → 分割数
        }

        const float AspectThreshold = 4f;
        const float Padding = 0.01f;

        UV1Layout CalculateUV1Layout(float[] perimeters, int loopCount, float splineLength)
        {
            var layout = new UV1Layout();
            layout.loopStripStart = new int[loopCount];
            layout.loopSplitCount = new int[loopCount];

            // ステップ1: 各ループの分割数を決定し、短冊リストを作る
            var stripList = new List<StripInfo>();

            for (int loop = 0; loop < loopCount; loop++)
            {
                float perimeter = (loop < perimeters.Length) ? perimeters[loop] : 0.01f;
                if (perimeter < 0.0001f) perimeter = 0.0001f;

                // --- 現状の課題 ---
                // メッシュ生成時に頂点を共有しているため、UV空間でアイランドを分割（splits > 1）すると
                // 境界部分の三角形がUV空間上で島を跨いでしまい、ストレッチや重なりが発生する。
                // 解決には分割点での頂点複製が必要。
                // いったん無効化（1本1アイランド）として、面積比に応じた重なりを回避する。
                int splits = 1; 
                /*
                float ratio = splineLength / perimeter;
                if (ratio > AspectThreshold)
                    splits = Mathf.CeilToInt(Mathf.Sqrt(ratio));
                */

                layout.loopStripStart[loop] = stripList.Count;
                layout.loopSplitCount[loop] = splits;

                float stripHeight = splineLength / splits;

                for (int s = 0; s < splits; s++)
                {
                    stripList.Add(new StripInfo
                    {
                        loopIndex = loop,
                        splitIndex = s,
                        splitCount = splits,
                        realWidth = perimeter,
                        realHeight = stripHeight,
                    });
                }
            }

            // ステップ2: 棚パッキング（Shelf Packing）
            // 全短冊の実寸から正規化サイズを決定
            // まず全短冊の合計面積からスケールの目安を計算
            var strips = stripList.ToArray();

            if (strips.Length == 0)
            {
                layout.strips = strips;
                return layout;
            }

            // 短冊を高さ降順でソート（元のインデックスを保持）
            var sortedIndices = new int[strips.Length];
            for (int i = 0; i < sortedIndices.Length; i++) sortedIndices[i] = i;
            System.Array.Sort(sortedIndices, (a, b) =>
                strips[b].realHeight.CompareTo(strips[a].realHeight));

            // 全短冊を統一スケールで正規化するために最大寸法を求める
            float totalArea = 0f;
            for (int i = 0; i < strips.Length; i++)
                totalArea += strips[i].realWidth * strips[i].realHeight;

            // UV空間を1×1に収めるためのスケール: sqrt(totalArea) を基準にする
            // パディング分も考慮
            float totalPadW = Padding * (strips.Length + 1);
            float scaleFactor = 1f / Mathf.Sqrt(totalArea);

            // 初期スケールで棚パッキングを試行し、収まるまでスケールを調整
            float bestScale = scaleFactor;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                if (TryShelfPack(strips, sortedIndices, bestScale, Padding, out _, out _))
                    break;
                bestScale *= 0.85f;
            }

            // 最終パッキング
            TryShelfPack(strips, sortedIndices, bestScale, Padding, out float usedW, out float usedH);

            // 0〜1空間を最大限に活用するようリスケール
            float fillScale = 1f;
            if (usedW > 0.0001f && usedH > 0.0001f)
                fillScale = Mathf.Min((1f - Padding) / usedW, (1f - Padding) / usedH);

            for (int i = 0; i < strips.Length; i++)
            {
                var s = strips[i];
                s.packU = s.packU * fillScale + Padding * 0.5f;
                s.packV = s.packV * fillScale + Padding * 0.5f;
                s.packW *= fillScale;
                s.packH *= fillScale;
                strips[i] = s;
            }

            layout.strips = strips;
            return layout;
        }

        bool TryShelfPack(StripInfo[] strips, int[] sortedIndices, float scale, float pad,
            out float usedWidth, out float usedHeight)
        {
            float cursorU = pad;
            float cursorV = pad;
            float shelfHeight = 0f;
            float maxU = 0f;
            float maxV = 0f;

            for (int si = 0; si < sortedIndices.Length; si++)
            {
                int idx = sortedIndices[si];
                float w = strips[idx].realWidth * scale;
                float h = strips[idx].realHeight * scale;

                // 現在の棚に収まらなければ次の段へ
                if (cursorU + w + pad > 1f)
                {
                    cursorU = pad;
                    cursorV += shelfHeight + pad;
                    shelfHeight = 0f;
                }

                var s = strips[idx];
                s.packU = cursorU;
                s.packV = cursorV;
                s.packW = w;
                s.packH = h;
                strips[idx] = s;

                cursorU += w + pad;
                if (h > shelfHeight) shelfHeight = h;
                if (cursorU > maxU) maxU = cursorU;
            }

            maxV = cursorV + shelfHeight + pad;
            usedWidth = maxU;
            usedHeight = maxV;

            return maxV <= 1f;
        }

        Vector2 CalculateUV1(UV1Layout layout, int loopIdx, float localU, float t, int ringIndex)
        {
            if (layout.strips == null || layout.strips.Length == 0)
                return new Vector2(localU, t);

            int splitCount = layout.loopSplitCount[loopIdx];
            int stripStart = layout.loopStripStart[loopIdx];

            // t が属する短冊を特定
            int splitIdx = Mathf.FloorToInt(t * splitCount);
            if (splitIdx >= splitCount) splitIdx = splitCount - 1;

            int stripIdx = stripStart + splitIdx;
            var strip = layout.strips[stripIdx];

            // 短冊内での局所V座標
            float splitT = t * splitCount - splitIdx;

            return new Vector2(
                strip.packU + localU * strip.packW,
                strip.packV + splitT * strip.packH
            );
        }
    }
}
