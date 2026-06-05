using UnityEngine;
using UnityEngine.UI;

public class FilePanel : MonoBehaviour
{
    public ChartEditorManager manager;
    public EditorPlayback playback;

    InputField fileNameInput, bpmInput;
    Dropdown loadDropdown;

    public void Init(ChartEditorManager mgr, EditorPlayback pb, RectTransform parent)
    {
        manager = mgr;
        playback = pb;

        var rt = gameObject.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0.85f);
        rt.anchorMax = new Vector2(0.75f, 1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.18f);

        float x = 10;

        // File name
        fileNameInput = MakeInputField(rt, ref x, 140, "MyChart");
        x += 5;

        // Save
        var saveBtn = MakeButton(rt, ref x, 60, "Save");
        saveBtn.onClick.AddListener(() =>
        {
            string name = fileNameInput.text.Trim();
            if (string.IsNullOrEmpty(name)) name = "MyChart";
            ChartFileIO.Save(manager.chart, name);
            RefreshLoadDropdown();
        });
        x += 5;

        // Load dropdown
        loadDropdown = MakeDropdown(rt, ref x, 140);
        x += 5;

        // Load button
        var loadBtn = MakeButton(rt, ref x, 60, "Load");
        loadBtn.onClick.AddListener(() =>
        {
            if (loadDropdown.options.Count == 0) return;
            string name = loadDropdown.options[loadDropdown.value].text;
            var chart = ChartFileIO.Load(name);
            if (chart != null)
            {
                manager.LoadChart(chart);
                fileNameInput.text = name;
                bpmInput.text = chart.bpm.ToString("F1");
            }
        });
        x += 15;

        // BPM
        MakeLabel(rt, ref x, 35, "BPM:");
        bpmInput = MakeInputField(rt, ref x, 60, manager.chart.bpm.ToString("F1"));
        bpmInput.onEndEdit.AddListener(v =>
        {
            if (float.TryParse(v, out float bpm) && bpm > 0)
                manager.chart.bpm = bpm;
        });
        x += 15;

        // Play
        var playBtn = MakeButton(rt, ref x, 50, "Play");
        playBtn.onClick.AddListener(() => playback.Play());

        // Stop
        var stopBtn = MakeButton(rt, ref x, 50, "Stop");
        stopBtn.onClick.AddListener(() => playback.Stop());
        x += 15;

        // Audio file open
        var audioBtn = MakeButton(rt, ref x, 70, "Audio...");
        audioBtn.onClick.AddListener(() =>
        {
            string path = FileDialogHelper.OpenAudioFileDialog();
            if (string.IsNullOrEmpty(path)) return;
            string fileName = System.IO.Path.GetFileName(path);
            Debug.Log($"Loading audio: {path}");
            StartCoroutine(AudioImporter.LoadClipFromPath(path, fileName, clip =>
            {
                if (clip == null) { Debug.LogError("AudioClip load returned null"); return; }
                Debug.Log($"Audio loaded: {clip.name}, length={clip.length}s");
                manager.SetAudioTrack(clip, fileName, manager.chart.bpm);
                if (playback.conductor != null && playback.conductor.audioSource != null)
                    playback.conductor.audioSource.clip = clip;
            }));
        });

        RefreshLoadDropdown();
    }

    void RefreshLoadDropdown()
    {
        loadDropdown.ClearOptions();
        foreach (var f in ChartFileIO.ListFiles())
            loadDropdown.options.Add(new Dropdown.OptionData(f));
        loadDropdown.RefreshShownValue();
    }

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

    Dropdown MakeDropdown(RectTransform parent, ref float x, float w)
    {
        var go = new GameObject("LoadDD");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0);
        rt.sizeDelta = new Vector2(w, 28);
        x += w;

        go.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);

        // Label
        var lblGo = new GameObject("Label");
        var lrt = lblGo.AddComponent<RectTransform>();
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(4, 0);
        lrt.offsetMax = new Vector2(-20, 0);
        var lblTxt = lblGo.AddComponent<Text>();
        lblTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lblTxt.fontSize = 13;
        lblTxt.color = Color.white;

        // Template
        var tmplGo = new GameObject("Template");
        var tmplRt = tmplGo.AddComponent<RectTransform>();
        tmplRt.SetParent(rt, false);
        tmplRt.anchorMin = new Vector2(0, 0);
        tmplRt.anchorMax = new Vector2(1, 0);
        tmplRt.pivot = new Vector2(0.5f, 1);
        tmplRt.anchoredPosition = Vector2.zero;
        tmplRt.sizeDelta = new Vector2(0, 120);
        tmplGo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
        var scroll = tmplGo.AddComponent<ScrollRect>();

        var vpGo = new GameObject("Viewport");
        var vpRt = vpGo.AddComponent<RectTransform>();
        vpRt.SetParent(tmplRt, false);
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
        vpGo.AddComponent<Image>();
        vpGo.AddComponent<Mask>().showMaskGraphic = true;
        scroll.viewport = vpRt;

        var contentGo = new GameObject("Content");
        var contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.SetParent(vpRt, false);
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.sizeDelta = new Vector2(0, 25);
        scroll.content = contentRt;

        var itemGo = new GameObject("Item");
        var itemRt = itemGo.AddComponent<RectTransform>();
        itemRt.SetParent(contentRt, false);
        itemRt.anchorMin = new Vector2(0, 0.5f);
        itemRt.anchorMax = new Vector2(1, 0.5f);
        itemRt.sizeDelta = new Vector2(0, 25);
        itemGo.AddComponent<Toggle>();

        var itemLblGo = new GameObject("Item Label");
        var itemLblRt = itemLblGo.AddComponent<RectTransform>();
        itemLblRt.SetParent(itemRt, false);
        itemLblRt.anchorMin = Vector2.zero;
        itemLblRt.anchorMax = Vector2.one;
        itemLblRt.offsetMin = new Vector2(4, 0);
        itemLblRt.offsetMax = new Vector2(-4, 0);
        var itemTxt = itemLblGo.AddComponent<Text>();
        itemTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        itemTxt.fontSize = 13;
        itemTxt.color = Color.white;

        tmplGo.SetActive(false);

        var dd = go.AddComponent<Dropdown>();
        dd.template = tmplRt;
        dd.captionText = lblTxt;
        dd.itemText = itemTxt;
        return dd;
    }
}
