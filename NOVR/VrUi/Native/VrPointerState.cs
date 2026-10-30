using UnityEngine;

namespace NOVR.VrUi.Native;

public class VrPointerState
{
    public bool IsAvailable { get; private set; }
    public Vector2 ScreenPosition { get; private set; }
    public Vector3 WorldPosition { get; private set; }
    public bool PrimaryButtonPressed { get; private set; }
    public bool PrimaryButtonHeld { get; private set; }
    public Vector2 Scroll { get; private set; }

    public void Update(VrUiCursor? cursor)
    {
        IsAvailable = cursor != null && cursor.IsActive;
        if (!IsAvailable)
        {
            PrimaryButtonPressed = false;
            PrimaryButtonHeld = false;
            Scroll = Vector2.zero;
            return;
        }

        ScreenPosition = cursor!.GetScreenPoint();
        WorldPosition = cursor.CursorPosition;
        PrimaryButtonPressed = cursor.PrimaryPressed;
        PrimaryButtonHeld = cursor.PrimaryHeld;
        Scroll = cursor.Scroll;
    }
}
