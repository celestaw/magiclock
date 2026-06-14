using System;
using System.Collections.Generic;
using UnityEngine;

public class ChartEditorManager : MonoBehaviour
{
    public Chart chart = new Chart();
    public NoteData selectedNote;
    public readonly List<NoteData> selectedNotes = new List<NoteData>();
    List<NoteData> clipboard = new List<NoteData>();

    public double currentBeat; // Preview表示用の現在拍

    public bool quantizeEnabled = false;
    public int quantizeDivision = 8;

    public AudioClip audioClip;

    public event Action OnChartChanged;
    public event Action OnSelectionChanged;
    public event Action OnCurrentBeatChanged;
    public event Action OnTimeSignatureChanged;
    public event Action OnQuantizeChanged;
    public event Action OnAudioTrackChanged;
    public event Action OnBpmChanged;

    public void SelectNote(NoteData note)
    {
        selectedNote = note;
        selectedNotes.Clear();
        selectedNotes.Add(note);
        OnSelectionChanged?.Invoke();
    }

    public void SelectNotes(List<NoteData> notes)
    {
        selectedNotes.Clear();
        selectedNotes.AddRange(notes);
        selectedNote = notes.Count > 0 ? notes[0] : null;
        OnSelectionChanged?.Invoke();
    }

    public void DeselectNote()
    {
        selectedNote = null;
        selectedNotes.Clear();
        OnSelectionChanged?.Invoke();
    }

    public bool IsSelected(NoteData note)
    {
        return selectedNotes.Contains(note);
    }

    public void CopySelection()
    {
        clipboard.Clear();
        foreach (var n in selectedNotes)
            clipboard.Add(CloneNote(n));
    }

    public void CutSelection()
    {
        CopySelection();
        foreach (var n in new List<NoteData>(selectedNotes))
            chart.notes.Remove(n);
        DeselectNote();
        OnChartChanged?.Invoke();
    }

    public void Paste()
    {
        if (clipboard.Count == 0) return;

        // Find the earliest spawnBeat in clipboard
        double minSpawn = double.MaxValue;
        foreach (var n in clipboard)
            minSpawn = Math.Min(minSpawn, n.hitBeat - n.leadBeat);

        double offset = currentBeat - minSpawn;
        var pasted = new List<NoteData>();
        foreach (var src in clipboard)
        {
            var n = CloneNote(src);
            n.hitBeat += offset;
            chart.notes.Add(n);
            pasted.Add(n);
        }
        SelectNotes(pasted);
        OnChartChanged?.Invoke();
    }

    static NoteData CloneNote(NoteData src)
    {
        return new NoteData
        {
            noteType = src.noteType,
            hitBeat = src.hitBeat,
            leadBeat = src.leadBeat,
            shape = src.shape,
            x = src.x,
            y = src.y,
            scale = src.scale,
            slashAngle = src.slashAngle
        };
    }

    public NoteData AddNote(double hitBeat, float x = 0f, float y = 0f, int beats = 4, double leadBeat = 4.0)
    {
        var note = new NoteData
        {
            noteType = NoteType.MagicCircle,
            hitBeat = hitBeat,
            x = x,
            y = y,
            leadBeat = leadBeat
        };
        BeatShapeMap.Apply(note, beats);
        chart.notes.Add(note);
        OnChartChanged?.Invoke();
        return note;
    }

    public NoteData AddSlashNote(double hitBeat, float x = 0f, float y = 0f, double leadBeat = 1.0, float angle = 45f)
    {
        var note = new NoteData
        {
            noteType = NoteType.Slash,
            hitBeat = hitBeat,
            x = x,
            y = y,
            leadBeat = leadBeat,
            slashAngle = angle
        };
        chart.notes.Add(note);
        OnChartChanged?.Invoke();
        return note;
    }

    public void DeleteNote(NoteData note)
    {
        chart.notes.Remove(note);
        selectedNotes.Remove(note);
        if (selectedNote == note)
            selectedNote = selectedNotes.Count > 0 ? selectedNotes[0] : null;
        OnSelectionChanged?.Invoke();
        OnChartChanged?.Invoke();
    }

    public void NotifyNoteChanged()
    {
        OnChartChanged?.Invoke();
        OnSelectionChanged?.Invoke();
    }

    public void SetCurrentBeat(double beat)
    {
        currentBeat = beat;
        OnCurrentBeatChanged?.Invoke();
    }

    public void SetTimeSignature(int measure, int numerator, int denominator)
    {
        var list = chart.timeSignatures;
        list.Sort((a, b) => a.measure.CompareTo(b.measure));
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].measure == measure)
            {
                list[i].numerator = numerator;
                list[i].denominator = denominator;
                OnTimeSignatureChanged?.Invoke();
                OnChartChanged?.Invoke();
                return;
            }
        }
        list.Add(new TimeSignatureChange(measure, numerator, denominator));
        list.Sort((a, b) => a.measure.CompareTo(b.measure));
        OnTimeSignatureChanged?.Invoke();
        OnChartChanged?.Invoke();
    }

    public TimeSignatureChange GetTimeSignatureAt(int measure)
    {
        return TimeSignatureHelper.GetTimeSignatureAt(chart.timeSignatures, measure);
    }

    public TimeSignatureHelper.MeasureInfo GetCurrentMeasureInfo()
    {
        return TimeSignatureHelper.BeatToMeasure(chart.timeSignatures, currentBeat);
    }

    /// <summary>現在小節の先頭拍にBPM変更を設定する。小節0の場合はbaseBpmを変更する。</summary>
    public void SetBpmAtMeasure(int measure, float newBpm)
    {
        if (newBpm <= 0) return;
        double beat = TimeSignatureHelper.MeasureStartBeat(chart.timeSignatures, measure);

        if (measure == 0)
        {
            chart.bpm = newBpm;
            // 拍0のBpmChangeがあれば更新、なければ不要（baseBpmで代用）
            chart.bpmChanges.RemoveAll(c => c.beat < 0.001);
        }
        else
        {
            // 既存のエントリを探して更新 or 追加
            var existing = chart.bpmChanges.Find(c => System.Math.Abs(c.beat - beat) < 0.001);
            if (existing != null)
                existing.bpm = newBpm;
            else
                chart.bpmChanges.Add(new BpmChange(beat, newBpm));
            chart.bpmChanges.Sort((a, b) => a.beat.CompareTo(b.beat));
        }

        OnBpmChanged?.Invoke();
        OnChartChanged?.Invoke();
    }

    /// <summary>指定小節のBPM変更を削除する（小節0は削除不可）。</summary>
    public void RemoveBpmChange(int measure)
    {
        if (measure == 0) return;
        double beat = TimeSignatureHelper.MeasureStartBeat(chart.timeSignatures, measure);
        chart.bpmChanges.RemoveAll(c => System.Math.Abs(c.beat - beat) < 0.001);
        OnBpmChanged?.Invoke();
        OnChartChanged?.Invoke();
    }

    /// <summary>指定拍でのBPMを返す。</summary>
    public float GetBpmAtBeat(double beat)
    {
        float result = chart.bpm;
        foreach (var c in chart.bpmChanges)
        {
            if (c.beat <= beat + 0.001)
                result = c.bpm;
            else
                break;
        }
        return result;
    }

    public void ToggleQuantize()
    {
        quantizeEnabled = !quantizeEnabled;
        OnQuantizeChanged?.Invoke();
    }

    public void SetQuantizeDivision(int div)
    {
        quantizeDivision = div;
        OnQuantizeChanged?.Invoke();
    }

    public void SetAudioTrack(AudioClip clip, string fileName, float bpm)
    {
        audioClip = clip;
        // Conductorがあればテンポマップ経由で正確に変換、なければ初期BPMで概算
        var conductor = Conductor.Instance;
        double durationBeats;
        if (conductor != null)
        {
            conductor.BuildTempoMap(chart.bpm, chart.bpmChanges);
            durationBeats = conductor.SecondsToBeat(clip.length + chart.firstBeatOffset);
        }
        else
        {
            durationBeats = clip.length / (60.0 / bpm);
        }
        chart.audioTrack = new AudioTrackData
        {
            startBeat = 0,
            fileName = fileName,
            durationBeats = durationBeats
        };
        OnAudioTrackChanged?.Invoke();
        OnChartChanged?.Invoke();
    }

    public void RemoveAudioTrack()
    {
        audioClip = null;
        chart.audioTrack = null;
        OnAudioTrackChanged?.Invoke();
        OnChartChanged?.Invoke();
    }

    public void MoveAudioTrack(double newStartBeat)
    {
        if (chart.audioTrack == null) return;
        chart.audioTrack.startBeat = newStartBeat;
        OnAudioTrackChanged?.Invoke();
        OnChartChanged?.Invoke();
    }

    public void LoadChart(Chart c)
    {
        chart = c;
        if (chart.timeSignatures == null || chart.timeSignatures.Count == 0)
            chart.timeSignatures = new System.Collections.Generic.List<TimeSignatureChange>
            {
                new TimeSignatureChange(0, 4, 4)
            };
        selectedNote = null;
        selectedNotes.Clear();
        OnChartChanged?.Invoke();
        OnSelectionChanged?.Invoke();
        OnAudioTrackChanged?.Invoke();
    }
}
