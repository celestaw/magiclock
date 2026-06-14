using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 点列を progress(0→1) に応じて部分的に描く。外周の円も内側の図形も
/// 同じこのコンポーネントで描けるので、座標系が統一されてCanvas不要。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ShapeDrawer : MonoBehaviour
{
    LineRenderer line;
    Vector2[] shape;
    float radius = 1f;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false; // 親の位置に追従させる
    }

    public void SetShape(Vector2[] points, float r)
    {
        if (line == null) line = GetComponent<LineRenderer>();
        shape = points;
        radius = r;
        line.positionCount = 0;
    }

    /// <summary>progress(0→1) のところまで一筆書きで描く。
    /// セグメント単位でスナップするので、拍ごとに辺がパッと現れる。</summary>
    public void Draw(float progress)
    {
        if (shape == null || shape.Length < 2) return;
        progress = Mathf.Clamp01(progress);

        // セグメント数に合わせて量子化（例: 三角形なら 0, 1/3, 2/3, 1 にスナップ）
        int segments = shape.Length - 1;
        int completed = Mathf.FloorToInt(progress * segments);
        if (completed >= segments) completed = segments; // progress==1 のとき全辺

        if (completed <= 0)
        {
            line.positionCount = 0;
            return;
        }

        line.positionCount = completed + 1;
        for (int i = 0; i <= completed; i++)
            line.SetPosition(i, (Vector3)(shape[i] * radius));
    }
}
