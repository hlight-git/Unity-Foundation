# Hlight Application Foundation

Application Foundation provides a root application lifetime, hierarchical scene
dependency injection, additive scene loading, and a small scene-transition contract.
All runtime APIs belong to the single `Hlight.Foundation` assembly and namespace; the
package does not split its public surface into `Core` and `Scoped` layers.

## Scope hierarchy

`ARootScope` is the application-wide dependency source. A concrete root implements
`IDependencyResolvable<T>` for every target type it configures. Its `Injector` is the
parent of each `SceneScope<TSceneRoot>`'s.

`ARootScope` also owns the serialized `RuntimeApplicationConfig` (target frame rate,
sleep timeout, multi-touch) and republishes Unity's player loop and application
callbacks as events: `OnFixedUpdate`, `OnUpdate`, `OnLateUpdate`,
`OnPauseStateChanged`, `OnFocusStateChanged`, `OnQuit`, and `OnDestroyed`. Services
that need per-frame work subscribe to the root instead of adding their own
`MonoBehaviour`.

`ASceneRoot` is the dependency source for one scene. Injecting a target runs the root
scope's resolvers first, then the scene's, so a scene refines what the application set.

```csharp
public sealed class HomeRoot : ASceneRoot,
    IDependencyResolvable<HomeHud>
{
    public void ResolveDependenciesFor(HomeHud target) => target.Camera = camera;
}

homeScope.SetParentScope(rootScope);

await homeScope.LoadIfNeededAsync(cancellationToken);
await homeScope.EnableAsync(cancellationToken);

hud.GetComponentsInChildren<HomeHud>();     // whatever the scene creates
homeRoot.Injector.Inject(hud);
```

Scene roots may use `Injector` after the scope has bound them. An `Awake`
override must call `base.Awake()` so the root enters the pending-root queue.

`ARootScope` and `SceneScope<TSceneRoot>` implement `IScope`. Their only shared
contract is ownership of a `DependencyInjector`; root and scene lifecycles intentionally
remain separate. A scene scope must receive its parent before its first load.

**A scene's injector exists only while its root is bound.** It is built at bind time and
dropped on unload or cache, so `SceneScope.Injector` throws while the scene is not loaded
and a reused scene gets a fresh one — an injector holds the parent it was chained onto, and
the previous load's parent must not survive into the next.

## Scene lifecycle

A scene normally follows this sequence:

```text
Unloaded <-release- LoadedInactive <-> Active
                       |
                  cache/restore
                       |
                     Cached
```

When `reuseScene` is enabled, `UnloadAsync` moves the scene to `Cached` instead of
physically unloading it. A later `LoadIfNeededAsync` restores it. Use `ReleaseAsync`
to unload a cached scene permanently. Caching invokes `OnSceneUnload(true)`; releasing
that cached scene later invokes `OnSceneUnload(false)` before the physical unload.
An initial load invokes `OnSceneLoaded(false)`; restoring a cached scene invokes
`OnSceneLoaded(true)`.
While cached, the scene root is detached from its locator. Resolving through the scene
scope therefore skips local providers and bubbles directly to the parent until the
scene is restored or released.

Only one operation may run on a `SceneScope` at a time. Concurrent calls fail with an
`InvalidOperationException`; the composition root decides whether transitions should
be rejected, queued, or coalesced. `State` contains only stable states; an in-flight
operation leaves the last stable state visible until it commits.

Application-level scene changes should be represented by `ISceneTransition`
implementations. A transition may be a class or a struct. Use a `readonly struct` for
small immutable transitions; use a class when phases share mutable state, reference
identity matters, or the transition has a longer lifetime.

`SceneTransitionExtensions.Perform` runs `BeginAsync`, `ExecuteAsync`, and `EndAsync`
behind a process-wide gate: only one transition runs at a time in the whole
application, and a concurrent `Perform` throws an `InvalidOperationException` rather
than interleaving two scene changes.

## Design rules

- Keep one `Hlight.Foundation` runtime assembly and namespace. Do not reintroduce
  separate `Core` or `Scoped` layers.
- Do not add catch-all `Utils` or `Common` folders. Group reusable code by its domain,
  use plural names for extension containers, and keep editor-only code outside
  `Runtime`.
- Put optional third-party helpers under `Runtime/Integrations` and guard them with
  the dependency's feature symbol. A convenience helper must not create a mandatory
  package dependency.
- Keep `SceneScopeState` limited to committed, externally meaningful states. Track an
  in-flight async operation with one private boolean instead of adding
  `Loading`, `Enabling`, `Disabling`, or `Unloading` states.
- Call `SceneScope` lifecycle operations on the Unity main thread. The operation guard
  prevents re-entry; it does not make scene loading thread-safe.
- Keep `IScope` limited to `ServiceLocator`. It exists only for hierarchy wiring; do
  not expose parent identity or add scene lifecycle members merely to make root and
  scene scopes look alike. The composition root owns the hierarchy.
- Validate mistakes developers commonly make: missing parent, missing scene key,
  concurrent operations, missing scene root, failed lifecycle callbacks, and failed
  Addressables operations. Do not build a general-purpose hierarchy graph validator.
- Keep physical scene ownership inside the internal scene lease. A failed rollback must
  retain its lease until `ReleaseAsync` or the next load successfully cleans it up; it
  must never load a duplicate scene after merely logging an unload failure.
- Make a project `RootScope` partial. Keep provider fields and hierarchy initialization
  in `RootScope.cs`; keep transition values and transition implementations in
  `RootScope.Transitions.cs`.
- Represent application orchestration with `ISceneTransition`, not convenience methods
  such as `OpenHomeAsync`, `ActivateAsync`, or `DeactivateAsync` on the root scope.
- Choose the transition representation from its behavior. A `readonly struct` works
  well when it only captures immutable scope/service references; a class is appropriate
  when it owns mutable state or identity. Generic `Perform` supports both.
- Do not rely on mutable struct fields to communicate between `BeginAsync`,
  `ExecuteAsync`, and `EndAsync`. Async instance methods operate on struct copies. Use
  an explicit reference-type context, or use a class transition when mutable phase
  state is required.
- `EndAsync` is cleanup and must be safe even when `BeginAsync` or `ExecuteAsync`
  failed. `Perform` intentionally invokes it with `CancellationToken.None` and reports
  both exceptions when the transition and its cleanup fail.
- Load the target before disabling the source. If target activation fails after the
  source was disabled, restore the source before propagating the failure.

## Bootstrap

`ABootstrap<TRootScope>` applies the runtime configuration and executes serialized
bootstrap tasks in order. Setup hooks and tasks receive the bootstrap object's destroy
cancellation token. `IsSetupCompleted` and `SetupCompleted` expose successful
completion. Application loading work belongs in `ABootstrapTask<TRootScope>`
implementations rather than setup hooks.

A missing root scope or runtime config is reported with `Debug.LogError` and disables
the bootstrap instead of throwing during `Awake`. A failing task is wrapped in an
`InvalidOperationException` that names the task type and its index; the bootstrap then
disables itself and logs the exception. Cancellation caused by the object being
destroyed is silent.

Each task has a positive `Weight`. Its share of total progress is
`task.Weight / totalWeight`. A task receives an `IProgress<float>` for reporting its
normalized local progress from `0` to `1`; the bootstrap maps that value into the
task's weighted range and forces the task to complete when `Execute` returns. Tasks
that do not report intermediate progress still advance the bar correctly on completion.

`ABootstrap.Progress`, `ProgressChanged`, and the protected `OnProgressChanged` hook can
drive a loading bar without coupling bootstrap orchestration to a concrete loading UI.
`Progress` is clamped and monotonic: a task that reports a value below the current
progress does not move the bar backwards.

## Addressables

Addressables support is optional and enabled automatically when
`com.unity.addressables` is installed: the runtime assembly declares a version define
that sets `ADDRESSABLE`. A scope configured with `useAddressable` throws a clear
`NotSupportedException` when the package is unavailable. An Addressables scene
that was loaded outside its scope cannot be adopted from the pending-root queue because
the scope does not own its load handle; let the scope initiate that load instead.

## Runtime utilities

Application lifecycle code is grouped under `Bootstrap`, `Configuration`, `Scopes`,
and `Transitions`. Reusable components are grouped under `Animation`, `Collections`,
`Extensions`, `Input`, `Motion`, `Randomization`, `Time`, and `UI`. They remain in the
single `Hlight.Foundation` namespace; folder names organize source code and do not
create additional public layers.

Third-party helpers belong under `Runtime/Integrations` and must be guarded by a
feature symbol. The DOTween touch responder requires `DOTWEEN`; the Spine animation
helpers require `SPINE`. Neither symbol is a version define, because neither dependency
ships through the package manager: add them to the project's scripting define symbols
yourself. Foundation does not install either dependency. Odin drawers live under
`Editor` and compile only when `ODIN_INSPECTOR` is enabled, which Odin defines itself.

`SerializableDictionary<TKey, TValue>` uses a serialized entry list as its source of
truth and implements `IDictionary<TKey, TValue>` through composition. All runtime
mutations must go through that interface so the lookup and serialized entries remain
synchronized.

`AMover.Stop` and `ARotator.Stop` cancel motion. `ReachedDestination` is raised only
after an implementation reaches its target, with the moving state already set to
false. Update-driven motion exposes constant-speed and stable smoothing modes; it does
not use unclamped interpolation.

`AnimatorExtensions.PlayAsync` accepts only a non-looping state. It completes when that
state reaches normalized time `1`, observes caller and animator-destruction cancellation,
and throws when another state interrupts it. `TrySetLossyScale` reports failure instead
of leaving a partially changed transform; Unity's reported lossy scale remains an
approximation under rotated, non-uniformly scaled ancestors.

`SafeAreaFitter` preserves `anchorMin` by default for layouts that own their lower and
left anchors. Its optional top-inset reduction ignores half of the top inset by default.
Disable anchor preservation when the component should apply all four safe-area edges.
The horizontal and vertical insets can be applied independently, an invalid screen state
is skipped and retried instead of applied, and `SafeAreaFitter.EditorSimulationDevice`
substitutes a notched-device safe area in the Editor only.

The Button and Toggle inspectors can upgrade the built-in controls to their
multi-graphic variants. The editor changes the component's serialized script reference
instead of destroying and recreating it, preserving the component file ID and reverse
serialized references. Convert prefab controls in their prefab source; script changes
are intentionally rejected on prefab instances.

## Assemblies

`Runtime` builds `Hlight.Foundation` behind a `UNITASK` define constraint, so the
assembly is skipped entirely rather than reporting errors when UniTask is missing.
`Editor` builds
`Hlight.Foundation.Editor`. `Tests` builds the EditMode-only `Hlight.Foundation.Tests`
covering scope resolution, the scene lifecycle, bootstrap progress, and transitions.

## Dependencies

- UniTask 2.5.11
- `com.hlight.dependency-inversion` 1.0.0
- Unity Animation module 1.0.0
- Unity Physics module 1.0.0
- Unity Physics 2D module 1.0.0
- Unity uGUI 2.0.0
- Unity Addressables when addressable scene loading is used
