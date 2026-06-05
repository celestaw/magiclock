using System;
using System.Collections.Generic;

/// <summary>魔法陣の図形種類。ShapeLibraryで点列に変換される。</summary>
public enum ShapeType
{
    Diameter,   // 直径の線（一番シンプル）
    Triangle,   // 三角形
    Square,     // 四角形
    Pentagram,  // 五芒星
    Hexagram    // 六芒星（近似）
}

/// <summary>
/// 1ノーツの譜面データ。拍で記述する（曲に追従しやすい）。
/// 判定が使うのは hitBeat のみ。leadBeat は描画専用＝可変長プリロールの正体。
/// </summary>
[Serializable]
public class NoteData
{
    public double hitBeat;   // 叩く拍
    public double leadBeat;  // 可変長プリロール（拍）。出現拍 = hitBeat - leadBeat
    public ShapeType shape;  // 描く図形
    public float x;          // 出現位置（ワールド座標）
    public float y;
}

[Serializable]
public class TimeSignatureChange
{
    public int measure;      // この小節番号(0-based)から適用
    public int numerator;    // 分子
    public int denominator;  // 分母 (2, 4, 8, 16)

    public TimeSignatureChange() { }
    public TimeSignatureChange(int measure, int numerator, int denominator)
    {
        this.measure = measure;
        this.numerator = numerator;
        this.denominator = denominator;
    }
}

[Serializable]
public class AudioTrackData
{
    public double startBeat;
    public string fileName;
    public double durationBeats;
}

/// <summary>1曲分の譜面。JsonUtilityでJSON化／読込が可能。</summary>
[Serializable]
public class Chart
{
    public AudioTrackData audioTrack;
    public float bpm = 120f;
    public double firstBeatOffset = 0.0;
    public List<NoteData> notes = new List<NoteData>();
    public List<TimeSignatureChange> timeSignatures = new List<TimeSignatureChange>
    {
        new TimeSignatureChange(0, 4, 4)
    };
}
