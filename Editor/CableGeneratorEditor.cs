using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using CableGeneratorRuntime;

namespace CableGeneratorEditor
{
    [CustomEditor(typeof(CableGenerator))]
    public class CableGeneratorEditor : Editor
    {
        // ---- 点選択状態（static: 複数インスペクタ間で共有） ----
        static CableGenerator s_pickingTarget = null;
        static int            s_pickCount     = 0;
        static Vector3[]      s_pickedPoints  = new Vector3[2];
        static Vector3[]      s_pickedNormals = new Vector3[2];
        // Inspector のスライダーで共有する接線スケール（1.0 = デフォルト）
        static float          s_tangentScale  = 1f;

        // ---- Inspector GUI ----
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("スプライン初期設定", EditorStyles.boldLabel);

            // ── 注意表示 ──────────────────────────────────────────────────
            EditorGUILayout.HelpBox(
                "注意:\n" +
                "・この機能を使用するには、対象メッシュにコライダーが設定されている必要があります（MeshCollider 推奨）。\n" +
                "・Box / Capsule / Sphere コライダーなど近似形状のコライダーを使用している場合、" +
                "取得される法線方向が実際のメッシュ面と一致しないことがあります。",
                MessageType.Warning);

            EditorGUILayout.Space(4);

            // ハンドル（接線）長さの調整スライダー
            EditorGUI.BeginChangeCheck();
            float newScale = EditorGUILayout.Slider("ハンドル強さ", s_tangentScale, 0f, 2f);
            if (EditorGUI.EndChangeCheck())
            {
                s_tangentScale = newScale;
                // 既に生成済みのスプラインがあれば即時適用
                ApplyTangentScaleToSpline((CableGenerator)target, s_tangentScale);
            }

            bool isMyTarget = s_pickingTarget == (CableGenerator)target;

            if (!isMyTarget)
            {
                // 別のオブジェクトが選択中であれば無効化表示
                using (new EditorGUI.DisabledScope(s_pickingTarget != null))
                {
                    if (GUILayout.Button("2点選択でSplineを設定"))
                    {
                        StartPickingMode((CableGenerator)target);
                    }
                }

                if (s_pickingTarget != null)
                    EditorGUILayout.LabelField("別オブジェクトで選択中です。", EditorStyles.miniLabel);
            }
            else
            {
                // ── 選択中UI ─────────────────────────────────────────────
                string hint = s_pickCount == 0
                    ? "シーンで 1点目 をクリックしてください"
                    : "シーンで 2点目 をクリックしてください";

                EditorGUILayout.HelpBox(hint, MessageType.Info);

                if (s_pickCount > 0)
                {
                    EditorGUILayout.LabelField(
                        "1点目",
                        $"座標 {s_pickedPoints[0]:F3}   法線 {s_pickedNormals[0]:F2}",
                        EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(2);
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("やり直し"))
                        s_pickCount = 0;

                    GUI.color = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("キャンセル"))
                        CancelPickingMode();
                    GUI.color = Color.white;
                }
            }
        }

        // ---- Scene GUI ----
        void OnSceneGUI()
        {
            if (s_pickingTarget != (CableGenerator)target) return;

            // デフォルトのシーン操作を無効化（クリックを横取りする）
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);

            DrawPickedPoints();
            DrawSceneHintLabel();

            Event e = Event.current;

            // ── 左クリックでRaycast ──────────────────────────────────────
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    s_pickedPoints[s_pickCount]  = hit.point;
                    s_pickedNormals[s_pickCount] = hit.normal;
                    s_pickCount++;

                    if (s_pickCount >= 2)
                    {
                        ApplySplineFromPoints(
                            (CableGenerator)target,
                            s_pickedPoints[0], s_pickedNormals[0],
                            s_pickedPoints[1], s_pickedNormals[1]);
                        CancelPickingMode();
                    }

                    e.Use();
                    Repaint();
                    SceneView.RepaintAll();
                }
            }

            // ── Escape でキャンセル ────────────────────────────────────
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                CancelPickingMode();
                e.Use();
            }

            SceneView.RepaintAll();
        }

        // ---- ビジュアルフィードバック ----
        void DrawPickedPoints()
        {
            for (int i = 0; i < s_pickCount; i++)
            {
                Color c = i == 0 ? new Color(0.2f, 1f, 0.3f) : new Color(0.3f, 0.6f, 1f);
                Handles.color = c;

                float size = HandleUtility.GetHandleSize(s_pickedPoints[i]) * 0.07f;
                Handles.SphereHandleCap(0, s_pickedPoints[i], Quaternion.identity, size, EventType.Repaint);

                // 法線方向の矢印
                Vector3 normalEnd = s_pickedPoints[i] + s_pickedNormals[i] * size * 4f;
                Handles.DrawLine(s_pickedPoints[i], normalEnd, 2f);
                Handles.ArrowHandleCap(0, s_pickedPoints[i],
                    Quaternion.LookRotation(s_pickedNormals[i]),
                    size * 2.5f, EventType.Repaint);
            }
        }

        static void DrawSceneHintLabel()
        {
            Handles.BeginGUI();
            string msg = s_pickCount == 0
                ? "[ Spline設定 ]  1点目を選択   Esc: キャンセル"
                : "[ Spline設定 ]  2点目を選択   Esc: キャンセル";

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 12,
            };
            Vector2 size = style.CalcSize(new GUIContent(msg));
            float x = (Screen.width - size.x) * 0.5f;
            GUI.Box(new Rect(x, 8, size.x + 16, size.y + 8), msg, style);
            Handles.EndGUI();
        }

        // ---- 状態管理 ----
        static void StartPickingMode(CableGenerator target)
        {
            s_pickingTarget = target;
            s_pickCount     = 0;
            SceneView.RepaintAll();
        }

        static void CancelPickingMode()
        {
            s_pickingTarget = null;
            s_pickCount     = 0;
            SceneView.RepaintAll();
        }

        // ---- スプラインの接線スケールを現在のスプラインへ適用 ----
        static void ApplyTangentScaleToSpline(CableGenerator cableGen, float scale)
        {
            var splineContainer = cableGen.GetComponent<SplineContainer>();
            if (splineContainer == null || splineContainer.Splines.Count == 0) return;

            var spline = splineContainer.Splines[0];
            int count = spline.Count;
            if (count == 0) return;

            // 保存してからクリアして再構築する（ノット数を維持）
            bool closed = spline.Closed;

            // 既存ノットの座標・回転・タンジェント・モードを収集
            Vector3[] positions = new Vector3[count];
            quaternion[] rotations = new quaternion[count];
            float3[] prevIn = new float3[count];
            float3[] prevOut = new float3[count];
            TangentMode[] modes = new TangentMode[count];

            for (int i = 0; i < count; i++)
            {
                var k = spline[i];
                positions[i] = (Vector3)k.Position;
                rotations[i] = k.Rotation;
                prevIn[i] = k.TangentIn;
                prevOut[i] = k.TangentOut;
                modes[i] = spline.GetTangentMode(i);
            }

            // 各ノットごとの基準長さ（隣接ノット距離の平均 / 3）を算出
            float[] baseLens = new float[count];
            for (int i = 0; i < count; i++)
            {
                float dPrev = 0f, dNext = 0f;
                if (closed)
                {
                    int prev = (i - 1 + count) % count;
                    int next = (i + 1) % count;
                    dPrev = Vector3.Distance(positions[prev], positions[i]);
                    dNext = Vector3.Distance(positions[i], positions[next]);
                    baseLens[i] = ((dPrev + dNext) * 0.5f) / 3f * scale;
                }
                else
                {
                    if (i > 0) dPrev = Vector3.Distance(positions[i - 1], positions[i]);
                    if (i < count - 1) dNext = Vector3.Distance(positions[i], positions[i + 1]);

                    float avg;
                    if (i > 0 && i < count - 1) avg = (dPrev + dNext) * 0.5f;
                    else avg = dPrev > 0f ? dPrev : dNext;

                    baseLens[i] = (avg / 3f) * scale;
                }
            }

            // 再構築（Autoモードのノットは元のタンジェントを維持）
            Undo.RecordObject(splineContainer, "Adjust Spline Tangent Strength");
            spline.Clear();
            spline.Closed = closed;

            for (int i = 0; i < count; i++)
            {
                float len = baseLens[i];
                float3 inT, outT;
                // Auto相当（自動/連続など Broken 以外）のモードは既存タンジェントを保持
                if (modes[i] != TangentMode.Broken)
                {
                    inT = prevIn[i];
                    outT = prevOut[i];
                }
                else
                {
                    inT = new float3(0f, 0f, -len);
                    outT = new float3(0f, 0f,  len);
                }

                var knot = new BezierKnot(
                    (float3)positions[i],
                    inT,
                    outT,
                    rotations[i]
                );
                spline.Add(knot, modes[i]);
            }

            EditorUtility.SetDirty(splineContainer);
        }

        // ---- Spline 生成 ----
        static void ApplySplineFromPoints(
            CableGenerator cableGen,
            Vector3 worldA, Vector3 normalA,
            Vector3 worldB, Vector3 normalB)
        {
            var splineContainer = cableGen.GetComponent<SplineContainer>();
            if (splineContainer == null || splineContainer.Splines.Count == 0) return;

            Transform tf = splineContainer.transform;

            // ワールド座標 → SplineContainer ローカル空間
            Vector3 localA = tf.InverseTransformPoint(worldA);
            Vector3 localB = tf.InverseTransformPoint(worldB);

            // 法線は方向ベクトル（TransformDirection の逆）
            Vector3 localNormalA = tf.InverseTransformDirection(normalA).normalized;
            Vector3 localNormalB = tf.InverseTransformDirection(normalB).normalized;

            // 接線の長さ = 2点間距離の 1/3（自然なベジェカーブの目安）
            float tangentLen = Vector3.Distance(localA, localB) / 3f;

            // ノットの向き: ローカルZ軸 → 法線方向
            //   TangentOut at A = (0,0,+L) → ケーブルが法線A方向に出発
            //   TangentIn  at B = (0,0,-L) として扱うため、
            //   ローカルZが -法線B を向くよう回転を作る（法線Bを反転して扱う）
            quaternion rotA = SafeLookRotation(localNormalA);
            quaternion rotB = SafeLookRotation(-localNormalB);

            var knot0 = new BezierKnot(
                (float3)localA,
                new float3(0f, 0f, -tangentLen),   // TangentIn
                new float3(0f, 0f,  tangentLen),   // TangentOut → 法線A方向に出発
                rotA
            );
            var knot1 = new BezierKnot(
                (float3)localB,
                new float3(0f, 0f, -tangentLen),   // TangentIn → 逆算: 法線B方向に到着
                new float3(0f, 0f,  tangentLen),   // TangentOut
                rotB
            );

            Undo.RecordObject(splineContainer, "Set Spline from Surface Points");

            var spline = splineContainer.Splines[0];
            spline.Clear();
            spline.Closed = false;
            spline.Add(knot0, TangentMode.Broken);
            spline.Add(knot1, TangentMode.Broken);

            EditorUtility.SetDirty(splineContainer);
        }

        // 安全な LookRotation（forward が上方向と並行な場合に対応）
        static quaternion SafeLookRotation(Vector3 forward)
        {
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f
                ? Vector3.forward
                : Vector3.up;
            return (quaternion)Quaternion.LookRotation(forward, up);
        }
    }
}
