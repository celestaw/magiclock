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
        pool.prefab = template;
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
        filePanel.Init(mgr, playback, canvasRt);

        // --- Add-note button on timeline background ---
        var addBtnGo = new GameObject("AddNoteBtn");
        var addRt = addBtnGo.AddComponent<RectTransform>();
        addRt.SetParent(canvasRt, false);
        addRt.anchorMin = new Vector2(0, 0);
        addRt.anchorMax = new Vector2(0, 0);
        addRt.pivot = new Vector2(0, 0);
        addRt.anchoredPosition = new Vector2(10, 10);
        addRt.sizeDelta = new Vector2(100, 28);
        addBtnGo.AddComponent<Image>().color = new Color(0.2f, 0.6f, 0.3f);
        var addBtn = addBtnGo.AddComponent<Button>();
        var addLbl = new GameObject("Lbl");
        var addLrt = addLbl.AddComponent<RectTransform>();
        addLrt.SetParent(addRt, false);
        addLrt.anchorMin = Vector2.zero;
        addLrt.anchorMax = Vector2.one;
        addLrt.offsetMin = addLrt.offsetMax = Vector2.zero;
        var addTxt = addLbl.AddComponent<Text>();
        addTxt.text = "+ Add Note";
        addTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        addTxt.fontSize = 13;
        addTxt.color = Color.white;
        addTxt.alignment = TextAnchor.MiddleCenter;
        addBtn.onClick.AddListener(() =>
        {
            // デフォルト4拍(Triangle)。spawnBeat = currentBeat → hitBeat = currentBeat + 4
            int defaultBeats = 4;
            double hitBeat = mgr.currentBeat + defaultBeats;
            mgr.AddNote(hitBeat, beats: defaultBeats);
        });
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
        scaler.referenceResolution = new Vector2(1280, 720);
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
