using System.Collections.Generic;
using System.Reflection;
using NuclearOption.MissionEditorScripts;
using UnityEngine;

namespace NOVR.VrUi.Native;

// These native dialogs leave their underlying menu active. Availability of that
// menu therefore cannot decide whether NOVR may suppress its entire canvas.
internal sealed class NativeStockMenuHandoff
{
    private static readonly FieldInfo? CustomizeHolder = typeof(CustomizeMissionMenu)
        .GetField("holder", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly List<GameObject> _dialogs = new();
    private GameObject? _canvas;

    internal bool IsVisible(GameObject? canvas)
    {
        if (canvas == null) { _canvas = null; _dialogs.Clear(); return false; }
        if (_canvas != canvas)
        {
            _canvas = canvas; _dialogs.Clear();
            foreach (var fileMenu in canvas.GetComponentsInChildren<FileMenu>(true))
                _dialogs.Add(fileMenu.gameObject);
            foreach (var customize in canvas.GetComponentsInChildren<CustomizeMissionMenu>(true))
                if (CustomizeHolder?.GetValue(customize) is GameObject holder) _dialogs.Add(holder);
        }
        foreach (var dialog in _dialogs)
            if (dialog != null && dialog.activeInHierarchy) return true;
        return false;
    }
}
