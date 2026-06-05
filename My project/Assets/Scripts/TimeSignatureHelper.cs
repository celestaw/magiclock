using System.Collections.Generic;

public static class TimeSignatureHelper
{
    public static double BeatsPerMeasure(TimeSignatureChange ts)
    {
        return ts.numerator * (4.0 / ts.denominator);
    }

    public static TimeSignatureChange GetTimeSignatureAt(List<TimeSignatureChange> list, int measure)
    {
        TimeSignatureChange result = list[0];
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i].measure <= measure)
                result = list[i];
            else
                break;
        }
        return result;
    }

    public static double MeasureStartBeat(List<TimeSignatureChange> list, int measure)
    {
        double beat = 0;
        int tsIdx = 0;
        for (int m = 0; m < measure; m++)
        {
            while (tsIdx + 1 < list.Count && list[tsIdx + 1].measure <= m)
                tsIdx++;
            beat += BeatsPerMeasure(list[tsIdx]);
        }
        return beat;
    }

    public struct MeasureInfo
    {
        public int measure;
        public double startBeat;
        public double beatsPerMeasure;
        public TimeSignatureChange timeSignature;
    }

    public static MeasureInfo BeatToMeasure(List<TimeSignatureChange> list, double beat)
    {
        double accumulated = 0;
        int tsIdx = 0;
        int m = 0;
        while (true)
        {
            while (tsIdx + 1 < list.Count && list[tsIdx + 1].measure <= m)
                tsIdx++;
            double bpm = BeatsPerMeasure(list[tsIdx]);
            if (accumulated + bpm > beat)
            {
                return new MeasureInfo
                {
                    measure = m,
                    startBeat = accumulated,
                    beatsPerMeasure = bpm,
                    timeSignature = list[tsIdx]
                };
            }
            accumulated += bpm;
            m++;
        }
    }

    public static List<MeasureInfo> BuildMeasureList(List<TimeSignatureChange> list, double upToBeat)
    {
        var result = new List<MeasureInfo>();
        double beat = 0;
        int tsIdx = 0;
        int m = 0;
        while (beat < upToBeat)
        {
            while (tsIdx + 1 < list.Count && list[tsIdx + 1].measure <= m)
                tsIdx++;
            double bpm = BeatsPerMeasure(list[tsIdx]);
            result.Add(new MeasureInfo
            {
                measure = m,
                startBeat = beat,
                beatsPerMeasure = bpm,
                timeSignature = list[tsIdx]
            });
            beat += bpm;
            m++;
        }
        return result;
    }
}
