using BepInEx.Configuration;
using UnityEngine;

namespace NOVR;

public class NOVRBehaviour : MonoBehaviour
{
    public static T Create<T>(Transform parent) where T : NOVRBehaviour
    {
        return new GameObject(typeof(T).Name)
        {
            transform =
            {
                parent = parent,
                localPosition = Vector3.zero,
                localRotation = Quaternion.identity
            }
        }.AddComponent<T>();
    }

    protected virtual void Awake()
    {
    }

    protected virtual void OnEnable()
    {
        Application.onBeforeRender += OnBeforeRender;
        ModConfiguration.Instance.Config.SettingChanged += ConfigOnSettingChanged;
    }

    protected virtual void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
        ModConfiguration.Instance.Config.SettingChanged -= ConfigOnSettingChanged;
    }

    private void ConfigOnSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        OnSettingChanged();
    }

    protected virtual void OnBeforeRender()
    {
    }

    protected virtual void OnSettingChanged()
    {
    }
}
