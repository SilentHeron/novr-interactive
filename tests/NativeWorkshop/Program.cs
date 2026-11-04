using NOVR.VrUi.Native;
using NuclearOption.Workshop;
using Cysharp.Threading.Tasks;
using UnityEngine;

var passed = 0;
void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
    passed++;
}
void Disposed(CancellationTokenSource? source, string message)
{
    try
    {
        _ = source!.Token;
        throw new Exception(message);
    }
    catch (ObjectDisposedException) { passed++; }
}

var panel = new NativeWorkshopPanel();
panel.Open();
Check(panel.Busy && panel.Steam.Queries.Count == 1, "Opening starts one query");
Check(panel.Steam.Queries[0].Tag == "Mission", "Initial query uses the missions tab");
panel.Liveries();
panel.ChangeOrder(1);
panel.ChangeOrder(1);
panel.Reload();
Check(panel.Steam.Queries.Count == 1 && panel.Items.Count == 0, "Changes during a request are queued without overlapping Steam queries");
panel.Steam.Queries[0].Finish(true, new SteamWorkshopItem("Stale mission"));
Check(panel.Items.Count == 0 && panel.Steam.Queries.Count == 2 && panel.Busy, "Stale results are discarded before starting latest request");
var latest = panel.Steam.Queries[1];
Check(latest.Tag == "AircraftLivery" && latest.Order == OrderBy.New && latest.Page == 1, "Queued request uses final tab, order and first page");
var item = new SteamWorkshopItem("Livery");
latest.Finish(true, item);
Check(!panel.Busy && panel.Items.Single() == item && panel.HasMore, "Only latest query populates rows and more-page state");
Check(item.Listeners == 1 && panel.HasSelection, "Selected item owns one author callback");
item.RenameOwner("New author");
Check(panel.Owner == "By New author", "Selected author's later update reaches the view");
item.CompletePreview();
Check(panel.Preview == "Livery", "Selected item preview completes normally");
var oldSource = item.PreviewSource;
panel.Choose(-1);
item.CompletePreview();
Check(item.Listeners == 0 && item.PreviewToken.IsCancellationRequested && panel.Preview == null, "Clearing selection cancels image work and author events");
Disposed(oldSource, "Cleared preview source must be disposed");
panel.Choose(0);
oldSource = item.PreviewSource;
panel.Close();
var rowsAfterClose = panel.RowRefreshes;
item.RenameOwner("Hidden author");
item.CompletePreview();
Check(item.Listeners == 0 && item.PreviewToken.IsCancellationRequested && !panel.HasSelection, "Explicit hide releases selection resources");
Check(panel.RowRefreshes == rowsAfterClose, "Hidden item cannot refresh rows through retained author listeners");
Disposed(oldSource, "Hidden preview source must be disposed");
panel.Disable();
panel.Close();
Check(item.Listeners == 0, "Repeated hide/disable is idempotent");
panel.Open();
panel.Steam.Queries[2].Finish(true, item);
panel.More();
Check(panel.Steam.Queries[3].Page == 2 && panel.DisplayPage == 0, "More loads next Steam page at first display page");
panel.More();
Check(panel.Steam.Queries.Count == 4, "More cannot overlap an active query");
panel.Close();
panel.Steam.Queries[3].Finish(true, new SteamWorkshopItem("Hidden result"));
Check(panel.Items.Count == 0 && !panel.HasMore && !panel.Busy, "Completion while hidden cannot repopulate or select items");
panel.Open();
panel.Close();
panel.Open();
Check(panel.Steam.Queries.Count == 5, "Reopening while old query is pending coalesces refresh");
panel.Steam.Queries[4].Fail();
Check(panel.Steam.Queries.Count == 6 && Debug.Errors.Count == 0, "Stale failed request does not overwrite latest state and still runs queued refresh");
panel.Steam.Queries[5].Fail();
Check(!panel.Busy && panel.Items.Count == 0 && Debug.Errors.Count == 1 && panel.Status.Contains("failed"), "Current failure is reported and releases busy state");
panel.Reload();
panel.Steam.Queries[6].Finish(false, item);
Check(panel.HasSelection && !panel.HasMore, "Retry recovers normally after failure");
oldSource = item.PreviewSource;
panel.Disable();
Check(item.Listeners == 0 && item.PreviewToken.IsCancellationRequested, "Parent disable releases author callback and preview");
Disposed(oldSource, "Disabled preview source must be disposed");
panel.Open();
panel.Steam.Queries[7].Finish(false, item);
oldSource = item.PreviewSource;
panel.DestroyPanel();
panel.DestroyPanel();
Check(item.Listeners == 0 && item.PreviewToken.IsCancellationRequested, "Destroy releases selection even without a preceding disable");
Disposed(oldSource, "Destroyed preview source must be disposed");
var destroyed = new NativeWorkshopPanel();
destroyed.Open();
destroyed.Liveries();
destroyed.DestroyPanel();
var beforeCompletion = destroyed.RowRefreshes;
destroyed.Steam.Queries[0].Finish(true, new SteamWorkshopItem("Late"));
Check(destroyed.Items.Count == 0 && destroyed.RowRefreshes == beforeCompletion && destroyed.Steam.Queries.Count == 1, "Destroyed query neither redraws nor launches queued work");
var failedView = new NativeWorkshopPanel { FailPresentation = true };
failedView.Open();
failedView.Steam.Queries[0].Finish(true, item);
Check(!failedView.Busy && failedView.Items.Count == 0 && !failedView.HasMore && !failedView.HasSelection,
    "Presentation failure clears a partially applied result and releases request ownership");
failedView.FailPresentation = false;
failedView.Reload();
failedView.Steam.Queries[1].Finish(false, item);
Check(failedView.HasSelection, "Presentation failure does not prevent a later successful retry");
failedView.Close();
var failedRedraw = new NativeWorkshopPanel();
failedRedraw.Open();
failedRedraw.FailNextRows = true;
failedRedraw.Liveries();
failedRedraw.FailNextButtons = true;
failedRedraw.Steam.Queries[0].Finish(false, new SteamWorkshopItem("Discard"));
Check(failedRedraw.Busy && failedRedraw.Steam.Queries.Count == 2,
    "Row and completion-button redraw faults cannot discard a queued tab change");
failedRedraw.Steam.Queries[1].Finish(false, item);
Check(!failedRedraw.Busy && failedRedraw.Items.Single() == item,
    "Request queued through rendering faults still completes normally");
failedRedraw.Close();
Check(UniTaskVoid.Started.All(task => task.IsCompletedSuccessfully), "All started request paths finish without unobserved failures");
ActionChecks.Run(Check);
Console.WriteLine($"PASS {passed} native Workshop checks");
