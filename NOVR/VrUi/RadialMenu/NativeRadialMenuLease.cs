using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// Borrow the native wheel, entries and callbacks. Never clone its action
// ScriptableObjects or replace the aircraft-specific allowed-action list.
internal sealed class NativeRadialMenuLease : IDisposable
{
    private static readonly FieldInfo Menu = Field("menuObject"), Main = Field("actionsMainContainer"), Weapons = Field("actionsWeaponsContainer"),
        MainActions = Field("allowedActionsMain"), WeaponActions = Field("allowedActionsWeapons"), State = Field("currentState"),
        Selected = Field("selectedAction"), Caption = Field("selectedActionText"), Group = Field("canvasGroup");
    private static FieldInfo Field(string name) => AccessTools.Field(typeof(RadialMenuMain), name) ??
        throw new MissingFieldException(typeof(RadialMenuMain).FullName, name);
    internal readonly RadialMenuMain Native;
    private readonly RectTransform _rect;
    private readonly Transform _parent;
    private readonly int _index;
    private readonly Vector2 _anchorMin, _anchorMax, _pivot, _size;
    private readonly Vector3 _position, _scale;
    private readonly Quaternion _rotation;
    private readonly CanvasGroup _group;
    private readonly float _alpha;
    private readonly GameObject _main, _weapons;
    private readonly bool _mainActive, _weaponsActive;
    private readonly TMP_Text _caption;
    private readonly List<RadialMenuAction> _mainActions, _weaponActions;
    private readonly Dictionary<GameObject, int> _layers = new();
    private readonly Dictionary<Graphic, bool> _raycasts = new();
    private RadialMenuAction? _hovered;
    internal bool WeaponsPage
    {
        get; private set;
    }
    internal int Count => Actions.Count;
    private List<RadialMenuAction> Actions => WeaponsPage ? _weaponActions : _mainActions;
    internal bool Valid => Native != null && Native.isActiveAndEnabled && _rect != null && _parent != null;

    internal NativeRadialMenuLease(RadialMenuMain native, RectTransform host)
    {
        if (native == null || host == null)
            throw new ArgumentNullException(native == null ? nameof(native) : nameof(host));
        Native = native;
        // Resolve every dependency before native setup or hierarchy mutation.
        // The owner cannot dispose an object whose constructor never returned.
        _rect = Require(Require(Menu.GetValue(native) as GameObject, "menuObject").transform as RectTransform, "menu rect");
        _group = Require(Group.GetValue(native) as CanvasGroup, "canvasGroup");
        _alpha = _group.alpha;
        _main = Require(Main.GetValue(native) as GameObject, "actionsMainContainer");
        _weapons = Require(Weapons.GetValue(native) as GameObject, "actionsWeaponsContainer");
        _caption = Require(Caption.GetValue(native) as TMP_Text, "selectedActionText");
        _mainActions = MainActions.GetValue(native) as List<RadialMenuAction> ?? throw new InvalidOperationException("Missing native main actions");
        _weaponActions = WeaponActions.GetValue(native) as List<RadialMenuAction> ?? throw new InvalidOperationException("Missing native weapon actions");
        if (!_main.transform.IsChildOf(_rect) || !_weapons.transform.IsChildOf(_rect) || !_caption.transform.IsChildOf(_rect))
            throw new InvalidOperationException("Native option wheel content is outside its menu object");
        if (_group.transform != _rect && !_rect.IsChildOf(_group.transform) && !_group.transform.IsChildOf(_rect))
            throw new InvalidOperationException("Native option wheel fade group is unrelated to its menu object");
        Require(_rect.parent, "native parent");
        _parent = _rect.parent;
        _index = _rect.GetSiblingIndex();
        _anchorMin = _rect.anchorMin;
        _anchorMax = _rect.anchorMax;
        _pivot = _rect.pivot;
        _size = _rect.sizeDelta;
        _position = _rect.localPosition;
        _rotation = _rect.localRotation;
        _scale = _rect.localScale;
        _mainActive = _main.activeSelf;
        _weaponsActive = _weapons.activeSelf;
        try
        {
            native.OpenMenu(); // Native aircraft filtering, station creation and ammo readouts.
            foreach (var node in _rect.GetComponentsInChildren<Transform>(true))
            {
                _layers[node.gameObject] = node.gameObject.layer;
                node.gameObject.layer = host.gameObject.layer;
            }
            foreach (var graphic in _rect.GetComponentsInChildren<Graphic>(true))
            {
                _raycasts[graphic] = graphic.raycastTarget;
                graphic.raycastTarget = false;
            }
            _rect.SetParent(host, false);
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(.5f, .5f);
            _rect.localPosition = Vector3.zero;
            _rect.localRotation = Quaternion.identity;
            _rect.localScale = Vector3.one;
            // The installed prefab's group is on the old parent. Its fade must
            // not hide the detached hand canvas; preserve its value for release.
            _group.alpha = 1;
            _rect.gameObject.SetActive(true);
            ShowPage(false);
        }
        catch { Dispose(); throw; }
    }
    private static T Require<T>(T? value, string name) where T : UnityEngine.Object =>
        value != null ? value : throw new InvalidOperationException("Missing native option wheel " + name);

    internal void ChangePage()
    {
        if (_weaponActions.Count > 0)
            ShowPage(!WeaponsPage);
    }
    private void ShowPage(bool weapons)
    {
        Hover(-1);
        WeaponsPage = weapons;
        _main.SetActive(!weapons);
        _weapons.SetActive(weapons);
        State.SetValue(Native, weapons ? RadialMenuMain.RadialMenuType.Weapons : RadialMenuMain.RadialMenuType.Main);
        if (weapons)
            Native.RefreshWeapons();
    }
    internal bool Hover(int index)
    {
        var next = index >= 0 && index < Actions.Count ? Actions[index] : null;
        if (next == _hovered)
            return false;
        if (_hovered != null)
            _hovered.UnHover();
        _hovered = next;
        Selected.SetValue(Native, next);
        _caption.text = next != null ? next.DisplayName : "";
        if (next != null)
            next.Hover();
        return next != null;
    }
    internal RadialMenuAction? ActionAt(int index) => index >= 0 && index < Actions.Count ? Actions[index] : null;
    public void Dispose()
    {
        try
        {
            if (Native != null)
            {
                try
                {
                    Hover(-1);
                }
                finally { Native.CloseMenu(); }
            }
        }
        finally { Restore(); }
    }
    private void Restore()
    {
        if (_rect != null)
        {
            // Cancellation is immediate, never a native fade/release action.
            _rect.gameObject.SetActive(false);
            if (_parent != null)
            {
                _rect.SetParent(_parent, false);
                _rect.SetSiblingIndex(_index);
                _rect.anchorMin = _anchorMin;
                _rect.anchorMax = _anchorMax;
                _rect.pivot = _pivot;
                _rect.sizeDelta = _size;
                _rect.localPosition = _position;
                _rect.localRotation = _rotation;
                _rect.localScale = _scale;
            }
            else
                UnityEngine.Object.Destroy(_rect.gameObject);
        }
        foreach (var pair in _layers)
            if (pair.Key != null)
                pair.Key.layer = pair.Value;
        foreach (var pair in _raycasts)
            if (pair.Key != null)
                pair.Key.raycastTarget = pair.Value;
        if (_group != null)
            _group.alpha = _alpha;
        if (_main != null)
            _main.SetActive(_mainActive);
        if (_weapons != null)
            _weapons.SetActive(_weaponsActive);
    }
}
