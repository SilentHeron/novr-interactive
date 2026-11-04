// Boundary semantics verified against the installed Unity.InputSystem.dll:
// indexed ApplyBindingOverride assigns all three override fields, including nulls,
// before calling OnBindingModified. It does not change the original binding or ID.
namespace UnityEngine.InputSystem
{
    internal struct InputBinding
    {
        public Guid id;
        public string? name, path, action, groups, interactions, processors;
        public string? overridePath, overrideInteractions, overrideProcessors;
        public bool isComposite, isPartOfComposite;
    }

    internal sealed class InputAction
    {
        public string name = "UI action";
        internal readonly List<InputBinding> Items = new();
        internal bool FailReads;
        internal int Writes;
        internal Action<InputAction, int, InputBinding>? BeforeWrite, AfterWrite;
        public IReadOnlyList<InputBinding> bindings => FailReads ? throw new ObjectDisposedException(name) : Items;
    }

    internal static class InputActionRebindingExtensions
    {
        public static void ApplyBindingOverride(this InputAction action, int index, InputBinding value)
        {
            if (index < 0 || index >= action.Items.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            action.BeforeWrite?.Invoke(action, index, value);
            var binding = action.Items[index];
            binding.overridePath = value.overridePath;
            binding.overrideInteractions = value.overrideInteractions;
            binding.overrideProcessors = value.overrideProcessors;
            action.Items[index] = binding;
            action.Writes++;
            action.AfterWrite?.Invoke(action, index, value);
        }
    }

    internal sealed class InputActionReference
    {
        public InputAction? action;
        public InputActionReference(InputAction value) => action = value;
    }
}

namespace UnityEngine.InputSystem.UI
{
    using UnityEngine.InputSystem;
    internal sealed class InputSystemUIInputModule
    {
        public InputActionReference? point, leftClick, middleClick, rightClick, scrollWheel, move;
    }
}

namespace UnityEngine
{
    internal static class Debug
    {
        internal static readonly List<string> Warnings = new();
        public static void Log(object value)
        {
        }
        public static void LogWarning(object value) => Warnings.Add(value.ToString()!);
    }
}
