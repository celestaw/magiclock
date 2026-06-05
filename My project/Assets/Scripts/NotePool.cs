using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 魔法陣ビューを使い回すプール。毎回 Instantiate/Destroy すると
/// GCスパイクでタイミングがブレるので最初から入れておく。
/// </summary>
public class NotePool : MonoBehaviour
{
    public MagicCircleView prefab;
    public int initialSize = 16;

    readonly Queue<MagicCircleView> pool = new Queue<MagicCircleView>();

    void Awake()
    {
        for (int i = 0; i < initialSize; i++) Create();
    }

    MagicCircleView Create()
    {
        var v = Instantiate(prefab, transform);
        v.Hide();
        pool.Enqueue(v);
        return v;
    }

    public MagicCircleView Get()
    {
        if (pool.Count == 0) Create();
        return pool.Dequeue();
    }

    public void Return(MagicCircleView v)
    {
        v.Hide();
        pool.Enqueue(v);
    }
}
