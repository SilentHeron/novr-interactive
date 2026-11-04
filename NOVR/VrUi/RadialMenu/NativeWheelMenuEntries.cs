using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// Adds presentation-only sectors without editing the game's action lists or
// shared RadialMenuAction assets. Native icons retain their original callbacks.
internal sealed class NativeWheelMenuEntries : IDisposable
{
    private readonly List<(Transform transform, Vector3 position, Quaternion rotation, Image? sector, float fill)> _originals = new();
    private readonly Image?[] _sectors = new Image?[2];
    private readonly GameObject?[] _labels = new GameObject?[2];
    private readonly Color[] _idle = new Color[2];
    internal const int Count = 2;
    internal int Index
    {
        get;
    }

    internal NativeWheelMenuEntries(Transform parent, List<GameObject> nativeObjects, int nativeCount, GameObject sectorTemplate)
    {
        Index = nativeCount;
        if (nativeObjects.Count != nativeCount * 2)
            throw new InvalidOperationException("Native wheel entry layout changed");
        // Native SetupMain stores an icon followed by its radial sector.
        // Resolve everything before changing the borrowed geometry.
        for (var index = 0; index < nativeCount; index++)
        {
            var icon = nativeObjects[index * 2];
            var sector = nativeObjects[index * 2 + 1];
            if (icon == null || sector == null || !icon.transform.IsChildOf(parent) || !sector.transform.IsChildOf(parent))
                throw new InvalidOperationException("Native wheel entry is outside its main page");
            var image = sector.GetComponent<Image>();
            if (image == null)
                throw new InvalidOperationException("Native wheel sector has no image");
            Remember(icon.transform, null);
            Remember(sector.transform, image);
        }
        var source = nativeCount > 0 ? nativeObjects[1] : sectorTemplate;
        if (source == null || source.GetComponent<Image>() == null)
            throw new InvalidOperationException("Native wheel sector template is unavailable");
        try
        {
            var degrees = 360f / (nativeCount + Count);
            for (var index = 0; index < nativeCount; index++)
            {
                nativeObjects[index * 2].transform.localPosition = Position(index, degrees);
                SetSector(nativeObjects[index * 2 + 1].GetComponent<Image>(), index, degrees);
            }
            for (var slot = 0; slot < Count; slot++)
            {
                var label = slot == 0 ? "GAME" : "VR";
                var sectorObject = UnityEngine.Object.Instantiate(source, parent);
                var sector = _sectors[slot] = sectorObject.GetComponent<Image>();
                sectorObject.name = "NOVR " + label + " Menu sector";
                sectorObject.SetActive(true);
                SetSector(sector, Index + slot, degrees);
                _idle[slot] = sector.color;
                var labelObject = _labels[slot] = new GameObject("NOVR " + label + " Menu label", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(parent, false);
                labelObject.transform.localPosition = Position(Index + slot, degrees);
                var text = labelObject.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = 14;
                text.text = label + "\nMENU";
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;
                text.rectTransform.sizeDelta = new Vector2(64, 42);
            }
        }
        catch { Dispose(); throw; }
    }

    private void Remember(Transform transform, Image? sector) =>
        _originals.Add((transform, transform.localPosition, transform.localRotation, sector, sector != null ? sector.fillAmount : 0));

    private static Vector3 Position(int index, float degrees)
    {
        var angle = index * degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0) * 90f;
    }

    private static void SetSector(Image sector, int index, float degrees)
    {
        sector.fillAmount = degrees / 360f;
        sector.transform.localRotation = Quaternion.Euler(0, 0, -(index - .5f) * degrees);
    }

    internal NativeWheelMenuAction ActionAt(int index) => index == Index ? NativeWheelMenuAction.GameMenu :
        index == Index + 1 ? NativeWheelMenuAction.VrMenu : NativeWheelMenuAction.None;

    internal void Hover(NativeWheelMenuAction action)
    {
        for (var slot = 0; slot < Count; slot++)
        {
            var sector = _sectors[slot];
            if (sector != null)
                sector.color = action == ActionAt(Index + slot)
                    ? new Color(.25f, .65f, .3f, Mathf.Max(_idle[slot].a, .6f)) : _idle[slot];
        }
    }

    public void Dispose()
    {
        foreach (var original in _originals)
        {
            if (original.transform == null)
                continue;
            original.transform.localPosition = original.position;
            original.transform.localRotation = original.rotation;
            if (original.sector != null)
                original.sector.fillAmount = original.fill;
        }
        _originals.Clear();
        for (var slot = 0; slot < Count; slot++)
        {
            if (_sectors[slot] != null)
                UnityEngine.Object.Destroy(_sectors[slot]!.gameObject);
            if (_labels[slot] != null)
                UnityEngine.Object.Destroy(_labels[slot]!);
            _sectors[slot] = null;
            _labels[slot] = null;
        }
    }
}
