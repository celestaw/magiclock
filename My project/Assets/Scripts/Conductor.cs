using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音ゲーの心臓部。AudioSettings.dspTime を基準に曲の経過時間を供給する。
/// テンポマップ対応：BPM変更リストから区間ごとに拍⇔秒を変換する。
/// メトロノーム：拍ごとにクリック音を鳴らす。
/// </summary>
public class Conductor : MonoBehaviour
{
    public static Conductor Instance { get; private set; }

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("譜面情報（外部から上書きされる）")]
    public float bpm = 120f;
    public double firstBeatOffset = 0.0;

    [Header("メトロノーム")]
    public bool metronomeEnabled = false;
    [Range(0f, 1f)] public float metronomeVolume = 0.5f;

    double dspSongStart;
    public bool IsPlaying { get; private set; }

    // --- テンポマップ ---
    // 各区間: (startBeat, bpm, startSeconds) をキャッシュ
    struct TempoSegment
    {
        public double startBeat;
        public double bpm;
        public double startSeconds; // firstBeatOffset からの相対秒
    }
    readonly List<TempoSegment> tempoMap = new List<TempoSegment>();

    // --- メトロノーム ---
    const int clickPoolSize = 4;
    const double clickLookahead = 0.1; // 100ms先読み
    AudioSource[] clickPool;
    int clickPoolIndex;
    AudioClip clickClip;       // 通常拍
    AudioClip accentClip;      // 1拍目（高音・大きめ）
    double nextClickBeat;
    List<TimeSignatureChange> timeSignatures;

    /// <summary>現在拍でのSecPerBeat。UIや簡易計算用。</summary>
    public double SecPerBeat => 60.0 / BpmAtBeat(SongTimeBeats);

    /// <summary>曲の経過時間（秒）。</summary>
    public double SongTime =>
        IsPlaying ? (AudioSettings.dspTime - dspSongStart) : 0.0;

    /// <summary>曲の経過時間（拍）。</summary>
    public double SongTimeBeats => SecondsToBeat(SongTime);

    void Awake()
    {
        Instance = this;
        GenerateClickClip();
        SetupMetronomeSource();
    }

    // ===== テンポマップ構築 =====

    /// <summary>BPM変更リストからテンポマップを構築する。</summary>
    public void BuildTempoMap(float baseBpm, List<BpmChange> changes, List<TimeSignatureChange> ts = null)
    {
        bpm = baseBpm;
        tempoMap.Clear();
        timeSignatures = ts;

        // baseBpmで拍0から開始
        tempoMap.Add(new TempoSegment { startBeat = 0, bpm = baseBpm, startSeconds = 0 });

        if (changes == null || changes.Count == 0) return;

        // ソートしてマージ
        var sorted = new List<BpmChange>(changes);
        sorted.Sort((a, b) => a.beat.CompareTo(b.beat));

        foreach (var ch in sorted)
        {
            if (ch.beat <= 0) // 拍0の変更はbaseBpmを上書き
            {
                tempoMap[0] = new TempoSegment { startBeat = 0, bpm = ch.bpm, startSeconds = 0 };
                continue;
            }

            // 前の区間の末尾から秒を計算
            var prev = tempoMap[tempoMap.Count - 1];
            double deltaBeat = ch.beat - prev.startBeat;
            double deltaSec = deltaBeat * (60.0 / prev.bpm);
            tempoMap.Add(new TempoSegment
            {
                startBeat = ch.beat,
                bpm = ch.bpm,
                startSeconds = prev.startSeconds + deltaSec
            });
        }
    }

    /// <summary>指定拍でのBPMを返す。</summary>
    public double BpmAtBeat(double beat)
    {
        if (tempoMap.Count == 0) return bpm;
        double result = tempoMap[0].bpm;
        for (int i = tempoMap.Count - 1; i >= 0; i--)
        {
            if (beat >= tempoMap[i].startBeat)
            {
                result = tempoMap[i].bpm;
                break;
            }
        }
        return result;
    }

    /// <summary>拍 → 秒。テンポマップを考慮する。</summary>
    public double BeatsToSeconds(double beats)
    {
        double relSec = BeatsToRelativeSeconds(beats);
        return firstBeatOffset + relSec;
    }

    /// <summary>秒 → 拍。テンポマップを考慮する。</summary>
    public double SecondsToBeat(double seconds)
    {
        double rel = seconds - firstBeatOffset;
        if (tempoMap.Count == 0) return rel / (60.0 / bpm);

        // rel がどの区間に入るかを探す
        for (int i = tempoMap.Count - 1; i >= 0; i--)
        {
            if (rel >= tempoMap[i].startSeconds)
            {
                double deltaSec = rel - tempoMap[i].startSeconds;
                double deltaBeat = deltaSec / (60.0 / tempoMap[i].bpm);
                return tempoMap[i].startBeat + deltaBeat;
            }
        }

        // firstBeatOffset より前（負の領域）
        double spb = 60.0 / tempoMap[0].bpm;
        return rel / spb;
    }

    double BeatsToRelativeSeconds(double beats)
    {
        if (tempoMap.Count == 0) return beats * (60.0 / bpm);

        // beats がどの区間に入るかを探す
        for (int i = tempoMap.Count - 1; i >= 0; i--)
        {
            if (beats >= tempoMap[i].startBeat)
            {
                double deltaBeat = beats - tempoMap[i].startBeat;
                double deltaSec = deltaBeat * (60.0 / tempoMap[i].bpm);
                return tempoMap[i].startSeconds + deltaSec;
            }
        }

        // 拍0より前（負の領域）
        double spb = 60.0 / tempoMap[0].bpm;
        return beats * spb;
    }

    // ===== 再生制御 =====

    public void StartSong(double delay = 0.5)
    {
        dspSongStart = AudioSettings.dspTime + delay;
        if (audioSource != null && audioSource.clip != null)
            audioSource.PlayScheduled(dspSongStart);
        IsPlaying = true;
        nextClickBeat = System.Math.Ceiling(SongTimeBeats);
    }

    public void StopSong()
    {
        if (audioSource != null) audioSource.Stop();
        StopAllClicks();
        IsPlaying = false;
    }

    public void PauseSong()
    {
        if (audioSource != null) audioSource.Pause();
        IsPlaying = false;
    }

    public void SeekToBeat(double beat)
    {
        double sec = BeatsToSeconds(beat);
        dspSongStart = AudioSettings.dspTime - sec;
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.time = Mathf.Clamp((float)sec, 0f, audioSource.clip.length);
            if (!audioSource.isPlaying) audioSource.Play();
        }
        IsPlaying = true;
        nextClickBeat = System.Math.Ceiling(beat);
    }

    public void StartFromBeat(double beat, double delay = 0.1)
    {
        double sec = BeatsToSeconds(beat);
        dspSongStart = AudioSettings.dspTime + delay - sec;
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.time = Mathf.Clamp((float)sec, 0f, audioSource.clip.length);
            audioSource.PlayScheduled(AudioSettings.dspTime + delay);
        }
        IsPlaying = true;
        nextClickBeat = System.Math.Ceiling(beat);
    }

    public void StartTimingFromBeat(double beat, double delay = 0.1)
    {
        double sec = BeatsToSeconds(beat);
        dspSongStart = AudioSettings.dspTime + delay - sec;
        IsPlaying = true;
        nextClickBeat = System.Math.Ceiling(beat);
    }

    public void ScheduleAudio(float audioTime, double delay = 0.1)
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.time = Mathf.Clamp(audioTime, 0f, audioSource.clip.length);
            audioSource.PlayScheduled(AudioSettings.dspTime + delay);
        }
    }

    // ===== メトロノーム =====

    void Update()
    {
        if (!IsPlaying || !metronomeEnabled) return;

        // 現在時刻 + lookahead 分までのクリックをDSPスケジュール
        double lookaheadTime = SongTime + clickLookahead;
        double lookaheadBeat = SecondsToBeat(lookaheadTime);

        while (nextClickBeat <= lookaheadBeat)
        {
            ScheduleClick(nextClickBeat);
            nextClickBeat += 1.0;
        }
    }

    bool IsDownbeat(double beat)
    {
        if (timeSignatures == null || timeSignatures.Count == 0) return true;
        var mi = TimeSignatureHelper.BeatToMeasure(timeSignatures, beat);
        double offset = beat - mi.startBeat;
        return offset < 0.01;
    }

    void ScheduleClick(double beat)
    {
        if (clickPool == null) return;

        // 拍 → DSP時刻
        double beatSeconds = BeatsToSeconds(beat);
        double dspTime = dspSongStart + beatSeconds;

        // 過去のクリックはスキップ（停止→再開時など）
        if (dspTime < AudioSettings.dspTime - 0.05) return;

        var src = clickPool[clickPoolIndex];
        clickPoolIndex = (clickPoolIndex + 1) % clickPoolSize;

        src.clip = IsDownbeat(beat) ? accentClip : clickClip;
        src.volume = metronomeVolume;
        src.PlayScheduled(System.Math.Max(dspTime, AudioSettings.dspTime));
    }

    void StopAllClicks()
    {
        if (clickPool == null) return;
        for (int i = 0; i < clickPool.Length; i++)
            if (clickPool[i] != null) clickPool[i].Stop();
    }

    void GenerateClickClip()
    {
        clickClip = GenerateTone(1000f, 0.8f);   // 通常拍: 1000Hz
        accentClip = GenerateTone(1400f, 1.0f);   // 1拍目: 1400Hz, 音量大
    }

    AudioClip GenerateTone(float freq, float amplitude)
    {
        int sampleRate = 44100;
        int length = sampleRate / 50; // 20ms
        var clip = AudioClip.Create($"Click_{freq}", length, 1, sampleRate, false);
        float[] data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (float)i / length;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * amplitude;
        }
        clip.SetData(data, 0);
        return clip;
    }

    void SetupMetronomeSource()
    {
        clickPool = new AudioSource[clickPoolSize];
        for (int i = 0; i < clickPoolSize; i++)
        {
            clickPool[i] = gameObject.AddComponent<AudioSource>();
            clickPool[i].playOnAwake = false;
        }
        clickPoolIndex = 0;
    }
}
