using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 手作業のシーン組み立て（カメラ設定・プレハブ作成・参照割り当て）を全部コードでやる。
/// 空のシーンに空GameObjectを1つ作り、このコンポーネントを付けてPlayするだけで動く。
/// エディタ操作が不要になるので Claude Code に渡しやすい。
/// </summary>
public class Bootstrap : MonoBehaviour
{
    [Header("任意：曲。未設定なら無音でタイミングだけ進む")]
    public AudioClip song;

    [Header("線の見た目")]
    public float lineWidth = 0.05f;
    public Color ringColor = new Color(0.3f, 0.6f, 1f);
    public Color shapeColor = Color.white;

    void Awake()
    {
        // --- カメラを Orthographic に ---
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0, 0, -10);

        // --- Conductor ---
        var condGo = new GameObject("Conductor");
        var audio = condGo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.clip = song;
        var conductor = condGo.AddComponent<Conductor>();
        conductor.audioSource = audio;

        // --- 魔法陣テンプレート（プレハブ代わり）---
        var template = BuildTemplate();

        // --- NotePool（Awakeでprefabを使うので、inactiveで参照を入れてから有効化）---
        var poolGo = new GameObject("NotePool");
        poolGo.SetActive(false);
        var pool = poolGo.AddComponent<NotePool>();
        pool.magicCirclePrefab = template;
        pool.initialSize = 16;
        poolGo.SetActive(true);

        // --- GameManager（Startでconductor/poolを使うので、同様に後から有効化）---
        var gmGo = new GameObject("GameManager");
        gmGo.SetActive(false);
        var gm = gmGo.AddComponent<GameManager>();
        gm.conductor = conductor;
        gm.pool = pool;

        // --- UI ---
        var canvas = CreateCanvas();
        gm.scoreText = CreateText(canvas, "ScoreText", TextAnchor.UpperLeft, new Vector2(20, -20));
        gm.comboText = CreateText(canvas, "ComboText", TextAnchor.UpperCenter, new Vector2(0, -80));
        gm.comboText.fontSize = 48;

        gmGo.SetActive(true);
    }

    MagicCircleView BuildTemplate()
    {
        var root = new GameObject("MagicCircleTemplate");
        var view = root.AddComponent<MagicCircleView>();

        view.ringDrawer = MakeDrawer(root.transform, "Ring", ringColor);
        view.shapeDrawer = MakeDrawer(root.transform, "Shape", shapeColor);

        root.SetActive(false); // テンプレートは表示しない。プールが複製して使う
        return view;
    }

    Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    Text CreateText(Canvas canvas, string name, TextAnchor anchor, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(canvas.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(
            anchor == TextAnchor.UpperLeft ? 0 : 0.5f,
            1f);
        rt.pivot = new Vector2(anchor == TextAnchor.UpperLeft ? 0 : 0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(400, 60);

        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 32;
        text.color = Color.white;
        text.alignment = anchor;
        text.text = "";
        return text;
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