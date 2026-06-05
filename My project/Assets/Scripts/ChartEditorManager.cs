using System;
using System.Collections.Generic;
using UnityEngine;

public class ChartEditorManager : MonoBehaviour
{
    public Chart chart = new Chart();
    public NoteData selectedNote;

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

    public void SelectNote(NoteData note)
    {
        selectedNote = note;
        OnSelectionChanged?.Invoke();
    }

    public void DeselectNote()
    {
        selectedNote = null;
        OnSelectionChanged?.Invoke();
    }

    public NoteData AddNote(double hitBeat, float x = 0f, float y = 0f, int beats = 4, double leadBeat = 4.0)
    {
        var note = new NoteData
        {
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

    public void DeleteNote(NoteData note)
    {
        chart.notes.Remove(note);
        if (selectedNote == note) DeselectNote();
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
        double secPerBeat = 60.0 / bpm;
        chart.audioTrack = new AudioTrackData
        {
            startBeat = 0,
            fileName = fileName,
            durationBeats = clip.length / secPerBeat
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
        OnChartChanged?.Invoke();
        OnSelectionChanged?.Invoke();
        OnAudioTrackChanged?.Invoke();
    }
}
