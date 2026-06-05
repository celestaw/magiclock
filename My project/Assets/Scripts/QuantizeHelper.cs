using System;

public static class QuantizeHelper
{
    /// <summary>全音符あたりの分割数の選択肢。</summary>
    public static readonly int[] Divisions = { 3, 4, 5, 6, 8, 10, 12, 15, 16, 24, 32, 48 };

    /// <summary>
    /// beatを指定divisionのグリッドにスナップする。
    /// division = 全音符あたりの分割数。グリッド間隔 = 4.0 / division (拍単位)。
    /// </summary>
    public static double Snap(double beat, int division)
    {
        double grid = 4.0 / division;
        return Math.Round(beat / grid) * grid;
    }

    /// <summary>分割数に対応するラベル(例: "8分")。</summary>
    public static string Label(int division)
    {
        return $"{division}分";
    }
}
