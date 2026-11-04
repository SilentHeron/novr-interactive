using System.Windows.Input;
using NOVR.Installer.Services;
using NOVR.Installer.ViewModels;

internal static class Program
{
    private static int _checks;

    private static void Main()
    {
        FolderPickerOwnsOperation();
        PickerCancellationAndFailures();
        PackageDestinationIsCaptured();
        PackageValidationAndRetry();
        PickerRefreshesInstallationState();
        DownloadFilesBelongToTheOperation();
        Check(!Directory.Exists(Boundary.Root), "The fake boundaries never create game or package files.");
        Console.WriteLine($"Installer UI: {_checks} checks passed.");
    }

    private static MainWindowViewModel Create()
    {
        Boundary.Reset();
        return new MainWindowViewModel { GamePath = Boundary.GameA };
    }

    private static ICommand[] Commands(MainWindowViewModel vm) =>
    [vm.BrowseCommand, vm.InstallPackageCommand, vm.PrimaryActionCommand,
        vm.RepairUpdateCommand, vm.UninstallCommand, vm.FinishUninstallCommand, vm.RescanCommand];

    private static void AssertBusy(MainWindowViewModel vm)
    {
        Check(vm.IsBusy, "The picker/install owns the shared busy state.");
        Check(!vm.CanEditGamePath, "The destination field is disabled during the operation.");
        foreach (var command in Commands(vm))
        {
            Check(!command.CanExecute(null), "Every command is disabled during the operation.");
            command.Execute(null); // Disabled UI commands must also reject programmatic execution.
        }
        Check(Boundary.Downloads == 0 && Boundary.Scans == 0 && Boundary.Uninstalls == 0,
            "Competing commands did not reach network, scanning or uninstall boundaries.");
    }

    private static void AssertIdle(MainWindowViewModel vm)
    {
        Check(!vm.IsBusy && vm.CanEditGamePath, "Completion releases the busy state and destination field.");
        Check(vm.BrowseCommand.CanExecute(null) && vm.PrimaryActionCommand.CanExecute(null),
            "Completion re-enables usable commands.");
    }

    private static void FolderPickerOwnsOperation()
    {
        var vm = Create();
        var picker = new TaskCompletionSource<string?>();
        var openings = 0;
        var editabilityChanges = 0;
        vm.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(vm.CanEditGamePath)) editabilityChanges++; };
        vm.SetFolderBrowser(() => { openings++; return picker.Task; });
        vm.BrowseCommand.Execute(null);
        AssertBusy(vm);
        Check(openings == 1, "Duplicate requests cannot open a second picker.");
        Check(Boundary.Installs.Count == 0, "A competing install did not start.");
        picker.SetResult(Boundary.GameB);
        AssertIdle(vm);
        Check(vm.GamePath == Boundary.GameB && vm.GameInfo?.GameDir == Boundary.GameB,
            "The selected folder is shown after completion.");
        Check(editabilityChanges == 2, "The view is notified on entry and exit.");
    }

    private static void PickerCancellationAndFailures()
    {
        foreach (var package in new[] { false, true })
        {
            foreach (var outcome in new[] { "cancel", "fault", "sync-fault" })
            {
                var vm = Create();
                var picker = new TaskCompletionSource<string?>();
                Func<Task<string?>> callback = outcome == "sync-fault"
                    ? () => throw new InvalidOperationException("Picker unavailable")
                    : () => picker.Task;
                if (package)
                    vm.SetPackageBrowser(callback);
                else
                    vm.SetFolderBrowser(callback);
                var command = package ? vm.InstallPackageCommand : vm.BrowseCommand;
                command.Execute(null);
                if (outcome != "sync-fault")
                {
                    AssertBusy(vm);
                    if (outcome == "cancel")
                        picker.SetResult(null);
                    else
                        picker.SetException(new InvalidOperationException("Picker unavailable"));
                }
                AssertIdle(vm);
                Check(vm.GamePath == Boundary.GameA && Boundary.Installs.Count == 0,
                    "Cancelled or failed picker does not change destination or install.");
                if (outcome != "cancel")
                    Check(vm.Status == "Operation failed." && vm.Details == "Picker unavailable",
                        "Picker failures are reported without escaping async-void ICommand.Execute.");
            }
        }
        var noPicker = Create();
        noPicker.BrowseCommand.Execute(null);
        AssertIdle(noPicker);
        Check(noPicker.Status.Contains("picker is unavailable"), "Missing folder-picker callback remains actionable.");
        noPicker.InstallPackageCommand.Execute(null);
        AssertIdle(noPicker);
        Check(Boundary.Installs.Count == 0, "Missing package picker does not install.");
    }

    private static void PackageDestinationIsCaptured()
    {
        var vm = Create();
        var picker = new TaskCompletionSource<string?>();
        Boundary.PendingInstall = new TaskCompletionSource();
        vm.SetPackageBrowser(() => picker.Task);
        vm.InstallPackageCommand.Execute(null);
        AssertBusy(vm);
        // The UI field is disabled; also defend against a programmatic selection change.
        vm.GamePath = Boundary.GameB;
        picker.SetResult(Boundary.Package);
        AssertBusy(vm);
        Check(Boundary.Installs.Count == 1 && Boundary.Installs[0] == (Boundary.GameA, Boundary.Package),
            "A delayed picker uses its captured destination and installs exactly once.");
        Boundary.PendingInstall.SetResult();
        AssertIdle(vm);
        Check(vm.Status == "Install complete." && vm.GamePath == Boundary.GameA,
            "Completion reports the destination actually installed.");
    }

    private static void PackageValidationAndRetry()
    {
        var vm = Create();
        var openings = 0;
        vm.SetPackageBrowser(() => { openings++; return Task.FromResult<string?>(Boundary.Package); });
        // The folder disappears after selection but before the user opens the picker.
        Boundary.GameAvailable = false;
        vm.InstallPackageCommand.Execute(null);
        AssertIdle(vm);
        Check(openings == 0 && Boundary.Installs.Count == 0 && vm.Status == "Invalid Nuclear Option folder.",
            "Validation rejects an unavailable game before opening a picker.");

        Boundary.GameAvailable = true;
        vm.GamePath = Boundary.GameB;
        Boundary.PendingInstall = new TaskCompletionSource();
        vm.InstallPackageCommand.Execute(null);
        AssertBusy(vm);
        Boundary.PendingInstall.SetException(new IOException("Install boundary failed"));
        AssertIdle(vm);
        Check(vm.Status == "Operation failed." && vm.Details == "Install boundary failed", "Install failure is reported.");
        Boundary.PendingInstall = null;
        vm.InstallPackageCommand.Execute(null);
        AssertIdle(vm);
        Check(Boundary.Installs.Count == 2 && vm.Status == "Install complete.", "A failed operation can be retried.");
        Check(Boundary.Downloads == 0, "A local package never requests a release download.");
    }

    private static void PickerRefreshesInstallationState()
    {
        var vm = Create();
        var picker = new TaskCompletionSource<string?>();
        vm.SetPackageBrowser(() => picker.Task);
        vm.InstallPackageCommand.Execute(null);
        Boundary.GameAvailable = false;
        picker.SetResult(Boundary.Package);
        AssertIdle(vm);
        Check(Boundary.Installs.Count == 0 && vm.Status == "Invalid Nuclear Option folder.",
            "A destination removed while the picker was open is rejected.");

        vm = Create();
        picker = new TaskCompletionSource<string?>();
        vm.SetPackageBrowser(() => picker.Task);
        vm.InstallPackageCommand.Execute(null);
        // Use the actual GameInstallInfo version reader in a private temporary
        // fixture. No installer/file-operation boundary writes to this folder.
        var versionFile = new GameLocator().Inspect(Boundary.GameA).VersionFile;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(versionFile)!);
            File.WriteAllText(versionFile, "2.0.0");
            Boundary.ModState = NOVR.Installer.Models.InstallState.FullyInstalled;
            picker.SetResult(Boundary.Package);
            AssertIdle(vm);
            Check(Boundary.Installs.Count == 0 && vm.Status == "Operation failed." && vm.Details.Contains("newer than"),
                "A newer installation completed during the picker is protected from downgrade.");
        }
        finally
        {
            if (Directory.Exists(Boundary.Root))
                Directory.Delete(Boundary.Root, true);
        }
    }

    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException($"Check {_checks}: {message}");
    }

    private static void DownloadFilesBelongToTheOperation()
    {
        var vm = Create();
        Boundary.MaterializeDownloads = true;
        try
        {
            Boundary.PendingInstall = new TaskCompletionSource();
            vm.PrimaryActionCommand.Execute(null);
            Check(vm.IsBusy && Boundary.Downloads == 1 && Boundary.Installs.Count == 1,
                "A download remains owned by its pending installation.");
            Check(File.Exists(Boundary.Installs[0].Package), "The package survives until the installation finishes reading it.");
            Boundary.PendingInstall.SetResult();
            AssertIdle(vm);
            Check(!Directory.Exists(Boundary.DownloadDirectories[0]), "A successful operation removes its downloaded package directory.");

            Boundary.PendingInstall = new TaskCompletionSource();
            vm.RepairUpdateCommand.Execute(null);
            Check(Boundary.Downloads == 2 && Boundary.Installs.Count == 2,
                "A new explicit update checks the channel again rather than reusing an old release.");
            Boundary.PendingInstall.SetException(new IOException("Fixture install failed"));
            AssertIdle(vm);
            Check(!Directory.Exists(Boundary.DownloadDirectories[1]), "Failed installation also releases its downloaded package.");

            Boundary.PendingInstall = null;
            foreach (var error in new Exception[] { new IOException("Fixture download failed"), new OperationCanceledException("Fixture download cancelled") })
            {
                Boundary.DownloadError = error;
                var installs = Boundary.Installs.Count;
                vm.PrimaryActionCommand.Execute(null);
                AssertIdle(vm);
                Check(Boundary.Installs.Count == installs, "A failed or cancelled download never reaches installation.");
                Check(!Directory.Exists(Boundary.DownloadDirectories[^1]), "Partial download files are removed after failure or cancellation.");
            }
            Boundary.DownloadError = null;
            vm.PrimaryActionCommand.Execute(null);
            AssertIdle(vm);
            Check(Boundary.Downloads == 5 && Boundary.Installs.Count == 3,
                "A download failure leaves no poisoned cache and supports a fresh retry.");
            Check(Boundary.DownloadDirectories.Distinct().Count() == 5 && Boundary.DownloadDirectories.All(dir => !Directory.Exists(dir)),
                "Each operation owns a unique directory and releases it.");
        }
        finally
        {
            // A failing regression must not leave its synthetic downloads behind.
            foreach (var dir in Boundary.DownloadDirectories)
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
        }

        vm = Create();
        Directory.CreateDirectory(Boundary.Root);
        File.WriteAllText(Boundary.Package, "User-selected package fixture");
        try
        {
            vm.SetPackageBrowser(() => Task.FromResult<string?>(Boundary.Package));
            vm.InstallPackageCommand.Execute(null);
            AssertIdle(vm);
            Check(File.Exists(Boundary.Package) && Boundary.Downloads == 0,
                "An operation never cleans up the user's original local package.");
        }
        finally { Directory.Delete(Boundary.Root, true); }
    }
}
