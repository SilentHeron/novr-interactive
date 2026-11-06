using System.Reflection;
using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

internal static class CursorChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
            checks++;
        }

        {
            var s = new CursorScene();
            s.Frame();
            Check(s.Cursor.IsControllerPointer, "Idle initial VR pointer retains the default controller");
            s.MouseDown();
            s.Frame();
            Check(!s.Cursor.IsControllerPointer && s.Cursor.PrimaryPressed && s.VirtualMouse.State.buttons == 1,
                "First desktop mouse down takes over from idle tracked controllers and reaches the virtual UI mouse");
            Check(s.Scene.Slider.Downs == 0 && s.Lease.Module != null, "Mouse takeover keeps one input route and dispatches no controller press");
            for (var i = 0; i < 30; i++)
            {
                s.Scene.Aim(false, new(i * .02f, 0, 1));
                s.Frame();
                Check(!s.Cursor.IsControllerPointer && !s.Cursor.PrimaryPressed,
                    "Tracked controller motion cannot reclaim mouse ownership or repeat its press");
            }
            s.MouseUp();
            s.Frame();
            Check(s.VirtualMouse.State.buttons == 0 && !s.Cursor.IsControllerPointer, "Mouse release remains with its owner");
            s.Scene.Aim(false, new(0, 0, 1));
            s.Frame(1);
            Check(s.Cursor.IsControllerPointer && s.Scene.Slider.Downs == 1 && s.Cursor.PrimaryPressed,
                "Fresh controller trigger reclaims ownership and presses on the same frame");
            Check(s.VirtualMouse.State.buttons == 0 && s.VirtualMouse.State.position.x < 0,
                "Controller dispatch keeps the virtual mouse offscreen and released");
        }

        {
            var s = new CursorScene();
            s.Frame();
            s.MouseDown();
            s.Frame();
            s.Frame(1);
            Check(!s.Cursor.IsControllerPointer && s.Scene.Slider.Downs == 0, "Controller press cannot steal an active mouse drag");
            s.MouseUp();
            s.Frame(1);
            Check(!s.Cursor.IsControllerPointer, "Mouse owns its release frame despite held controller input");
            s.Frame(1);
            Check(!s.Cursor.IsControllerPointer && s.Scene.Slider.Downs == 0, "Ignored controller input is not queued after mouse release");
            s.Frame(0);
            s.Frame(1);
            Check(s.Cursor.IsControllerPointer && s.Scene.Slider.Downs == 1, "Controller can take over after release and a deliberate new press");
        }

        {
            var s = new CursorScene();
            s.Frame();
            s.Frame(1);
            s.MouseDown();
            s.Mouse.delta.Value = new(3, 0);
            s.Frame(1);
            Check(s.Cursor.IsControllerPointer && s.VirtualMouse.State.buttons == 0,
                "Mouse button and movement cannot steal an active controller drag");
            s.Mouse.delta.Value = new(3, 0);
            s.Frame(0);
            Check(s.Cursor.IsControllerPointer && s.Scene.Slider.Ends == 1, "Controller owns its release frame");
            s.Mouse.delta.Value = new(3, 0);
            s.Frame();
            Check(!s.Cursor.IsControllerPointer && !s.Cursor.PrimaryHeld && s.VirtualMouse.State.buttons == 0,
                "Moving the mouse after a rejected held press does not replay that press");
            s.MouseUp();
            s.Frame();
            s.MouseDown();
            s.Frame();
            Check(s.Cursor.PrimaryPressed && s.VirtualMouse.State.buttons == 1,
                "Rejected mouse capture rearms after release and new press");
        }

        {
            var s = new CursorScene();
            s.Frame();
            OpenXRUtility.IsSessionFocused = false;
            s.MouseDown();
            s.Frame();
            Check(s.Cursor.IsActive && !s.Cursor.IsControllerPointer && s.Cursor.PrimaryPressed,
                "XR unfocused plus desktop focused accepts the first real mouse click");
            Check(s.VirtualMouse.State.buttons == 1 && s.Lease.Module != null,
                "Focused desktop mouse still uses the projected cursor's sole input binding");
            Application.isFocused = false;
            s.Frame();
            Check(!s.Cursor.IsActive && s.Lease.Module == null && s.VirtualMouse.State.buttons == 0 &&
                s.VirtualMouse.State.position.x < 0, "Losing both focuses cancels mouse offscreen before restoring native bindings");
            Application.isFocused = true;
            s.Frame();
            Check(s.Cursor.IsActive && !s.Cursor.PrimaryHeld && s.VirtualMouse.State.buttons == 0,
                "A held button after desktop focus returns cannot replay a click");
            s.MouseUp();
            s.Frame();
            s.MouseDown();
            s.Frame();
            Check(s.Cursor.PrimaryPressed, "A focused desktop press works after focus loss and release");
        }

        {
            var s = new CursorScene();
            s.Frame();
            Application.isFocused = false;
            s.MouseDown();
            s.Mouse.delta.Value = new(40, 40);
            s.Mouse.scroll.Value = new(0, 1);
            s.Frame();
            Check(s.Cursor.IsControllerPointer && !s.Cursor.PrimaryPressed && s.VirtualMouse.State.buttons == 0,
                "Background desktop mouse cannot take over a focused XR pointer");
            s.Frame(1);
            Check(s.Scene.Slider.Downs == 1, "XR input works without desktop focus");
            OpenXRUtility.IsSessionFocused = false;
            s.Frame(1);
            Check(!s.Cursor.IsActive && s.Scene.Slider.Ends == 1 && s.Lease.Module == null,
                "Losing XR focus cancels the captured controller and releases its binding lease");
            OpenXRUtility.IsSessionFocused = true;
            s.Frame(1);
            Check(s.Cursor.IsControllerPointer && !s.Cursor.PrimaryHeld && s.Scene.Slider.Downs == 1,
                "Held trigger on XR focus return cannot replay a press");
            s.Frame(0);
            s.Frame(1);
            Check(s.Scene.Slider.Downs == 2, "Fresh controller press after focus recovery works");
        }

        foreach (var loss in new[] { "camera", "ui", "xr", "disable", "destroy" })
        {
            var s = new CursorScene();
            s.Frame();
            var virtualMouse = s.VirtualMouse;
            Check(s.Lease.Module != null, "Visible VR cursor acquires the binding lease");
            if (loss == "camera") APIBus.CockpitHudCamera = null;
            if (loss == "ui") Cursor.visible = false;
            if (loss == "xr") XRSettings.enabled = false;
            if (loss is "disable" or "destroy") s.Invoke(loss == "disable" ? "OnDisable" : "OnDestroy");
            else s.Frame();
            Check(s.Lease.Module == null && s.Lease.Releases == 1, $"{loss}: cursor restores the native mouse binding lease");
            if (loss is not "destroy")
                Check(!s.Cursor.IsActive && virtualMouse.State.buttons == 0, $"{loss}: hidden cursor sends no virtual mouse press");
            if (loss is "xr" or "disable" or "destroy")
                Check(!virtualMouse.added && InputSystem.devices.Count == 1 && Mouse.current == s.Mouse,
                    $"{loss}: returning to native input removes the synthetic mouse and restores the real current device");
            else
                Check(virtualMouse.added, $"{loss}: temporary UI hiding does not churn devices");
        }

        {
            var s = new CursorScene();
            s.Frame();
            var first = s.VirtualMouse;
            XRSettings.enabled = false;
            s.Frame();
            s.Frame();
            Check(!first.added && InputSystem.devices.Count == 1 && s.Lease.Releases == 1,
                "Repeated desktop frames dispose the virtual mouse idempotently without reacquiring native bindings");
            XRSettings.enabled = true;
            s.Frame();
            Check(s.Cursor.IsControllerPointer && s.VirtualMouse != first && InputSystem.devices.Count == 2 && s.Lease.Acquires == 2,
                "VR re-entry creates exactly one new virtual mouse and reacquires its lease");
            s.Invoke("OnDisable");
            s.Invoke("OnDisable");
            s.Invoke("OnDestroy");
            Check(InputSystem.devices.Count == 1 && Mouse.current == s.Mouse,
                "Repeated disable/destroy never removes the real mouse");
        }

        {
            var s = new CursorScene();
            s.Frame();
            s.MouseDown();
            s.Frame();
            EventSystem.current = new GameObject("replacement system").AddComponent<EventSystem>();
            var newModule = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
            s.Mouse.delta.Value = new(2, 0);
            s.Frame();
            Check(s.Lease.Releases == 1 && s.Lease.Acquires == 2 && s.Lease.Module == newModule,
                "Replacing the EventSystem restores its old lease and acquires the new module");
            Check(!s.Cursor.PrimaryHeld && s.VirtualMouse.State.buttons == 0,
                "An old mouse press cannot replay into a replacement EventSystem module");
            s.MouseUp();
            s.Frame();
            s.MouseDown();
            s.Frame();
            Check(s.Cursor.PrimaryPressed && s.VirtualMouse.State.buttons == 1,
                "A fresh mouse press works after the input module is replaced");
        }

        {
            var s = new CursorScene();
            s.MouseDown();
            s.Frame();
            Check(!s.Cursor.IsControllerPointer && s.Cursor.PrimaryPressed,
                "Fresh mouse click on the very first cursor frame is accepted");
        }
        {
            var s = new CursorScene();
            OpenXRUtility.IsSessionFocused = false;
            s.Mouse.leftButton.isPressed = true;
            s.Frame();
            Check(!s.Cursor.PrimaryHeld && s.VirtualMouse.State.buttons == 0,
                "A mouse button already held before the first cursor frame is not a fresh click");
        }
        return checks;
    }
}

internal sealed class CursorScene
{
    internal readonly Scene Scene = new();
    internal readonly VrUiCursor Cursor;
    internal readonly Mouse Mouse = new();
    internal VirtualMouseBindingOwnership Lease => VirtualMouseBindingOwnership.Instances.Last();
    internal Mouse VirtualMouse => InputSystem.devices.OfType<Mouse>().Single(m => m != Mouse);

    internal CursorScene()
    {
        Application.isFocused = OpenXRUtility.IsSessionFocused = XRSettings.enabled = true;
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        WristMenu.IsOpen = false;
        APIBus.CockpitHudCamera = Scene.Camera;
        InputSystem.devices.Clear();
        InputSystem.devices.Add(Mouse);
        Mouse.MakeCurrent();
        Mouse.position.Value = new(Screen.width * .5f, Screen.height * .5f);
        EventSystem.current!.gameObject.AddComponent<InputSystemUIInputModule>();
        Cursor = new GameObject("VrUiCursor").AddComponent<VrUiCursor>();
        Invoke("Awake");
        Invoke("Start");
    }

    internal void MouseDown()
    {
        Mouse.leftButton.isPressed = Mouse.leftButton.wasPressedThisFrame = true;
    }

    internal void MouseUp() => Mouse.leftButton.isPressed = false;

    internal void Frame(float trigger = 0)
    {
        Scene.Right.Trigger = trigger;
        TrackedControllers.Left = Scene.Left;
        TrackedControllers.Right = Scene.Right;
        Time.unscaledTime += Time.unscaledDeltaTime;
        Invoke("Update");
        Mouse.leftButton.wasPressedThisFrame = Mouse.rightButton.wasPressedThisFrame = Mouse.middleButton.wasPressedThisFrame = false;
        Mouse.delta.Value = Mouse.scroll.Value = default;
    }

    internal void Invoke(string name)
    {
        try { typeof(VrUiCursor).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Cursor, null); }
        catch (TargetInvocationException e) when (e.InnerException != null) { throw e.InnerException; }
    }
}
