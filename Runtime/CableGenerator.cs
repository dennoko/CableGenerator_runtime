using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace CableGeneratorRuntime
{
    [RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
    [ExecuteInEditMode]
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

        void OnValidate()
        {
            // Inspector でパラメータ変更時に再生成
            if (splineContainer != null)
                RebuildMesh();
        }

        void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            if (splineContainer == null) return;

            // このコンポーネントに属するスプラインが変更された場合のみ再生成
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

            // メッシュ初期化（一度だけ）
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

            // プロファイルデータ取得
            Vector2[] profileVerts = profile.GetVertices();
            Vector2[] profileNormals = profile.GetNormals();
            float[] profileUs = profile.GetUCoords();
            int profileVertCount = profileVerts.Length;

            if (profileVertCount == 0) return;

            // スプライン長を計算
            float splineLength = SplineUtility.CalculateLength(spline, splineContainer.transform.localToWorldMatrix);
            if (splineLength < 0.0001f) return;

            // サンプリング & 頂点生成
            Vector3 prevUp = Vector3.up;

            for (int i = 0; i <= resolution; i++)
            {
                float t = (float)i / resolution;

                // スプライン上の位置・接線・上方向を取得
                SplineUtility.Evaluate(spline, t, out float3 pos, out float3 tangent, out float3 up);

                Vector3 position = splineContainer.transform.TransformPoint((Vector3)pos);
                Vector3 tan = splineContainer.transform.TransformDirection(math.normalize(tangent));
                Vector3 upDir = splineContainer.transform.TransformDirection(math.normalize(up));

                // 接線がゼロに近い場合のフォールバック
                if (tan.sqrMagnitude < 0.0001f)
                    tan = Vector3.forward;

                // RMF的なアプローチ: 前フレームのupを基準に安定した座標系を構築
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

                // ローカル座標に戻す
                Vector3 localPos = splineContainer.transform.InverseTransformPoint(position);
                Vector3 localRight = splineContainer.transform.InverseTransformDirection(right);
                Vector3 localUp = splineContainer.transform.InverseTransformDirection(upDir);

                // V座標（累積距離ベース）
                float v = (splineLength * t) * uvTiling;

                // 断面頂点を3D空間に配置
                for (int j = 0; j < profileVertCount; j++)
                {
                    Vector3 vertPos = localPos
                        + localRight * profileVerts[j].x
                        + localUp * profileVerts[j].y;
                    verts.Add(vertPos);

                    Vector3 norm = (localRight * profileNormals[j].x
                        + localUp * profileNormals[j].y).normalized;
                    normals.Add(norm);

                    uvs.Add(new Vector2(profileUs[j], v));
                    uv2s.Add(new Vector2(profileUs[j], t));
                }
            }

            // 三角形インデックス生成
            for (int i = 0; i < resolution; i++)
            {
                int ringStart = i * profileVertCount;
                int nextRingStart = (i + 1) * profileVertCount;

                for (int j = 0; j < profileVertCount - 1; j++)
                {
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

            // メッシュに反映
            generatedMesh.SetVertices(verts);
            generatedMesh.SetNormals(normals);
            generatedMesh.SetUVs(0, uvs);
            generatedMesh.SetUVs(1, uv2s);
            generatedMesh.SetTriangles(tris, 0);
            generatedMesh.RecalculateBounds();

            meshFilter.sharedMesh = generatedMesh;
        }
    }
}
