using UnityEngine;
using UnityEngine.UI;

public class InspectorPanel : MonoBehaviour
{
    public ChartEditorManager manager;
    RectTransform panelRect;

    InputField hitBeatInput, leadBeatInput, xInput, yInput, scaleInput, slashAngleInput;
    Button[] shapeButtons;
    Text shapeLabel;
    Text slashAngleLabel;
    Button[] noteTypeButtons;
    Text noteTypeLabel;
    Button deleteButton;
    Text noSelLabel;

    // Time signature UI
    Text tsLabel;
    InputField tsNumeratorInput;
    Dropdown tsDenominatorDropdown;

    // BPM UI
    Text bpmLabel;
    InputField bpmInput;
    Button bpmRemoveButton;

    // Quantize UI
    Button quantizeToggle;
    Text quantizeToggleLabel;
    Dropdown quantizeDivDropdown;

    ShapeType selectedShape = ShapeType.Triangle;

    static readonly string[] shapeLabels = { "径", "△", "□", "☆", "✡" };
    static readonly ShapeType[] shapeTypes =
    {
        ShapeType.Diameter, ShapeType.Triangle, ShapeType.Square,
        ShapeType.Pentagram, ShapeType.Hexagram
    };

    public void Init(ChartEditorManager mgr, RectTransform parent)
    {
        manager = mgr;
        panelRect = gameObject.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        panelRect.anchorMin = new Vector2(0.75f, 0);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f);

        BuildUI();
        manager.OnSelectionChanged += Refresh;
        manager.OnCurrentBeatChanged += RefreshTimeSignature;
        manager.OnCurrentBeatChanged += RefreshBpm;
        manager.OnTimeSignatureChanged += RefreshTimeSignature;
        manager.OnBpmChanged += RefreshBpm;
        manager.OnQuantizeChanged += RefreshQuantizeUI;
        Refresh();
        RefreshTimeSignature();
        RefreshBpm();
        RefreshQuantizeUI();
    }

    void BuildUI()
    {
        float y = -10;

        var title = MakeLabel("Inspector", 18);
        title.GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        y -= 30;

        // --- Time Signature section (always visible) ---
        MakeLabel("拍子:", 13).GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        tsLabel = MakeLabel("", 13);
        tsLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(60, y);
        y -= 28;

        MakeLabel("分子:", 12).GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        tsNumeratorInput = MakeInputField(y);
        tsNumeratorInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(60, y);
        tsNumeratorInput.GetComponent<RectTransform>().sizeDelta = new Vector2(50, 25);
        tsNumeratorInput.contentType = InputField.ContentType.IntegerNumber;
        tsNumeratorInput.onEndEdit.AddListener(OnTimeSignatureEdited);

        MakeLabel("分母:", 12).GetComponent<RectTransform>().anchoredPosition = new Vector2(120, y);
        BuildDenominatorDropdown(y);
        y -= 35;

        // --- BPM section ---
        MakeLabel("BPM:", 13).GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        bpmLabel = MakeLabel("", 13);
        bpmLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(60, y);
        y -= 28;

        MakeLabel("値:", 12).GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        bpmInput = MakeInputField(y);
        bpmInput.GetComponent<RectTransform>().anchoredPosition = new Vector2(60, y);
        bpmInput.GetComponent<RectTransform>().sizeDelta = new Vector2(70, 25);
        bpmInput.contentType = InputField.ContentType.DecimalNumber;
        bpmInput.onEndEdit.AddListener(OnBpmEdited);

        bpmRemoveButton = MakeButton("x", y, new Color(0.6f, 0.2f, 0.2f));
        var bpmRemRt = bpmRemoveButton.GetComponent<RectTransform>();
        bpmRemRt.anchoredPosition = new Vector2(140, y);
        bpmRemRt.sizeDelta = new Vector2(25, 25);
        bpmRemoveButton.onClick.AddListener(() =>
        {
            var mi = manager.GetCurrentMeasureInfo();
            manager.RemoveBpmChange(mi.measure);
        });
        y -= 35;

        // --- Quantize section ---
        BuildQuantizeUI(ref y);
        y -= 10;

        var sep = MakeLabel("──────────────", 10);
        sep.GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        sep.color = new Color(1, 1, 1, 0.3f);
        y -= 20;

        // --- Note section ---
        noSelLabel = MakeLabel("No note selected", 14);
        noSelLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);

        y -= 30;
        MakeLabel("hitBeat:", 13).GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        hitBeatInput = MakeInputField(y);
        hitBeatInput.onEndEdit.AddListener(v =>
        {
            if (manager.selectedNote != null && double.TryParse(v, out double d))
            {
                manager.selectedNote.hitBeat = d;
                manager.NotifyNoteChanged();
            }
        });

        y -= 35;
        MakeLabel("leadBeat:", 13).GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        leadBeatInput = MakeInputField(y);
        leadBeatInput.onEndEdit.AddListener(v =>
        {
            if (manager.selectedNote != null && double.TryParse(v, out double d))
            {
                manager.selectedNote.leadBeat = System.Math.Max(0.25, d);
                manager.NotifyNoteChanged();
            }
        });

        y -= 35;
        noteTypeLabel = MakeLabel("種別:", 13);
        noteTypeLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        BuildNoteTypeButtons(y);

        y -= 35;
        shapeLabel = MakeLabel("図形:", 13);
        shapeLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        BuildShapeButtons(y);

        y -= 35;
        slashAngleLabel = MakeLabel("角度:", 13);
        slashAngleLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        slashAngleInput = MakeInputField(y);
        slashAngleInput.contentType = InputField.ContentType.DecimalNumber;
        slashAngleInput.onEndEdit.AddListener(v =>
        {
            if (manager.selectedNote != null && float.TryParse(v, out float f))
            {
                manager.selectedNote.slashAngle = f;
                manager.NotifyNoteChanged();
            }
        });

        y -= 40;
        MakeLabel("x:", 13).GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        xInput = MakeInputField(y);
        xInput.onEndEdit.AddListener(v =>
        {
            if (manager.selectedNote != null && float.TryParse(v, out float f))
            {
                manager.selectedNote.x = f;
                manager.NotifyNoteChanged();
            }
        });

        y -= 35;
        MakeLabel("y:", 13).GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        yInput = MakeInputField(y);
        yInput.onEndEdit.AddListener(v =>
        {
            if (manager.selectedNote != null && float.TryParse(v, out float f))
            {
                manager.selectedNote.y = f;
                manager.NotifyNoteChanged();
            }
        });

        y -= 35;
        MakeLabel("大きさ:", 13).GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);
        scaleInput = MakeInputField(y);
        scaleInput.contentType = InputField.ContentType.DecimalNumber;
        scaleInput.onEndEdit.AddListener(v =>
        {
            if (manager.selectedNote != null && float.TryParse(v, out float f))
            {
                manager.selectedNote.scale = Mathf.Max(0.1f, f);
                manager.NotifyNoteChanged();
            }
        });

        y -= 45;
        deleteButton = MakeButton("Delete", y, new Color(0.8f, 0.2f, 0.2f));
        deleteButton.onClick.AddListener(() =>
        {
            if (manager.selectedNotes.Count > 0)
            {
                var toDelete = new System.Collections.Generic.List<NoteData>(manager.selectedNotes);
                foreach (var n in toDelete)
                    manager.chart.notes.Remove(n);
                manager.DeselectNote();
                manager.NotifyNoteChanged();
            }
            else if (manager.selectedNote != null)
                manager.DeleteNote(manager.selectedNote);
        });
    }

    void BuildQuantizeUI(ref float y)
    {
        MakeLabel("クオンタイズ:", 12).GetComponent<RectTransform>().anchoredPosition = new Vector2(10, y);

        // [Q] toggle button
        quantizeToggle = MakeButton("Q", y, new Color(0.25f, 0.25f, 0.3f));
        var qrt = quantizeToggle.GetComponent<RectTransform>();
        qrt.anchoredPosition = new Vector2(110, y);
        qrt.sizeDelta = new Vector2(30, 25);
        quantizeToggleLabel = quantizeToggle.GetComponentInChildren<Text>();
        quantizeToggle.onClick.AddListener(() => manager.ToggleQuantize());

        // Division dropdown
        BuildQuantizeDivDropdown(y);
        y -= 30;
    }

    void BuildQuantizeDivDropdown(float y)
    {
        var go = new GameObject("QuantizeDivDd");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(panelRect, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(145, y);
        rt.sizeDelta = new Vector2(65, 25);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.3f);

        quantizeDivDropdown = go.AddComponent<Dropdown>();
        quantizeDivDropdown.targetGraphic = img;

        // Caption text
        var capGo = new GameObject("Label");
        var crt = capGo.AddComponent<RectTransform>();
        crt.SetParent(rt, false);
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(5, 2);
        crt.offsetMax = new Vector2(-5, -2);
        var capTxt = capGo.AddComponent<Text>();
        capTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        capTxt.fontSize = 11;
        capTxt.color = Color.white;
        capTxt.alignment = TextAnchor.MiddleCenter;
        quantizeDivDropdown.captionText = capTxt;

        // Template
        var tmplGo = new GameObject("Template");
        var tmplRt = tmplGo.AddComponent<RectTransform>();
        tmplRt.SetParent(rt, false);
        tmplRt.anchorMin = new Vector2(0, 0);
        tmplRt.anchorMax = new Vector2(1, 0);
        tmplRt.pivot = new Vector2(0.5f, 1);
        tmplRt.anchoredPosition = Vector2.zero;
        tmplRt.sizeDelta = new Vector2(0, 150);
        tmplGo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
        tmplGo.AddComponent<ScrollRect>();

        var itemGo = new GameObject("Item");
        var irt = itemGo.AddComponent<RectTransform>();
        irt.SetParent(tmplRt, false);
        irt.anchorMin = new Vector2(0, 0.5f);
        irt.anchorMax = new Vector2(1, 0.5f);
        irt.sizeDelta = new Vector2(0, 25);
        var toggle = itemGo.AddComponent<Toggle>();

        var itemLblGo = new GameObject("Item Label");
        var ilrt = itemLblGo.AddComponent<RectTransform>();
        ilrt.SetParent(irt, false);
        ilrt.anchorMin = Vector2.zero;
        ilrt.anchorMax = Vector2.one;
        ilrt.offsetMin = ilrt.offsetMax = Vector2.zero;
        var itemTxt = itemLblGo.AddComponent<Text>();
        itemTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        itemTxt.fontSize = 11;
        itemTxt.color = Color.white;
        itemTxt.alignment = TextAnchor.MiddleCenter;

        quantizeDivDropdown.itemText = itemTxt;
        toggle.targetGraphic = itemGo.AddComponent<Image>();
        toggle.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);
        quantizeDivDropdown.template = tmplRt;
        tmplGo.SetActive(false);

        quantizeDivDropdown.options.Clear();
        foreach (int d in QuantizeHelper.Divisions)
            quantizeDivDropdown.options.Add(new Dropdown.OptionData(QuantizeHelper.Label(d)));

        // Default to 8 (index 4)
        quantizeDivDropdown.value = System.Array.IndexOf(QuantizeHelper.Divisions, 8);

        quantizeDivDropdown.onValueChanged.AddListener(idx =>
        {
            manager.SetQuantizeDivision(QuantizeHelper.Divisions[idx]);
        });
    }

    void RefreshQuantizeUI()
    {
        if (quantizeToggle == null) return;
        var img = quantizeToggle.GetComponent<Image>();
        img.color = manager.quantizeEnabled
            ? new Color(0.3f, 0.6f, 1f)
            : new Color(0.25f, 0.25f, 0.3f);

        int idx = System.Array.IndexOf(QuantizeHelper.Divisions, manager.quantizeDivision);
        if (idx >= 0) quantizeDivDropdown.value = idx;
    }

    void BuildNoteTypeButtons(float y)
    {
        string[] labels = { "魔法陣", "斬撃" };
        NoteType[] types = { NoteType.MagicCircle, NoteType.Slash };
        noteTypeButtons = new Button[types.Length];
        float btnW = 55f;
        float startX = 60f;

        for (int i = 0; i < types.Length; i++)
        {
            var go = new GameObject($"NoteTypeBtn_{types[i]}");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(panelRect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(startX + i * (btnW + 2), y);
            rt.sizeDelta = new Vector2(btnW, 25);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.3f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var lblGo = new GameObject("Lbl");
            var lrt = lblGo.AddComponent<RectTransform>();
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var txt = lblGo.AddComponent<Text>();
            txt.text = labels[i];
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 12;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;

            int idx = i;
            btn.onClick.AddListener(() =>
            {
                if (manager.selectedNote != null)
                {
                    manager.selectedNote.noteType = types[idx];
                    if (types[idx] == NoteType.Slash && manager.selectedNote.slashAngle == 0f)
                        manager.selectedNote.slashAngle = 45f;
                    manager.NotifyNoteChanged();
                }
            });

            noteTypeButtons[i] = btn;
        }
    }

    void BuildShapeButtons(float y)
    {
        int count = shapeTypes.Length;
        shapeButtons = new Button[count];
        float btnW = 36f;
        float startX = 90f;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"ShapeBtn_{shapeTypes[i]}");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(panelRect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(startX + i * (btnW + 2), y);
            rt.sizeDelta = new Vector2(btnW, 25);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.3f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var lblGo = new GameObject("Lbl");
            var lrt = lblGo.AddComponent<RectTransform>();
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var txt = lblGo.AddComponent<Text>();
            txt.text = shapeLabels[i];
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 14;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;

            int capturedIdx = i;
            btn.onClick.AddListener(() =>
            {
                if (manager.selectedNote != null)
                {
                    BeatShapeMap.ApplyShape(manager.selectedNote, shapeTypes[capturedIdx]);
                    manager.NotifyNoteChanged();
                }
            });

            shapeButtons[i] = btn;
        }
    }

    void UpdateShapeButtonColors()
    {
        if (shapeButtons == null) return;
        for (int i = 0; i < shapeButtons.Length; i++)
        {
            var img = shapeButtons[i].GetComponent<Image>();
            img.color = (shapeTypes[i] == selectedShape)
                ? new Color(0.3f, 0.6f, 1f)
                : new Color(0.25f, 0.25f, 0.3f);
        }
    }

    static readonly int[] denominatorValues = { 2, 4, 8, 16 };

    void BuildDenominatorDropdown(float y)
    {
        var go = new GameObject("TsDenomDd");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(panelRect, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(160, y);
        rt.sizeDelta = new Vector2(55, 25);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.3f);

        tsDenominatorDropdown = go.AddComponent<Dropdown>();
        tsDenominatorDropdown.targetGraphic = img;

        // Caption text
        var capGo = new GameObject("Label");
        var crt = capGo.AddComponent<RectTransform>();
        crt.SetParent(rt, false);
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(5, 2);
        crt.offsetMax = new Vector2(-5, -2);
        var capTxt = capGo.AddComponent<Text>();
        capTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        capTxt.fontSize = 12;
        capTxt.color = Color.white;
        capTxt.alignment = TextAnchor.MiddleCenter;
        tsDenominatorDropdown.captionText = capTxt;

        // Template
        var tmplGo = new GameObject("Template");
        var tmplRt = tmplGo.AddComponent<RectTransform>();
        tmplRt.SetParent(rt, false);
        tmplRt.anchorMin = new Vector2(0, 0);
        tmplRt.anchorMax = new Vector2(1, 0);
        tmplRt.pivot = new Vector2(0.5f, 1);
        tmplRt.anchoredPosition = Vector2.zero;
        tmplRt.sizeDelta = new Vector2(0, 100);
        tmplGo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
        tmplGo.AddComponent<ScrollRect>();

        var itemGo = new GameObject("Item");
        var irt = itemGo.AddComponent<RectTransform>();
        irt.SetParent(tmplRt, false);
        irt.anchorMin = new Vector2(0, 0.5f);
        irt.anchorMax = new Vector2(1, 0.5f);
        irt.sizeDelta = new Vector2(0, 25);
        var toggle = itemGo.AddComponent<Toggle>();

        var itemLblGo = new GameObject("Item Label");
        var ilrt = itemLblGo.AddComponent<RectTransform>();
        ilrt.SetParent(irt, false);
        ilrt.anchorMin = Vector2.zero;
        ilrt.anchorMax = Vector2.one;
        ilrt.offsetMin = ilrt.offsetMax = Vector2.zero;
        var itemTxt = itemLblGo.AddComponent<Text>();
        itemTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        itemTxt.fontSize = 12;
        itemTxt.color = Color.white;
        itemTxt.alignment = TextAnchor.MiddleCenter;

        tsDenominatorDropdown.itemText = itemTxt;
        toggle.targetGraphic = itemGo.AddComponent<Image>();
        toggle.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);
        tsDenominatorDropdown.template = tmplRt;
        tmplGo.SetActive(false);

        tsDenominatorDropdown.options.Clear();
        foreach (int d in denominatorValues)
            tsDenominatorDropdown.options.Add(new Dropdown.OptionData(d.ToString()));
        tsDenominatorDropdown.value = 1; // default 4

        tsDenominatorDropdown.onValueChanged.AddListener(_ => OnTimeSignatureEdited(null));
    }

    void OnTimeSignatureEdited(string _)
    {
        var mi = manager.GetCurrentMeasureInfo();
        int num = mi.timeSignature.numerator;
        if (int.TryParse(tsNumeratorInput.text, out int n) && n >= 1)
            num = n;
        int den = denominatorValues[tsDenominatorDropdown.value];
        manager.SetTimeSignature(mi.measure, num, den);
    }

    void RefreshTimeSignature()
    {
        var mi = manager.GetCurrentMeasureInfo();
        var ts = mi.timeSignature;
        tsLabel.text = $"小節{mi.measure + 1}: {ts.numerator}/{ts.denominator}";
        tsNumeratorInput.text = ts.numerator.ToString();
        int denomIdx = System.Array.IndexOf(denominatorValues, ts.denominator);
        if (denomIdx >= 0) tsDenominatorDropdown.value = denomIdx;
    }

    void OnBpmEdited(string _)
    {
        if (!float.TryParse(bpmInput.text, out float val) || val <= 0) return;
        var mi = manager.GetCurrentMeasureInfo();
        manager.SetBpmAtMeasure(mi.measure, val);
    }

    void RefreshBpm()
    {
        var mi = manager.GetCurrentMeasureInfo();
        float currentBpm = manager.GetBpmAtBeat(mi.startBeat);
        bpmLabel.text = $"小節{mi.measure + 1}: {currentBpm:F3}";
        bpmInput.text = currentBpm.ToString("F3");

        // 小節0はbaseBpmなので削除不可
        bool hasChange = mi.measure > 0 &&
            manager.chart.bpmChanges.Exists(c => System.Math.Abs(c.beat - mi.startBeat) < 0.001);
        bpmRemoveButton.gameObject.SetActive(hasChange);
    }

    void Refresh()
    {
        var note = manager.selectedNote;
        int selCount = manager.selectedNotes.Count;
        bool has = note != null;
        bool isMC = has && note.noteType == NoteType.MagicCircle;
        bool isSlash = has && note.noteType == NoteType.Slash;
        bool multi = selCount > 1;

        noSelLabel.gameObject.SetActive(!has);
        if (!has)
            noSelLabel.text = "No note selected";
        else if (multi)
            noSelLabel.gameObject.SetActive(true);

        if (multi)
            noSelLabel.text = $"{selCount} notes selected";
        hitBeatInput.gameObject.SetActive(has);
        leadBeatInput.gameObject.SetActive(has);
        xInput.gameObject.SetActive(has);
        yInput.gameObject.SetActive(has);
        scaleInput.gameObject.SetActive(has);
        deleteButton.gameObject.SetActive(has);

        // Note type buttons
        noteTypeLabel.gameObject.SetActive(has);
        if (noteTypeButtons != null)
            foreach (var b in noteTypeButtons)
                b.gameObject.SetActive(has);

        // Shape buttons: MagicCircle only
        shapeLabel.gameObject.SetActive(isMC);
        if (shapeButtons != null)
            foreach (var b in shapeButtons)
                b.gameObject.SetActive(isMC);

        // Slash angle: Slash only
        slashAngleLabel.gameObject.SetActive(isSlash);
        slashAngleInput.gameObject.SetActive(isSlash);

        if (!has) return;
        hitBeatInput.text = note.hitBeat.ToString("F2");
        leadBeatInput.text = note.leadBeat.ToString("F2");
        xInput.text = note.x.ToString("F2");
        yInput.text = note.y.ToString("F2");
        scaleInput.text = note.scale.ToString("F2");

        if (isSlash)
            slashAngleInput.text = note.slashAngle.ToString("F1");

        // Note type button highlight
        UpdateNoteTypeButtonColors(note.noteType);

        if (isMC)
        {
            selectedShape = note.shape;
            UpdateShapeButtonColors();
        }
    }

    void UpdateNoteTypeButtonColors(NoteType type)
    {
        if (noteTypeButtons == null) return;
        NoteType[] types = { NoteType.MagicCircle, NoteType.Slash };
        for (int i = 0; i < noteTypeButtons.Length; i++)
        {
            var img = noteTypeButtons[i].GetComponent<Image>();
            img.color = (types[i] == type)
                ? new Color(0.3f, 0.6f, 1f)
                : new Color(0.25f, 0.25f, 0.3f);
        }
    }

    Text MakeLabel(string text, int size)
    {
        var go = new GameObject("Lbl");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(panelRect, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(200, 25);
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.color = Color.white;
        return t;
    }

    InputField MakeInputField(float y)
    {
        var go = new GameObject("Input");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(panelRect, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(90, y);
        rt.sizeDelta = new Vector2(120, 25);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.3f);

        var textGo = new GameObject("Text");
        var trt = textGo.AddComponent<RectTransform>();
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(5, 2);
        trt.offsetMax = new Vector2(-5, -2);
        var txt = textGo.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 13;
        txt.color = Color.white;
        txt.supportRichText = false;

        var input = go.AddComponent<InputField>();
        input.textComponent = txt;
        input.targetGraphic = img;
        return input;
    }

    Button MakeButton(string label, float y, Color col)
    {
        var go = new GameObject("Btn_" + label);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(panelRect, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(10, y);
        rt.sizeDelta = new Vector2(200, 30);

        var img = go.AddComponent<Image>();
        img.color = col;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var lbl = new GameObject("Label");
        var lrt = lbl.AddComponent<RectTransform>();
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var txt = lbl.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 14;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        return btn;
    }
}
