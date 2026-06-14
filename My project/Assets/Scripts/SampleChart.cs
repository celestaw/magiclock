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
        //     hitOffsetBeats=0 なので、描画時間 = leadBeat、完成した瞬間が判定。
        c.notes.Add(N(4,  2, ShapeType.Diameter,  -3, 0)); // 描画2拍 → 完成＝判定
        c.notes.Add(N(8,  4, ShapeType.Triangle,   0, 0)); // 描画3拍
        c.notes.Add(N(13, 6, ShapeType.Pentagram,  3, 0)); // 描画5拍

        // (2) 収束パターン：出現はバラバラ、完成が18-1=17拍に集まり、18拍で1入力。
        //     描画時間が 4→3→2→1拍 と減りつつ、完成タイミングが揃う。
        c.notes.Add(N(18, 5, ShapeType.Triangle,  -3, 0)); // 描画4拍
        c.notes.Add(N(18, 4, ShapeType.Square,    -1, 0)); // 描画3拍
        c.notes.Add(N(18, 3, ShapeType.Pentagram,  1, 0)); // 描画2拍
        c.notes.Add(N(18, 2, ShapeType.Hexagram,   3, 0)); // 描画1拍

        // (3) 斬撃ノーツ：溜め→斬撃の演出
        c.notes.Add(Slash(22, 2,  0,  2, 45f));   // 右上斬り、2拍猶予
        c.notes.Add(Slash(25, 1,  0, -2, -30f));  // 左下斬り、1拍猶予（短い）
        c.notes.Add(Slash(28, 3, -2,  0, 90f));   // 縦斬り、3拍猶予（長め）

        // (4) 魔法陣＋斬撃の混合：同時押し
        c.notes.Add(N(32, 4, ShapeType.Triangle, -2, 0));
        c.notes.Add(Slash(32, 2, 2, 0, 60f));

        return c;
    }

    static NoteData N(double hit, double lead, ShapeType s, float x, float y)
        => new NoteData { noteType = NoteType.MagicCircle, hitBeat = hit, leadBeat = lead, shape = s, x = x, y = y };

    static NoteData Slash(double hit, double lead, float x, float y, float angle)
        => new NoteData { noteType = NoteType.Slash, hitBeat = hit, leadBeat = lead, x = x, y = y, slashAngle = angle, scale = 1f };
}
