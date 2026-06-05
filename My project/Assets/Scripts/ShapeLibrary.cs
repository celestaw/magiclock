using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 各図形を「単位円(-1..1)内の点列（一筆書き順）」として返す。
/// 形を増やしたいときはここに case を足すだけ。素材を作る必要がない。
/// </summary>
public static class ShapeLibrary
{
    public static Vector2[] Get(ShapeType type)
    {
        switch (type)
        {
            case ShapeType.Diameter:
                return new[] { new Vector2(-1, 0), new Vector2(1, 0) };
            case ShapeType.Triangle:
                return Polygon(3, 90f);
            case ShapeType.Square:
                return Polygon(4, 45f);
            case ShapeType.Pentagram:
                return Star(5, 2);
            case ShapeType.Hexagram:
                return Star(6, 2); // 近似。厳密な六芒星は2つの三角形に分けたいが MVP は一筆書きで代用
            default:
                return Polygon(3, 90f);
        }
    }

    /// <summary>正n角形（閉じる）。</summary>
    static Vector2[] Polygon(int n, float startDeg)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i <= n; i++) // 最後に始点へ戻して閉じる
        {
            float a = (startDeg + 360f / n * i) * Mathf.Deg2Rad;
            pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
        }
        return pts.ToArray();
    }

    /// <summary>星型。頂点を skip 個飛ばしながら一筆書きで結ぶ。</summary>
    static Vector2[] Star(int points, int skip)
    {
        var verts = new Vector2[points];
        for (int i = 0; i < points; i++)
        {
            float a = (90f + 360f / points * i) * Mathf.Deg2Rad;
            verts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
        var pts = new List<Vector2>();
        int idx = 0;
        for (int i = 0; i <= points; i++)
        {
            pts.Add(verts[idx]);
            idx = (idx + skip) % points;
        }
        return pts.ToArray();
    }
}
