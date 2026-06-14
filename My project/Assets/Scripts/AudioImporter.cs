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
        var mp3s = Directory.GetFiles(AudioDir, "*.mp3");
        var m4as = Directory.GetFiles(AudioDir, "*.m4a");
        var all = new string[wavs.Length + oggs.Length + mp3s.Length + m4as.Length];
        wavs.CopyTo(all, 0);
        oggs.CopyTo(all, wavs.Length);
        mp3s.CopyTo(all, wavs.Length + oggs.Length);
        m4as.CopyTo(all, wavs.Length + oggs.Length + mp3s.Length);
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
        // M4A は UnityWebRequest 非対応なので NAudio (Windows) でデコードする
        if (fullPath.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
        {
            AudioClip clip = LoadM4aWithNAudio(fullPath, displayName);
            onLoaded?.Invoke(clip);
            yield break;
        }

        AudioType type;
        if (fullPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            type = AudioType.OGGVORBIS;
        else if (fullPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            type = AudioType.MPEG;
        else
            type = AudioType.WAV;

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

    /// <summary>NAudio の MediaFoundationReader で M4A を読み、AudioClip に変換する（Windows専用）。</summary>
    static AudioClip LoadM4aWithNAudio(string fullPath, string displayName)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            using (var reader = new NAudio.Wave.MediaFoundationReader(fullPath))
            {
                var fmt = reader.WaveFormat;
                int sampleRate = fmt.SampleRate;
                int channels = fmt.Channels;
                int bytesPerSample = fmt.BitsPerSample / 8;

                // 全データ読み込み
                using (var ms = new MemoryStream())
                {
                    reader.CopyTo(ms);
                    byte[] bytes = ms.ToArray();

                    int totalSamples = bytes.Length / bytesPerSample;
                    int samplesPerChannel = totalSamples / channels;
                    float[] data = new float[totalSamples];

                    if (fmt.Encoding == NAudio.Wave.WaveFormatEncoding.IeeeFloat)
                    {
                        // 32bit float
                        for (int i = 0; i < totalSamples; i++)
                            data[i] = BitConverter.ToSingle(bytes, i * 4);
                    }
                    else
                    {
                        // 16bit PCM
                        for (int i = 0; i < totalSamples; i++)
                        {
                            short sample = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
                            data[i] = sample / 32768f;
                        }
                    }

                    var clip = AudioClip.Create(displayName, samplesPerChannel, channels, sampleRate, false);
                    clip.SetData(data, 0);
                    return clip;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"M4A load failed (NAudio): {e.Message}");
            return null;
        }
#else
        Debug.LogError("M4A is only supported on Windows. Please convert to WAV.");
        return null;
#endif
    }
}
