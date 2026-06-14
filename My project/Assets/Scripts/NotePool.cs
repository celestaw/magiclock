using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ノーツビューを使い回すプール。毎回 Instantiate/Destroy すると
/// GCスパイクでタイミングがブレるので最初から入れておく。
/// </summary>
public class NotePool : MonoBehaviour
{
    public MagicCircleView magicCirclePrefab;
    public SlashView slashPrefab;
    public int initialSize = 16;

    readonly Queue<MagicCircleView> mcPool = new Queue<MagicCircleView>();
    readonly Queue<SlashView> slashPool = new Queue<SlashView>();

    void Awake()
    {
        for (int i = 0; i < initialSize; i++) CreateMC();
        for (int i = 0; i < initialSize / 2; i++) CreateSlash();
    }

    MagicCircleView CreateMC()
    {
        var v = Instantiate(magicCirclePrefab, transform);
        v.Hide();
        mcPool.Enqueue(v);
        return v;
    }

    SlashView CreateSlash()
    {
        if (slashPrefab != null)
        {
            var v = Instantiate(slashPrefab, transform);
            v.Hide();
            slashPool.Enqueue(v);
            return v;
        }
        // Prefabが無い場合は動的生成
        var go = new GameObject("SlashView");
        go.transform.SetParent(transform, false);
        var sv = go.AddComponent<SlashView>();
        sv.Hide();
        slashPool.Enqueue(sv);
        return sv;
    }

    public MagicCircleView Get()
    {
        if (mcPool.Count == 0) CreateMC();
        return mcPool.Dequeue();
    }

    public SlashView GetSlash()
    {
        if (slashPool.Count == 0) CreateSlash();
        return slashPool.Dequeue();
    }

    public void Return(MagicCircleView v)
    {
        v.Hide();
        mcPool.Enqueue(v);
    }

    public void Return(SlashView v)
    {
        v.Hide();
        slashPool.Enqueue(v);
    }
}
