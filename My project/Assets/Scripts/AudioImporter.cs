using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class AudioImporter
{
    public static string AudioDir =>
        Path.Combine(Application.persistentDataPath, "Audio");

    public static string[] ListFiles()
    {
        if (!Directory.Exists(AudioDir)) return new string[0];
        var wavs = Directory.GetFiles(AudioDir, "*.wav");
        var oggs = Directory.GetFiles(AudioDir, "*.ogg");
        var all = new string[wavs.Length + oggs.Length];
        wavs.CopyTo(all, 0);
        oggs.CopyTo(all, wavs.Length);
        for (int i = 0; i < all.Length; i++)
            all[i] = Path.GetFileName(all[i]);
        return all;
    }

    public static IEnumerator LoadClip(string fileName, Action<AudioClip> onLoaded)
    {
        string path = Path.Combine(AudioDir, fileName);
        return LoadClipFromPath(path, fileName, onLoaded);
    }

    /// <summary>任意のフルパスから AudioClip をロードする。</summary>
    public static IEnumerator LoadClipFromPath(string fullPath, string displayName, Action<AudioClip> onLoaded)
    {
        AudioType type = fullPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
            ? AudioType.OGGVORBIS : AudioType.WAV;

        string uri = "file:///" + fullPath.Replace('\\', '/');
        using (var req = UnityWebRequestMultimedia.GetAudioClip(uri, type))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = displayName;
                onLoaded?.Invoke(clip);
            }
            else
            {
                Debug.LogError($"Audio load failed: {req.error}");
                onLoaded?.Invoke(null);
            }
        }
    }
}
