using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TimelinePanel : MonoBehaviour
{
    public ChartEditorManager manager;
    public RectTransform content;
    public float beatWidth = 80f;
    public float laneHeight = 28f;
    public float lanePadding = 2f;
    int totalBeats = 32;

    RectTransform panelRect;
    RectTransform viewport;
    ScrollRect scrollRect;

    // Cursor: lives under viewport (not content) so it stays visible while scrolling
    RectTransform cursorLine;

    public void Init(ChartEditorManager mgr, RectTransform parent)
    {
        manager = mgr;

        panelRect = gameObject.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(0.75f, 0.4f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

        // ScrollRect (vertical only — horizontal drag moves the cursor)
        scrollRect = gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 0; // we handle scroll manually

        // Viewport
        var vpGo = new GameObject("Viewport");
        viewport = vpGo.AddComponent<RectTransform>();
        viewport.SetParent(panelRect, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        vpGo.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f);
        vpGo.AddComponent<Mask>().showMaskGraphic = true;
        scrollRect.viewport = viewport;

        // Content
        var cGo = new GameObject("Content");
        content = cGo.AddComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(0, 1);
        content.pivot = new Vector2(0, 1);
        scrollRect.content = content;

        // Background drag handler: dragging empty area moves the cursor
        var bgDrag = vpGo.AddComponent<TimelineBgDragHandler>();
        bgDrag.Init(this);

        // Cursor line: child of viewport, rendered on top of content
        BuildCursor();

        manager.OnChartChanged += OnRebuildRequested;
        manager.OnSelectionChanged += OnRebuildRequested;
        manager.OnCurrentBeatChanged += UpdateCursor;
        manager.OnTimeSignatureChanged += OnRebuildRequested;
        manager.OnQuantizeChanged += OnRebuildRequested;
        manager.OnAudioTrackChanged += OnRebuildRequested;
        Rebuild();
    }

    void BuildCursor()
    {
        var go = new GameObject("Cursor");
        cursorLine = go.AddComponent<RectTransform>();
        cursorLine.SetParent(viewport, false);
        // Stretch full height of viewport
        cursorLine.anchorMin = new Vector2(0, 0);
        cursorLine.anchorMax = new Vector2(0, 1);
        cursorLine.pivot = new Vector2(0.5f, 0.5f);
        cursorLine.sizeDelta = new Vector2(2, 0);
        cursorLine.anchoredPosition = new Vector2(0, 0);
        go.AddComponent<Image>().color = new Color(1f, 0.3f, 0.3f, 0.9f);
    }

    // Margins: when cursor reaches this close to viewport edge, scroll timeline instead
    const float cursorMarginLeft = 60f;
    const float cursorMarginRight = 60f;
    const float minBeatWidth = 20f;
    const float maxBeatWidth = 300f;

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        float scrollY = mouse.scroll.ReadValue().y;
        if (scrollY == 0) return;

        if (kb.ctrlKey.isPressed)
        {
            // Ctrl+Scroll: zoom timeline (up=shrink, down=enlarge)
            float factor = scrollY > 0 ? 0.85f : 1.18f;
            float oldBeatWidth = beatWidth;
            beatWidth = Mathf.Clamp(beatWidth * factor, minBeatWidth, maxBeatWidth);

            // Keep currentBeat at same viewport position after zoom
            float cursorViewX = (float)manager.currentBeat * oldBeatWidth + content.anchoredPosition.x;
            float newContentX = cursorViewX - (float)manager.currentBeat * beatWidth;
            content.anchoredPosition = new Vector2(
                Mathf.Clamp(newContentX, -(content.sizeDelta.x - viewport.rect.width), 0),
                content.anchoredPosition.y);

            Rebuild();
        }
        else if (kb.shiftKey.isPressed)
        {
            // Shift+Scroll: down=right(advance), up=left(rewind)
            double delta = scrollY < 0 ? 0.25 : -0.25;
            double newBeat = Math.Max(0, manager.currentBeat + delta);
            manager.SetCurrentBeat(newBeat);

            // Auto-scroll timeline if cursor hits viewport edge
            EnsureCursorVisible();
        }
    }

    void EnsureCursorVisible()
    {
        float vpWidth = viewport.rect.width;
        if (vpWidth <= 0) return;

        float beatX = (float)manager.currentBeat * beatWidth;
        float viewX = beatX + content.anchoredPosition.x;

        float maxContentScroll = Mathf.Max(0, content.sizeDelta.x - vpWidth);

        if (viewX > vpWidth - cursorMarginRight)
        {
            // Cursor is too far right — scroll content left
            float newContentX = -(beatX - (vpWidth - cursorMarginRight));
            content.anchoredPosition = new Vector2(
                Mathf.Clamp(newContentX, -maxContentScroll, 0),
                content.anchoredPosition.y);
        }
        else if (viewX < cursorMarginLeft)
        {
            // Cursor is too far left — scroll content right
            float newContentX = -(beatX - cursorMarginLeft);
            content.anchoredPosition = new Vector2(
                Mathf.Clamp(newContentX, -maxContentScroll, 0),
                content.anchoredPosition.y);
        }
    }

    void UpdateCursor()
    {
        if (cursorLine == null) return;
        float beatX = (float)manager.currentBeat * beatWidth;
        float viewX = beatX + content.anchoredPosition.x;
        cursorLine.anchoredPosition = new Vector2(viewX, 0);
    }

    // Height reserved for the audio track row above note lanes
    const float audioTrackHeight = 24f;

    void Rebuild()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        float maxBeat = 16f;
        foreach (var n in manager.chart.notes)
            maxBeat = Mathf.Max(maxBeat, (float)n.hitBeat + 4);
        if (manager.chart.audioTrack != null)
            maxBeat = Mathf.Max(maxBeat,
                (float)(manager.chart.audioTrack.startBeat + manager.chart.audioTrack.durationBeats) + 2);
        totalBeats = Mathf.CeilToInt(maxBeat);

        var lanes = AssignLanes();
        int laneCount = Mathf.Max(1, lanes.Values.Count > 0 ? lanes.Values.Max() + 1 : 1);

        bool hasAudio = manager.chart.audioTrack != null;
        float audioRowHeight = hasAudio ? audioTrackHeight + lanePadding : 0f;
        float contentHeight = audioRowHeight + laneCount * (laneHeight + lanePadding) + 20f;
        content.sizeDelta = new Vector2(totalBeats * beatWidth, contentHeight);

        DrawGrid(contentHeight);
        DrawAudioTrack();
        DrawNotes(lanes, audioRowHeight);
        UpdateCursor();
    }

    Dictionary<NoteData, int> AssignLanes()
    {
        var result = new Dictionary<NoteData, int>();
        var sorted = manager.chart.notes
            .OrderBy(n => n.hitBeat - n.leadBeat)
            .ThenBy(n => n.hitBeat)
            .ToList();

        var laneEnds = new List<double>();

        foreach (var note in sorted)
        {
            double spawnBeat = note.hitBeat - note.leadBeat;
            int lane = -1;
            for (int i = 0; i < laneEnds.Count; i++)
            {
                if (laneEnds[i] <= spawnBeat)
                {
                    lane = i;
                    laneEnds[i] = note.hitBeat;
                    break;
                }
            }
            if (lane < 0)
            {
                lane = laneEnds.Count;
                laneEnds.Add(note.hitBeat);
            }
            result[note] = lane;
        }
        return result;
    }

    void DrawGrid(float contentHeight)
    {
        var measures = TimeSignatureHelper.BuildMeasureList(
            manager.chart.timeSignatures, totalBeats);

        foreach (var mi in measures)
        {
            float mx = (float)mi.startBeat * beatWidth;

            // Measure line (bright)
            MakeGridLine(mx, contentHeight, new Color(1, 1, 1, 0.4f));

            // Measure number label
            var lbl = new GameObject($"Lbl_{mi.measure}");
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.SetParent(content, false);
            lrt.anchorMin = new Vector2(0, 1);
            lrt.anchorMax = new Vector2(0, 1);
            lrt.pivot = new Vector2(0, 1);
            lrt.anchoredPosition = new Vector2(mx + 2, 0);
            lrt.sizeDelta = new Vector2(80, 18);
            var txt = lbl.AddComponent<Text>();
            txt.text = $"{mi.measure + 1} ({mi.timeSignature.numerator}/{mi.timeSignature.denominator})";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 11;
            txt.color = Color.white;

            // Beat lines within this measure
            int beatCount = mi.timeSignature.numerator;
            double beatUnit = 4.0 / mi.timeSignature.denominator;
            for (int b = 1; b < beatCount; b++)
            {
                float bx = (float)(mi.startBeat + b * beatUnit) * beatWidth;
                MakeGridLine(bx, contentHeight, new Color(1, 1, 1, 0.15f));
            }
        }

        // Quantize subdivision grid
        if (manager.quantizeEnabled)
        {
            double grid = 4.0 / manager.quantizeDivision;
            for (double b = grid; b < totalBeats; b += grid)
            {
                float qx = (float)b * beatWidth;
                MakeGridLine(qx, contentHeight, new Color(0.5f, 0.8f, 1f, 0.08f));
            }
        }
    }

    void MakeGridLine(float x, float height, Color color)
    {
        var line = new GameObject("Grid");
        var rt = line.AddComponent<RectTransform>();
        rt.SetParent(content, false);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(x, 0);
        rt.sizeDelta = new Vector2(1, height);
        line.AddComponent<Image>().color = color;
    }

    void DrawAudioTrack()
    {
        var at = manager.chart.audioTrack;
        if (at == null) return;

        float leftX = (float)at.startBeat * beatWidth;
        float w = (float)at.durationBeats * beatWidth;
        float topY = -18f; // just below the label row

        var block = new GameObject("AudioBlock");
        var rt = block.AddComponent<RectTransform>();
        rt.SetParent(content, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(leftX, topY);
        rt.sizeDelta = new Vector2(w, audioTrackHeight);

        block.AddComponent<Image>().color = new Color(0.2f, 0.7f, 0.4f);

        // Label
        var lbl = new GameObject("Label");
        var lrt = lbl.AddComponent<RectTransform>();
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(4, 0);
        lrt.offsetMax = new Vector2(-2, 0);
        var txt = lbl.AddComponent<Text>();
        txt.text = $"\u266A {at.fileName}";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 11;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;

        var handler = block.AddComponent<AudioBlockHandler>();
        handler.Init(this);
    }

    void DrawNotes(Dictionary<NoteData, int> lanes, float audioRowOffset = 0f)
    {
        foreach (var note in manager.chart.notes)
        {
            int lane = lanes.ContainsKey(note) ? lanes[note] : 0;
            double spawnBeat = note.hitBeat - note.leadBeat;

            float leftX = (float)spawnBeat * beatWidth;
            float rightX = (float)note.hitBeat * beatWidth;
            float barWidth = Mathf.Max(rightX - leftX, 6f);

            float topY = -(18f + audioRowOffset + lane * (laneHeight + lanePadding));

            var block = new GameObject("NoteBar");
            var rt = block.AddComponent<RectTransform>();
            rt.SetParent(content, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(leftX, topY);
            rt.sizeDelta = new Vector2(barWidth, laneHeight);

            var img = block.AddComponent<Image>();
            bool sel = note == manager.selectedNote;
            img.color = sel ? new Color(1f, 0.8f, 0.2f) : ShapeColor(note.shape);

            // Hit marker (right edge)
            var hitMark = new GameObject("HitMark");
            var hmRt = hitMark.AddComponent<RectTransform>();
            hmRt.SetParent(rt, false);
            hmRt.anchorMin = new Vector2(1, 0);
            hmRt.anchorMax = new Vector2(1, 1);
            hmRt.pivot = new Vector2(1, 0.5f);
            hmRt.anchoredPosition = Vector2.zero;
            hmRt.sizeDelta = new Vector2(3, 0);
            hitMark.AddComponent<Image>().color = new Color(1, 1, 1, 0.8f);

            // Label
            var lbl = new GameObject("Label");
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(2, 0);
            lrt.offsetMax = new Vector2(-2, 0);
            var txt = lbl.AddComponent<Text>();
            txt.text = $"{note.leadBeat:F1}拍 b{note.hitBeat:F1}";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 11;
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleLeft;

            var handler = block.AddComponent<NoteBlockHandler>();
            handler.Init(note, this);
        }
    }

    // Drag state: suppress Rebuild while dragging to avoid destroying the dragged object
    bool isDragging;

    public enum DragMode { MoveBody, ResizeLeft, ResizeRight }

    public void OnNoteBlockClicked(NoteData note)
    {
        DismissContextMenu();
        manager.SelectNote(note);
    }

    public void OnBgDrag(float deltaX)
    {
        double deltaBeat = deltaX / beatWidth;
        double newBeat = Math.Max(0, manager.currentBeat + deltaBeat);
        manager.SetCurrentBeat(newBeat);
        EnsureCursorVisible();
    }

    public DragMode DetectDragMode(RectTransform blockRt, Vector2 screenPos)
    {
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            blockRt, screenPos, null, out localPos);
        if (localPos.x - blockRt.rect.xMin < 10f)
            return DragMode.ResizeLeft;
        if (blockRt.rect.xMax - localPos.x < 10f)
            return DragMode.ResizeRight;
        return DragMode.MoveBody;
    }

    public void OnDragStart()
    {
        isDragging = true;
        scrollRect.enabled = false;
    }

    public void OnNoteBlockDrag(NoteData note, float deltaX, RectTransform blockRt, DragMode mode)
    {
        double deltaBeat = deltaX / beatWidth;

        if (mode == DragMode.ResizeLeft)
        {
            // Left edge drag: change leadBeat, hitBeat stays fixed
            double newLead = note.leadBeat - deltaBeat;
            note.leadBeat = Math.Max(0.25, newLead);
        }
        else if (mode == DragMode.ResizeRight)
        {
            // Right edge drag: change hitBeat, spawnBeat stays fixed (leadBeat changes accordingly)
            double spawn = note.hitBeat - note.leadBeat;
            note.hitBeat = Math.Max(spawn + 0.25, note.hitBeat + deltaBeat);
            note.leadBeat = note.hitBeat - spawn;
        }
        else
        {
            // Body drag: move hitBeat
            note.hitBeat = Math.Max(0, note.hitBeat + deltaBeat);
        }

        // Update block visuals
        double spawnBeat = note.hitBeat - note.leadBeat;
        float leftX = (float)spawnBeat * beatWidth;
        float barWidth = Mathf.Max((float)note.leadBeat * beatWidth, 6f);
        blockRt.anchoredPosition = new Vector2(leftX, blockRt.anchoredPosition.y);
        blockRt.sizeDelta = new Vector2(barWidth, blockRt.sizeDelta.y);
    }

    public void OnDragEnd(NoteData note)
    {
        if (manager.quantizeEnabled)
        {
            int div = manager.quantizeDivision;
            double snappedHit = QuantizeHelper.Snap(note.hitBeat, div);
            double spawnBeat = note.hitBeat - note.leadBeat;
            double snappedSpawn = QuantizeHelper.Snap(spawnBeat, div);
            note.hitBeat = snappedHit;
            note.leadBeat = Math.Max(0.25, snappedHit - snappedSpawn);
        }

        isDragging = false;
        scrollRect.enabled = true;
        manager.NotifyNoteChanged();
    }

    public void OnAudioDragStart()
    {
        isDragging = true;
        scrollRect.enabled = false;
    }

    public void OnAudioBlockDrag(float deltaX, RectTransform blockRt)
    {
        var at = manager.chart.audioTrack;
        if (at == null) return;
        double deltaBeat = deltaX / beatWidth;
        at.startBeat = Math.Max(0, at.startBeat + deltaBeat);
        blockRt.anchoredPosition = new Vector2((float)at.startBeat * beatWidth, blockRt.anchoredPosition.y);
    }

    public void OnAudioDragEnd()
    {
        var at = manager.chart.audioTrack;
        if (at != null && manager.quantizeEnabled)
            at.startBeat = QuantizeHelper.Snap(at.startBeat, manager.quantizeDivision);

        isDragging = false;
        scrollRect.enabled = true;
        manager.MoveAudioTrack(manager.chart.audioTrack.startBeat);
    }

    // ---- Context menu ----
    GameObject contextMenu;

    public void ShowContextMenu(NoteData note, Vector2 screenPos)
    {
        DismissContextMenu();

        contextMenu = new GameObject("ContextMenu");
        var rt = contextMenu.AddComponent<RectTransform>();
        rt.SetParent(viewport, false);
        rt.sizeDelta = new Vector2(80, 28);

        // Convert screen pos to viewport local
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPos, null, out localPos);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(localPos.x, localPos.y + viewport.rect.height);

        contextMenu.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);

        // Delete button
        var btnGo = new GameObject("DeleteBtn");
        var brt = btnGo.AddComponent<RectTransform>();
        brt.SetParent(rt, false);
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        btnGo.AddComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f);
        var btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            DismissContextMenu();
            manager.DeleteNote(note);
        });

        var lblGo = new GameObject("Lbl");
        var lrt = lblGo.AddComponent<RectTransform>();
        lrt.SetParent(brt, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var txt = lblGo.AddComponent<Text>();
        txt.text = "Delete";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 12;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
    }

    public void DismissContextMenu()
    {
        if (contextMenu != null)
        {
            Destroy(contextMenu);
            contextMenu = null;
        }
    }

    void OnRebuildRequested()
    {
        DismissContextMenu();
        if (!isDragging) Rebuild();
    }

    Color ShapeColor(ShapeType s)
    {
        switch (s)
        {
            case ShapeType.Diameter:  return new Color(0.4f, 0.7f, 1f);
            case ShapeType.Triangle:  return new Color(0.5f, 1f, 0.5f);
            case ShapeType.Square:    return new Color(1f, 0.6f, 0.3f);
            case ShapeType.Pentagram: return new Color(1f, 0.4f, 0.4f);
            case ShapeType.Hexagram:  return new Color(0.8f, 0.4f, 1f);
            default: return Color.gray;
        }
    }
}

public class NoteBlockHandler : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IInitializePotentialDragHandler
{
    NoteData note;
    TimelinePanel panel;
    Vector2 lastDragPos;
    TimelinePanel.DragMode dragMode;

    public void Init(NoteData n, TimelinePanel p)
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
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            panel.ShowContextMenu(note, eventData.position);
            return;
        }
        panel.DismissContextMenu();
        panel.OnNoteBlockClicked(note);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        lastDragPos = eventData.position;
        dragMode = panel.DetectDragMode(GetComponent<RectTransform>(), eventData.position);
        panel.OnDragStart();
    }

    public void OnDrag(PointerEventData eventData)
    {
        float dx = eventData.position.x - lastDragPos.x;
        lastDragPos = eventData.position;
        panel.OnNoteBlockDrag(note, dx, GetComponent<RectTransform>(), dragMode);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        panel.OnDragEnd(note);
    }
}

public class AudioBlockHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler
{
    TimelinePanel panel;
    Vector2 lastDragPos;

    public void Init(TimelinePanel p) { panel = p; }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        lastDragPos = eventData.position;
        panel.OnAudioDragStart();
    }

    public void OnDrag(PointerEventData eventData)
    {
        float dx = eventData.position.x - lastDragPos.x;
        lastDragPos = eventData.position;
        panel.OnAudioBlockDrag(dx, GetComponent<RectTransform>());
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        panel.OnAudioDragEnd();
    }
}

public class TimelineBgDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    TimelinePanel panel;
    Vector2 lastDragPos;

    public void Init(TimelinePanel p) { panel = p; }

    public void OnBeginDrag(PointerEventData eventData)
    {
        lastDragPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        float dx = eventData.position.x - lastDragPos.x;
        lastDragPos = eventData.position;
        panel.OnBgDrag(dx);
    }

    public void OnEndDrag(PointerEventData eventData) { }
}
