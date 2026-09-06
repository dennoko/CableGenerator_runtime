# CableGenerator — Unity 6 移行調査

- 調査日: 2026-09-06
- 現行: Unity 2022.3.22f1 / Built-in RP
- 目標: Unity 6 (6000.0 LTS) / **BiRP 維持**
- 共通調査: [`../../../Docs/Impl/unity6-migration-overview.md`](../../../Docs/Impl/unity6-migration-overview.md)

## 判定

⚠️ **要修正**（Obsolete API 2 箇所）＋ 🔍 **要検証**（Splines パッケージ内部 API）

ワークスペース内で **修正箇所が最も多いツール**。ただし修正自体は機械的で、
本質的な難所は Unity 6 同梱の Splines パッケージのバージョン差異にある。

## 構成

| 項目 | 内容 |
|---|---|
| 規模 | C# 28 ファイル / 約 5,513 行 |
| asmdef | `dennokoworks.CableGenerator.Editor`（→ Runtime, `Unity.Splines`, `Unity.Splines.Editor`, `Unity.Mathematics`）<br>`dennokoworks.CableGenerator.Runtime`（→ `Unity.Splines`, `Unity.Mathematics`） |
| エントリ | `MenuItem("GameObject/Cable Generator/Create Cable")` |
| UI | IMGUI（`CableGeneratorTheme`）+ カスタムインスペクタ + SceneGUI |
| 外部依存 | **`com.unity.splines` 2.5.2**（VRChat SDK / NDMF には非依存） |

VRChat SDK / NDMF に依存しないため、**SDK の Unity 6 対応を待たずに単体検証できる**。

## 検出事項

### 1. `Object.FindObjectsOfType` の Obsolete 化（⚠️ 要修正・2 箇所）

Unity 6 で `Object.FindObjectsOfType` は Obsolete（警告）になる。コンパイルは通るが警告が出る。

#### `Editor/Inspector/CablePickingColliderManager.cs:90`

```csharp
// 現行
foreach (var mf in Object.FindObjectsOfType<MeshFilter>(false))

// 置換後
foreach (var mf in Object.FindObjectsByType<MeshFilter>(
             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
```

#### `Editor/Shared/EditorMeshRaycaster.cs:77`

```csharp
// 現行
foreach (var mf in Object.FindObjectsOfType<MeshFilter>(false))

// 置換後
foreach (var mf in Object.FindObjectsByType<MeshFilter>(
             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
```

**引数の対応**

| 旧 | 新 |
|---|---|
| `FindObjectsOfType<T>(false)` | 第 1 引数 `FindObjectsInactive.Exclude` |
| `FindObjectsOfType<T>(true)` | 第 1 引数 `FindObjectsInactive.Include` |
| （旧は常に InstanceID 順） | 第 2 引数でソート指定 |

**ソートモードの選択**: 両箇所とも「シーン中の全 MeshFilter を走査してレイキャスト対象を集める」
用途であり、**列挙順は結果に影響しない**。したがって高速な `FindObjectsSortMode.None` を選ぶ。

> ヒットしたコライダから最近傍を選ぶロジックは距離比較で決まるため、
> 走査順が変わっても結果は変わらない。ただし**同一距離のタイが発生した場合のみ**
> 選ばれる対象が変わり得る。厳密な再現性が必要なら `FindObjectsSortMode.InstanceID` にする。

**実施タイミング**: `FindObjectsByType` は Unity **2022.2 以降**に存在するため、
**現行の 2022.3.22f1 のまま置換・動作確認できる**。フェーズ 1 で先行実施する。

### 2. Splines パッケージのバージョン差異（🔍 要検証・本ツール最大のリスク）

Unity 6 は `com.unity.splines` の新しいバージョンを同梱する（現行は 2.5.2）。

#### 2-1. 内部 Editor 型へのリフレクション

`Editor/SplineContainerEditor/CableSplineContainerEditor.cs:54`

```csharp
defaultType = asm.GetType("UnityEditor.Splines.SplineContainerEditor");
if (defaultType != null)
    _defaultEditor = CreateEditor(targets, defaultType);
```

`Editor/SplineContainerEditor/CableSplineContainerEditor.cs:119`

```csharp
var m = _defaultEditor.GetType().GetMethod("OnSceneGUI",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
m?.Invoke(_defaultEditor, null);
```

- `UnityEditor.Splines.SplineContainerEditor` は Splines パッケージの **internal 型**。
  パッケージ更新で改名・削除されると解決に失敗する。
- 実装はガード済みで、失敗時は `DrawDefaultInspector()` へフォールバックする（`DrawFallbackUI()`）。
  **例外は出ないが、標準 Spline インスペクタの UI が素の Inspector に置き換わる。**

**対応**

1. Unity 6 上で「カスタム UI を使用」を OFF にし、標準 Spline インスペクタが
   正しく表示されるか確認する（素の `DrawDefaultInspector` に落ちていないか）。
2. 解決失敗時に警告ログを追加する。

#### 2-2. 公開 Splines API の互換性

`UnityEngine.Splines` の `SplineContainer` / `Spline` / `BezierKnot` などを 15 ファイルで使用。
これらは公開 API であり Splines 2.x 系で安定しているが、
**Unity 6 同梱版でのシグネチャ変更の有無は実際にインポートして確認する必要がある**。

重点確認: `container.Splines`、`spline.Count`、ノットの追加・削除・座標変換周り。

#### 2-3. asmdef 参照

```json
"references": [ "dennokoworks.CableGenerator.Runtime", "Unity.Splines", "Unity.Splines.Editor", "Unity.Mathematics" ]
```

アセンブリ名（`Unity.Splines` / `Unity.Splines.Editor` / `Unity.Mathematics`）は
パッケージ更新でも維持される見込みだが、**インポート直後に asmdef の参照解決エラーが
出ていないか確認**する。

### 3. `Camera.current` の null チェック（✅ 影響なし・良い実装）

`Editor/SplineContainerEditor/CableSplineContainerEditor.cs:118` 付近

```csharp
// Repaint 以外のイベントでは Camera.current が null になることがある
if (Camera.current == null) return;
```

既に防御的に書かれている。Unity 6 で `Camera.current` の挙動は変わらない。**修正不要。**

### 4. IMGUI テーマ: 共有 `EditorStyles` の書き換え（🔍 視覚検証）

`Editor/Shared/CableGeneratorTheme.cs:175,185` ほか

共通調査 3.3 節と同じパターン。Unity 6 の Editor スキン刷新で余白・枠線がずれる可能性。
**対応**: 目視確認し、必要なら `RectOffset` の数値のみ調整。

### 5. `Handles` / SceneGUI（✅ 影響なし）

`OnSceneGUI` でのノットインデックスラベル描画。`Handles` / `HandleUtility` は Unity 6 で変更なし。

## 非該当の確認

| 確認項目 | 結果 |
|---|---|
| `Object.FindObjectOfType`（単数形） | **なし** |
| UnityEditor（Unity 本体）内部 API へのリフレクション | **なし**（Splines パッケージのみ） |
| `GraphicsFormat.DepthAuto` / `ShadowAuto` / `VideoAuto` | **なし** |
| UI Toolkit（`ExecuteDefaultAction` / `UxmlFactory` 等） | **なし**（IMGUI のみ） |
| Compute シェーダ / カスタムシェーダ | **なし** |
| VRChat SDK / NDMF / lilToon 依存 | **なし** |
| `Lightmapping` / 物理 API | **なし** |
| バージョン定義 `#if UNITY_*` | **なし** |

## 移行手順

### フェーズ 1（Unity 2022.3.22f1 のまま実施可）

- [ ] `CablePickingColliderManager.cs:90` を `FindObjectsByType` に置換
- [ ] `EditorMeshRaycaster.cs:77` を `FindObjectsByType` に置換
- [ ] 現行環境でケーブル生成・ピッキング・サーフェス描画が従来どおり動くことを確認
- [ ] `CableSplineContainerEditor` のリフレクション解決失敗時に警告ログを追加

### フェーズ 2（Unity 6 検証プロジェクト）

VRChat SDK / NDMF に依存しないため、Unity 6 の空プロジェクトに
`CableGenerator/` をコピーして先行検証できる。

- [ ] `com.unity.splines` が Unity 6 同梱版で解決される
- [ ] asmdef の参照解決エラーが出ない
- [ ] コンパイル警告・エラーが 0 件

## 検証チェックリスト（Unity 6）

- [ ] `GameObject/Cable Generator/Create Cable` でケーブルが生成される
- [ ] `SplineContainer` のカスタムインスペクタが表示される
- [ ] 「カスタム UI を使用」を OFF にしたとき、**標準 Spline インスペクタが表示される**
      （素の `DrawDefaultInspector` に落ちていない）
- [ ] SceneGUI 上のノットインデックスラベルが表示される
- [ ] ノットの追加・削除・移動が正しく反映される
- [ ] **ピッキング機能**（`CablePickingColliderManager`）でメッシュを拾える — `FindObjectsByType` 置換の確認
- [ ] **サーフェス描画機能**（`EditorMeshRaycaster`）でメッシュ表面にケーブルを引ける — 同上
- [ ] 非アクティブなオブジェクトが対象に含まれていない（`FindObjectsInactive.Exclude` の確認）
- [ ] メッシュエクスポートが正しく動作する
- [ ] IMGUI テーマの枠線・余白・文字色が崩れていない
