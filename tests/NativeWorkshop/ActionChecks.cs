using Cysharp.Threading.Tasks;
using NOVR.VrUi.Native;
using NuclearOption.Workshop;

internal static class ActionChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var panel = Ready(new SteamWorkshopItem("Mission"));
        var buttonBusyStates = new List<bool>();
        panel.BeforeButtons = () => buttonBusyStates.Add(panel.ActionBusy);
        var updates = SteamWorkshop.Updates.Count;
        panel.Subscribe();
        check(panel.ActionBusy && panel.Steam.Actions.Count == 1 && panel.Details == "Subscribing and downloading.",
            "Subscribe starts work and displays its current progress");
        panel.Subscribe();
        panel.UpdateSubscribed();
        check(panel.Steam.Actions.Count == 1 && SteamWorkshop.Updates.Count == updates,
            "Item and update-all actions share one busy gate");
        panel.Steam.Actions[0].Complete();
        check(!panel.ActionBusy && panel.Items[0].Subscribed && panel.Details == "Installed locally." &&
            buttonBusyStates[0] && !buttonBusyStates[^1], "Successful subscribe restores buttons and reports the selected item");
        panel.Subscribe();
        check(panel.Steam.Actions[1].Unsubscribe && panel.Details == "Unsubscribing.", "Subscribed item dispatches native unsubscribe");
        panel.Steam.Actions[1].Complete();
        check(!panel.ActionBusy && !panel.Items[0].Subscribed && panel.Details == "Unsubscribed.", "Unsubscribe completes normally");
        panel.Subscribe();
        panel.Steam.Actions[2].Fail();
        check(!panel.ActionBusy && panel.Details.Contains("failed"), "Current item failure is visible and releases busy state");
        panel.Subscribe();
        panel.Steam.Actions[3].Complete();
        check(!panel.ActionBusy && panel.Items[0].Subscribed, "Item action can retry after failure");

        foreach (var fail in new[] { false, true })
        {
            var first = new SteamWorkshopItem("First");
            var selected = Ready(first, new SteamWorkshopItem("Second"));
            selected.Subscribe();
            selected.Choose(1);
            var details = selected.Details;
            if (fail)
                selected.Steam.Actions[0].Fail();
            else
                selected.Steam.Actions[0].Complete();
            check(!selected.ActionBusy && selected.Details == details && !selected.Items[1].Subscribed,
                "Old item completion cannot overwrite another selection's status");
            check(first.Subscribed == !fail, "Changing selection does not cancel the accepted Steam operation");
        }

        foreach (var end in new Action<NativeWorkshopPanel>[] { p => p.Close(), p => p.Disable(), p => p.DestroyPanel() })
        {
            var item = new SteamWorkshopItem("Closing");
            var closing = Ready(item);
            closing.Subscribe();
            end(closing);
            var rows = closing.RowRefreshes;
            var buttons = closing.ButtonRefreshes;
            var details = closing.Details;
            closing.Steam.Actions[0].Complete();
            check(item.Subscribed && !closing.ActionBusy, "Hide/disable/destroy does not cancel accepted work or retain busy state");
            check(closing.RowRefreshes == rows && closing.ButtonRefreshes == buttons && closing.Details == details,
                "Late action completion cannot redraw a hidden or destroyed panel");
        }

        var original = new SteamWorkshopItem("Original");
        var reopened = Ready(original);
        reopened.Subscribe();
        reopened.Close();
        reopened.Open();
        reopened.Steam.Queries[^1].Finish(false, new SteamWorkshopItem("Reopened"));
        check(reopened.ActionBusy, "Reopening cannot bypass the running Steam action gate");
        var reopenedStatus = reopened.Status;
        var reopenedDetails = reopened.Details;
        bool? busyAtRefresh = null;
        reopened.BeforeButtons = () => busyAtRefresh = reopened.ActionBusy;
        reopened.Steam.Actions[0].Complete();
        check(original.Subscribed && !reopened.ActionBusy && busyAtRefresh == false,
            "Old operation releases and refreshes the current live view's global busy gate");
        check(reopened.Status == reopenedStatus && reopened.Details == reopenedDetails,
            "Old presentation's item status cannot leak into the reopened menu");

        var refreshItem = new SteamWorkshopItem("Same object, new query");
        var refreshed = Ready(refreshItem);
        refreshed.Subscribe();
        refreshed.Reload();
        refreshed.Steam.Queries[^1].Finish(false, refreshItem);
        var refreshedDetails = refreshed.Details;
        refreshed.Steam.Actions[0].Complete();
        check(refreshed.Details == refreshedDetails && !refreshed.ActionBusy,
            "A new query generation suppresses old completion status even when it retains the same item object");

        foreach (var fail in new[] { false, true })
        {
            var update = Ready(new SteamWorkshopItem("Update"));
            update.UpdateSubscribed();
            var operation = SteamWorkshop.Updates[^1];
            check(update.ActionBusy && update.Status == "Updating subscribed Workshop items.", "Update-all shows current progress");
            update.Subscribe();
            check(update.Steam.Actions.Count == 0, "Update-all blocks overlapping item operations");
            if (fail)
                operation.Fail();
            else
                operation.Complete();
            check(!update.ActionBusy && (fail ? update.Status.Contains("failed") : update.Status == "Subscribed Workshop items are up to date."),
                "Current update-all success/failure releases busy and reports its result");
        }
        var hiddenUpdate = Ready(new SteamWorkshopItem("Update while closed"));
        hiddenUpdate.UpdateSubscribed();
        var hiddenOperation = SteamWorkshop.Updates[^1];
        hiddenUpdate.Close();
        var hiddenStatus = hiddenUpdate.Status;
        var hiddenButtons = hiddenUpdate.ButtonRefreshes;
        hiddenOperation.Fail();
        check(!hiddenUpdate.ActionBusy && hiddenUpdate.Status == hiddenStatus && hiddenUpdate.ButtonRefreshes == hiddenButtons,
            "Late update-all failure does not redraw a hidden view");
        var reopenedUpdate = Ready(new SteamWorkshopItem("Update before reopen"));
        reopenedUpdate.UpdateSubscribed();
        var reopeningOperation = SteamWorkshop.Updates[^1];
        reopenedUpdate.Close();
        reopenedUpdate.Open();
        reopenedUpdate.Steam.Queries[^1].Finish(false, new SteamWorkshopItem("New page"));
        var currentStatus = reopenedUpdate.Status;
        reopeningOperation.Complete();
        check(!reopenedUpdate.ActionBusy && reopenedUpdate.Status == currentStatus,
            "Old update-all completion cannot overwrite the reopened query's status");

        var rendering = Ready(new SteamWorkshopItem("Rendering failure"));
        rendering.BeforeButtons = rendering.BeforeDetails = () => throw new InvalidOperationException("simulated action rendering failure");
        rendering.Subscribe();
        check(rendering.Steam.Actions.Count == 1 && rendering.ActionBusy, "Rendering failure cannot prevent accepted Steam work");
        var rowsBefore = rendering.RowRefreshes;
        rendering.Steam.Actions[0].Complete();
        check(!rendering.ActionBusy && rendering.Items[0].Subscribed && rendering.RowRefreshes > rowsBefore,
            "Failed button/status redraw cannot strand busy state or skip the independent row refresh");
        rendering.BeforeButtons = rendering.BeforeDetails = null;
        rendering.BeforeRows = () => throw new InvalidOperationException("simulated row failure");
        rendering.Subscribe();
        rendering.Steam.Actions[1].Complete();
        check(!rendering.ActionBusy && !rendering.Items[0].Subscribed, "Final row redraw failure does not escape or retain busy state");
        rendering.BeforeRows = null;
        rendering.Steam.FailNextActionStart = true;
        rendering.Subscribe();
        check(!rendering.ActionBusy && rendering.Details.Contains("failed"), "Synchronous Steam startup failure releases busy state");
        rendering.Subscribe();
        rendering.Steam.Actions[2].Complete();
        check(!rendering.ActionBusy && rendering.Items[0].Subscribed, "Startup failure remains retryable");
        var renderingUpdate = Ready(new SteamWorkshopItem("Update render failure"));
        renderingUpdate.BeforeStatus = () => throw new InvalidOperationException("simulated update status failure");
        renderingUpdate.UpdateSubscribed();
        SteamWorkshop.Updates[^1].Complete();
        check(!renderingUpdate.ActionBusy, "Update status rendering failure does not cancel work or retain busy state");

        var hidden = new NativeWorkshopPanel();
        updates = SteamWorkshop.Updates.Count;
        hidden.Subscribe();
        hidden.UpdateSubscribed();
        check(hidden.Steam.Actions.Count == 0 && SteamWorkshop.Updates.Count == updates, "Hidden controls cannot initiate Steam mutations");
        check(UniTaskVoid.Started.All(task => task.IsCompletedSuccessfully), "All Workshop action paths finish without unobserved failures");
    }

    private static NativeWorkshopPanel Ready(params SteamWorkshopItem[] items)
    {
        var panel = new NativeWorkshopPanel();
        panel.Open();
        panel.Steam.Queries[0].Finish(false, items);
        return panel;
    }
}
