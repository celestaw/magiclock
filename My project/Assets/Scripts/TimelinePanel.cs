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

    /// <summary>true: カーソル固定でタイムラインが動く。false: タイムラインが固定でカーソルが動く。</summary>
    public bool cursorLocked;
    const float lockedCursorRatio = 0.1f; // 固定時のカーソル位置（左端から10%）

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

        // Ctrl+C / Ctrl+X / Ctrl+V
        if (kb.ctrlKey.isPressed)
        {
            if (kb.cKey.wasPressedThisFrame)
                manager.CopySelection();
            else if (kb.xKey.wasPressedThisFrame)
                manager.CutSelection();
            else if (kb.vKey.wasPressedThisFrame)
                manager.Paste();
        }

        // Delete / Backspace: delete selected notes
        if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
        {
            if (manager.selectedNotes.Count > 0)
            {
                var toDelete = new List<NoteData>(manager.selectedNotes);
                foreach (var n in toDelete)
                    manager.chart.notes.Remove(n);
                manager.DeselectNote();
                manager.NotifyNoteChanged();
            }
        }

        float scrollY = mouse.scroll.ReadValue().y;
        if (scrollY == 0) return;

        if (kb.ctrlKey.isPressed)
        {
            // Ctrl+Scroll: zoom timeline (up=shrink, down=enlarge)
            float factor = scrollY > 0 ? 0.85f : 1.18f;
            float oldBeatWidth = beatWidth;
            beatWidth = Mathf.Clamp(beatWidth * factor, minBeatWidth, maxBeatWidth);

            // Keep currentBeat at same viewport position after zoom
            if (!cursorLocked)
            {
                float cursorViewX = (float)manager.currentBeat * oldBeatWidth + content.anchoredPosition.x;
                float newContentX = cursorViewX - (float)manager.currentBeat * beatWidth;
                content.anchoredPosition = new Vector2(
                    Mathf.Min(newContentX, 0),
                    content.anchoredPosition.y);
            }

            Rebuild();
        }
        else if (kb.shiftKey.isPressed)
        {
            // Shift+Scroll: down=right(advance), up=left(rewind)
            double delta = scrollY < 0 ? 0.25 : -0.25;
            double newBeat = Math.Max(0, manager.currentBeat + delta);
            manager.SetCurrentBeat(newBeat);

            // Lockedモードでは UpdateCursor が全部やるので不要
            if (!cursorLocked)
                EnsureCursorVisible();
        }
    }

    void EnsureCursorVisible()
    {
        float vpWidth = viewport.rect.width;
        if (vpWidth <= 0) return;

        float beatX = (float)manager.currentBeat * beatWidth;
        float viewX = beatX + content.anchoredPosition.x;

        if (viewX > vpWidth - cursorMarginRight)
        {
            float newContentX = -(beatX - (vpWidth - cursorMarginRight));
            content.anchoredPosition = new Vector2(
                Mathf.Min(newContentX, 0),
                content.anchoredPosition.y);
        }
        else if (viewX < cursorMarginLeft)
        {
            float newContentX = -(beatX - cursorMarginLeft);
            content.anchoredPosition = new Vector2(
                Mathf.Min(newContentX, 0),
                content.anchoredPosition.y);
        }
    }

    void UpdateCursor()
    {
        if (cursorLine == null) return;

        // currentBeatがコンテンツ範囲を超えたらレイアウト再計算
        if (manager.currentBeat + 4 > totalBeats)
            RebuildLayout();

        float beatX = (float)manager.currentBeat * beatWidth;

        if (cursorLocked)
        {
            float vpWidth = viewport.rect.width;
            float fixedX = vpWidth * lockedCursorRatio;
            cursorLine.anchoredPosition = new Vector2(fixedX, 0);

            // コンテンツ幅が足りなければ拡張
            float requiredWidth = beatX + vpWidth;
            if (content.sizeDelta.x < requiredWidth)
                content.sizeDelta = new Vector2(requiredWidth, content.sizeDelta.y);

            float newContentX = -(beatX - fixedX);
            content.anchoredPosition = new Vector2(
                Mathf.Min(newContentX, 0),
                content.anchoredPosition.y);
        }
        else
        {
            float viewX = beatX + content.anchoredPosition.x;
            cursorLine.anchoredPosition = new Vector2(viewX, 0);

            float vpWidth = viewport.rect.width;
            if (vpWidth > 0 && (viewX > vpWidth - cursorMarginRight || viewX < cursorMarginLeft))
                EnsureCursorVisible();
        }

        // 可視範囲が変わっていれば再描画
        RedrawVisible();
    }

    // Height reserved for the audio track row above note lanes
    const float audioTrackHeight = 24f;

    // キャッシュ: Rebuildで計算、DrawVisibleで使い回す
    Dictionary<NoteData, int> cachedLanes = new Dictionary<NoteData, int>();
    int cachedLaneCount;
    float cachedAudioRowHeight;
    float cachedContentHeight;
    double lastVisibleMinBeat = -1, lastVisibleMaxBeat = -1;
    bool layoutDirty = true;

    /// <summary>譜面構造が変わったときに呼ぶ。レイアウト再計算 + 可視領域再描画。</summary>
    void Rebuild()
    {
        layoutDirty = true;
        RebuildLayout();
        RedrawVisible(true);
        UpdateCursor();
    }

    /// <summary>レイアウト（content幅・レーン割り当て）だけ再計算。</summary>
    void RebuildLayout()
    {
        float maxBeat = 16f;
        foreach (var n in manager.chart.notes)
            maxBeat = Mathf.Max(maxBeat, (float)n.hitBeat + 4);
        if (manager.chart.audioTrack != null)
            maxBeat = Mathf.Max(maxBeat,
                (float)(manager.chart.audioTrack.startBeat + manager.chart.audioTrack.durationBeats) + 2);
        maxBeat = Mathf.Max(maxBeat, (float)manager.currentBeat + 8);
        totalBeats = Mathf.CeilToInt(maxBeat);

        cachedLanes = AssignLanes();
        cachedLaneCount = Mathf.Max(1, cachedLanes.Values.Count > 0 ? cachedLanes.Values.Max() + 1 : 1);

        bool hasAudio = manager.chart.audioTrack != null;
        cachedAudioRowHeight = hasAudio ? audioTrackHeight + lanePadding : 0f;
        cachedContentHeight = cachedAudioRowHeight + cachedLaneCount * (laneHeight + lanePadding) + 20f;
        content.sizeDelta = new Vector2(totalBeats * beatWidth, cachedContentHeight);

        layoutDirty = false;
    }

    /// <summary>ビューポートに見える範囲の要素だけ描画する。</summary>
    void RedrawVisible(bool force = false)
    {
        float vpWidth = viewport.rect.width;
        if (vpWidth <= 0) return;

        // 可視範囲を拍で計算（余白付き）
        float contentX = -content.anchoredPosition.x;
        float margin = beatWidth * 2; // 2拍分の余白
        double minBeat = (contentX - margin) / beatWidth;
        double maxBeat = (contentX + vpWidth + margin) / beatWidth;

        // 範囲が大きく変わっていなければスキップ（性能対策）
        if (!force &&
            System.Math.Abs(minBeat - lastVisibleMinBeat) < 1.0 &&
            System.Math.Abs(maxBeat - lastVisibleMaxBeat) < 1.0)
            return;

        lastVisibleMinBeat = minBeat;
        lastVisibleMaxBeat = maxBeat;

        // 全子要素を削除して再描画
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        DrawGridVisible(cachedContentHeight, minBeat, maxBeat);
        DrawAudioTrackVisible(minBeat, maxBeat);
        DrawNotesVisible(cachedLanes, cachedAudioRowHeight, minBeat, maxBeat);
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

    void DrawGridVisible(float contentHeight, double minBeat, double maxBeat)
    {
        var measures = TimeSignatureHelper.BuildMeasureList(
            manager.chart.timeSignatures, totalBeats);

        foreach (var mi in measures)
        {
            double measureEnd = mi.startBeat + mi.beatsPerMeasure;
            // 小節がまるごと可視範囲外ならスキップ
            if (measureEnd < minBeat) continue;
            if (mi.startBeat > maxBeat) break;

            float mx = (float)mi.startBeat * beatWidth;

            MakeGridLine(mx, contentHeight, new Color(1, 1, 1, 0.4f));

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

            int beatCount = mi.timeSignature.numerator;
            double beatUnit = 4.0 / mi.timeSignature.denominator;
            for (int b = 1; b < beatCount; b++)
            {
                double bb = mi.startBeat + b * beatUnit;
                if (bb < minBeat) continue;
                if (bb > maxBeat) break;
                float bx = (float)bb * beatWidth;
                MakeGridLine(bx, contentHeight, new Color(1, 1, 1, 0.15f));
            }
        }

        if (manager.quantizeEnabled)
        {
            double grid = 4.0 / manager.quantizeDivision;
            double startGrid = System.Math.Max(grid, System.Math.Floor(minBeat / grid) * grid);
            for (double b = startGrid; b < maxBeat && b < totalBeats; b += grid)
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

    void DrawAudioTrackVisible(double minBeat, double maxBeat)
    {
        var at = manager.chart.audioTrack;
        if (at == null) return;

        double atStart = at.startBeat;
        double atEnd = at.startBeat + at.durationBeats;

        // 可視範囲外なら描画しない
        if (atEnd < minBeat || atStart > maxBeat) return;

        // 可視範囲にクランプして描画
        double drawStart = System.Math.Max(atStart, minBeat);
        double drawEnd = System.Math.Min(atEnd, maxBeat);

        float leftX = (float)drawStart * beatWidth;
        float w = (float)(drawEnd - drawStart) * beatWidth;
        float topY = -18f;

        var block = new GameObject("AudioBlock");
        var rt = block.AddComponent<RectTransform>();
        rt.SetParent(content, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(leftX, topY);
        rt.sizeDelta = new Vector2(w, audioTrackHeight);
        block.AddComponent<Image>().color = new Color(0.2f, 0.7f, 0.4f);

        // ラベルはオーディオ左端が見えているときだけ表示
        if (atStart >= minBeat)
        {
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
        }

        var handler = block.AddComponent<AudioBlockHandler>();
        handler.Init(this);
    }

    void DrawNotesVisible(Dictionary<NoteData, int> lanes, float audioRowOffset, double minBeat, double maxBeat)
    {
        foreach (var note in manager.chart.notes)
        {
            double spawnBeat = note.hitBeat - note.leadBeat;
            // 可視範囲外ならスキップ
            if (note.hitBeat < minBeat || spawnBeat > maxBeat) continue;
            int lane = lanes.ContainsKey(note) ? lanes[note] : 0;

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
            bool isCurrent = note == manager.selectedNote;
            bool isMultiSel = manager.IsSelected(note);
            img.color = isCurrent ? new Color(1f, 0.8f, 0.2f) : NoteColor(note);

            // Orange selection border for multi-selected notes
            if (isMultiSel && !isCurrent)
            {
                AddSelectionBorder(rt);
            }

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
            string typePrefix = note.noteType == NoteType.Slash ? "\u2694 " : "";
            txt.text = $"{typePrefix}{note.leadBeat:F1}拍 b{note.hitBeat:F1}";
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
        if (!cursorLocked)
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

    Color NoteColor(NoteData note)
    {
        if (note.noteType == NoteType.Slash)
            return new Color(1f, 0.3f, 0.3f); // red for slash

        switch (note.shape)
        {
            case ShapeType.Diameter:  return new Color(0.4f, 0.7f, 1f);
            case ShapeType.Triangle:  return new Color(0.5f, 1f, 0.5f);
            case ShapeType.Square:    return new Color(1f, 0.6f, 0.3f);
            case ShapeType.Pentagram: return new Color(1f, 0.4f, 0.4f);
            case ShapeType.Hexagram:  return new Color(0.8f, 0.4f, 1f);
            default: return Color.gray;
        }
    }

    void AddSelectionBorder(RectTransform parent)
    {
        Color borderCol = new Color(1f, 0.6f, 0f, 0.9f);
        float thickness = 2f;

        // Top
        MakeBorderEdge(parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, thickness), borderCol);
        // Bottom
        MakeBorderEdge(parent, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, thickness), borderCol);
        // Left
        MakeBorderEdge(parent, new Vector2(0, 0), new Vector2(0, 1), new Vector2(thickness, 0), borderCol);
        // Right
        MakeBorderEdge(parent, new Vector2(1, 0), new Vector2(1, 1), new Vector2(thickness, 0), borderCol);
    }

    void MakeBorderEdge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Color col)
    {
        var go = new GameObject("Border");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
    }

    // ---- Selection rect state ----
    bool isSelecting;
    Vector2 selStartContentPos; // content-local position where drag started
    GameObject selectionRectObj;

    public void OnBgSelectionStart(Vector2 screenPos)
    {
        isSelecting = true;
        isDragging = true;
        scrollRect.enabled = false;

        // Convert screen pos to content local
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            content, screenPos, null, out selStartContentPos);

        // Create selection rect visual
        if (selectionRectObj != null) Destroy(selectionRectObj);
        selectionRectObj = new GameObject("SelectionRect");
        var rt = selectionRectObj.AddComponent<RectTransform>();
        rt.SetParent(content, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        var img = selectionRectObj.AddComponent<Image>();
        img.color = new Color(1f, 0.6f, 0f, 0.15f);
        img.raycastTarget = false;

        // Orange border on the selection rect
        AddSelectionBorder(rt);
    }

    public void OnBgSelectionDrag(Vector2 screenPos)
    {
        if (!isSelecting || selectionRectObj == null) return;

        Vector2 curContentPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            content, screenPos, null, out curContentPos);

        float x = Mathf.Min(selStartContentPos.x, curContentPos.x);
        float y = Mathf.Max(selStartContentPos.y, curContentPos.y); // y is inverted (top=0)
        float w = Mathf.Abs(curContentPos.x - selStartContentPos.x);
        float h = Mathf.Abs(curContentPos.y - selStartContentPos.y);

        var rt = selectionRectObj.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    public void OnBgSelectionEnd(Vector2 screenPos)
    {
        if (!isSelecting) return;
        isSelecting = false;
        isDragging = false;
        scrollRect.enabled = true;

        if (selectionRectObj == null) return;

        Vector2 curContentPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            content, screenPos, null, out curContentPos);

        // Calculate beat range from x coordinates
        float minX = Mathf.Min(selStartContentPos.x, curContentPos.x);
        float maxX = Mathf.Max(selStartContentPos.x, curContentPos.x);
        double minBeatSel = minX / beatWidth;
        double maxBeatSel = maxX / beatWidth;

        // Calculate lane range from y coordinates
        float minY = Mathf.Min(selStartContentPos.y, curContentPos.y); // more negative = lower
        float maxY = Mathf.Max(selStartContentPos.y, curContentPos.y);

        // Find all notes within the rect
        var selected = new List<NoteData>();
        foreach (var note in manager.chart.notes)
        {
            double spawnBeat = note.hitBeat - note.leadBeat;
            // Note overlaps if its bar intersects the selection rect horizontally
            if (note.hitBeat < minBeatSel || spawnBeat > maxBeatSel) continue;

            // Check vertical overlap using lane position
            int lane = cachedLanes.ContainsKey(note) ? cachedLanes[note] : 0;
            float noteTop = -(18f + cachedAudioRowHeight + lane * (laneHeight + lanePadding));
            float noteBottom = noteTop - laneHeight;
            // maxY/minY are in content local (negative downward from top)
            if (noteTop < minY || noteBottom > maxY) continue;

            selected.Add(note);
        }

        Destroy(selectionRectObj);
        selectionRectObj = null;

        if (selected.Count > 0)
            manager.SelectNotes(selected);
        else
            manager.DeselectNote();
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
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    TimelinePanel panel;
    bool shiftDragMode; // true = cursor drag, false = selection rect
    Vector2 lastDragPos;

    public void Init(TimelinePanel p) { panel = p; }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Click on empty area deselects
        if (!eventData.dragging)
            panel.manager.DeselectNote();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var kb = Keyboard.current;
        shiftDragMode = kb != null && kb.shiftKey.isPressed;

        if (shiftDragMode)
        {
            lastDragPos = eventData.position;
        }
        else
        {
            panel.OnBgSelectionStart(eventData.position);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (shiftDragMode)
        {
            float dx = eventData.position.x - lastDragPos.x;
            lastDragPos = eventData.position;
            panel.OnBgDrag(dx);
        }
        else
        {
            panel.OnBgSelectionDrag(eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (shiftDragMode)
        {
            // nothing special to clean up
        }
        else
        {
            panel.OnBgSelectionEnd(eventData.position);
        }
    }
}
