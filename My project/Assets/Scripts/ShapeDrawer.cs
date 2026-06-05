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

    /// <summary>progress(0→1) のところまで一筆書きで描く。</summary>
    public void Draw(float progress)
    {
        if (shape == null || shape.Length < 2) return;
        progress = Mathf.Clamp01(progress);

        // 全体の道のりを測る
        float total = 0f;
        var seg = new float[shape.Length - 1];
        for (int i = 0; i < shape.Length - 1; i++)
        {
            seg[i] = Vector2.Distance(shape[i], shape[i + 1]);
            total += seg[i];
        }

        float target = total * progress; // ここまで描く
        var pts = new List<Vector3> { (Vector3)(shape[0] * radius) };
        float acc = 0f;

        for (int i = 0; i < seg.Length; i++)
        {
            if (acc + seg[i] <= target)
            {
                pts.Add((Vector3)(shape[i + 1] * radius));
                acc += seg[i];
            }
            else
            {
                // 線分の途中で打ち切る
                float t = seg[i] > 0 ? (target - acc) / seg[i] : 0f;
                Vector2 mid = Vector2.Lerp(shape[i], shape[i + 1], t);
                pts.Add((Vector3)(mid * radius));
                break;
            }
        }

        line.positionCount = pts.Count;
        line.SetPositions(pts.ToArray());
    }
}
