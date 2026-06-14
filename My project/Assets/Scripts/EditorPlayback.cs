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

    [Header("判定窓（秒）")]
    public double perfectWindow = 0.05;
    public double goodWindow = 0.11;
    public double missWindow = 0.15;

    // プレビュー時のみ true。エディタ通常再生ではキー入力を判定に使わない。
    public bool judgeEnabled;
    // 判定結果の通知（拍ずれ秒つき）。EditorBootstrapが画面表示に使う。
    public System.Action<Judgement, double> OnJudged;

    readonly List<JudgeEvent> events = new List<JudgeEvent>();
    int nextEventIndex;

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
        conductor.BuildTempoMap(manager.chart.bpm, manager.chart.bpmChanges, manager.chart.timeSignatures);

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

        // 判定イベント構築（同一hitTimeのノーツを1イベントにまとめる）
        events.Clear();
        nextEventIndex = 0;
        const double eps = 0.001;
        foreach (var rn in runtimeNotes.OrderBy(n => n.hitTime))
        {
            if (events.Count > 0 && System.Math.Abs(events[events.Count - 1].hitTime - rn.hitTime) < eps)
                events[events.Count - 1].notes.Add(rn);
            else
            {
                var ev = new JudgeEvent { hitTime = rn.hitTime };
                ev.notes.Add(rn);
                events.Add(ev);
            }
        }

        // タイミングだけ開始（オーディオは個別管理）
        conductor.StartTimingFromBeat(startBeat);

        // オーディオ再生をaudioTrackの位置に合わせてスケジュール
        if (at != null && manager.audioClip != null && conductor.audioSource != null)
        {
            float audioTime = (float)(conductor.BeatsToSeconds(startBeat) - conductor.BeatsToSeconds(at.startBeat));
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
            if (rn.view != null) { pool.Return(rn.view); rn.view = null; }
            if (rn.slashView != null) { pool.Return(rn.slashView); rn.slashView = null; }
        }
        runtimeNotes.Clear();
        events.Clear();
        nextEventIndex = 0;
    }

    public bool IsPlaying => playing;
    public bool suppressSpaceToggle;

    void Update()
    {
        if (!suppressSpaceToggle &&
            UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (playing) Stop(); else Play();
        }

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
        // --- 判定（プレビュー時のみ）---
        if (judgeEnabled)
        {
            // 見逃しMiss：判定窓を過ぎたイベントを順に処理
            while (nextEventIndex < events.Count &&
                   t - events[nextEventIndex].hitTime > missWindow)
            {
                ResolveEvent(events[nextEventIndex], Judgement.Miss, missWindow);
                nextEventIndex++;
            }

            // Esc以外の任意キー／クリック／タップで1入力ぶん判定
            if (AnyJudgeKeyPressed()) JudgeInput(t);
        }

        double visualOffset = 0.0 * conductor.SecPerBeat;

        foreach (var rn in runtimeNotes)
        {
            if (rn.resolved) continue;

            if (!rn.spawned && t >= rn.startTime)
            {
                Vector3 pos = new Vector3(rn.data.x, rn.data.y, 0f);
                if (rn.data.noteType == NoteType.Slash)
                {
                    rn.slashView = pool.GetSlash();
                    rn.slashView.Setup(pos, rn.data.scale, rn.data.slashAngle, (float)rn.data.leadBeat);
                }
                else
                {
                    rn.view = pool.Get();
                    rn.view.Setup(rn.data.shape, pos, rn.data.scale);
                }
                rn.spawned = true;
            }

            if (rn.spawned)
            {
                double visualEnd = rn.hitTime - visualOffset;
                double lead = visualEnd - rn.startTime;
                float p = lead > 0 ? (float)((t - rn.startTime) / lead) : 1f;
                p = Mathf.Clamp01(p);

                if (rn.data.noteType == NoteType.Slash && rn.slashView != null)
                    rn.slashView.UpdateVisual(p);
                else if (rn.view != null)
                    rn.view.UpdateVisual(p);

                // Auto-resolve after hit time + 1 beat
                if (t > rn.hitTime + conductor.SecPerBeat)
                {
                    rn.resolved = true;
                    if (rn.view != null) { pool.Return(rn.view); rn.view = null; }
                    if (rn.slashView != null) { pool.Return(rn.slashView); rn.slashView = null; }
                }
            }
        }

        // エディタではノーツ終了後も再生を続ける（手動Stopまで）
    }

    bool AnyJudgeKeyPressed()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        var touch = UnityEngine.InputSystem.Touchscreen.current;

        // Escはプレビュー終了専用なので判定入力から除外
        if (kb != null && kb.anyKey.wasPressedThisFrame && !kb.escapeKey.wasPressedThisFrame)
            return true;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
        return false;
    }

    void JudgeInput(double t)
    {
        if (nextEventIndex >= events.Count) return;

        var ev = events[nextEventIndex];
        double diff = t - ev.hitTime;          // +が遅れ、-が早入り
        double adiff = System.Math.Abs(diff);

        Judgement j;
        if (adiff < perfectWindow) j = Judgement.Perfect;
        else if (adiff < goodWindow) j = Judgement.Good;
        else return; // 早すぎる空打ちはノーツを消費しない

        ResolveEvent(ev, j, diff);
        nextEventIndex++;
    }

    void ResolveEvent(JudgeEvent ev, Judgement j, double diff)
    {
        if (ev.resolved) return;
        ev.resolved = true;

        foreach (var rn in ev.notes)
        {
            rn.resolved = true;
            if (rn.view != null) { pool.Return(rn.view); rn.view = null; }
            if (rn.slashView != null) { pool.Return(rn.slashView); rn.slashView = null; }
        }

        OnJudged?.Invoke(j, diff);
    }
}
