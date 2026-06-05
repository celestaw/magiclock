using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EditorPlayback : MonoBehaviour
{
    public ChartEditorManager manager;
    public Conductor conductor;
    public NotePool pool;

    readonly List<RuntimeNote> runtimeNotes = new List<RuntimeNote>();
    bool playing;
    bool audioStartPending;
    double pendingAudioStartBeat;

    public void Init(ChartEditorManager mgr, Conductor cond, NotePool p)
    {
        manager = mgr;
        conductor = cond;
        pool = p;
    }

    public void Play()
    {
        Stop();

        double startBeat = manager.currentBeat;

        conductor.bpm = manager.chart.bpm;
        conductor.firstBeatOffset = manager.chart.firstBeatOffset;

        // Set audio clip
        var at = manager.chart.audioTrack;
        if (manager.audioClip != null && conductor.audioSource != null)
            conductor.audioSource.clip = manager.audioClip;

        runtimeNotes.Clear();
        foreach (var d in manager.chart.notes.OrderBy(n => n.hitBeat))
        {
            // currentBeatより前に終わっているノーツはスキップ
            if (d.hitBeat + 1.0 < startBeat) continue;

            runtimeNotes.Add(new RuntimeNote
            {
                data = d,
                hitTime = conductor.BeatsToSeconds(d.hitBeat),
                startTime = conductor.BeatsToSeconds(d.hitBeat - d.leadBeat)
            });
        }

        // タイミングだけ開始（オーディオは個別管理）
        conductor.StartTimingFromBeat(startBeat);

        // オーディオ再生をaudioTrackの位置に合わせてスケジュール
        if (at != null && manager.audioClip != null && conductor.audioSource != null)
        {
            float audioTime = (float)((startBeat - at.startBeat) * conductor.SecPerBeat);
            if (audioTime < 0)
            {
                // 再生開始位置がオーディオブロックより前 → ブロック到達時に再生開始
                audioStartPending = true;
                pendingAudioStartBeat = at.startBeat;
            }
            else if (audioTime < manager.audioClip.length)
            {
                // オーディオブロックの途中から再生
                conductor.ScheduleAudio(audioTime);
            }
        }

        playing = true;
    }

    public void Stop()
    {
        if (!playing) return;
        playing = false;
        audioStartPending = false;
        conductor.StopSong();

        foreach (var rn in runtimeNotes)
        {
            if (rn.view != null)
            {
                pool.Return(rn.view);
                rn.view = null;
            }
        }
        runtimeNotes.Clear();
    }

    void Update()
    {
        if (!playing) return;

        double t = conductor.SongTime;

        // currentBeatを再生位置に同期（タイムラインの赤線・Previewが追従する）
        manager.SetCurrentBeat(conductor.SongTimeBeats);

        // Deferred audio start: play audio when we reach the audioTrack's startBeat
        if (audioStartPending && conductor.SongTimeBeats >= pendingAudioStartBeat)
        {
            audioStartPending = false;
            if (conductor.audioSource != null && manager.audioClip != null)
            {
                conductor.audioSource.time = 0f;
                conductor.audioSource.Play();
            }
        }
        double visualOffset = 1.0 * conductor.SecPerBeat;

        foreach (var rn in runtimeNotes)
        {
            if (rn.resolved) continue;

            if (!rn.spawned && t >= rn.startTime)
            {
                rn.view = pool.Get();
                rn.view.Setup(rn.data.shape, new Vector3(rn.data.x, rn.data.y, 0f));
                rn.spawned = true;
            }

            if (rn.spawned && rn.view != null)
            {
                double visualEnd = rn.hitTime - visualOffset;
                double lead = visualEnd - rn.startTime;
                float p = lead > 0 ? (float)((t - rn.startTime) / lead) : 1f;
                rn.view.UpdateVisual(Mathf.Clamp01(p));

                // Auto-resolve after hit time + 1 beat
                if (t > rn.hitTime + conductor.SecPerBeat)
                {
                    rn.resolved = true;
                    pool.Return(rn.view);
                    rn.view = null;
                }
            }
        }

        // エディタではノーツ終了後も再生を続ける（手動Stopまで）
    }
}
