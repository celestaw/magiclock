using UnityEngine;

/// <summary>
/// 1つの魔法陣の見た目。外周の円(ringDrawer)と内側の図形(shapeDrawer)を
/// 同じ progress で同時に描く。progress=1 で完成する。
/// プール管理されるので Destroy せず Hide で使い回す。
/// </summary>
public class MagicCircleView : MonoBehaviour
{
    [Header("外周の円と内側の図形、それぞれにLineRenderer+ShapeDrawerを付ける")]
    public ShapeDrawer ringDrawer;
    public ShapeDrawer shapeDrawer;
    public float radius = 0.8f;

    static Vector2[] circlePoints; // 円は全ノーツ共通なので一度だけ生成

    void EnsureCircle()
    {
        if (circlePoints != null) return;
        const int n = 64;
        circlePoints = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.PI * 2f * i / n;
            circlePoints[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
    }

    public void Setup(ShapeType shape, Vector3 worldPos)
    {
        EnsureCircle();
        transform.position = worldPos;
        ringDrawer.SetShape(circlePoints, radius);
        shapeDrawer.SetShape(ShapeLibrary.Get(shape), radius);
        UpdateVisual(0f);
        gameObject.SetActive(true);
    }

    /// <summary>progress: 0→1 で魔法陣が完成する。</summary>
    public void UpdateVisual(float progress)
    {
        ringDrawer.Draw(progress);
        shapeDrawer.Draw(progress);
    }

    public void Hide() => gameObject.SetActive(false);
}
