using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class EditorBootstrap : MonoBehaviour
{
    [Header("任意：曲。未設定なら無音でタイミングだけ進む")]
    public AudioClip song;

    [Header("線の見た目")]
    public float lineWidth = 0.05f;
    public Color ringColor = new Color(0.3f, 0.6f, 1f);
    public Color shapeColor = Color.white;

    void Awake()
    {
        // --- Camera ---
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);

        // --- Conductor ---
        var condGo = new GameObject("Conductor");
        var audio = condGo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.clip = song;
        var conductor = condGo.AddComponent<Conductor>();
        conductor.audioSource = audio;

        // --- MagicCircleView template (for preview playback) ---
        var template = BuildTemplate();

        // --- NotePool ---
        var poolGo = new GameObject("NotePool");
        poolGo.SetActive(false);
        var pool = poolGo.AddComponent<NotePool>();
        pool.magicCirclePrefab = template;
        pool.initialSize = 16;
        poolGo.SetActive(true);

        // --- ChartEditorManager ---
        var mgrGo = new GameObject("ChartEditorManager");
        var mgr = mgrGo.AddComponent<ChartEditorManager>();

        // --- EditorPlayback ---
        var pbGo = new GameObject("EditorPlayback");
        pbGo.SetActive(false);
        var playback = pbGo.AddComponent<EditorPlayback>();
        playback.Init(mgr, conductor, pool);
        pbGo.SetActive(true);

        // --- EventSystem (これが無いとUIクリックが効かない) ---
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        // --- Canvas (UI) ---
        var canvas = CreateCanvas();
        var canvasRt = canvas.GetComponent<RectTransform>();

        // --- Panels ---
        var tlGo = new GameObject("TimelinePanel");
        tlGo.AddComponent<RectTransform>();
        var timeline = tlGo.AddComponent<TimelinePanel>();
        timeline.Init(mgr, canvasRt);

        var pvGo = new GameObject("PreviewPanel");
        pvGo.AddComponent<RectTransform>();
        var preview = pvGo.AddComponent<PreviewPanel>();
        preview.Init(mgr, canvasRt);

        var inspGo = new GameObject("InspectorPanel");
        inspGo.AddComponent<RectTransform>();
        var inspector = inspGo.AddComponent<InspectorPanel>();
        inspector.Init(mgr, canvasRt);

        var fpGo = new GameObject("FilePanel");
        fpGo.AddComponent<RectTransform>();
        var filePanel = fpGo.AddComponent<FilePanel>();
        filePanel.Init(mgr, playback, canvasRt, timeline);

        // --- Note type selector + Add button (left bottom) ---
        BuildNoteTypeBar(mgr, canvasRt);

        // Keep references for preview mode
        editorCanvas = canvas.gameObject;
        editorPlayback = playback;
        editorManager = mgr;
        noteTypeBarParent = canvasRt;
    }

    // Preview mode state
    GameObject editorCanvas;
    GameObject previewUI;
    EditorPlayback editorPlayback;
    ChartEditorManager editorManager;
    RectTransform noteTypeBarParent;
    bool inPreviewMode;
    Text judgeText;
    float judgeTextTimer;
    const float judgeTextDuration = 0.6f;

    NoteType selectedNoteType = NoteType.MagicCircle;
    Image mcBtnImg, slashBtnImg;

    void BuildNoteTypeBar(ChartEditorManager mgr, RectTransform canvasRt)
    {
        float x = 10f;
        float y = 10f;

        // 「魔法陣」ボタン
        mcBtnImg = MakeBottomButton(canvasRt, "魔法陣", x, y, 70, new Color(0.2f, 0.5f, 0.7f), () =>
        {
            selectedNoteType = NoteType.MagicCircle;
            UpdateNoteTypeBar();
        });
        x += 74;

        // 「斬撃」ボタン
        slashBtnImg = MakeBottomButton(canvasRt, "斬撃", x, y, 55, new Color(0.5f, 0.2f, 0.2f), () =>
        {
            selectedNoteType = NoteType.Slash;
            UpdateNoteTypeBar();
        });
        x += 64;

        // 「+ 追加」ボタン
        MakeBottomButton(canvasRt, "+ 追加", x, y, 70, new Color(0.2f, 0.6f, 0.3f), () =>
        {
            if (selectedNoteType == NoteType.Slash)
            {
                double hitBeat = mgr.currentBeat + 1.0;
                mgr.AddSlashNote(hitBeat, leadBeat: 1.0);
            }
            else
            {
                int defaultBeats = 4;
                double hitBeat = mgr.currentBeat + defaultBeats;
                mgr.AddNote(hitBeat, beats: defaultBeats);
            }
        });
        x += 80;

        // 「Preview」ボタン
        MakeBottomButton(canvasRt, "Preview", x, y, 75, new Color(0.6f, 0.4f, 0.1f), () =>
        {
            ShowPreviewDialog(canvasRt);
        });

        UpdateNoteTypeBar();
    }

    void UpdateNoteTypeBar()
    {
        mcBtnImg.color = selectedNoteType == NoteType.MagicCircle
            ? new Color(0.3f, 0.6f, 1f)
            : new Color(0.2f, 0.25f, 0.3f);
        slashBtnImg.color = selectedNoteType == NoteType.Slash
            ? new Color(1f, 0.4f, 0.4f)
            : new Color(0.2f, 0.25f, 0.3f);
    }

    Image MakeBottomButton(RectTransform parent, string label, float x, float y, float w,
        Color col, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Btn_{label}");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, 28);
        var img = go.AddComponent<Image>();
        img.color = col;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var lblGo = new GameObject("Lbl");
        var lrt = lblGo.AddComponent<RectTransform>();
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var txt = lblGo.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 13;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        return img;
    }

    void Update()
    {
        if (!inPreviewMode) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            ExitPreviewMode();
            return;
        }

        // 判定テキストのフェードアウト
        if (judgeText != null && judgeTextTimer > 0f)
        {
            judgeTextTimer -= Time.deltaTime;
            var c = judgeText.color;
            c.a = Mathf.Clamp01(judgeTextTimer / judgeTextDuration);
            judgeText.color = c;
        }
    }

    void OnPreviewJudged(Judgement j, double diff)
    {
        if (judgeText == null) return;

        string label;
        Color c;
        switch (j)
        {
            case Judgement.Perfect: label = "PERFECT"; c = new Color(1f, 0.9f, 0.3f); break;
            case Judgement.Good:    label = "GOOD";    c = new Color(0.4f, 0.8f, 1f); break;
            default:                label = "MISS";    c = new Color(1f, 0.4f, 0.4f); break;
        }

        // Miss以外は早い/遅いのズレ量(ms)を併記
        if (j != Judgement.Miss)
        {
            int ms = (int)System.Math.Round(diff * 1000.0);
            label += ms >= 0 ? $"  (+{ms}ms 遅)" : $"  ({ms}ms 早)";
        }

        judgeText.text = label;
        judgeText.color = c;
        judgeTextTimer = judgeTextDuration;
    }

    GameObject previewDialog;

    void ShowPreviewDialog(RectTransform parent)
    {
        if (inPreviewMode) return;
        if (previewDialog != null) { Destroy(previewDialog); previewDialog = null; }

        // Full-screen overlay to catch clicks outside the dialog
        previewDialog = new GameObject("PreviewDialogOverlay");
        var overlayRt = previewDialog.AddComponent<RectTransform>();
        overlayRt.SetParent(parent, false);
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = overlayRt.offsetMax = Vector2.zero;
        var overlayImg = previewDialog.AddComponent<Image>();
        overlayImg.color = new Color(0, 0, 0, 0.3f);
        var overlayBtn = previewDialog.AddComponent<Button>();
        overlayBtn.targetGraphic = overlayImg;
        overlayBtn.onClick.AddListener(() => { Destroy(previewDialog); previewDialog = null; });

        // Dialog box
        var dialogGo = new GameObject("DialogBox");
        var rt = dialogGo.AddComponent<RectTransform>();
        rt.SetParent(overlayRt, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(280, 120);

        var bg = dialogGo.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f);

        // Title
        var titleGo = new GameObject("Title");
        var trt = titleGo.AddComponent<RectTransform>();
        trt.SetParent(rt, false);
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = new Vector2(0, -8);
        trt.sizeDelta = new Vector2(0, 24);
        var ttxt = titleGo.AddComponent<Text>();
        ttxt.text = "Preview";
        ttxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ttxt.fontSize = 15;
        ttxt.color = Color.white;
        ttxt.alignment = TextAnchor.MiddleCenter;

        // "From Beginning" button
        MakeDialogButton(rt, "From Beginning", new Vector2(-70, -20), () =>
        {
            Destroy(previewDialog); previewDialog = null;
            EnterPreviewMode(false);
        });

        // "From Current Position" button
        MakeDialogButton(rt, "From Current Position", new Vector2(-70, -52), () =>
        {
            Destroy(previewDialog); previewDialog = null;
            EnterPreviewMode(true);
        });
    }

    void MakeDialogButton(RectTransform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Btn_{label}");
        var brt = go.AddComponent<RectTransform>();
        brt.SetParent(parent, false);
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1);
        brt.pivot = new Vector2(0, 1);
        brt.anchoredPosition = pos;
        brt.sizeDelta = new Vector2(240, 28);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.35f, 0.45f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var lblGo = new GameObject("Lbl");
        var lrt = lblGo.AddComponent<RectTransform>();
        lrt.SetParent(brt, false);
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var txt = lblGo.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 13;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    void EnterPreviewMode(bool fromCurrent)
    {
        if (inPreviewMode) return;
        inPreviewMode = true;

        // Hide editor UI
        editorCanvas.SetActive(false);

        // Dark background
        Camera.main.backgroundColor = new Color(0.02f, 0.02f, 0.05f);

        // Create minimal preview UI (Esc hint)
        previewUI = new GameObject("PreviewUI");
        var canvas = previewUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = previewUI.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;

        var hintGo = new GameObject("Hint");
        var hrt = hintGo.AddComponent<RectTransform>();
        hrt.SetParent(previewUI.GetComponent<RectTransform>(), false);
        hrt.anchorMin = new Vector2(0, 1);
        hrt.anchorMax = new Vector2(0, 1);
        hrt.pivot = new Vector2(0, 1);
        hrt.anchoredPosition = new Vector2(10, -10);
        hrt.sizeDelta = new Vector2(250, 30);
        var txt = hintGo.AddComponent<Text>();
        txt.text = "Esc: Back to Editor  /  任意キー・クリックで判定";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 16;
        txt.color = new Color(1, 1, 1, 0.5f);

        // Judgment feedback text (center)
        var judgeGo = new GameObject("JudgeText");
        var jrt = judgeGo.AddComponent<RectTransform>();
        jrt.SetParent(previewUI.GetComponent<RectTransform>(), false);
        jrt.anchorMin = jrt.anchorMax = new Vector2(0.5f, 0.5f);
        jrt.pivot = new Vector2(0.5f, 0.5f);
        jrt.anchoredPosition = new Vector2(0, 160);
        jrt.sizeDelta = new Vector2(700, 90);
        judgeText = judgeGo.AddComponent<Text>();
        judgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        judgeText.fontSize = 54;
        judgeText.fontStyle = FontStyle.Bold;
        judgeText.alignment = TextAnchor.MiddleCenter;
        judgeText.color = new Color(1, 1, 1, 0);
        judgeTextTimer = 0f;

        // Start playback (判定を有効化してから再生)
        if (!fromCurrent)
            editorManager.SetCurrentBeat(0);
        editorPlayback.suppressSpaceToggle = true;
        editorPlayback.judgeEnabled = true;
        editorPlayback.OnJudged += OnPreviewJudged;
        editorPlayback.Play();
    }

    void ExitPreviewMode()
    {
        if (!inPreviewMode) return;
        inPreviewMode = false;

        editorPlayback.judgeEnabled = false;
        editorPlayback.OnJudged -= OnPreviewJudged;
        editorPlayback.Stop();
        editorPlayback.suppressSpaceToggle = false;
        judgeText = null;

        if (previewUI != null)
        {
            Destroy(previewUI);
            previewUI = null;
        }

        // Restore editor UI
        editorCanvas.SetActive(true);
    }

    MagicCircleView BuildTemplate()
    {
        var root = new GameObject("MagicCircleTemplate");
        var view = root.AddComponent<MagicCircleView>();
        view.ringDrawer = MakeDrawer(root.transform, "Ring", ringColor);
        view.shapeDrawer = MakeDrawer(root.transform, "Shape", shapeColor);
        root.SetActive(false);
        return view;
    }

    Canvas CreateCanvas()
    {
        var go = new GameObject("EditorCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    ShapeDrawer MakeDrawer(Transform parent, string childName, Color color)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.positionCount = 0;
        lr.startWidth = lr.endWidth = lineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;
        lr.numCapVertices = 4;
        return go.AddComponent<ShapeDrawer>();
    }
}
