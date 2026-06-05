/// <summary>
/// 拍数とShapeTypeの対応表。
/// 3拍=Diameter, 4拍=Triangle, 5拍=Square, 6拍=Pentagram, 7拍=Hexagram
/// </summary>
public static class BeatShapeMap
{
    public const int MinBeats = 3;
    public const int MaxBeats = 7;

    public static ShapeType ToShape(int beats)
    {
        switch (beats)
        {
            case 3: return ShapeType.Diameter;
            case 4: return ShapeType.Triangle;
            case 5: return ShapeType.Square;
            case 6: return ShapeType.Pentagram;
            case 7: return ShapeType.Hexagram;
            default: return ShapeType.Triangle;
        }
    }

    public static int ToBeats(ShapeType shape)
    {
        switch (shape)
        {
            case ShapeType.Diameter:  return 3;
            case ShapeType.Triangle:  return 4;
            case ShapeType.Square:    return 5;
            case ShapeType.Pentagram: return 6;
            case ShapeType.Hexagram:  return 7;
            default: return 4;
        }
    }

    /// <summary>図形のみ設定する。leadBeatは変更しない。</summary>
    public static void Apply(NoteData note, int beats)
    {
        note.shape = ToShape(beats);
    }

    /// <summary>図形を直接設定する。leadBeatは変更しない。</summary>
    public static void ApplyShape(NoteData note, ShapeType shape)
    {
        note.shape = shape;
    }

    public static string Label(int beats)
    {
        return $"{beats}拍";
    }
}
