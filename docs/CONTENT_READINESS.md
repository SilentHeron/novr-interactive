# Mission startup and additional content

During initial supported content loading, the [VR startup splash](STARTUP_SPLASH.md) hides the menu until the loader completes. If a single-player menu is reached outside that startup presentation, its independent launch gate keeps mission browsing and Back available while a supported content loader is working. **Start Mission** and **Customize Mission** stay unavailable with a **Preparing Content** label. Once the loader completes, the menu refreshes its mission list and restores the selected mission if it still exists. No mission is queued or launched automatically.

A loader failure leaves a clear status asking the pilot to check the mod log and restart. The adapter does not retry a failed aircraft loader or silently bypass it. Normal mission-file/host failures retain the existing mission retry behavior, and the duplicate-launch gate remains in place. Launch readiness is checked again after the first yielded frame, before loading a mission or opening a host.

## Verified integration: Blueprinter 2.0.1

Inspection of the installed 2.0.1 assembly established these facts:

- Its `Encyclopedia.AfterLoad` postfix starts `Blueprinter.Plugin.RunRoutine` once.
- The root coroutine can create an offline host to load additional assets, then stop that host.
- There is no public ready flag. `BundlesSignature` is assigned before patching completes.
- The loading-screen object is also removed on early failure, so its disappearance does not establish readiness.
- Successful completion emits an Info event with `[Plugin] Done in {duration}s` only after patch operations and temporary-host cleanup. Failure paths can end the iterator without emitting that event.

NOVR initializes the optional adapter from an `Encyclopedia.AfterLoad` prefix, before Blueprinter's postfix. It then observes the loader's own root coroutine and subscribes only to the exact Blueprinter `ManualLogSource` during that operation. Launch is unlocked only after both the terminal success event and normal coroutine completion. The elapsed number in the event is parsed according to the loader's numeric culture solely to recognize its message; no elapsed-time threshold or log-file polling authorizes launch.

The coroutine wrapper passes yielded objects through unchanged. Early termination, cancellation/disposal, and exceptions cannot become a success. Exceptions propagate back to the loader. The log subscription is removed on completion/failure; the adapter's hooks and subscription are removed on application shutdown.

This adapter observes startup; it does not repair bad content, intercept multiplayer, or prevent freezes inside a third-party loader's synchronous code. Nonfatal unresolved-patch warnings remain the loader's responsibility and do not override its explicit successful completion.

## Compatibility and limits

No Blueprinter installation means normal mission launch behavior. An unsupported version, incompatible API shape, or attachment after its first startup operation has begun reports **readiness unavailable** and retains the game's existing launch/session checks. It does not label unknown content ready or permanently lock an already-completed loader. Restarting with a supported version enables early tracking. This fallback is not a safety guarantee for unknown loaders.

The development copy inspected on 2026-10-30 had SHA-256 `a2cec71bda003824695b5c5b55879a50c4dad579a5597c795600428636f1f7ec`. No third-party assembly or decompiled source is included in this repository.

## Validation

From the repository root:

```sh
dotnet run --project validation/MissionReadiness/NOVR.MissionReadiness.Tests.csproj -c Release
```

The 20 portable checks compile the production state, coroutine wrapper, completion recognizer, and adapter against narrow boundary doubles. They cover no-loader startup, early registration, unknown/late attachment, exact log source/level/message, stale events, normal completion, early termination, exceptions, cancellation, retry, unchanged yield ordering, culture, and subscription cleanup. They do not simulate the Unity player, actual Harmony patch execution, or a headset.

For an optional local integration check with the game and Blueprinter installed, start from a cold process and inspect NOVR's `startup readiness tracking enabled` message before Blueprinter's completion event, then `Additional content ready; mission launch unlocked`. Open the mission menu during loading: browsing and Back should remain usable, launch/customize should stay disabled, and launch should become available after content completion. Also check a stock installation. Do not use an intentional broken-mod run against a personal save or active multiplayer session.
