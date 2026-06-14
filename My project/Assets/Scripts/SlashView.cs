using UnityEngine;

/// <summary>
/// 斬撃ノーツのビジュアル。
///
/// 予告フェーズ (progress 0〜lastBeatProgress):
///   菱形の辺を内側に凹ませた四芒星（手裏剣型）を一筆書きで描画。
///   progressに応じて辺が1本ずつ現れる（魔法陣と同じセグメント方式）。
///
/// 斬撃フェーズ (最後の1拍 = lastBeatProgress〜1.0):
///   画面全体を横切る大きな斬撃ラインが一瞬で走る。
///   四芒星は消える。
/// </summary>
public class SlashView : MonoBehaviour
{
    LineRenderer starLine;   // 四芒星（予告）
    LineRenderer slashLine;  // 斬撃ライン

    float scale = 1f;
    float angle; // degrees
    float slashProgress; // 斬撃開始後の経過（0未満=予告中、0〜1=斬撃中）

    // 四芒星の頂点（凹み菱形、一筆書き閉じ）
    static readonly Vector2[] starTemplate;

    static SlashView()
    {
        // 四芒星: 尖った頂点4つ + 凹み点4つ を交互に配置
        // 尖り: 上、右、下、左 (距離1.0)
        // 凹み: 各間に内側へ凹んだ点 (距離0.25)
        const float outer = 1f;
        const float inner = 0.25f;
        starTemplate = new Vector2[]
        {
            new Vector2(0, outer),                              // 上 尖り
            new Vector2(inner, inner),                          // 右上 凹み
            new Vector2(outer, 0),                              // 右 尖り
            new Vector2(inner, -inner),                         // 右下 凹み
            new Vector2(0, -outer),                             // 下 尖り
            new Vector2(-inner, -inner),                        // 左下 凹み
            new Vector2(-outer, 0),                             // 左 尖り
            new Vector2(-inner, inner),                         // 左上 凹み
            new Vector2(0, outer),                              // 上 尖り（閉じる）
        };
    }

    void Awake()
    {
        // Star line (tell shape)
        var starGo = new GameObject("StarLine");
        starGo.transform.SetParent(transform, false);
        starLine = starGo.AddComponent<LineRenderer>();
        starLine.useWorldSpace = false;
        starLine.positionCount = 0;
        starLine.numCapVertices = 2;
        starLine.material = new Material(Shader.Find("Sprites/Default"));

        // Slash line (big screen-crossing cut)
        var slashGo = new GameObject("SlashLine");
        slashGo.transform.SetParent(transform, false);
        slashLine = slashGo.AddComponent<LineRenderer>();
        slashLine.useWorldSpace = true;
        slashLine.positionCount = 0;
        slashLine.numCapVertices = 3;
        slashLine.material = new Material(Shader.Find("Sprites/Default"));
    }

    public void Setup(Vector3 worldPos, float s, float angleDeg, float leadBeats)
    {
        transform.position = worldPos;
        scale = s;
        angle = angleDeg;
        slashProgress = -1f;
        UpdateVisual(0f);
        gameObject.SetActive(true);
    }

    public void UpdateVisual(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (progress < 1f)
        {
            // --- 予告フェーズ: 四芒星を完成形で表示 ---
            DrawStar();
            slashLine.positionCount = 0;
            slashProgress = -1f;
        }
        else
        {
            // --- 斬撃フェーズ: hitBeat到達、斬撃発動 ---
            if (slashProgress < 0f) slashProgress = 0f;
            DrawSlash(slashProgress);
            starLine.positionCount = 0;
            slashProgress += Time.deltaTime * 4f; // 斬撃アニメーション速度
        }
    }

    void DrawStar()
    {
        float r = 0.7f * scale;
        starLine.positionCount = starTemplate.Length;
        starLine.startWidth = 0.04f * scale;
        starLine.endWidth = 0.04f * scale;

        Color c = new Color(1f, 0.3f, 0.3f, 1f);
        starLine.startColor = c;
        starLine.endColor = c;

        for (int i = 0; i < starTemplate.Length; i++)
            starLine.SetPosition(i, (Vector3)(starTemplate[i] * r));
    }

    void DrawSlash(float t)
    {
        // 斬撃: 一瞬で画面を横切る大きなライン
        float rad = angle * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        Vector3 center = transform.position;

        const float slashLength = 20f; // 画面全体を覆う長さ

        if (t < 0.15f)
        {
            // 斬撃が走る（先端が伸びる）
            float extend = t / 0.15f;
            Vector3 start = center - dir * slashLength * 0.5f;
            Vector3 end = start + dir * slashLength * extend;

            slashLine.positionCount = 2;
            slashLine.SetPosition(0, start);
            slashLine.SetPosition(1, end);

            float w = 0.2f * scale;
            slashLine.startWidth = w;
            slashLine.endWidth = w * 0.5f;
            slashLine.startColor = Color.white;
            slashLine.endColor = new Color(1f, 0.8f, 0.8f, 1f);
        }
        else if (t < 0.5f)
        {
            // 斬撃が残る（フル表示）
            slashLine.positionCount = 2;
            slashLine.SetPosition(0, center - dir * slashLength * 0.5f);
            slashLine.SetPosition(1, center + dir * slashLength * 0.5f);

            float fade = 1f - (t - 0.15f) / 0.35f;
            float w = 0.2f * scale * fade;
            slashLine.startWidth = w;
            slashLine.endWidth = w;
            Color c = new Color(1f, 0.9f, 0.9f, fade);
            slashLine.startColor = c;
            slashLine.endColor = c;
        }
        else
        {
            // フェードアウト完了
            slashLine.positionCount = 0;
        }
    }

    public void Hide()
    {
        starLine.positionCount = 0;
        slashLine.positionCount = 0;
        gameObject.SetActive(false);
    }
}
