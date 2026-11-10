using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

internal sealed partial class TerrainDetail
{
    // Smaller images are never shrunk enough on the widget to need this.
    internal const int MinimumSourceSize = 1024;
    private const int DensitySpan = 64;

    // Everything stays on the GPU: the game's texture is compressed and not
    // readable from scripts. Each level is the one above it halved by a
    // bilinear blit, which makes every pixel the average of four, taken in
    // linear light because the sampler decodes sRGB first. (The device's own
    // mipmap generation averages the encoded bytes instead, measured in the
    // running game; that darkens fine bright detail.) The finished levels are
    // copied into a texture a sprite can use.
    private static partial SpriteCopy? MakeCopy(Sprite source)
    {
        var texture = source.texture;
        var rect = source.rect;
        // Only a sprite that is its whole texture; an atlas entry would bleed
        // into its neighbours in the lower mipmap levels.
        if (texture == null || texture.mipmapCount > 1 || source.packed ||
            Mathf.Max(texture.width, texture.height) < MinimumSourceSize ||
            (int)rect.width != texture.width || (int)rect.height != texture.height)
            return null;
        var width = Mathf.Max(1, texture.width / 2);
        var height = Mathf.Max(1, texture.height / 2);
        // Same colour encoding as the original, so nothing shifts in brightness.
        var format = GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat)
            ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
        var copy = new Texture2D(width, height, format, TextureCreationFlags.MipChain | TextureCreationFlags.DontInitializePixels)
        {
            name = texture.name + " (NOVR mipmapped)",
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            wrapMode = texture.wrapMode,
            hideFlags = HideFlags.HideAndDontSave
        };
        var active = RenderTexture.active;
        var filter = texture.filterMode;
        RenderTexture? above = null, step = null;
        var finished = false;
        try
        {
            // Hand the (blank) pixels to the GPU and drop the script-side
            // copy first; the real contents then arrive GPU to GPU.
            copy.Apply(false, true);
            texture.filterMode = FilterMode.Bilinear;
            Texture from = texture;
            for (var level = 0; level < copy.mipmapCount; level++)
            {
                step = RenderTexture.GetTemporary(new RenderTextureDescriptor(
                    Mathf.Max(1, width >> level), Mathf.Max(1, height >> level), format, 0));
                step.filterMode = FilterMode.Bilinear;
                step.wrapMode = TextureWrapMode.Clamp;
                Graphics.Blit(from, step);
                Graphics.CopyTexture(step, 0, 0, copy, 0, level);
                if (above != null)
                    RenderTexture.ReleaseTemporary(above);
                above = step;
                step = null;
                from = above;
            }
            var scale = width / rect.width;
            var sprite = Sprite.Create(copy, new Rect(0, 0, width, height),
                new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height),
                source.pixelsPerUnit * scale, 0, SpriteMeshType.FullRect, source.border * scale);
            sprite.name = source.name + " (NOVR mipmapped)";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            finished = true;
            return new SpriteCopy(source, sprite, () =>
            {
                UnityEngine.Object.Destroy(sprite);
                UnityEngine.Object.Destroy(copy);
            });
        }
        finally
        {
            // The game's texture and the engine's active target are left as found.
            texture.filterMode = filter;
            RenderTexture.active = active;
            if (step != null)
                RenderTexture.ReleaseTemporary(step);
            if (above != null)
                RenderTexture.ReleaseTemporary(above);
            if (!finished)
                UnityEngine.Object.Destroy(copy);
        }
    }

    // Projects a short run of texels at the widget through the headset camera.
    private static partial float PixelsPerTexel(Image image, Transform widget)
    {
        var camera = APIBus.MainCamera;
        var sprite = image.sprite;
        var rect = image.rectTransform;
        if (camera == null || sprite == null || widget == null || rect.rect.width <= 0)
            return float.NaN;
        var texelsPerUnit = sprite.rect.width / rect.rect.width;
        var from = camera.WorldToScreenPoint(widget.position);
        var to = camera.WorldToScreenPoint(widget.position + rect.TransformVector(Vector3.right) * (DensitySpan / texelsPerUnit));
        // Behind the camera the projection is meaningless.
        if (from.z <= 0 || to.z <= 0)
            return float.NaN;
        return ((Vector2)(to - from)).magnitude / DensitySpan;
    }
}
