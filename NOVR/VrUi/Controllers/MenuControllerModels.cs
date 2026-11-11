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
    private const float CanvasScale = .0005f, LabelWidth = 200f, LineHeight = 16f, LabelPadding = 10f, LabelGap = 24f;
    // Metres: the line's thickness and the bead at its end.
    private const float LeaderWidth = .0012f, LeaderDot = .0045f;
    // A pressed button's white copy stands this far proud of the button, so
    // it covers it cleanly: a share of its size plus a fixed sliver.
    private const float PressedGrow = .03f, PressedLift = .0004f, PressThreshold = .5f;
    // Plain white, for the lines, their beads and pressed buttons.
    private Material? _white;
    private const long MaxFileBytes = 24L * 1024 * 1024;

    internal sealed class LoadedModel
    {
        internal string Name = "";
        internal ObjMesh Mesh = null!;
        internal RenderModelInfo Info = null!;
        internal byte[]? Texture;
        internal Dictionary<string, ControllerPartShape> Shapes = new();
        // The whole controller as one shape: what a label's line must not cut through.
        internal ControllerPartShape? Whole;
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
        // The buttons' own meshes, and the lines from the labels to them.
        internal Dictionary<string, ControllerPartShape>? Shapes;
        internal ControllerPartShape? Whole;
        internal GameObject? Leaders;
        // A white copy of each button, shown while that button is pressed.
        internal readonly List<(string part, MeshRenderer renderer)> Pressed = new();
        internal readonly List<Mesh> PressedMeshes = new();
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
                Create(hand, task.Result, left, sample.NativeFrame);
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
        UpdatePressed(hand, left, sample);
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
        return new LoadedModel
        {
            Name = name, Mesh = mesh, Info = info, Texture = texture,
            Shapes = ControllerPartShape.LoadAll(directory, info), Whole = ControllerPartShape.From(mesh, info)
        };
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

    private Material White()
    {
        if (_white != null)
            return _white;
        _white = CockpitMaterials.Opaque("NOVR controller white", Color.white);
        if (_white.HasProperty("_Cull"))
            _white.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        return _white;
    }

    // Lights up what the hand is pressing, on the model in that hand.
    private static void UpdatePressed(Hand hand, bool left, in ControllerSample sample)
    {
        if (hand.Pressed.Count == 0)
            return;
        var show = ModConfiguration.Instance.MenuControllerPresses.Value;
        var presses = new ControllerPresses
        {
            Trigger = sample.Trigger > PressThreshold, Grip = sample.Grip > PressThreshold, StickClick = sample.StickClick,
            Primary = sample.Primary, Secondary = sample.Secondary, Top = sample.Top, Outside = sample.Outside,
            Menu = sample.Menu, Shoulder = sample.Shoulder
        };
        foreach (var (part, renderer) in hand.Pressed)
        {
            var on = show && ControllerCallouts.Pressed(part, sample.NativeFrame, left, presses);
            if (renderer != null && renderer.enabled != on)
                renderer.enabled = on;
        }
    }

    // The button's own mesh, grown a little about its middle.
    internal static Mesh BuildPressedMesh(ControllerPartShape shape, string name)
    {
        var positions = shape.Positions;
        var count = positions.Length / 3;
        var vertices = new Vector3[count];
        var centre = new Vector3(shape.CentreX, shape.CentreY, shape.CentreZ);
        for (var i = 0; i < count; i++)
        {
            var offset = new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]) - centre;
            var length = offset.magnitude;
            vertices[i] = centre + offset * (1f + PressedGrow) + (length > 1e-6f ? offset / length * PressedLift : Vector3.zero);
        }
        var triangles = (int[])shape.Triangles.Clone();
        for (var i = 0; i + 2 < triangles.Length; i += 3)
            (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
        var mesh = new Mesh
        {
            name = "NOVR pressed " + name,
            indexFormat = count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16
        };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void Create(Hand hand, LoadedModel model, bool left, bool frame)
    {
        hand.Info = model.Info;
        hand.Shapes = model.Shapes;
        hand.Whole = model.Whole;
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
        foreach (var pair in model.Shapes)
        {
            if (!ControllerCallouts.Pressable(pair.Key, frame, left))
                continue;
            var mesh = BuildPressedMesh(pair.Value, pair.Key);
            hand.PressedMeshes.Add(mesh);
            var pressed = new GameObject("Pressed " + pair.Key, typeof(MeshFilter), typeof(MeshRenderer));
            pressed.transform.SetParent(root.transform, false);
            pressed.layer = root.layer;
            pressed.GetComponent<MeshFilter>().sharedMesh = mesh;
            var highlight = pressed.GetComponent<MeshRenderer>();
            highlight.sharedMaterial = White();
            highlight.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            highlight.receiveShadows = false;
            highlight.enabled = false;
            hand.Pressed.Add((pair.Key, highlight));
        }
        root.SetActive(false);
        hand.Root = root;
        hand.LabelKey = default;
        Debug.Log($"[NOVR] Menu controller model '{model.Name}': {hand.Mesh.vertexCount} vertices, texture {(hand.Texture != null ? hand.Texture.width + "x" + hand.Texture.height : "none")}, {model.Shapes.Count} button shapes.");
    }

    private void UpdateLabels(Hand hand, bool left, bool frame, ControllerHintContext context, Camera camera, VrUiCursor? cursor)
    {
        var settings = ModConfiguration.Instance;
        if (!settings.MenuControllerTooltips.Value || hand.Info == null)
        {
            if (hand.Labels != null && hand.Labels.activeSelf)
                hand.Labels.SetActive(false);
            if (hand.Leaders != null && hand.Leaders.activeSelf)
                hand.Leaders.SetActive(false);
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
            if (hand.Leaders != null)
                Destroy(hand.Leaders);
            hand.LabelKey = key;
            hand.Labels = BuildLabels(hand, left, context, pointerHand, frame, stickLeft, bindings.Get, position, rotation);
            hand.LabelCanvas = hand.Labels.GetComponent<Canvas>();
        }
        var labels = hand.Labels.transform;
        var toHead = camera.transform.position - labels.position;
        var show = ControllerCallouts.LookedAt(Vector3.Dot(-labels.forward, toHead.normalized), toHead.magnitude);
        if (hand.Labels.activeSelf != show)
            hand.Labels.SetActive(show);
        if (hand.Leaders != null && hand.Leaders.activeSelf != show)
            hand.Leaders.SetActive(show);
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
        // The columns stand clear of the whole controller as seen on the
        // label plane, not just of the labelled buttons: with two labels a
        // hand, those sit near the middle and the panels covered the head.
        float leftReach = reach, rightReach = reach;
        if (hand.Whole != null)
        {
            var across = rotation * Vector3.right;
            var points = hand.Whole.Positions;
            for (var i = 0; i + 2 < points.Length; i += 3)
            {
                var x = Vector3.Dot(new Vector3(points[i], points[i + 1], points[i + 2]) - position, across) / CanvasScale;
                leftReach = Mathf.Max(leftReach, -x);
                rightReach = Mathf.Max(rightReach, x);
            }
        }
        var spots = ControllerCallouts.Layout(anchors, -(leftReach + LabelGap + LabelWidth * .5f), rightReach + LabelGap + LabelWidth * .5f,
            tallest * LineHeight + LabelPadding + 4f);
        // The lines are real geometry in the controller's own space, not part
        // of the flat label panel: each runs from its label to the button
        // itself, so it stays on the button from any angle.
        var leaders = new GameObject("NOVR Controller Leaders");
        leaders.transform.SetParent(hand.Root.transform, false);
        leaders.layer = hand.Root.layer;
        hand.Leaders = leaders;
        for (var i = 0; i < callouts.Count && i < spots.Length; i++)
        {
            var edge = AddLabel(view.transform, callouts[i], new Vector2(spots[i].x, spots[i].y));
            var from = position + rotation * (new Vector3(edge.x, edge.y, 0f) * CanvasScale);
            AddLeader(leaders.transform, LeaderPath(hand, callouts[i].Part, from, rotation * Vector3.back));
        }
        return view;
    }

    // The line from a label to its button: it ends on the surface of the
    // button's own mesh and goes round the controller rather than through
    // it. Without a mesh for the button (SteamVR's files gave none) it runs
    // straight to the button's origin.
    private static Vector3[] LeaderPath(Hand hand, string name, Vector3 from, Vector3 towardLabels)
    {
        if (hand.Shapes != null && hand.Shapes.TryGetValue(name, out var shape))
        {
            var path = ControllerPartShape.Leader(shape, hand.Whole, from.x, from.y, from.z, towardLabels.x, towardLabels.y, towardLabels.z);
            var points = new Vector3[path.Length / 3];
            for (var i = 0; i < points.Length; i++)
                points[i] = new Vector3(path[i * 3], path[i * 3 + 1], path[i * 3 + 2]);
            return points;
        }
        foreach (var part in hand.Info!.Parts)
            if (part.Name == name)
                return new[] { from, new Vector3(part.X, part.Y, part.Z) };
        return new[] { from, from };
    }

    private void AddLeader(Transform parent, Vector3[] path)
    {
        var to = path[path.Length - 1];
        var white = White();
        var layer = parent.gameObject.layer;
        var line = new GameObject("Leader", typeof(LineRenderer)).GetComponent<LineRenderer>();
        line.transform.SetParent(parent, false);
        line.gameObject.layer = layer;
        line.useWorldSpace = false;
        line.positionCount = path.Length;
        line.SetPositions(path);
        line.startWidth = line.endWidth = LeaderWidth;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.sharedMaterial = white;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        // A bead where the line meets the button.
        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var collider = dot.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
        dot.name = "Button";
        dot.transform.SetParent(parent, false);
        dot.layer = layer;
        dot.transform.localPosition = to;
        dot.transform.localScale = Vector3.one * LeaderDot;
        var renderer = dot.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = white;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // Returns the middle of the label's edge that faces the controller.
    private Vector2 AddLabel(Transform parent, ControllerCallout callout, Vector2 spot)
    {
        var layer = parent.gameObject.layer;
        var rightSide = spot.x > 0;
        var edge = new Vector2(spot.x + (rightSide ? -LabelWidth * .5f : LabelWidth * .5f), spot.y);
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
        return edge;
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
        if (hand.Leaders != null)
            Destroy(hand.Leaders);
        hand.Leaders = null;
        hand.Shapes = null;
        hand.Whole = null;
        // The white copies are children of the root and went with it.
        hand.Pressed.Clear();
        foreach (var mesh in hand.PressedMeshes)
            if (mesh != null)
                Destroy(mesh);
        hand.PressedMeshes.Clear();
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
        if (_white != null)
            Destroy(_white);
        if (_instance == this)
            _instance = null;
    }
}
