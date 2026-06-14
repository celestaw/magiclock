# 魔法陣ノーツシステム MVP（Unity 2D）

可変長プリロールを核にした音ゲーの最小動作版。「予告から実行までの時間」を
譜面側で自由に指定でき、入力はタイミングのみ判定（ADOFAI方式）、同じ瞬間に
完成する複数の魔法陣を1入力でまとめて取れる。

## 設計の核

- **判定は `hitTime` だけ見る** → 表現の自由度を上げても判定は複雑にならない
- **`leadTime`（可変長プリロール）は描画専用** → 判定と表現が完全分離
- **progress は常に 0→1 に正規化** → leadTimeの長短を描画が一切気にしない
- **同じhitTimeのノーツは1イベントにまとめ、1入力で全解決** → 「n個出てても1回押す」

## ファイル構成

| ファイル | 役割 |
|---|---|
| `Conductor.cs` | dspTimeベースの曲時間供給（音ゲーの心臓部） |
| `Chart.cs` | 譜面データモデル（NoteData / Chart / ShapeType） |
| `ShapeLibrary.cs` | 図形を単位円内の点列に変換。形を増やすのはここだけ |
| `ShapeDrawer.cs` | LineRendererで点列をprogress分だけ描く |
| `MagicCircleView.cs` | 1ノーツの見た目（外周円＋内図形） |
| `NotePool.cs` | 魔法陣オブジェクトのプール |
| `SampleChart.cs` | 収束パターンを含む動作確認用譜面 |
| `GameManager.cs` | 全体をつなぐメインループ |

## シーンの組み立て手順

### 1. カメラ
- Main Camera を **Orthographic**、Size を 5 程度に。

### 2. Conductor
- 空の GameObject 「Conductor」を作成。
- `Conductor.cs` と `AudioSource` を付ける。
- AudioSource の Clip に曲を割り当て、**Play On Awake はオフ**。
- （曲がなくても動く。その場合は無音でタイミングだけ進む）

### 3. 魔法陣プレハブ（MagicCircle）
1. 空の GameObject 「MagicCircle」を作成し `MagicCircleView.cs` を付ける。
2. 子に「Ring」を作り、`LineRenderer` + `ShapeDrawer.cs` を付ける。
3. 子に「Shape」を作り、`LineRenderer` + `ShapeDrawer.cs` を付ける。
4. 両方の LineRenderer の設定：
   - **Use World Space：オフ**（コードでも設定済みだが念のため）
   - **Width：0.04〜0.06** 程度
   - **Material：** Sprites-Default（光らせたいなら加算/発光マテリアル）
   - **Color：** 好みで（Ring を青系、Shape を白系など）
   - Loop はオフ
5. MagicCircleView の `ringDrawer` に Ring の、`shapeDrawer` に Shape の
   ShapeDrawer を割り当てる。
6. これを Prefab 化して、シーンからは削除。

### 4. NotePool
- 空の GameObject 「NotePool」に `NotePool.cs` を付ける。
- `prefab` に上で作った MagicCircle プレハブを割り当てる。

### 5. GameManager
- 空の GameObject 「GameManager」に `GameManager.cs` を付ける。
- `conductor` に Conductor、`pool` に NotePool を割り当てる。
- `chart` は空のままでOK（SampleChart が自動で使われる）。

### 6. 再生
- Play すると、最初に可変長プリロールの3ノーツ、次に4つの魔法陣が時間差で
  描かれて同時完成する収束パターンが流れる。
- どのキー／クリック／タップでも判定。Console に Perfect/Good/Miss が出る。

## 最初に詰めるべきポイント

1. **タイミング精度**：まず Conductor だけで拍が正確に刻めているか確認する。
   ここが正しければ後が楽。
2. **判定窓**：`perfectWindow` / `goodWindow` を遊びながら調整。
3. **演出オフセット**：既定は `hitOffsetBeats = 0`（完成した瞬間が判定）。
   「完成した N 拍後に叩く」演出にしたいなら GameManager の `hitOffsetBeats`
   を N に。描画だけ早く完成し、判定タイミング自体は変わらない。

## 次の拡張候補

- 判定エフェクト（完成時のポップ／フェード）を MagicCircleView に追加
- 特定の図形だけ連番スプライトのスクラブ描画に差し替えて見栄え向上
- JSON譜面ローダー（JsonUtility.FromJson<Chart> で読み込み）
- 「画面に出ている複数を任意順で叩ける」モードにするなら、nextEventIndex方式を
  最近傍イベント探索に変更
