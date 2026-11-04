using NuclearOption.Workshop;

namespace NuclearOption.Workshop
{
    public sealed partial class SteamWorkshop
    {
        public readonly List<PendingAction> Actions = new();
        public static readonly List<PendingAction> Updates = new();
        public bool FailNextActionStart;
        public Task DownloadItem(SteamWorkshopItem item) => StartAction(item, false);
        public Task Unsubscribe(SteamWorkshopItem item) => StartAction(item, true);
        private Task StartAction(SteamWorkshopItem item, bool unsubscribe)
        {
            if (FailNextActionStart)
            {
                FailNextActionStart = false;
                throw new InvalidOperationException("simulated Steam action startup failure");
            }
            var pending = new PendingAction(item, unsubscribe);
            Actions.Add(pending);
            return pending.Completion.Task;
        }
        public static Task UpdateAllSubscribedItems()
        {
            var pending = new PendingAction(null, false);
            Updates.Add(pending);
            return pending.Completion.Task;
        }
        public sealed class PendingAction(SteamWorkshopItem? item, bool unsubscribe)
        {
            public readonly SteamWorkshopItem? Item = item;
            public readonly bool Unsubscribe = unsubscribe;
            public readonly TaskCompletionSource Completion = new();
            public void Complete()
            {
                if (Item != null)
                    Item.Subscribed = !Unsubscribe;
                Completion.SetResult();
            }
            public void Fail() => Completion.SetException(new InvalidOperationException("simulated Steam action failure"));
        }
    }
}

namespace NOVR.VrUi.Native
{
    public partial class NativeWorkshopPanel
    {
        private bool _itemActionInProgress;
        internal Action? BeforeButtons, BeforeRows, BeforeStatus, BeforeDetails;
        internal bool ActionBusy => _itemActionInProgress;
        internal string Details => _detailsStatusText.text;
        internal void Subscribe() => ToggleSubscribe();
        internal void UpdateSubscribed() => UpdateAllSubscribed();
        private void SetDetailsStatus(string status)
        {
            BeforeDetails?.Invoke();
            _detailsStatusText.text = status;
        }
    }
}
