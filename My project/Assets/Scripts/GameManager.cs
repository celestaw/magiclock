using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>1判定単位。同じhitTimeを持つノーツをまとめ、1入力で全部解決する。</summary>
public class JudgeEvent
{
    public double hitTime;
    public List<RuntimeNote> notes = new List<RuntimeNote>();
    public bool resolved;
}

/// <summary>譜面データに、計算済みの秒時刻と実体ビューをくっつけた実行時ノーツ。</summary>
public class RuntimeNote
{
    public NoteData data;
    public double startTime; // 出現（秒）
    public double hitTime;   // 叩く（秒）
    public MagicCircleView view;
    public SlashView slashView;
    public bool spawned;
    public bool resolved;
}

public enum Judgement { Perfect, Good, Miss }

/// <summary>
/// 全体をつなぐ司令塔。
/// ・出現：songTimeがstartTimeに達したらプールから取り出して描画開始
/// ・描画：各ノーツ独立に progress を進める（可変長はここだけが気にする）
/// ・判定：hitTimeでグループ化したイベントを1入力ずつ消費（ADOFAI方式）
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("参照")]
    public Conductor conductor;
    public NotePool pool;

    [Header("譜面（空ならSampleChartを使う）")]
    public Chart chart;

    [Header("判定窓（秒）")]
    public double perfectWindow = 0.05;
    public double goodWindow = 0.11;
    public double missWindow = 0.15;

    [Header("演出オフセット（拍）")]
    [Tooltip("0なら完成＝叩く。1なら完成の1拍後に叩く。判定には影響しない描画専用。")]
    public double hitOffsetBeats = 0.0;

    readonly List<RuntimeNote> notes = new List<RuntimeNote>();
    readonly List<JudgeEvent> events = new List<JudgeEvent>();
    int nextEventIndex = 0;

    [Header("結果（読み取り用）")]
    public int score;
    public int combo;
    public int maxCombo;

    [Header("UI")]
    public Text scoreText;
    public Text comboText;

    void Start()
    {
        if (chart == null || chart.notes.Count == 0)
            chart = SampleChart.Build();

        conductor.bpm = chart.bpm;
        conductor.firstBeatOffset = chart.firstBeatOffset;
        conductor.BuildTempoMap(chart.bpm, chart.bpmChanges, chart.timeSignatures);

        BuildNotes();
        BuildEvents();
        conductor.StartSong(0.5);
    }

    void BuildNotes()
    {
        double offset = hitOffsetBeats;
        foreach (var d in chart.notes.OrderBy(n => n.hitBeat).ThenBy(n => n.leadBeat))
        {
            if (d.leadBeat <= offset)
                Debug.LogWarning(
                    $"hitBeat={d.hitBeat} のノーツは leadBeat({d.leadBeat}) が " +
                    $"hitOffsetBeats({offset}) 以下なので、描画されずいきなり完成します。");

            notes.Add(new RuntimeNote
            {
                data = d,
                hitTime = conductor.BeatsToSeconds(d.hitBeat),
                startTime = conductor.BeatsToSeconds(d.hitBeat - d.leadBeat)
            });
        }
    }

    void BuildEvents()
    {
        const double eps = 0.001; // 1ms以内は同時とみなす
        foreach (var rn in notes.OrderBy(n => n.hitTime))
        {
            if (events.Count > 0 && Math.Abs(events[events.Count - 1].hitTime - rn.hitTime) < eps)
                events[events.Count - 1].notes.Add(rn);
            else
            {
                var ev = new JudgeEvent { hitTime = rn.hitTime };
                ev.notes.Add(rn);
                events.Add(ev);
            }
        }
    }

    void Update()
    {
        double t = conductor.SongTime;
        double visualOffset = hitOffsetBeats * conductor.SecPerBeat;

        // --- 出現＆描画更新 ---
        foreach (var rn in notes)
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
                // 完成時刻 = hitTime - visualOffset。各ノーツ独立にprogressを進める。
                double visualEnd = rn.hitTime - visualOffset;
                double lead = visualEnd - rn.startTime;
                float p = lead > 0 ? (float)((t - rn.startTime) / lead) : 1f;
                p = Mathf.Clamp01(p);

                if (rn.data.noteType == NoteType.Slash && rn.slashView != null)
                    rn.slashView.UpdateVisual(p);
                else if (rn.view != null)
                    rn.view.UpdateVisual(p);
            }
        }

        // --- 入力判定 ---
        if (AnyInputPressed()) JudgeInput(t);

        // --- 見逃しMiss ---
        while (nextEventIndex < events.Count &&
               t - events[nextEventIndex].hitTime > missWindow)
        {
            ResolveEvent(events[nextEventIndex], Judgement.Miss);
            nextEventIndex++;
        }
    }

    bool AnyInputPressed()
    {
        // 入力不問（ADOFAI方式）：どのキー・クリック・タップでも1イベント扱い
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        var touch = Touchscreen.current;

        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
        return false;
    }

    void JudgeInput(double t)
    {
        if (nextEventIndex >= events.Count) return;

        var ev = events[nextEventIndex];
        double diff = Math.Abs(t - ev.hitTime);

        Judgement j;
        if (diff < perfectWindow) j = Judgement.Perfect;
        else if (diff < goodWindow) j = Judgement.Good;
        else return; // 早すぎる空打ちは無視（ノーツを消費しない）

        ResolveEvent(ev, j);
        nextEventIndex++;
    }

    void ResolveEvent(JudgeEvent ev, Judgement j)
    {
        if (ev.resolved) return;
        ev.resolved = true;

        switch (j)
        {
            case Judgement.Perfect: score += 1000; combo++; break;
            case Judgement.Good:    score += 500;  combo++; break;
            case Judgement.Miss:    combo = 0;             break;
        }
        maxCombo = Mathf.Max(maxCombo, combo);

        // グループ内の魔法陣を全部同じ判定で片付ける（1入力でn個取る）
        foreach (var rn in ev.notes)
        {
            rn.resolved = true;
            if (rn.view != null)
            {
                pool.Return(rn.view);
                rn.view = null;
            }
            if (rn.slashView != null)
            {
                pool.Return(rn.slashView);
                rn.slashView = null;
            }
        }

        Debug.Log($"{j}  combo:{combo}  score:{score}");
        UpdateUI();
    }

    void UpdateUI()
    {
        if (scoreText != null) scoreText.text = $"Score: {score}";
        if (comboText != null) comboText.text = combo > 0 ? $"{combo} Combo" : "";
    }
}
