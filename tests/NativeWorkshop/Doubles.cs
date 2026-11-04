using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;
using NuclearOption.Workshop;

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object? a, Object? b) =>
            (ReferenceEquals(a, null) || a.Destroyed) && (ReferenceEquals(b, null) || b.Destroyed) || ReferenceEquals(a, b);
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
    public class MonoBehaviour : Object
    {
    }
    public sealed class RectTransform : Object
    {
    }
    public readonly record struct Color(float R, float G, float B, float A);
    public static class Debug
    {
        public static readonly List<string> Errors = new();
        public static void LogError(string message) => Errors.Add(message);
    }
}
namespace UnityEngine.UI
{
    public sealed class Text : UnityEngine.Object
    {
        public string text = "";
    }
    public sealed class Image : UnityEngine.Object
    {
        public string? sprite;
        public UnityEngine.Color color;
    }
}
namespace Cysharp.Threading.Tasks
{
    // Await timing comes from real Tasks controlled by the Steam boundary below.
    // The production methods themselves (including catch/finally) compile unchanged.
    [AsyncMethodBuilder(typeof(TestUniTaskVoidBuilder))]
    public readonly struct UniTaskVoid(Task task)
    {
        internal static readonly List<Task> Started = new();
        public void Forget() => Started.Add(task);
    }
    public struct TestUniTaskVoidBuilder
    {
        private AsyncTaskMethodBuilder _builder;
        public static TestUniTaskVoidBuilder Create() => new() { _builder = AsyncTaskMethodBuilder.Create() };
        public UniTaskVoid Task => new(_builder.Task);
        public void SetResult() => _builder.SetResult();
        public void SetException(Exception exception) => _builder.SetException(exception);
        public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine(stateMachine);
        public void Start<T>(ref T stateMachine) where T : IAsyncStateMachine => _builder.Start(ref stateMachine);
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine => _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine => _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
    }
}
namespace NuclearOption.ModScripts.Impl
{
    public static class MissionMod
    {
        public const string MOD_NAME = "Mission";
    }
    public static class AircraftLiveryMod
    {
        public const string MOD_NAME = "AircraftLivery";
    }
}
namespace NuclearOption.Workshop
{
    public enum OrderBy
    {
        Trend30Days, TopAllTime, New
    }
    public sealed partial class SteamWorkshop
    {
        public readonly List<Query> Queries = new();
        public Task<bool> RefreshItems(OrderBy order, string tag, List<SteamWorkshopItem> result, uint page)
        {
            var query = new Query(order, tag, result, page);
            Queries.Add(query);
            return query.Completion.Task;
        }
        public sealed class Query(OrderBy order, string tag, List<SteamWorkshopItem> result, uint page)
        {
            public readonly OrderBy Order = order;
            public readonly string Tag = tag;
            public readonly uint Page = page;
            public readonly TaskCompletionSource<bool> Completion = new();
            public void Finish(bool hasMore, params SteamWorkshopItem[] items)
            {
                result.Clear();
                result.AddRange(items);
                Completion.SetResult(hasMore);
            }
            public void Fail() => Completion.SetException(new InvalidOperationException("simulated Steam failure"));
        }
    }
    public sealed class SteamWorkshopItem(string name)
    {
        public readonly string Name = name;
        public string OwnerName = "Pilot", Description = "Description";
        public bool Subscribed;
        public event Action? OwnerNameChanged;
        public int Listeners => OwnerNameChanged?.GetInvocationList().Length ?? 0;
        public CancellationTokenSource? PreviewSource;
        public CancellationToken PreviewToken;
        private UnityEngine.UI.Image? _image;
        public void RenameOwner(string name)
        {
            OwnerName = name;
            OwnerNameChanged?.Invoke();
        }
        public UniTaskVoid SetPreviewImageAsync(UnityEngine.UI.Image image, ref CancellationTokenSource? source)
        {
            source?.Cancel();
            source = new();
            PreviewSource = source;
            PreviewToken = source.Token;
            _image = image;
            return new(Task.CompletedTask);
        }
        public void CompletePreview()
        {
            if (!PreviewToken.IsCancellationRequested && _image != null)
                _image.sprite = Name;
        }
    }
}
namespace NOVR.VrUi.Native
{
    internal static class NativePanelTransition
    {
        internal static void SetVisible(UnityEngine.RectTransform target, bool visible)
        {
        }
    }
    // Only rendering/search and Unity/Steam boundaries are doubled. Request,
    // selection, cancellation and panel-lifetime methods are production source.
    public partial class NativeWorkshopPanel : UnityEngine.MonoBehaviour
    {
        private readonly List<SteamWorkshopItem> _items = new(), _visibleItems = new();
        private readonly UnityEngine.RectTransform _container = new();
        private readonly SteamWorkshop _steamWorkshop = new();
        private readonly UnityEngine.UI.Text _titleText = new(), _ownerText = new(), _descriptionText = new(), _detailsStatusText = new();
        private readonly UnityEngine.UI.Image _previewImage = new();
        private WorkshopTab _tab = WorkshopTab.Missions;
        private OrderBy _orderBy = OrderBy.Trend30Days;
        private int _displayPage, _steamPage = 1, _selectedIndex = -1;
        private bool _hasMoreSteamPages, _queryInProgress, _wasVisible;
        private SteamWorkshopItem? _selectedItem;
        private enum WorkshopTab
        {
            Missions, AircraftLiveries
        }
        internal string Status = "";
        internal int RowRefreshes, ButtonRefreshes;
        internal bool FailPresentation;
        internal bool FailNextRows, FailNextButtons;
        internal SteamWorkshop Steam => _steamWorkshop;
        internal IReadOnlyList<SteamWorkshopItem> Items => _items;
        internal bool Busy => _queryInProgress;
        internal bool HasMore => _hasMoreSteamPages;
        internal int DisplayPage => _displayPage;
        internal string? Preview => _previewImage.sprite;
        internal string Owner => _ownerText.text;
        internal bool HasSelection => _selectedItem != null;
        internal void Open() => SetVisible(true);
        internal void Close() => SetVisible(false);
        internal void Disable() => OnDisable();
        internal void DestroyPanel()
        {
            Destroyed = true;
            OnDestroy();
        }
        internal void Liveries() => SelectTab(WorkshopTab.AircraftLiveries);
        internal void Missions() => SelectTab(WorkshopTab.Missions);
        internal void ChangeOrder(int direction) => CycleOrder(direction);
        internal void Reload() => RefreshFirstSteamPage();
        internal void More() => LoadNextSteamPage();
        internal void Choose(int index) => SelectItem(index);
        private void ApplySearchFromCurrentPage()
        {
            if (FailPresentation)
                throw new InvalidOperationException("simulated presentation failure");
            _visibleItems.Clear();
            _visibleItems.AddRange(_items);
            SelectItem(_visibleItems.Count == 0 ? -1 : 0);
        }
        private void RefreshButtons()
        {
            BeforeButtons?.Invoke();
            if (FailNextButtons)
            {
                FailNextButtons = false;
                throw new InvalidOperationException("simulated button redraw failure");
            }
            ButtonRefreshes++;
        }
        private void RefreshRows()
        {
            BeforeRows?.Invoke();
            if (FailNextRows)
            {
                FailNextRows = false;
                throw new InvalidOperationException("simulated row redraw failure");
            }
            RowRefreshes++;
        }
        private void SetStatus(string text)
        {
            BeforeStatus?.Invoke();
            Status = text;
        }
        private static int WrapIndex(int index, int count) => (index % count + count) % count;
    }
}
