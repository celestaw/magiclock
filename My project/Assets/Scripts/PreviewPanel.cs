using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PreviewPanel : MonoBehaviour
{
    public ChartEditorManager manager;
    RectTransform panelRect;
    RectTransform areaRect;
    RectTransform gridContainer;
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
        areaBg.color = new Color(0.08f, 0.08f, 0.12f);

        // Grid container (persists across Rebuild)
        var gridGo = new GameObject("Grid");
        gridContainer = gridGo.AddComponent<RectTransform>();
        gridContainer.SetParent(areaRect, false);
        gridContainer.anchorMin = Vector2.zero;
        gridContainer.anchorMax = Vector2.one;
        gridContainer.offsetMin = gridContainer.offsetMax = Vector2.zero;
        BuildGrid();

        manager.OnChartChanged += OnRebuildRequested;
        manager.OnSelectionChanged += OnRebuildRequested;
        manager.OnCurrentBeatChanged += OnRebuildRequested;
        Rebuild();
    }

    void Rebuild()
    {
        for (int i = areaRect.childCount - 1; i >= 0; i--)
        {
            var child = areaRect.GetChild(i);
            if (child == gridContainer) continue;
            Destroy(child.gameObject);
        }

        double t = manager.currentBeat;
        beatLabel.text = $"Beat: {t:F2}";

        foreach (var note in manager.chart.notes)
        {
            double spawnBeat = note.hitBeat - note.leadBeat;
            if (t < spawnBeat || t > note.hitBeat) continue;

            float baseSize = 30f;
            float iconSize = baseSize * note.scale;

            var icon = new GameObject("NoteIcon");
            var rt = icon.AddComponent<RectTransform>();
            rt.SetParent(areaRect, false);
            rt.sizeDelta = new Vector2(iconSize, iconSize);

            float pw = areaRect.rect.width > 0 ? areaRect.rect.width : 400f;
            float ph = areaRect.rect.height > 0 ? areaRect.rect.height : 300f;
            float nx = note.x / worldSize * pw;
            float ny = note.y / worldSize * ph;
            rt.anchoredPosition = new Vector2(nx, ny);

            var img = icon.AddComponent<Image>();
            bool sel = manager.IsSelected(note);
            bool isSlash = note.noteType == NoteType.Slash;
            img.color = sel ? Color.yellow : (isSlash ? new Color(1f, 0.4f, 0.4f) : Color.cyan);

            double lead = note.hitBeat - spawnBeat;
            float progress = lead > 0 ? Mathf.Clamp01((float)((t - spawnBeat) / lead)) : 1f;

            var lbl = new GameObject("Lbl");
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var txt = lbl.AddComponent<Text>();
            string icon_label = isSlash ? "\u2694" : note.shape.ToString().Substring(0, 1);
            txt.text = $"{icon_label}\n{(int)(progress * 100)}%";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 10;
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleCenter;

            var handler = icon.AddComponent<PreviewNoteHandler>();
            handler.Init(note, this);

            // Corner resize handles (all 4 corners) - only for primary selected note
            if (note == manager.selectedNote)
            {
                Vector2[] corners = {
                    new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(0, 1), new Vector2(1, 1)
                };
                foreach (var corner in corners)
                {
                    var handle = new GameObject("ResizeHandle");
                    var hrt = handle.AddComponent<RectTransform>();
                    hrt.SetParent(rt, false);
                    hrt.anchorMin = hrt.anchorMax = corner;
                    hrt.pivot = new Vector2(0.5f, 0.5f);
                    hrt.anchoredPosition = Vector2.zero;
                    hrt.sizeDelta = new Vector2(10, 10);
                    var himg = handle.AddComponent<Image>();
                    himg.color = Color.white;
                    var resizer = handle.AddComponent<PreviewResizeHandler>();
                    // opposite corner anchor
                    Vector2 opposite = new Vector2(1f - corner.x, 1f - corner.y);
                    resizer.Init(note, this, rt, opposite);
                }
            }
        }
    }

    bool isDragging;

    void BuildGrid()
    {
        // worldSize range: -worldSize..+worldSize mapped to area width/height
        // Draw lines every 1 world unit
        int divisions = (int)(worldSize * 2); // e.g. 20 lines each axis
        Color lineCol = new Color(1f, 1f, 1f, 0.12f);
        Color axisCol = new Color(1f, 1f, 1f, 0.35f);

        for (int i = 0; i <= divisions; i++)
        {
            float frac = (float)i / divisions; // 0..1
            bool isCenter = (i == divisions / 2);
            Color col = isCenter ? axisCol : lineCol;

            // Vertical line
            var vGo = new GameObject("GridV");
            var vRt = vGo.AddComponent<RectTransform>();
            vRt.SetParent(gridContainer, false);
            vRt.anchorMin = new Vector2(frac, 0);
            vRt.anchorMax = new Vector2(frac, 1);
            vRt.sizeDelta = new Vector2(isCenter ? 1.5f : 1f, 0);
            var vImg = vGo.AddComponent<Image>();
            vImg.color = col;
            vImg.raycastTarget = false;

            // Horizontal line
            var hGo = new GameObject("GridH");
            var hRt = hGo.AddComponent<RectTransform>();
            hRt.SetParent(gridContainer, false);
            hRt.anchorMin = new Vector2(0, frac);
            hRt.anchorMax = new Vector2(1, frac);
            hRt.sizeDelta = new Vector2(0, isCenter ? 1.5f : 1f);
            var hImg = hGo.AddComponent<Image>();
            hImg.color = col;
            hImg.raycastTarget = false;
        }
    }

    public void OnNoteResize(NoteData note, Vector2 screenPos, RectTransform iconRt,
        Vector2 pivotAnchor, float initialDist, float initialScale,
        Vector2 initialNotePos)
    {
        // Compute opposite corner position in screen space (using initial scale, stable)
        Vector2 pivotScreen = GetCornerScreenPos(iconRt, pivotAnchor, initialScale);
        float currentDist = Vector2.Distance(screenPos, pivotScreen);

        if (initialDist <= 1f) return;

        float newScale = Mathf.Max(0.1f, initialScale * (currentDist / initialDist));
        float baseSize = 30f;
        float oldHalf = baseSize * initialScale * 0.5f;
        float newHalf = baseSize * newScale * 0.5f;

        // The pivot corner offset from center: (pivotAnchor - 0.5) * size
        // Keep that corner in the same world position by shifting note center
        float pw = areaRect.rect.width > 0 ? areaRect.rect.width : 400f;
        float ph = areaRect.rect.height > 0 ? areaRect.rect.height : 300f;

        // Shift in local pixels = difference in pivot corner offset
        float dx = (pivotAnchor.x - 0.5f) * 2f * (newHalf - oldHalf);
        float dy = (pivotAnchor.y - 0.5f) * 2f * (newHalf - oldHalf);

        // Convert pixel shift to world coords and apply to initial position
        note.x = initialNotePos.x - dx / pw * worldSize;
        note.y = initialNotePos.y - dy / ph * worldSize;
        note.scale = newScale;

        float iconSize = baseSize * newScale;
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);

        float nx = note.x / worldSize * pw;
        float ny = note.y / worldSize * ph;
        iconRt.anchoredPosition = new Vector2(nx, ny);
    }

    /// <summary>Get a corner's screen position using the initial scale (stable pivot).</summary>
    Vector2 GetCornerScreenPos(RectTransform rt, Vector2 anchor, float scale)
    {
        float baseSize = 30f;
        float halfSize = baseSize * scale * 0.5f;
        // rt.anchoredPosition is center; offset by anchor relative to center
        Vector2 localOffset = new Vector2(
            (anchor.x - 0.5f) * 2f * halfSize,
            (anchor.y - 0.5f) * 2f * halfSize);
        // Convert parent-local to world then screen
        Vector3 worldPos = rt.parent.TransformPoint(rt.anchoredPosition + localOffset);
        return RectTransformUtility.WorldToScreenPoint(null, worldPos);
    }

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

public class PreviewResizeHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler
{
    NoteData note;
    PreviewPanel panel;
    RectTransform parentRt;
    Vector2 pivotAnchor; // opposite corner anchor (0-1)
    float initialDist;
    float initialScale;
    Vector2 initialNotePos;

    public void Init(NoteData n, PreviewPanel p, RectTransform iconRt, Vector2 oppositeAnchor)
    {
        note = n;
        panel = p;
        parentRt = iconRt;
        pivotAnchor = oppositeAnchor;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        initialScale = note.scale;
        initialNotePos = new Vector2(note.x, note.y);
        // Compute initial distance from opposite corner to drag start position
        float halfSize = 30f * initialScale * 0.5f;
        Vector2 localOffset = new Vector2(
            (pivotAnchor.x - 0.5f) * 2f * halfSize,
            (pivotAnchor.y - 0.5f) * 2f * halfSize);
        Vector3 worldPos = parentRt.parent.TransformPoint(parentRt.anchoredPosition + localOffset);
        Vector2 pivotScreen = RectTransformUtility.WorldToScreenPoint(null, worldPos);
        initialDist = Vector2.Distance(eventData.position, pivotScreen);
        panel.OnDragStart();
    }

    public void OnDrag(PointerEventData eventData)
    {
        panel.OnNoteResize(note, eventData.position, parentRt, pivotAnchor, initialDist, initialScale, initialNotePos);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        panel.OnDragEnd();
    }
}
