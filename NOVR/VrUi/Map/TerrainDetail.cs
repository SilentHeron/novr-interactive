using System;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// When to draw from the half-size mipmapped copy, given how many eye pixels
// one texel of the original covers. Below UseBelow the original is being
// shrunk enough to sparkle, and the copy's top level is close to one texel per
// pixel. Above LeaveAbove the original is the sharper of the two. The gap
// keeps the choice from flickering as the head moves.
internal sealed class MinificationSwitch
{
    internal const float UseBelow = .6f, LeaveAbove = .72f;

    internal bool Active { get; private set; }

    internal bool Update(float pixelsPerTexel)
    {
        // An unmeasurable frame (no camera, map behind the head) changes nothing.
        if (float.IsNaN(pixelsPerTexel) || float.IsInfinity(pixelsPerTexel) || pixelsPerTexel <= 0)
            return Active;
        if (pixelsPerTexel <= UseBelow)
            Active = true;
        else if (pixelsPerTexel >= LeaveAbove)
            Active = false;
        return Active;
    }

    internal void Reset() => Active = false;
}

// A stand-in for one sprite, and how to free it.
internal sealed class SpriteCopy
{
    internal readonly Sprite Source, Copy;
    private readonly Action _destroy;

    internal SpriteCopy(Sprite source, Sprite copy, Action destroy)
    {
        Source = source;
        Copy = copy;
        _destroy = destroy;
    }

    internal void Destroy() => _destroy();
}

// The cockpit map's terrain is one image thousands of pixels wide, shipped
// without mipmaps, shown on a panel a few hundred pixels across. Zoomed out it
// is shrunk several times over and sparkles with every head movement. While
// that is the case, draw it from a half-size copy that has mipmaps.
//
// Image.overrideSprite does the swap: the game's own sprite, and everything
// that reads it, stays as it is.
internal sealed partial class TerrainDetail
{
    internal const float MeasureSeconds = .25f;

    // One copy is kept between leases, so closing the full map does not
    // repeat the work. A different terrain replaces it.
    private static SpriteCopy? _cache;
    private static Sprite? _declined;

    private readonly Image _image;
    private readonly Transform _widget;
    private readonly MinificationSwitch _switch = new();
    private Sprite? _source;
    private SpriteCopy? _copy;
    private bool _overriding;
    private float _nextMeasure;

    internal TerrainDetail(Image image, Transform widget)
    {
        _image = image;
        _widget = widget;
    }

    internal bool Overriding => _overriding;

    internal void Update(bool enabled, float now)
    {
        var sprite = _image != null ? _image.sprite : null;
        if (!enabled || sprite == null)
        {
            Show(null);
            _source = null;
            _copy = null;
            if (!enabled)
                DropCache();
            return;
        }
        if (!ReferenceEquals(sprite, _source))
        {
            Show(null);
            _source = sprite;
            _copy = CopyOf(sprite);
            _switch.Reset();
            _nextMeasure = 0;
        }
        if (_copy == null)
            return;
        if (now >= _nextMeasure)
        {
            _nextMeasure = now + MeasureSeconds;
            _switch.Update(PixelsPerTexel(_image!, _widget));
        }
        Show(_switch.Active ? _copy.Copy : null);
    }

    // Put the game's own sprite back; the copy stays cached for the next lease.
    internal void Release()
    {
        Show(null);
        _source = null;
        _copy = null;
        _switch.Reset();
    }

    internal static void DropCache()
    {
        _cache?.Destroy();
        _cache = null;
        _declined = null;
    }

    private void Show(Sprite? copy)
    {
        var wanted = copy != null;
        if (wanted == _overriding)
            return;
        if (_image != null)
            _image.overrideSprite = copy!;
        _overriding = wanted;
    }

    private static SpriteCopy? CopyOf(Sprite sprite)
    {
        if (_cache != null && ReferenceEquals(_cache.Source, sprite) && _cache.Copy != null)
            return _cache;
        // A sprite that could not be copied is not tried again on every lease.
        if (ReferenceEquals(_declined, sprite))
            return null;
        DropCache();
        try
        {
            _cache = MakeCopy(sprite);
        }
        catch (Exception error)
        {
            _cache = null;
            Debug.LogWarning("[NOVR] Cockpit map keeps the game's terrain image: " + error.Message);
        }
        if (_cache == null)
            _declined = sprite;
        return _cache;
    }

    // A half-size copy of the sprite's texture with a full mipmap chain, or
    // null when this sprite needs none or cannot be copied.
    private static partial SpriteCopy? MakeCopy(Sprite source);

    // Eye pixels covered by one texel of the image's sprite where the widget
    // is, or NaN when that cannot be measured right now.
    private static partial float PixelsPerTexel(Image image, Transform widget);
}
