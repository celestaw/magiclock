using UnityEngine;
using UnityEngine.UI;

public class FilePanel : MonoBehaviour
{
    public ChartEditorManager manager;
    public EditorPlayback playback;
    public TimelinePanel timeline;

    InputField bpmInput;
    GameObject fileMenu;
    string lastSavePath; // overwrite save path

    public void Init(ChartEditorManager mgr, EditorPlayback pb, RectTransform parent, TimelinePanel tl = null)
    {
        manager = mgr;
        playback = pb;
        timeline = tl;

        var rt = gameObject.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0.85f);
        rt.anchorMax = new Vector2(0.75f, 1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.18f);

        float x = 10;

        // File menu button
        var fileBtn = MakeButton(rt, ref x, 65, "File");
        fileBtn.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.28f);
        fileBtn.onClick.AddListener(ToggleFileMenu);
        x += 15;

        // BPM
        MakeLabel(rt, ref x, 35, "BPM:");
        bpmInput = MakeInputField(rt, ref x, 85, manager.chart.bpm.ToString("F3"));
        bpmInput.onEndEdit.AddListener(v =>
        {
            if (float.TryParse(v, out float bpm) && bpm > 0)
                manager.SetBpmAtMeasure(0, bpm);
        });
        x += 15;

        // Play
        var playBtn = MakeButton(rt, ref x, 50, "Play");
        playBtn.onClick.AddListener(() => playback.Play());

        // Stop
        var stopBtn = MakeButton(rt, ref x, 50, "Stop");
        stopBtn.onClick.AddListener(() => playback.Stop());
        x += 5;

        // Metronome toggle
        var metBtn = MakeButton(rt, ref x, 50, "Met");
        var metBtnImg = metBtn.GetComponent<Image>();
        metBtnImg.color = new Color(0.3f, 0.3f, 0.4f);
        metBtn.onClick.AddListener(() =>
        {
            var cond = playback.conductor;
            if (cond == null) return;
            cond.metronomeEnabled = !cond.metronomeEnabled;
            metBtnImg.color = cond.metronomeEnabled
                ? new Color(0.2f, 0.7f, 0.3f)
                : new Color(0.3f, 0.3f, 0.4f);
        });
        x += 5;

        // Cursor lock toggle
        var lockBtn = MakeButton(rt, ref x, 50, "Lock");
        var lockBtnImg = lockBtn.GetComponent<Image>();
        lockBtnImg.color = new Color(0.3f, 0.3f, 0.4f);
        lockBtn.onClick.AddListener(() =>
        {
            if (timeline == null) return;
            timeline.cursorLocked = !timeline.cursorLocked;
            lockBtnImg.color = timeline.cursorLocked
                ? new Color(0.2f, 0.7f, 0.3f)
                : new Color(0.3f, 0.3f, 0.4f);
        });
    }

    // ---- File dropdown menu ----

    void ToggleFileMenu()
    {
        if (fileMenu != null) { DismissFileMenu(); return; }
        ShowFileMenu();
    }

    void ShowFileMenu()
    {
        DismissFileMenu();

        var panelRt = gameObject.GetComponent<RectTransform>();

        fileMenu = new GameObject("FileMenu");
        var rt = fileMenu.AddComponent<RectTransform>();
        rt.SetParent(panelRt, false);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(10, -2);
        rt.sizeDelta = new Vector2(160, 190);

        var bg = fileMenu.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.18f, 0.25f);

        // Build menu items without ref parameter (avoids lambda capture issues)
        BuildMenuItems(rt);
    }

    void BuildMenuItems(RectTransform parent)
    {
        float y = -5;

        // Save (overwrite)
        AddMenuItem(parent, y, "Save (Ctrl+S)", OnSaveOverwrite);
        y -= 30;

        // Save As...
        AddMenuItem(parent, y, "Save As...", OnSaveAs);
        y -= 30;

        // Open...
        AddMenuItem(parent, y, "Open...", OnOpen);
        y -= 30;

        // Audio...
        AddMenuItem(parent, y, "Audio...", OnAudio);
        y -= 30;

        // New
        AddMenuItem(parent, y, "New", OnNew);
    }

    void OnSaveOverwrite()
    {
        DismissFileMenu();
        if (!string.IsNullOrEmpty(lastSavePath))
        {
            ChartFileIO.SaveToPath(manager.chart, lastSavePath);
            Debug.Log($"Saved: {lastSavePath}");
        }
        else
        {
            OnSaveAs(); // no path yet, fall back to Save As
        }
    }

    void OnSaveAs()
    {
        DismissFileMenu();
        string path = FileDialogHelper.SaveChartFileDialog("chart");
        if (string.IsNullOrEmpty(path)) return;
        lastSavePath = path;
        ChartFileIO.SaveToPath(manager.chart, path);
        Debug.Log($"Saved: {path}");
    }

    void OnOpen()
    {
        DismissFileMenu();
        string path = FileDialogHelper.OpenChartFileDialog();
        if (string.IsNullOrEmpty(path)) return;
        var chart = ChartFileIO.LoadFromPath(path);
        if (chart == null) return;
        lastSavePath = path;
        manager.LoadChart(chart);
        bpmInput.text = chart.bpm.ToString("F3");
        Debug.Log($"Loaded: {path}");
        TryReloadAudio(chart);
    }

    void OnAudio()
    {
        DismissFileMenu();
        string path = FileDialogHelper.OpenAudioFileDialog();
        if (string.IsNullOrEmpty(path)) return;
        LoadAudioFromPath(path);
    }

    void OnNew()
    {
        DismissFileMenu();
        lastSavePath = null;
        manager.LoadChart(new Chart());
        bpmInput.text = manager.chart.bpm.ToString("F3");
    }

    void AddMenuItem(RectTransform parent, float y, string label, System.Action onClick)
    {
        var go = new GameObject($"MenuItem_{label}");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(4, y);
        rt.sizeDelta = new Vector2(-8, 28);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.32f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.45f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick());

        var lblGo = new GameObject("Lbl");
        var lrt = lblGo.AddComponent<RectTransform>();
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(8, 0);
        lrt.offsetMax = Vector2.zero;
        var txt = lblGo.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 13;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;
    }

    void DismissFileMenu()
    {
        if (fileMenu != null)
        {
            Destroy(fileMenu);
            fileMenu = null;
        }
    }

    void Update()
    {
        // Click outside to dismiss file menu
        if (fileMenu != null)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                var rt = fileMenu.GetComponent<RectTransform>();
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt, mouse.position.ReadValue(), null, out local);
                if (!rt.rect.Contains(local))
                    DismissFileMenu();
            }
        }

        // Ctrl+S: overwrite save
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.ctrlKey.isPressed && kb.sKey.wasPressedThisFrame)
            OnSaveOverwrite();
    }

    // ---- Audio loading ----

    void LoadAudioFromPath(string path)
    {
        string fileName = System.IO.Path.GetFileName(path);
        StartCoroutine(AudioImporter.LoadClipFromPath(path, fileName, clip =>
        {
            if (clip == null) { Debug.LogError("AudioClip load returned null"); return; }
            manager.SetAudioTrack(clip, fileName, manager.chart.bpm);
            if (manager.chart.audioTrack != null)
                manager.chart.audioTrack.filePath = path;
            if (playback.conductor != null && playback.conductor.audioSource != null)
                playback.conductor.audioSource.clip = clip;
        }));
    }

    void TryReloadAudio(Chart chart)
    {
        if (chart.audioTrack == null) return;

        string path = chart.audioTrack.filePath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            Debug.LogWarning($"Audio file not found: {path}");
            return;
        }

        string fileName = chart.audioTrack.fileName;
        StartCoroutine(AudioImporter.LoadClipFromPath(path, fileName, clip =>
        {
            if (clip == null) { Debug.LogError("AudioClip reload returned null"); return; }
            manager.audioClip = clip;
            if (playback.conductor != null && playback.conductor.audioSource != null)
                playback.conductor.audioSource.clip = clip;
            Debug.Log($"Audio reloaded: {path}");
        }));
    }

    // ---- UI helpers ----

    InputField MakeInputField(RectTransform parent, ref float x, float w, string defaultText)
    {
        var go = new GameObject("Input");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0);
        rt.sizeDelta = new Vector2(w, 28);
        x += w;

        go.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);

        var txtGo = new GameObject("Text");
        var trt = txtGo.AddComponent<RectTransform>();
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(4, 0);
        trt.offsetMax = new Vector2(-4, 0);
        var txt = txtGo.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 13;
        txt.color = Color.white;

        var input = go.AddComponent<InputField>();
        input.textComponent = txt;
        input.text = defaultText;
        return input;
    }

    Button MakeButton(RectTransform parent, ref float x, float w, string label)
    {
        var go = new GameObject("Btn_" + label);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0);
        rt.sizeDelta = new Vector2(w, 28);
        x += w;

        go.AddComponent<Image>().color = new Color(0.3f, 0.5f, 0.8f);
        var btn = go.AddComponent<Button>();

        var lbl = new GameObject("Lbl");
        var lrt = lbl.AddComponent<RectTransform>();
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var t = lbl.AddComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 13;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        return btn;
    }

    void MakeLabel(RectTransform parent, ref float x, float w, string text)
    {
        var go = new GameObject("Lbl");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0);
        rt.sizeDelta = new Vector2(w, 28);
        x += w;

        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 13;
        t.color = Color.white;
    }
}
