using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PreviewPanel : MonoBehaviour
{
    public ChartEditorManager manager;
    RectTransform panelRect;
    RectTransform areaRect;
    Text beatLabel;
    float worldSize = 10f;

    public void Init(ChartEditorManager mgr, RectTransform parent)
    {
        manager = mgr;
        panelRect = gameObject.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        panelRect.anchorMin = new Vector2(0, 0.4f);
        panelRect.anchorMax = new Vector2(0.75f, 0.85f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        var bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.01f, 0.01f, 0.01f);

        // Beat label at top-left
        var lblGo = new GameObject("BeatLabel");
        var lblRt = lblGo.AddComponent<RectTransform>();
        lblRt.SetParent(panelRect, false);
        lblRt.anchorMin = new Vector2(0, 1);
        lblRt.anchorMax = new Vector2(0, 1);
        lblRt.pivot = new Vector2(0, 1);
        lblRt.anchoredPosition = new Vector2(6, -4);
        lblRt.sizeDelta = new Vector2(200, 20);
        beatLabel = lblGo.AddComponent<Text>();
        beatLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        beatLabel.fontSize = 13;
        beatLabel.color = new Color(1, 1, 1, 0.7f);
        beatLabel.text = "Beat: 0.00";

        // 4:3 inner container
        var areaGo = new GameObject("PreviewArea");
        areaRect = areaGo.AddComponent<RectTransform>();
        areaRect.SetParent(panelRect, false);
        areaRect.anchorMin = new Vector2(0.5f, 0f);
        areaRect.anchorMax = new Vector2(0.5f, 1f);
        areaRect.pivot = new Vector2(0.5f, 0.5f);
        areaRect.offsetMin = new Vector2(-200, 2);
        areaRect.offsetMax = new Vector2(200, -2);

        var fitter = areaGo.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 4f / 3f;

        var areaBg = areaGo.AddComponent<Image>();
        areaBg.color = new Color(0.9f, 1.0f, 1.0f);

        manager.OnChartChanged += OnRebuildRequested;
        manager.OnSelectionChanged += OnRebuildRequested;
        manager.OnCurrentBeatChanged += OnRebuildRequested;
        Rebuild();
    }

    void Rebuild()
    {
        for (int i = areaRect.childCount - 1; i >= 0; i--)
            Destroy(areaRect.GetChild(i).gameObject);

        double t = manager.currentBeat;
        beatLabel.text = $"Beat: {t:F2}";

        foreach (var note in manager.chart.notes)
        {
            double spawnBeat = note.hitBeat - note.leadBeat;
            if (t < spawnBeat || t > note.hitBeat) continue;

            var icon = new GameObject("NoteIcon");
            var rt = icon.AddComponent<RectTransform>();
            rt.SetParent(areaRect, false);
            rt.sizeDelta = new Vector2(30, 30);

            float nx = note.x / worldSize * areaRect.rect.width;
            float ny = note.y / worldSize * areaRect.rect.height;
            if (areaRect.rect.width <= 0) nx = note.x / worldSize * 400f;
            if (areaRect.rect.height <= 0) ny = note.y / worldSize * 300f;
            rt.anchoredPosition = new Vector2(nx, ny);

            var img = icon.AddComponent<Image>();
            bool sel = note == manager.selectedNote;
            img.color = sel ? Color.yellow : Color.cyan;

            double lead = note.hitBeat - spawnBeat;
            float progress = lead > 0 ? Mathf.Clamp01((float)((t - spawnBeat) / lead)) : 1f;

            var lbl = new GameObject("Lbl");
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var txt = lbl.AddComponent<Text>();
            txt.text = $"{note.shape.ToString().Substring(0, 1)}\n{(int)(progress * 100)}%";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 10;
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleCenter;

            var handler = icon.AddComponent<PreviewNoteHandler>();
            handler.Init(note, this);
        }
    }

    bool isDragging;

    public void OnNoteDrag(NoteData note, Vector2 delta, RectTransform iconRt)
    {
        float pw = areaRect.rect.width > 0 ? areaRect.rect.width : 400;
        float ph = areaRect.rect.height > 0 ? areaRect.rect.height : 300;
        note.x += delta.x / pw * worldSize;
        note.y += delta.y / ph * worldSize;

        // Move icon directly without Rebuild
        float nx = note.x / worldSize * pw;
        float ny = note.y / worldSize * ph;
        iconRt.anchoredPosition = new Vector2(nx, ny);
    }

    public void OnDragStart()
    {
        isDragging = true;
    }

    public void OnDragEnd()
    {
        isDragging = false;
        manager.NotifyNoteChanged();
    }

    void OnRebuildRequested()
    {
        if (!isDragging) Rebuild();
    }

    public void OnNoteClicked(NoteData note)
    {
        manager.SelectNote(note);
    }
}

public class PreviewNoteHandler : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IInitializePotentialDragHandler
{
    NoteData note;
    PreviewPanel panel;
    Vector2 lastPos;

    public void Init(NoteData n, PreviewPanel p)
    {
        note = n;
        panel = p;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        panel.OnNoteClicked(note);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        lastPos = eventData.position;
        panel.OnDragStart();
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - lastPos;
        lastPos = eventData.position;
        panel.OnNoteDrag(note, delta, GetComponent<RectTransform>());
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        panel.OnDragEnd();
    }
}
