/// <summary>
/// 動作確認用のサンプル譜面。あなたが説明した「収束」パターンを含む。
/// GameManagerのchartが空のとき自動で使われる。
/// </summary>
public static class SampleChart
{
    public static Chart Build()
    {
        var c = new Chart { bpm = 120f, firstBeatOffset = 0.0 };

        // (1) 普通の可変長ノーツ：描画される時間が毎回違う
        //     hitOffsetBeats=1 のぶん、描画時間 = leadBeat - 1拍 になる点に注意。
        c.notes.Add(N(4,  2, ShapeType.Diameter,  -3, 0)); // 描画1拍 → 完成 → 1拍待ち
        c.notes.Add(N(8,  4, ShapeType.Triangle,   0, 0)); // 描画3拍
        c.notes.Add(N(13, 6, ShapeType.Pentagram,  3, 0)); // 描画5拍

        // (2) 収束パターン：出現はバラバラ、完成が18-1=17拍に集まり、18拍で1入力。
        //     描画時間が 4→3→2→1拍 と減りつつ、完成タイミングが揃う。
        c.notes.Add(N(18, 5, ShapeType.Triangle,  -3, 0)); // 描画4拍
        c.notes.Add(N(18, 4, ShapeType.Square,    -1, 0)); // 描画3拍
        c.notes.Add(N(18, 3, ShapeType.Pentagram,  1, 0)); // 描画2拍
        c.notes.Add(N(18, 2, ShapeType.Hexagram,   3, 0)); // 描画1拍

        return c;
    }

    static NoteData N(double hit, double lead, ShapeType s, float x, float y)
        => new NoteData { hitBeat = hit, leadBeat = lead, shape = s, x = x, y = y };
}
