using NOVR.VrUi;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

internal static class Program
{
    private static int _checks;
    private const string Mouse = "/VirtualMouse";

    private static void Main()
    {
        ActiveRulesAndRestoration();
        ReplacementAndExternalOwnership();
        BindingIdentityChanges();
        AcquisitionFailures();
        ReentrantLifetime();
        Console.WriteLine($"UI binding ownership checks passed ({_checks} assertions).");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        _checks++;
    }

    private static InputBinding Binding(string? path, bool withId = true) => new()
    {
        id = withId ? Guid.NewGuid() : Guid.Empty,
        path = path,
        name = "Native binding",
        action = "UI",
        groups = "Default",
        interactions = "native-interaction",
        processors = "native-processor",
        overridePath = "original-path",
        overrideInteractions = "original-interaction",
        overrideProcessors = "original-processor"
    };

    private static InputAction Action(params InputBinding[] bindings)
    {
        var action = new InputAction();
        action.Items.AddRange(bindings);
        return action;
    }

    private static InputSystemUIInputModule Module(InputAction point) => new() { point = new InputActionReference(point) };
    private static bool Same(InputBinding left, InputBinding right) => left.Equals(right);
    private static void Restored(InputAction action, params InputBinding[] original) =>
        Check(action.Items.Count == original.Length && action.Items.Zip(original).All(pair => Same(pair.First, pair.Second)),
            "Every original binding field and override is restored exactly");

    private static void ActiveRulesAndRestoration()
    {
        var roles = new[] { "position", "leftButton", "middleButton", "rightButton", "scroll" };
        var sources = roles.Select(role => new[] { Binding("<Mouse>/" + role), Binding("<Touchscreen>/position"), Binding(null) }).ToArray();
        var actions = sources.Select(Action).ToArray();
        var module = new InputSystemUIInputModule
        {
            point = new(actions[0]),
            leftClick = new(actions[1]),
            middleClick = new(actions[2]),
            rightClick = new(actions[3]),
            scrollWheel = new(actions[4])
        };
        var navSources = new[] { Binding("2DVector"), Binding("<Keyboard>/w"), Binding("<Gamepad>/leftStick"), Binding("<Joystick>/stick"), Binding("<XRController>/thumbstick"), Binding(null) };
        navSources[0].isComposite = true;
        navSources[1].isPartOfComposite = true;
        navSources[4].overridePath = "<Keyboard>/s"; // Restrictions intentionally use the native base path.
        var move = Action(navSources);
        module.move = new(move);
        var owner = new VirtualMouseBindingOwnership();
        Check(owner.Acquire(module, Mouse) && owner.Matches(module), "Complete module acquisition succeeds");
        for (var i = 0; i < actions.Length; i++)
        {
            Check(actions[i].Items[0].overridePath == Mouse + "/" + roles[i] && actions[i].Items[0].overrideInteractions == null && actions[i].Items[0].overrideProcessors == null,
                "Active pointer restrictions retain native string-overload semantics");
            Check(Same(actions[i].Items[1], sources[i][1]) && Same(actions[i].Items[2], sources[i][2]), "Non-mouse and pathless pointer bindings are untouched");
        }
        for (var i = 0; i < move.Items.Count; i++)
            Check(i < 3 ? Same(move.Items[i], navSources[i]) : move.Items[i].overridePath == "" && move.Items[i].overrideInteractions == null && move.Items[i].overrideProcessors == null,
                "Navigation keeps composites/keyboard/gamepad and disables other base paths");
        var writes = actions.Sum(action => action.Writes) + move.Writes;
        for (var i = 0; i < 100; i++)
            owner.Matches(module);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++)
            owner.Matches(module);
        Check(GC.GetAllocatedBytesForCurrentThread() == before && writes == actions.Sum(action => action.Writes) + move.Writes,
            "Unchanged-module ownership checks allocate nothing and do not reapply overrides");
        owner.Release();
        for (var i = 0; i < actions.Length; i++)
            Restored(actions[i], sources[i]);
        Restored(move, navSources);
        Check(!owner.Matches(module), "Release drops module ownership");
        writes = actions.Sum(action => action.Writes) + move.Writes;
        owner.Release();
        Check(writes == actions.Sum(action => action.Writes) + move.Writes, "Disable followed by destroy is idempotent");
        Check(owner.Acquire(module, Mouse), "Re-enable can acquire the restored native bindings again");
        owner.Release();
        for (var i = 0; i < actions.Length; i++)
            Restored(actions[i], sources[i]);

        var empty = new InputSystemUIInputModule();
        Check(owner.Acquire(empty, Mouse) && owner.Matches(empty), "Missing optional action references are allowed");
        empty.point = new(actions[0]);
        Check(!owner.Matches(empty), "Late-created module action triggers reacquisition");
        owner.Release();
    }

    private static void ReplacementAndExternalOwnership()
    {
        var firstBinding = Binding("<Mouse>/position");
        var secondBinding = Binding("<Mouse>/leftButton");
        var first = Action(firstBinding);
        var second = Action(secondBinding);
        var firstModule = Module(first);
        var secondModule = Module(second);
        var owner = new VirtualMouseBindingOwnership();
        owner.Acquire(firstModule, Mouse);
        Check(!owner.Matches(secondModule) && !owner.Matches(null), "Different/missing EventSystem modules do not retain ownership");
        owner.Acquire(secondModule, Mouse);
        Restored(first, firstBinding);
        Check(second.Items[0].overridePath == Mouse + "/leftButton", "New module receives its own restrictions");
        secondModule.point = new(first);
        Check(!owner.Matches(secondModule), "Action replacement inside an unchanged module is detected");
        owner.Acquire(secondModule, Mouse);
        Restored(second, secondBinding);
        secondModule.point = new(first);
        Check(owner.Matches(secondModule), "A new reference wrapper for the same action does not cause rebinding");
        owner.Release();
        Restored(first, firstBinding);

        foreach (var field in new[] { "path", "interaction", "processor" })
        {
            var action = Action(firstBinding);
            var module = Module(action);
            owner.Acquire(module, Mouse);
            var outside = action.Items[0];
            if (field == "path")
                outside.overridePath = "<Mouse>/delta";
            if (field == "interaction")
                outside.overrideInteractions = "tap";
            if (field == "processor")
                outside.overrideProcessors = "scale(factor=2)";
            action.ApplyBindingOverride(0, outside);
            owner.Release();
            Check(Same(action.Items[0], outside), "External " + field + " override wins; restoration never partially overwrites another owner's tuple");
        }
        var noOverrides = Binding("<Mouse>/position");
        noOverrides.overridePath = noOverrides.overrideInteractions = noOverrides.overrideProcessors = null;
        var cleared = Binding("<Mouse>/scroll");
        cleared.overridePath = cleared.overrideInteractions = cleared.overrideProcessors = "";
        var native = Action(noOverrides, cleared);
        owner.Acquire(Module(native), Mouse);
        owner.Release();
        Restored(native, noOverrides, cleared);
    }

    private static void BindingIdentityChanges()
    {
        var first = Binding("<Mouse>/position");
        var second = Binding("<Mouse>/scroll");
        var action = Action(first, second);
        var owner = new VirtualMouseBindingOwnership();
        owner.Acquire(Module(action), Mouse);
        var unrelated = Binding("<Mouse>/middleButton");
        action.Items.Insert(0, unrelated);
        action.Items.Reverse();
        owner.Release();
        Restored(action, second, first, unrelated);

        action = Action(first);
        owner.Acquire(Module(action), Mouse);
        var replaced = action.Items[0];
        replaced.id = Guid.NewGuid();
        action.Items[0] = replaced;
        owner.Release();
        Restored(action, replaced);
        action = Action(first);
        owner.Acquire(Module(action), Mouse);
        var changed = action.Items[0];
        changed.path = "<Pen>/position";
        action.Items[0] = changed;
        owner.Release();
        Restored(action, changed);

        action = Action(first);
        owner.Acquire(Module(action), Mouse);
        var ambiguous = action.Items[0];
        action.Items.Add(ambiguous);
        owner.Release();
        Restored(action, ambiguous, ambiguous);

        var anonymous = Binding("<Mouse>/position", withId: false);
        action = Action(anonymous);
        owner.Acquire(Module(action), Mouse);
        owner.Release();
        Restored(action, anonymous);
        action = Action(anonymous, second);
        owner.Acquire(Module(action), Mouse);
        var anonymousOwned = action.Items[0];
        action.Items.Reverse();
        owner.Release();
        Restored(action, second, anonymousOwned);
        action = Action(anonymous);
        owner.Acquire(Module(action), Mouse);
        anonymousOwned = action.Items[0];
        action.Items.Add(unrelated);
        owner.Release();
        Restored(action, anonymousOwned, unrelated);
    }

    private static void AcquisitionFailures()
    {
        foreach (var afterWrite in new[] { false, true })
        {
            var originals = new[] { Binding("<Mouse>/position"), Binding("<Mouse>/scroll") };
            var action = Action(originals);
            Action<InputAction, int, InputBinding> fail = (source, index, _) =>
            {
                if (index != 1)
                    return;
                source.BeforeWrite = source.AfterWrite = null;
                throw new InvalidOperationException("injected native mutation failure");
            };
            if (afterWrite)
                action.AfterWrite = fail;
            else
                action.BeforeWrite = fail;
            var owner = new VirtualMouseBindingOwnership();
            var module = Module(action);
            var failed = false;
            try
            {
                owner.Acquire(module, Mouse);
            }
            catch (InvalidOperationException) { failed = true; }
            Check(failed && !owner.Matches(module), "Partial native failure never claims a module");
            Restored(action, originals);
            Check(owner.Acquire(module, Mouse), "Acquisition can retry after rollback");
            owner.Release();
            Restored(action, originals);
        }
        foreach (var fail in new[] { "none", "before", "after" })
        {
            var original = Binding("<Mouse>/position");
            var shared = Action(original);
            var module = Module(shared);
            module.leftClick = new(shared);
            module.move = new(shared);
            Action<InputAction, int, InputBinding> fault = (source, _, value) =>
            {
                if (value.overridePath != "")
                    return;
                source.BeforeWrite = source.AfterWrite = null;
                throw new InvalidOperationException("shared action failure");
            };
            if (fail == "before")
                shared.BeforeWrite = fault;
            if (fail == "after")
                shared.AfterWrite = fault;
            var owner = new VirtualMouseBindingOwnership();
            var failed = false;
            try
            {
                owner.Acquire(module, Mouse);
            }
            catch (InvalidOperationException) { failed = true; }
            Check(failed == (fail != "none"), "Shared action failure is reported correctly");
            if (!failed)
                Check(shared.Items[0].overridePath == "", "Shared pointer/navigation action retains last restriction precedence");
            owner.Release();
            Restored(shared, original);
        }
        var aliveBinding = Binding("<Mouse>/position");
        var deadBinding = Binding("<Mouse>/leftButton");
        var alive = Action(aliveBinding);
        var dead = Action(deadBinding);
        var both = Module(alive);
        both.leftClick = new(dead);
        var lease = new VirtualMouseBindingOwnership();
        lease.Acquire(both, Mouse);
        dead.FailReads = true;
        UnityEngine.Debug.Warnings.Clear();
        lease.Release();
        Restored(alive, aliveBinding);
        Check(UnityEngine.Debug.Warnings.Count == 1 && !lease.Matches(both), "One disposed action does not prevent other restoration or retain the module");
    }

    private static void ReentrantLifetime()
    {
        var original = Binding("<Mouse>/position");
        var action = Action(original);
        var module = Module(action);
        var owner = new VirtualMouseBindingOwnership();
        action.AfterWrite = (_, _, _) => owner.Release();
        Check(!owner.Acquire(module, Mouse) && !owner.Matches(module), "Synchronous disable while acquiring cancels ownership");
        Restored(action, original);
        action.AfterWrite = null;
        Check(owner.Acquire(module, Mouse), "A canceled acquisition does not poison later enable");
        var nestedAcquired = true;
        action.AfterWrite = (_, _, _) => nestedAcquired = owner.Acquire(module, Mouse);
        owner.Release();
        Check(!nestedAcquired && !owner.Matches(module), "Synchronous acquire during restoration cannot leak a replacement lease");
        Restored(action, original);
        action.AfterWrite = null;
        owner.Acquire(module, Mouse);
        action.AfterWrite = (_, _, _) => owner.Release();
        owner.Release();
        Restored(action, original);
    }
}
