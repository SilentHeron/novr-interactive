// Only the external Rewired data API is doubled. Catalog construction, axis-slot
// interpretation, naming and descriptors are linked directly from production.
namespace Rewired
{
    internal enum AxisRange
    {
        Full, Positive, Negative
    }
    internal enum Pole
    {
        Positive, Negative
    }
    internal enum InputActionType
    {
        Axis, Button
    }
    internal enum ControllerElementType
    {
        Axis, Button
    }

    internal class Controller
    {
        public int id;
        public string name = string.Empty;
    }

    internal sealed class Joystick : Controller
    {
    }

    internal sealed class Player
    {
        public readonly PlayerControllers controllers = new();
    }

    internal sealed class PlayerControllers
    {
        public readonly MapCollection maps = new();
        public readonly List<Joystick> Joysticks = new();
    }

    internal sealed class MapCollection
    {
        private readonly Dictionary<(Type, int), object> _maps = new();
        public void Set<T>(int id, params T[] maps) where T : ControllerMap => _maps[(typeof(T), id)] = maps;
        public IEnumerable<T> GetMaps<T>(int id) where T : ControllerMap =>
            _maps.TryGetValue((typeof(T), id), out var maps) ? (IEnumerable<T>)maps : Array.Empty<T>();
    }

    internal class ControllerMap
    {
        public int id;
        public int categoryId;
        public string name = string.Empty;
        public readonly List<ActionElementMap> AllMaps = new();
    }

    internal sealed class KeyboardMap : ControllerMap
    {
    }
    internal sealed class MouseMap : ControllerMap
    {
    }
    internal sealed class JoystickMap : ControllerMap
    {
    }

    internal sealed class InputAction
    {
        public int id;
        public int categoryId;
        public bool userAssignable = true;
        public InputActionType type;
        public string descriptiveName = string.Empty;
        public string name = string.Empty;
        public string negativeDescriptiveName = string.Empty;
        public string positiveDescriptiveName = string.Empty;
    }

    internal sealed class ActionElementMap
    {
        public int id;
        public int actionId;
        public bool enabled = true;
        public bool invert;
        public ControllerElementType elementType;
        public AxisRange axisRange;
        public Pole axisContribution;
        public int elementIdentifierId;
        public string elementIdentifierName = string.Empty;
        public string actionDescriptiveName = string.Empty;
        public UnityEngine.KeyCode keyCode;
    }

    internal sealed class Category
    {
        public bool userAssignable = true;
        public string descriptiveName = string.Empty;
        public string name = string.Empty;
    }

    internal static class ReInput
    {
        public static Mapping mapping = new();
    }

    internal sealed class Mapping
    {
        public readonly List<InputAction> UserAssignableActions = new();
        public readonly Dictionary<int, Category> MapCategories = new();
        public readonly Dictionary<int, Category> ActionCategories = new();
        public InputAction? GetAction(int id) => UserAssignableActions.Find(action => action?.id == id);
        public Category? GetMapCategory(int id) => MapCategories.GetValueOrDefault(id);
        public Category? GetActionCategory(int id) => ActionCategories.GetValueOrDefault(id);
    }
}

namespace UnityEngine
{
    internal enum KeyCode
    {
        None, Space
    }
    internal static class Mathf
    {
        public static int Max(int left, int right) => Math.Max(left, right);
    }
}
