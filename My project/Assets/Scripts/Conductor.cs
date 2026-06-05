using UnityEngine;

/// <summary>
/// 音ゲーの心臓部。Time.deltaTimeの累積ではなく AudioSettings.dspTime を基準にして
/// 曲の経過時間を供給する。可変長プリロールでも誤差が蓄積しないための最重要クラス。
/// </summary>
public class Conductor : MonoBehaviour
{
    public static Conductor Instance { get; private set; }

    [Header("Audio")]
    public AudioSource audioSource;

    [Header("譜面情報（GameManagerからChartの値で上書きされる）")]
    public float bpm = 120f;
    public double firstBeatOffset = 0.0; // 曲頭から「拍0」までの秒数

    double dspSongStart;                 // 曲再生を予約したdspTime
    public bool IsPlaying { get; private set; }

    public double SecPerBeat => 60.0 / bpm;

    /// <summary>曲の経過時間（秒）。再生開始前のリードイン中は負になる。</summary>
    public double SongTime =>
        IsPlaying ? (AudioSettings.dspTime - dspSongStart) : 0.0;

    /// <summary>曲の経過時間（拍）。</summary>
    public double SongTimeBeats => (SongTime - firstBeatOffset) / SecPerBeat;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>delay秒だけ先のdspTimeで再生を予約する。予約再生がブレ防止の肝。</summary>
    public void StartSong(double delay = 0.5)
    {
        dspSongStart = AudioSettings.dspTime + delay;
        if (audioSource != null && audioSource.clip != null)
            audioSource.PlayScheduled(dspSongStart);
        IsPlaying = true;
    }

    public void StopSong()
    {
        if (audioSource != null) audioSource.Stop();
        IsPlaying = false;
    }

    public void PauseSong()
    {
        if (audioSource != null) audioSource.Pause();
        IsPlaying = false;
    }

    public void SeekToBeat(double beat)
    {
        double sec = firstBeatOffset + beat * SecPerBeat;
        dspSongStart = AudioSettings.dspTime - sec;
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.time = Mathf.Clamp((float)sec, 0f, audioSource.clip.length);
            if (!audioSource.isPlaying) audioSource.Play();
        }
        IsPlaying = true;
    }

    /// <summary>AudioClip無しでも指定拍から時間を進め始める。</summary>
    public void StartFromBeat(double beat, double delay = 0.1)
    {
        double sec = firstBeatOffset + beat * SecPerBeat;
        dspSongStart = AudioSettings.dspTime + delay - sec;
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.time = Mathf.Clamp((float)sec, 0f, audioSource.clip.length);
            audioSource.PlayScheduled(AudioSettings.dspTime + delay);
        }
        IsPlaying = true;
    }

    /// <summary>タイミングだけ開始し、オーディオは再生しない。EditorPlaybackがオーディオを個別管理するため。</summary>
    public void StartTimingFromBeat(double beat, double delay = 0.1)
    {
        double sec = firstBeatOffset + beat * SecPerBeat;
        dspSongStart = AudioSettings.dspTime + delay - sec;
        IsPlaying = true;
    }

    /// <summary>指定したオーディオ位置から再生をスケジュールする。</summary>
    public void ScheduleAudio(float audioTime, double delay = 0.1)
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.time = Mathf.Clamp(audioTime, 0f, audioSource.clip.length);
            audioSource.PlayScheduled(AudioSettings.dspTime + delay);
        }
    }

    /// <summary>拍 → 秒。</summary>
    public double BeatsToSeconds(double beats) => firstBeatOffset + beats * SecPerBeat;
}
