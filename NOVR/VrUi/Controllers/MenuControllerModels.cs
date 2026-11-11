using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NOVR.MotionControls;
using NOVR.VrCamera;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.Controllers;

// Shows the player's own controllers in the VR menus, at aircraft selection
// and on the full-screen map, with labels saying what their buttons do on
// that screen. The geometry is the model SteamVR installs for
// that controller, read from the player's disk and placed on the tracked grip
// pose; without it nothing is drawn and the menus behave as before.
internal sealed class MenuControllerModels : MonoBehaviour
{
    private const float CanvasScale = .0005f, LabelWidth = 200f, LineHeight = 16f, LabelPadding = 10f;
    private const long MaxFileBytes = 24L * 1024 * 1024;

    internal sealed class LoadedModel
    {
        internal string Name = "";
        internal ObjMesh Mesh = null!;
        internal RenderModelInfo Info = null!;
        internal byte[]? Texture;
    }

    private sealed class Hand
    {
        internal string? Name;
        internal Task<LoadedModel?>? Loading;
        internal GameObject? Root, Labels;
        internal Mesh? Mesh;
        internal Texture2D? Texture;
        internal Material? Material;
        internal RenderModelInfo? Info;
        internal (ControllerHintContext context, bool pointer, int bindings, bool frame, bool stickLeft) LabelKey;
        internal bool Shown, LabelsShown;
        internal Canvas? LabelCanvas;
    }

    private readonly Hand[] _hands = { new(), new() };
    private string _gameDataPath = "";
    private static MenuControllerModels? _instance;

    // True while a controller model is drawn on a hand. On the map, in a
    // cockpit, the models stand in for the pilot's gloves.
    internal static bool Active => _instance != null && (_instance._hands[0].Shown || _instance._hands[1].Shown);
    // True while that hand's labels are up; the map's own thumbstick hint
    // steps aside for them.
    internal static bool LabelsVisible(bool left) => _instance != null && _instance._hands[left ? 0 : 1].LabelsShown;

    private void Awake()
    {
        _gameDataPath = Application.dataPath;
        _instance = this;
    }

    // Which screen is asking for controllers, if any.
    internal static ControllerHintContext Context(VrUiCursor? cursor) =>
        cursor == null ? ControllerHintContext.None :
        cursor.NativeMenuVisible ? ControllerHintContext.Menus :
        AircraftWalkaround.Offered ? ControllerHintContext.AircraftSelection :
        InMission && DynamicMap.mapMaximized && !GameplayUI.GameIsPaused && !WristMenu.IsOpen ? ControllerHintContext.Map : ControllerHintContext.None;

    private void LateUpdate()
    {
        var settings = ModConfiguration.Instance;
        var cursor = VrUiCursor.I;
        var camera = cursor != null ? cursor.UiCamera : null;
        var context = settings.MenuControllerModels.Value && XRSettings.enabled && camera != null && TrackedControllers.Available
            ? Context(cursor) : ControllerHintContext.None;
        for (var side = 0; side < 2; side++)
        {
            try
            {
                UpdateHand(_hands[side], side == 0, context, camera, cursor);
            }
            catch (Exception e)
            {
                // A model that cannot be shown must never take the menus with it.
                Debug.LogWarning("[NOVR] Menu controller model unavailable: " + e.Message);
                Release(_hands[side]);
                _hands[side].Name = "failed";
            }
        }
    }

    private void UpdateHand(Hand hand, bool left, ControllerHintContext context, Camera? camera, VrUiCursor? cursor)
    {
        var sample = context != ControllerHintContext.None ? TrackedControllers.Read(left) : default;
        hand.Shown = hand.LabelsShown = false;
        if (context == ControllerHintContext.None || !sample.Valid)
        {
            if (hand.Root != null && hand.Root.activeSelf)
                hand.Root.SetActive(false);
            // The models stay loaded through a mission: the map brings them
            // back at any time, and loading one again costs a visible hitch.
            // Switching the feature off gives the textures back.
            if (hand.Root != null && !ModConfiguration.Instance.MenuControllerModels.Value)
                Release(hand);
            return;
        }
        var name = SteamVrRenderModels.ModelName(TrackedControllers.ModelFamily(left), left);
        if (name != hand.Name && hand.Name != "failed")
        {
            Release(hand);
            hand.Name = name;
            if (name != null)
            {
                var path = _gameDataPath;
                hand.Loading = Task.Run(() => Load(name, path));
            }
        }
        if (hand.Loading != null && hand.Loading.IsCompleted)
        {
            var task = hand.Loading;
            hand.Loading = null;
            if (task.Status == TaskStatus.RanToCompletion && task.Result != null)
                Create(hand, task.Result, left);
            else
                Debug.Log($"[NOVR] No SteamVR model for '{hand.Name}'; menus show no controller for that hand." +
                    (task.Exception != null ? " " + task.Exception.GetBaseException().Message : ""));
        }
        if (hand.Root == null)
            return;
        var trackingRoot = camera!.transform.parent;
        if (hand.Root.transform.parent != trackingRoot)
            hand.Root.transform.SetParent(trackingRoot, false);
        hand.Root.transform.localPosition = sample.Position;
        hand.Root.transform.localRotation = sample.Rotation;
        hand.Root.transform.localScale = Vector3.one;
        if (!hand.Root.activeSelf)
            hand.Root.SetActive(true);
        hand.Shown = true;
        UpdateLabels(hand, left, sample.NativeFrame, context, camera, cursor);
    }

    private static bool InMission => GameManager.gameState == GameState.SinglePlayer || GameManager.gameState == GameState.Multiplayer;

    // Runs off the main thread: files and parsing only, no Unity objects.
    internal static LoadedModel? Load(string name, string gameDataPath)
    {
        var directory = SteamVrRenderModels.Find(name, gameDataPath);
        if (directory == null)
            return null;
        var info = RenderModelInfo.Parse(ReadText(Path.Combine(directory, name + ".json")));
        ObjMesh mesh;
        var obj = Path.Combine(directory, name + ".obj");
        if (new FileInfo(obj).Length > MaxFileBytes)
            throw new InvalidDataException("Model file is too large.");
        using (var reader = new StreamReader(obj))
            mesh = ObjMesh.Parse(reader);
        byte[]? texture = null;
        var material = Path.Combine(directory, name + ".mtl");
        if (File.Exists(material))
            foreach (var line in ReadText(material).Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("map_Kd ", StringComparison.Ordinal))
                    continue;
                var file = trimmed.Substring(7).Trim();
                // A plain file name beside the model, never a path out of it.
                if (file.Length == 0 || file != Path.GetFileName(file) ||
                    !(file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)))
                    break;
                var image = Path.Combine(directory, file);
                if (File.Exists(image) && new FileInfo(image).Length <= MaxFileBytes)
                    texture = File.ReadAllBytes(image);
                break;
            }
        return new LoadedModel { Name = name, Mesh = mesh, Info = info, Texture = texture };
    }

    private static string ReadText(string path)
    {
        if (new FileInfo(path).Length > MaxFileBytes)
            throw new InvalidDataException("Model description is too large.");
        return File.ReadAllText(path);
    }

    // Main thread: the mesh in grip space, so the object's pose is the grip pose.
    internal static Mesh BuildMesh(LoadedModel model)
    {
        var source = model.Mesh;
        var count = source.VertexCount;
        var vertices = new Vector3[count];
        var uvs = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            model.Info.ToGrip(source.Positions[i * 3], source.Positions[i * 3 + 1], source.Positions[i * 3 + 2], out var x, out var y, out var z);
            vertices[i] = new Vector3(x, y, z);
            uvs[i] = new Vector2(source.Uvs[i * 2], source.Uvs[i * 2 + 1]);
        }
        // Mirroring Z into Unity's axes turns every triangle over.
        var triangles = (int[])source.Triangles.Clone();
        for (var i = 0; i + 2 < triangles.Length; i += 3)
            (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
        var mesh = new Mesh
        {
            name = "NOVR " + model.Name,
            indexFormat = count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16
        };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    internal static Material BuildMaterial(Texture2D? texture)
    {
        var material = CockpitMaterials.Opaque("NOVR menu controller", texture != null ? Color.white : new Color(.16f, .17f, .18f, 1f));
        if (texture != null)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
        }
        // Both faces: a model seen from an odd angle must not look hollow.
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        return material;
    }

    internal static Texture2D? BuildTexture(LoadedModel model)
    {
        if (model.Texture == null)
            return null;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
        {
            name = "NOVR " + model.Name,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 4
        };
        if (ImageConversion.LoadImage(texture, model.Texture, true))
            return texture;
        Destroy(texture);
        return null;
    }

    private void Create(Hand hand, LoadedModel model, bool left)
    {
        hand.Info = model.Info;
        hand.Mesh = BuildMesh(model);
        hand.Texture = BuildTexture(model);
        hand.Material = BuildMaterial(hand.Texture);
        var root = new GameObject("NOVR Menu Controller " + (left ? "Left" : "Right"), typeof(MeshFilter), typeof(MeshRenderer));
        root.layer = (int)LayerHelper.GetVrUiLayer();
        root.GetComponent<MeshFilter>().sharedMesh = hand.Mesh;
        var renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = hand.Material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        root.SetActive(false);
        hand.Root = root;
        hand.LabelKey = default;
        Debug.Log($"[NOVR] Menu controller model '{model.Name}': {hand.Mesh.vertexCount} vertices, texture {(hand.Texture != null ? hand.Texture.width + "x" + hand.Texture.height : "none")}.");
    }

    private void UpdateLabels(Hand hand, bool left, bool frame, ControllerHintContext context, Camera camera, VrUiCursor? cursor)
    {
        var settings = ModConfiguration.Instance;
        if (!settings.MenuControllerTooltips.Value || hand.Info == null)
        {
            if (hand.Labels != null && hand.Labels.activeSelf)
                hand.Labels.SetActive(false);
            return;
        }
        HandOptionWheelPose.Get(left, frame, out var offset, out var normal, out var forward);
        var position = offset - normal * .03f;
        var rotation = Quaternion.LookRotation(-normal, forward);
        var pointerHand = cursor != null && cursor.PointerHandLeft == left;
        var bindings = settings.ControllerBindings;
        var stickLeft = settings.MotionControls.LeftHanded.Value;
        // Only the map's labels depend on the bindings and the handedness.
        var map = context == ControllerHintContext.Map;
        var key = (context, pointerHand, map ? bindings.ChangeVersion : 0, frame, map && stickLeft);
        if (hand.Labels == null || hand.LabelKey != key)
        {
            if (hand.Labels != null)
                Destroy(hand.Labels);
            hand.LabelKey = key;
            hand.Labels = BuildLabels(hand, left, context, pointerHand, frame, stickLeft, bindings.Get, position, rotation);
            hand.LabelCanvas = hand.Labels.GetComponent<Canvas>();
        }
        var labels = hand.Labels.transform;
        var toHead = camera.transform.position - labels.position;
        var show = ControllerCallouts.LookedAt(Vector3.Dot(-labels.forward, toHead.normalized), toHead.magnitude);
        if (hand.Labels.activeSelf != show)
            hand.Labels.SetActive(show);
        hand.LabelsShown = show;
        if (show && hand.LabelCanvas != null)
            hand.LabelCanvas.worldCamera = camera;
    }

    private GameObject BuildLabels(Hand hand, bool left, ControllerHintContext context, bool pointerHand, bool frame, bool stickLeft,
        Func<ControllerActionId, ControllerActionBinding> get, Vector3 position, Quaternion rotation)
    {
        var view = new GameObject("NOVR Controller Labels", typeof(RectTransform), typeof(Canvas));
        view.transform.SetParent(hand.Root!.transform, false);
        view.transform.localPosition = position;
        view.transform.localRotation = rotation;
        view.transform.localScale = Vector3.one * CanvasScale;
        view.layer = hand.Root.layer;
        var canvas = view.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 460;
        var names = new List<string>();
        foreach (var part in hand.Info!.Parts)
            names.Add(part.Name);
        var callouts = ControllerCallouts.Build(names, context, left, pointerHand, frame, stickLeft, get);
        var tallest = 0;
        foreach (var callout in callouts)
            tallest = Mathf.Max(tallest, ControllerCallouts.Lines(callout));
        var anchors = new List<(float x, float y)>();
        var inverse = Quaternion.Inverse(rotation);
        var reach = 0f;
        foreach (var callout in callouts)
        {
            foreach (var part in hand.Info.Parts)
            {
                if (part.Name != callout.Part)
                    continue;
                var local = inverse * (new Vector3(part.X, part.Y, part.Z) - position) / CanvasScale;
                anchors.Add((local.x, local.y));
                reach = Mathf.Max(reach, Mathf.Abs(local.x));
                break;
            }
        }
        var spots = ControllerCallouts.Layout(anchors, reach + 24f + LabelWidth * .5f, tallest * LineHeight + LabelPadding + 4f);
        for (var i = 0; i < callouts.Count && i < spots.Length; i++)
            AddLabel(view.transform, callouts[i], new Vector2(anchors[i].x, anchors[i].y), new Vector2(spots[i].x, spots[i].y));
        return view;
    }

    private void AddLabel(Transform parent, ControllerCallout callout, Vector2 anchor, Vector2 spot)
    {
        var layer = parent.gameObject.layer;
        var rightSide = spot.x > 0;
        // Leader from the button to the label's inner edge.
        var edge = new Vector2(spot.x + (rightSide ? -LabelWidth * .5f : LabelWidth * .5f), spot.y);
        var line = new GameObject("Leader", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent, false);
        line.layer = layer;
        var lineRect = (RectTransform)line.transform;
        var delta = edge - anchor;
        lineRect.sizeDelta = new Vector2(delta.magnitude, 2f);
        lineRect.anchoredPosition = (edge + anchor) * .5f;
        lineRect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        var lineImage = line.GetComponent<Image>();
        lineImage.color = new Color(1f, 1f, 1f, .75f);
        lineImage.raycastTarget = false;
        var dot = new GameObject("Button", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(parent, false);
        dot.layer = layer;
        ((RectTransform)dot.transform).sizeDelta = new Vector2(7f, 7f);
        ((RectTransform)dot.transform).anchoredPosition = anchor;
        dot.GetComponent<Image>().raycastTarget = false;
        var box = new GameObject("Label", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(parent, false);
        box.layer = layer;
        var boxRect = (RectTransform)box.transform;
        var height = ControllerCallouts.Lines(callout) * LineHeight + LabelPadding;
        boxRect.sizeDelta = new Vector2(LabelWidth, height);
        boxRect.anchoredPosition = spot;
        var backing = box.GetComponent<Image>();
        backing.color = new Color(.03f, .05f, .07f, .88f);
        backing.raycastTarget = false;
        var label = new GameObject("Text", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(box.transform, false);
        label.layer = layer;
        var text = label.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 13;
        text.supportRichText = true;
        text.text = "<b>" + callout.Title + "</b>\n" + callout.Detail;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.rectTransform.sizeDelta = new Vector2(LabelWidth - 12f, height - 4f);
    }

    private static void Release(Hand hand)
    {
        if (hand.Labels != null)
            Destroy(hand.Labels);
        if (hand.Root != null)
            Destroy(hand.Root);
        if (hand.Mesh != null)
            Destroy(hand.Mesh);
        if (hand.Texture != null)
            Destroy(hand.Texture);
        if (hand.Material != null)
            Destroy(hand.Material);
        hand.Labels = hand.Root = null;
        hand.Mesh = null;
        hand.Texture = null;
        hand.Material = null;
        hand.Info = null;
        hand.Loading = null;
        hand.Name = null;
        hand.LabelKey = default;
        hand.LabelCanvas = null;
        hand.Shown = hand.LabelsShown = false;
    }

    private void OnDisable()
    {
        foreach (var hand in _hands)
        {
            hand.Shown = hand.LabelsShown = false;
            if (hand.Root != null)
                hand.Root.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        foreach (var hand in _hands)
            Release(hand);
        if (_instance == this)
            _instance = null;
    }
}
