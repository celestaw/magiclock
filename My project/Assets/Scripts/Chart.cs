using System;
using System.Collections.Generic;

/// <summary>ノーツの種別。</summary>
public enum NoteType
{
    MagicCircle, // 魔法陣（従来のノーツ）
    Slash        // 斬撃（予告→攻撃の1拍ノーツ）
}

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
    public NoteType noteType; // ノーツ種別（デフォルト: MagicCircle）
    public double hitBeat;    // 叩く拍
    public double leadBeat;   // 可変長プリロール（拍）。出現拍 = hitBeat - leadBeat
    public ShapeType shape;   // 描く図形（MagicCircle用）
    public float x;           // 出現位置（ワールド座標）
    public float y;
    public float scale = 1f;  // 大きさ倍率（デフォルト1）
    public float slashAngle;  // 斬撃の角度（度、Slash用。0=横、90=縦）
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

/// <summary>BPM変更イベント。指定拍からBPMが変わる。</summary>
[Serializable]
public class BpmChange
{
    public double beat;  // この拍からBPMが変わる
    public float bpm;    // 新しいBPM

    public BpmChange() { }
    public BpmChange(double beat, float bpm)
    {
        this.beat = beat;
        this.bpm = bpm;
    }
}

[Serializable]
public class AudioTrackData
{
    public double startBeat;
    public string fileName;
    public string filePath; // フルパス（再ロード用）
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
    public List<BpmChange> bpmChanges = new List<BpmChange>();
}
