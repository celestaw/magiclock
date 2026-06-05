using System.IO;
using UnityEngine;

public static class ChartFileIO
{
    static string ChartsDir =>
        Path.Combine(Application.persistentDataPath, "Charts");

    public static void Save(Chart chart, string fileName)
    {
        Directory.CreateDirectory(ChartsDir);
        string json = JsonUtility.ToJson(chart, true);
        File.WriteAllText(Path.Combine(ChartsDir, fileName + ".json"), json);
        Debug.Log($"Chart saved: {fileName}.json");
    }

    public static Chart Load(string fileName)
    {
        string path = Path.Combine(ChartsDir, fileName + ".json");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Chart not found: {path}");
            return null;
        }
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<Chart>(json);
    }

    public static string[] ListFiles()
    {
        if (!Directory.Exists(ChartsDir)) return new string[0];
        string[] files = Directory.GetFiles(ChartsDir, "*.json");
        for (int i = 0; i < files.Length; i++)
            files[i] = Path.GetFileNameWithoutExtension(files[i]);
        return files;
    }
}
