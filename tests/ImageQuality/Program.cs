using System.Reflection;
using NOVR;
using NOVR.Rendering;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;

var count = 0;
void Check(bool ok, string why) { if (!ok) throw new Exception(why); count++; }
void Near(float actual, float expected, string why) => Check(MathF.Abs(actual - expected) < 1e-5f, $"{why} ({actual} vs {expected})");

// ---------------------------------------------------------------- resting vignette
Near(RestingVignette.Remap(.4f), 0, "The game's resting value maps to no darkening");
Near(RestingVignette.Remap(1), 1, "Full blackout stays full");
Near(RestingVignette.Remap(.7f), .5f, "Values between rescale evenly");
Check(RestingVignette.Remap(.2f) == 0 && RestingVignette.Remap(3) == 1 && RestingVignette.Remap(0) == 0, "Out-of-range values stay in range");

var manager = new CameraStateManager();
SceneSingleton<CameraStateManager>.i = manager;
var volume = new Volume();
manager.Volume = volume;
var shared = new VolumeProfile();
var shipped = new Vignette();
shipped.intensity.value = .4f;
shared.Components.Add(shipped);
volume.sharedProfile = shared;
var resting = new RestingVignette();
resting.Tick(false);
Check(manager.Queries == 0 && shipped.intensity.value == .4f, "Switched off and never applied: nothing is looked up or changed");
resting.Tick(true);
Check(shipped.intensity.value == 0, "The value the profile ships with becomes zero");
Check(volume.ProfileReads == 0, "A volume that has not instantiated its profile is not made to");
resting.Tick(true);
Check(shipped.intensity.value == 0, "Its own zero is not rescaled again");

// The game instantiates the profile when the pilot's G-force model starts.
var instance = new VolumeProfile();
var live = new Vignette();
live.intensity.value = shipped.intensity.value;
instance.Components.Add(live);
volume.Instance = instance;
resting.Tick(true);
Near(shipped.intensity.value, .4f, "The shared profile gets its shipped value back once the volume stops using it");
Check(live.intensity.value == 0, "The instance starts from no darkening");
live.intensity.value = .4f;
resting.Tick(true);
Check(live.intensity.value == 0, "The game's per-step rewrite of the resting value is removed again");
live.intensity.value = .7f;
resting.Tick(true);
Near(live.intensity.value, .5f, "Rising G-force still darkens the edges");
resting.Tick(true);
resting.Tick(true);
Near(live.intensity.value, .5f, "Frames with no new native value leave the result alone");
live.intensity.value = 1;
resting.Tick(true);
Near(live.intensity.value, 1, "Blackout closes the view completely");
live.intensity.value = .7f;
resting.Tick(true);
resting.Tick(false);
Near(live.intensity.value, .7f, "Switching off puts back the game's last value");
resting.Tick(true);
Near(live.intensity.value, .5f, "Switching on applies again");
live.intensity.value = .4f;
resting.Tick(false);
Near(live.intensity.value, .4f, "A value the game wrote since is left as the game wrote it");

// Scene change: the volume and its profile go away, a new one arrives.
resting.Tick(true);
live.Dead = instance.Dead = true;
manager.Volume = null;
resting.Tick(true);
var nextVolume = new Volume { Instance = new VolumeProfile() };
var next = new Vignette();
next.intensity.value = .4f;
nextVolume.Instance.Components.Add(next);
manager.Volume = nextVolume;
resting.Tick(true);
Check(next.intensity.value == 0, "A new scene's vignette is picked up after the old one was destroyed");
nextVolume.Instance.Components.Clear();
nextVolume.Instance = new VolumeProfile();
resting.Tick(true);
Near(next.intensity.value, .4f, "A profile without a vignette is left alone, and the previous one restored");
SceneSingleton<CameraStateManager>.i = null;
resting.Tick(true);
resting.Tick(false);
Check(true, "No camera manager (menus): nothing to do, nothing thrown");

// ---------------------------------------------------------------- cockpit screens
// As the game ships them: 8-bit sRGB, no mipmaps, anisotropic level 0; the tactical screen 2x multisampled.
RenderTexture Screen(string name, bool created = true, int samples = 2)
{
    var texture = new RenderTexture
    {
        name = name, width = 1024, height = 512, antiAliasing = samples, anisoLevel = 0, filterMode = FilterMode.Bilinear,
        useMipMap = false, autoGenerateMips = true, graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB
    };
    if (created)
        texture.MarkCreated();
    return texture;
}
bool Storage(RenderTexture t, int samples, GraphicsFormat format, bool mips) => t.antiAliasing == samples && t.graphicsFormat == format && t.useMipMap == mips && t.autoGenerateMips;
bool Mipmapped(RenderTexture t) => Storage(t, 1, CockpitScreenFilter.MipmappedFormat, true) && t.filterMode == FilterMode.Trilinear && t.anisoLevel == CockpitScreenFilter.AnisoLevel;
bool AsShipped(RenderTexture t, int samples = 2) => Storage(t, samples, GraphicsFormat.R8G8B8A8_SRGB, false) && t.filterMode == FilterMode.Bilinear && t.anisoLevel == 0;
bool AnisotropicOnly(RenderTexture t, int samples = 2) => Storage(t, samples, GraphicsFormat.R8G8B8A8_SRGB, false) && t.filterMode == FilterMode.Bilinear && t.anisoLevel == CockpitScreenFilter.AnisoLevel;

CockpitScreenFilter.Sync(false);
var tactical = Screen("TacScreenRenderer");
CockpitScreenFilter.Track(null, true);
CockpitScreenFilter.Track(tactical, interfaceOnly: true);
Check(AsShipped(tactical) && tactical.Releases == 0, "Outside VR a tracked screen keeps what the game shipped");
CockpitScreenFilter.Sync(true);
Check(Mipmapped(tactical) && tactical.IsCreated() && tactical.Releases == 1 && tactical.Creates == 1,
    "In VR the tactical screen is rebuilt once: single-sampled half-float colour with mipmaps, trilinear and anisotropic filtering");
CockpitScreenFilter.Sync(true);
CockpitScreenFilter.Track(tactical, interfaceOnly: true);
Check(tactical.Releases == 1 && tactical.Creates == 1, "It is not rebuilt again while nothing changes");
var window = Screen("TargetWindowRender", samples: 1);
CockpitScreenFilter.Track(window, interfaceOnly: false);
Check(AnisotropicOnly(window, 1) && window.Releases == 0 && window.Creates == 0,
    "A screen that shows the outside world keeps its storage and format and only gains anisotropic filtering");
var unborn = Screen("not created yet", created: false);
CockpitScreenFilter.Track(unborn, interfaceOnly: true);
Check(Mipmapped(unborn) && unborn.Creates == 0 && unborn.Releases == 0, "A screen with no storage yet is set up but not created early");
CockpitScreenFilter.Sync(false);
Check(AsShipped(tactical) && tactical.IsCreated() && tactical.Releases == 2 && AsShipped(window, 1) && window.Releases == 0 && AsShipped(unborn) && !unborn.IsCreated(),
    "Leaving VR or switching the option off restores exactly what the game shipped: samples, format, no mipmaps, anisotropic level 0");
CockpitScreenFilter.Sync(false);
Check(tactical.Releases == 2, "Restoring twice does nothing more");
tactical.Dead = true;
CockpitScreenFilter.Sync(true);
Check(AnisotropicOnly(window, 1) && tactical.Releases == 2, "A destroyed screen is forgotten without being touched");
CockpitScreenFilter.Sync(false);

// A device without half-float render targets: anisotropic filtering only, nothing rebuilt.
SystemInfo.FloatTargets = false;
var plain = Screen("no float targets");
CockpitScreenFilter.Track(plain, interfaceOnly: true);
CockpitScreenFilter.Sync(true);
Check(AnisotropicOnly(plain) && plain.Releases == 0, "Without half-float targets the tactical screen keeps its storage and gains anisotropic filtering");
CockpitScreenFilter.Sync(false);
Check(AsShipped(plain), "and restores");
SystemInfo.FloatTargets = true;

// A device that refuses to create the mipmapped form.
var stubborn = Screen("refuses mipmaps");
stubborn.RefuseMipmaps = true;
Debug.Warnings.Clear();
CockpitScreenFilter.Sync(true);
CockpitScreenFilter.Track(stubborn, interfaceOnly: true);
Check(AnisotropicOnly(stubborn) && stubborn.IsCreated(), "A refused rebuild goes back to the shipped storage, created again, with anisotropic filtering");
Check(Debug.Warnings.Count == 1 && Debug.Warnings[0].Contains("refuses mipmaps"), "and says so once");
CockpitScreenFilter.Sync(false);
Check(AsShipped(stubborn), "The fallback restores cleanly too");

// The game binds the tactical screen's camera in TacScreen.Initialize.
var viaCamera = Screen("camera target");
CockpitScreenFilter.Sync(true);
var postfix = typeof(TacScreenFilterPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!;
postfix.Invoke(null, new object?[] { new Camera { targetTexture = viaCamera } });
Check(Mipmapped(viaCamera), "The tactical screen's camera target is tracked as an interface-only screen");
postfix.Invoke(null, new object?[] { null });
postfix.Invoke(null, new object?[] { new Camera() });
CockpitScreenFilter.Sync(false);
Check(AsShipped(viaCamera), "and is restored with the rest");

// ---------------------------------------------------------------- the driver
var driver = new VrImageQuality();
void Frame() => typeof(VrImageQuality).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(driver, null);
var settings = ModConfiguration.Instance.ImageQuality;
var driverVolume = new Volume { Instance = new VolumeProfile() };
var driverVignette = new Vignette();
driverVignette.intensity.value = .4f;
driverVolume.Instance.Components.Add(driverVignette);
SceneSingleton<CameraStateManager>.i = new CameraStateManager { Volume = driverVolume };
var driverScreen = Screen("driver screen");
CockpitScreenFilter.Track(driverScreen, interfaceOnly: true);
XRSettings.enabled = false;
Time.unscaledTime = 10;
Frame();
Check(driverVignette.intensity.value == .4f && AsShipped(driverScreen), "On the desktop nothing changes");
XRSettings.enabled = true;
Time.unscaledTime = 10.5f;
Frame();
Check(driverVignette.intensity.value == 0 && AsShipped(driverScreen), "In VR the vignette follows every frame; screens wait for their once-a-second pass");
Time.unscaledTime = 11;
Frame();
Check(Mipmapped(driverScreen), "The once-a-second pass filters the screens");
settings.RemoveRestingVignette.Value = false;
settings.FilterCockpitScreens.Value = false;
Time.unscaledTime = 12;
Frame();
Check(driverVignette.intensity.value == .4f && AsShipped(driverScreen), "Each option switches off live");
settings.RemoveRestingVignette.Value = settings.FilterCockpitScreens.Value = true;
Time.unscaledTime = 13;
Frame();
typeof(VrImageQuality).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(driver, null);
Check(driverVignette.intensity.value == .4f && AsShipped(driverScreen), "Disabling the component undoes both");
SceneSingleton<CameraStateManager>.i = null;
XRSettings.enabled = false;

// ---------------------------------------------------------------- when to use the copy
var choice = new MinificationSwitch();
Check(!choice.Active && !choice.Update(.9f), "A map shown near one texel per pixel uses the game's image");
Check(choice.Update(.5f), "Shrunk well below that, the mipmapped copy takes over");
Check(choice.Update(.65f) && choice.Update(float.NaN) && choice.Update(0) && choice.Update(float.PositiveInfinity), "It stays through the gap and through frames that cannot be measured");
Check(!choice.Update(.75f) && !choice.Update(.65f), "It leaves above the gap and stays out through it");
Check(choice.Update(MinificationSwitch.UseBelow) && !choice.Update(MinificationSwitch.LeaveAbove), "Both thresholds are inclusive");
choice.Update(.1f);
choice.Reset();
Check(!choice.Active, "A new terrain starts from the game's image");

// ---------------------------------------------------------------- the copy itself
Sprite Terrain(string name, int width, int height, GraphicsFormat format = GraphicsFormat.RGBA_DXT1_SRGB, int mips = 1)
{
    var texture = new Texture2D { name = name, width = width, height = height, graphicsFormat = format, mipmapCount = mips, wrapMode = TextureWrapMode.Clamp };
    return new Sprite { name = name, texture = texture, rect = new(0, 0, width, height), pivot = new(width * .5f, height * .25f), pixelsPerUnit = 100, border = new(8, 4, 2, 6) };
}
var camera = new Camera();
APIBus.MainCamera = camera;
var widget = new Transform { position = new(0, 0, 2) };
Image Map(Sprite sprite)
{
    var image = new Image { sprite = sprite };
    image.rectTransform.rect = new(0, 0, 1800, 900);
    image.rectTransform.Scale = .0005f;
    return image;
}
// Eye pixels per texel for the fixture above: scale x pixels-per-metre / (depth x texels per unit).
void SetDensity(Image image, float density) =>
    camera.PixelsPerMetreAtOne = density * widget.position.z * (image.sprite!.rect.width / image.rectTransform.rect.width) / image.rectTransform.Scale;

TerrainDetail.DropCache();
Graphics.Reset();
var naval = Terrain("terrain_naval_map", 8192, 4096);
// Whatever filter the game set must survive: halving needs bilinear only while it runs.
naval.texture.filterMode = FilterMode.Point;
var map = Map(naval);
var detail = new TerrainDetail(map, widget);
SetDensity(map, .3f);
detail.Update(true, 0);
var expected = new List<string> { "texture 4096x2048 R8G8B8A8_SRGB", "apply mips=False release=True" };
for (var level = 0; level < 13; level++)
{
    var from = level == 0 ? "terrain_naval_map" : "step " + Math.Max(1, 4096 >> (level - 1)) + "x" + Math.Max(1, 2048 >> (level - 1));
    expected.Add("blit " + from + " (Bilinear) -> " + Math.Max(1, 4096 >> level) + "x" + Math.Max(1, 2048 >> level));
    expected.Add("copy " + Math.Max(1, 4096 >> level) + "x" + Math.Max(1, 2048 >> level) + " -> level " + level);
}
expected.Add("sprite");
Check(Graphics.Calls.SequenceEqual(expected),
    "The copy is half size in the original's colour encoding; its script-side pixels are dropped first, then every level is the one above halved by a bilinear blit: " + string.Join(" | ", Graphics.Calls));
var made = Graphics.Made.Single();
Check((made.Flags & TextureCreationFlags.MipChain) != 0 && made.mipmapCount == 13 && made.filterMode == FilterMode.Trilinear &&
      made.anisoLevel > 1 && made.wrapMode == TextureWrapMode.Clamp && !made.Dead, "It has a full mipmap chain, trilinear and anisotropic filtering and the original's wrap mode");
Check(RenderTexture.Temporaries == 0 && RenderTexture.active == null, "Every working texture is handed back and the engine's active target is left as found");
Check(naval.texture.filterMode == FilterMode.Point, "The game's texture gets its own filter mode back");
var copy = map.Override!;
Check(detail.Overriding && copy != null && ReferenceEquals(copy.texture, made) && ReferenceEquals(map.sprite, naval), "A shrunk map draws from the copy; the game's own sprite is untouched");
Check(copy!.rect == new Rect(0, 0, 4096, 2048) && copy.MeshType == SpriteMeshType.FullRect, "The copy is the whole half-size texture");
Near(copy.pivot.x / copy.rect.width, .5f, "Pivot keeps its place horizontally");
Near(copy.pivot.y / copy.rect.height, .25f, "and vertically");
Near(copy.pixelsPerUnit, 50, "Half the texels per unit keeps the same size on the map");
Check(copy.border == new Vector4(4, 2, 1, 3), "Borders scale with it");

var projections = camera.Projections;
detail.Update(true, .1f);
detail.Update(true, .2f);
Check(camera.Projections == projections, "Density is measured again only after its interval");
SetDensity(map, .9f);
detail.Update(true, .2f);
Check(detail.Overriding, "so a change waits for the next measurement");
detail.Update(true, TerrainDetail.MeasureSeconds);
Check(!detail.Overriding && map.Override == null && camera.Projections == projections + 2, "A map near one texel per pixel goes back to the game's image");
var writes = map.OverrideWrites;
detail.Update(true, 1);
detail.Update(true, 2);
Check(map.OverrideWrites == writes, "The image is only written when the choice changes");
widget.position = new(0, 0, -1);
SetDensity(map, .1f);
detail.Update(true, 3);
Check(!detail.Overriding, "A map behind the head cannot be measured and changes nothing");
widget.position = new(0, 0, 2);
SetDensity(map, .3f);
detail.Update(true, 4);
Check(detail.Overriding, "Shrunk again, the copy returns");

var calls = Graphics.Calls.Count;
detail.Release();
Check(map.Override == null && !detail.Overriding && !made.Dead, "Releasing the map puts the game's image back and keeps the copy");
var again = new TerrainDetail(map, widget);
again.Update(true, 5);
Check(again.Overriding && Graphics.Calls.Count == calls && ReferenceEquals(map.Override!.texture, made), "The next lease of the same terrain reuses it without any GPU work");

// A different terrain replaces the cached copy.
var land = Terrain("terrain2_map", 4096, 4096, GraphicsFormat.RGBA_DXT1_UNorm);
map.sprite = land;
SetDensity(map, .3f);
again.Update(true, 6);
Check(made.Dead && copy.Dead, "A new terrain frees the previous copy");
Check(Graphics.Calls.Contains("texture 2048x2048 R8G8B8A8_UNorm") && again.Overriding &&
      map.Override!.texture.width == 2048 && map.Override.texture.graphicsFormat == GraphicsFormat.R8G8B8A8_UNorm,
    "and gets its own copy, linear when the original is linear");
var second = map.Override!.texture;
again.Update(false, 7);
Check(map.Override == null && second.Dead, "Switching the option off puts the game's image back and frees the copy");
again.Update(true, 8);
Check(again.Overriding && !map.Override!.texture.Dead, "Switching it on again makes a new one");
again.Release();
TerrainDetail.DropCache();

// Sprites that need no copy, or cannot have one.
foreach (var (reason, sprite) in new (string, Sprite)[]
{
    ("already has mipmaps", Terrain("mipped", 4096, 4096, mips: 13)),
    ("is small", Terrain("small", 512, 512)),
    ("is part of an atlas", new Func<Sprite>(() => { var s = Terrain("atlas", 4096, 4096); s.packed = true; return s; })()),
    ("is a region of its texture", new Func<Sprite>(() => { var s = Terrain("region", 4096, 4096); s.rect = new(0, 0, 2048, 4096); return s; })())
})
{
    Graphics.Reset();
    var image = Map(sprite);
    SetDensity(image, .1f);
    var none = new TerrainDetail(image, widget);
    none.Update(true, 0);
    none.Update(true, 1);
    Check(Graphics.Calls.Count == 0 && image.Override == null && image.OverrideWrites == 0, "A sprite that " + reason + " is left to the game");
    none.Release();
    new TerrainDetail(image, widget).Update(true, 2);
    Check(Graphics.Calls.Count == 0, "and is not examined again on the next lease");
    TerrainDetail.DropCache();
}

// Failures leave the game's image in place and clean up.
Graphics.Reset();
Debug.Warnings.Clear();
var failing = Map(Terrain("refused", 4096, 4096));
SetDensity(failing, .1f);
failing.sprite!.texture.filterMode = FilterMode.Point;
var before = new RenderTexture { name = "whatever was active" };
RenderTexture.active = before;
Graphics.FailCopy = true;
new TerrainDetail(failing, widget).Update(true, 0);
Check(failing.Override == null && Graphics.Made.Single().Dead && RenderTexture.Temporaries == 0 && Debug.Warnings.Count == 1,
    "A failed GPU copy frees what it made, warns once and leaves the game's image");
Check(ReferenceEquals(RenderTexture.active, before) && failing.sprite.texture.filterMode == FilterMode.Point,
    "even then the active target and the game texture's filter mode are put back");
RenderTexture.active = null;
TerrainDetail.DropCache();
Graphics.Reset();

// No terrain yet, no camera, destroyed image.
var empty = new Image();
var waiting = new TerrainDetail(empty, widget);
waiting.Update(true, 0);
Check(empty.OverrideWrites == 0 && Graphics.Calls.Count == 0, "A map with no terrain loaded yet is left alone");
empty.sprite = Terrain("late", 4096, 4096);
empty.rectTransform.rect = new(0, 0, 900, 900);
empty.rectTransform.Scale = .0005f;
APIBus.MainCamera = null;
waiting.Update(true, 1);
Check(!waiting.Overriding && Graphics.Calls.Count > 0, "Without a headset camera the copy exists but the game's image stays");
APIBus.MainCamera = camera;
SetDensity(empty, .2f);
waiting.Update(true, 2);
Check(waiting.Overriding, "It takes over once the camera can measure");
empty.Dead = true;
waiting.Update(true, 3);
waiting.Release();
Check(!waiting.Overriding, "A destroyed map image is released without touching it");
TerrainDetail.DropCache();
Console.WriteLine($"PASS {count} source-linked image-quality checks (vignette, cockpit screens, map terrain copy; engine calls doubled, no pixels drawn).");
