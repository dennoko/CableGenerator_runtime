using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace CableGeneratorRuntime
{
    /// <summary>
    /// スプラインの制御点（ノット）に任意のPrefabをアタッチするコンポーネント。
    /// Cable オブジェクトの子 GameObject に追加して使用する。
    /// </summary>
    [ExecuteAlways]
    public class CableKnotAttachment : MonoBehaviour
    {
        [Tooltip("アタッチ先のノットインデックス")]
        public int knotIndex = 0;

        [Tooltip("配置するPrefabまたはFBXモデル")]
        public GameObject prefab;

        [Tooltip("ノット位置からのオフセット（ローカル）")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("追加回転（オイラー角）")]
        public Vector3 rotationOffset = Vector3.zero;

        [Tooltip("スケール")]
        public Vector3 scale = Vector3.one;

        [SerializeField, HideInInspector]
        GameObject spawnedInstance;
        [SerializeField, HideInInspector]
        GameObject lastPrefab;

        public GameObject SpawnedInstance => spawnedInstance;
        SplineContainer splineContainer;

        void OnEnable()
        {
            splineContainer = GetComponentInParent<SplineContainer>();
            Spline.Changed += OnSplineChanged;
            if (spawnedInstance == null)
                RebuildInstance();
            UpdateAttachment();
        }

        void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
        }

        void OnValidate()
        {
            if (splineContainer == null)
                splineContainer = GetComponentInParent<SplineContainer>();

            // Prefab が変更された場合はインスタンスを再生成
            if (prefab != lastPrefab)
                RebuildInstance();

            UpdateAttachment();
        }

        void OnSplineChanged(Spline spline, int knotIdx, SplineModification modification)
        {
            if (splineContainer == null) return;

            bool isOurSpline = false;
            for (int i = 0; i < splineContainer.Splines.Count; i++)
            {
                if (splineContainer.Splines[i] == spline)
                {
                    isOurSpline = true;
                    break;
                }
            }
            if (!isOurSpline) return;

            if (modification == SplineModification.KnotInserted)
            {
                // 追従先より手前にノットが挿入された場合、インデックスをずらす
                if (knotIdx <= knotIndex)
                    knotIndex++;
            }
            else if (modification == SplineModification.KnotRemoved)
            {
                if (knotIdx == knotIndex)
                {
                    // 追従先のノットが削除された → 1つ若いインデックスへ移動
                    int newIndex = Mathf.Max(0, knotIndex - 1);

                    // 移動先に既に別のアタッチメントがあるか確認
                    if (HasOtherAttachmentAt(newIndex))
                    {
                        // 競合するため自身を削除
                        DestroySelf();
                        return;
                    }
                    knotIndex = newIndex;
                }
                else if (knotIdx < knotIndex)
                {
                    // 追従先より手前のノットが削除された → インデックスをずらす
                    knotIndex--;
                }
            }

            UpdateAttachment();
        }

        bool HasOtherAttachmentAt(int index)
        {
            if (transform.parent == null) return false;

            var siblings = transform.parent.GetComponentsInChildren<CableKnotAttachment>();
            for (int i = 0; i < siblings.Length; i++)
            {
                if (siblings[i] != this && siblings[i].knotIndex == index)
                    return true;
            }
            return false;
        }

        void DestroySelf()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null)
                        DestroyImmediate(gameObject);
                };
#endif
            }
        }

        void RebuildInstance()
        {
            // Prefab未変更かつインスタンス生存中 → スキップ
            if (prefab == lastPrefab && spawnedInstance != null) return;

            // 既存インスタンスを破棄
            if (spawnedInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(spawnedInstance);
                else
                    DestroyImmediate(spawnedInstance);
                spawnedInstance = null;
            }

            lastPrefab = prefab;

            if (prefab == null) return;

#if UNITY_EDITOR
            // 通常のPrefab(.prefab)はPrefab接続を維持するInstantiatePrefabで生成する。
            // FBXモデルアセット(PrefabAssetType.Model)はInstantiateで生成する。
            var assetType = UnityEditor.PrefabUtility.GetPrefabAssetType(prefab);
            bool isRegularPrefab = assetType == UnityEditor.PrefabAssetType.Regular
                                || assetType == UnityEditor.PrefabAssetType.Variant;
            if (isRegularPrefab)
                spawnedInstance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
            else
                spawnedInstance = Instantiate(prefab, transform);
#else
            spawnedInstance = Instantiate(prefab, transform);
#endif

            if (spawnedInstance != null)
                spawnedInstance.name = prefab.name + " (Attachment)";
        }

        /// <summary>
        /// ノットの位置・回転に基づいてこのオブジェクトの Transform を更新する。
        /// </summary>
        public void UpdateAttachment()
        {
            if (splineContainer == null || splineContainer.Splines.Count == 0) return;

            var spline = splineContainer.Splines[0];
            if (spline.Count == 0) return;

            // knotIndex を安全な範囲にクランプ
            int safeIndex = Mathf.Clamp(knotIndex, 0, spline.Count - 1);
            var knot = spline[safeIndex];

            Transform splineTransform = splineContainer.transform;

            // ノットの位置をワールド座標に変換
            Vector3 worldPos = splineTransform.TransformPoint((Vector3)(float3)knot.Position);

            // ノットの接線方向から回転を計算
            Quaternion worldRot = CalculateKnotRotation(spline, safeIndex, splineTransform);

            // オフセットを適用
            Quaternion offsetRot = Quaternion.Euler(rotationOffset);
            Quaternion finalRot = worldRot * offsetRot;
            Vector3 finalPos = worldPos + finalRot * positionOffset;

            transform.position = finalPos;
            transform.rotation = finalRot;
            transform.localScale = scale;
        }

        Quaternion CalculateKnotRotation(Spline spline, int index, Transform splineTransform)
        {
            var knot = spline[index];

            // ノットの接線方向を取得
            float3 tangentLocal;
            if (index < spline.Count - 1)
            {
                // TangentOut 方向を forward として使用
                tangentLocal = math.rotate(knot.Rotation, knot.TangentOut);
            }
            else
            {
                // 終点では TangentIn の逆方向を forward として使用
                tangentLocal = -math.rotate(knot.Rotation, knot.TangentIn);
            }

            Vector3 forward = splineTransform.TransformDirection(math.normalize(tangentLocal));

            if (forward.sqrMagnitude < 0.0001f)
                forward = splineTransform.forward;

            // スプラインの up を取得して回転を構築
            float t = (spline.Count > 1) ? (float)index / (spline.Count - 1) : 0f;
            SplineUtility.Evaluate(spline, t, out float3 _, out float3 _, out float3 upFloat3);
            Vector3 up = splineTransform.TransformDirection(math.normalize(upFloat3));

            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.up;

            return Quaternion.LookRotation(forward, up);
        }
    }
}
