using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Hlight.DesignPattern.DependencyInversion.DependencyInjection;
using Hlight.Foundation.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hlight.Foundation.Tests
{
    public sealed class FoundationTests
    {
        [Test]
        public async Task SceneScopeInjectsParentFirstThenItself()
        {
            var gameObject = new GameObject(nameof(TestSceneRoot));
            AddPendingSceneRoot<TestSceneRoot>(gameObject);
            var scope = new SceneScope<TestSceneRoot>()
                .SetParentScope(new TestParentScope());

            try
            {
                await scope.LoadIfNeededAsync().AsTask();
                var target = new ServiceTarget();
                scope.Injector.Inject(target);

                Assert.That(scope.State, Is.EqualTo(SceneScopeState.LoadedInactive));
                Assert.That(target.Service.Source, Is.EqualTo("scene"), "the scene runs last and wins");
                Assert.That(target.RootOnly, Is.Not.Null, "what only the parent sets must survive");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task SceneScopeHasNoInjectorUntilLoadedAndLosesItOnUnload()
        {
            var gameObject = new GameObject(nameof(LifecycleSceneRoot));
            AddPendingSceneRoot<LifecycleSceneRoot>(gameObject);
            var scope = new SceneScope<LifecycleSceneRoot>(new FakeSceneLease())
                .SetParentScope(new TestParentScope());

            try
            {
                Assert.Throws<InvalidOperationException>(() => _ = scope.Injector);

                await scope.LoadIfNeededAsync().AsTask();
                Assert.DoesNotThrow(() => _ = scope.Injector);

                await scope.UnloadAsync().AsTask();
                Assert.Throws<InvalidOperationException>(() => _ = scope.Injector);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task ReusedSceneGetsAFreshInjector()
        {
            var gameObject = new GameObject(nameof(LifecycleSceneRoot));
            AddPendingSceneRoot<LifecycleSceneRoot>(gameObject);
            var scope = new SceneScope<LifecycleSceneRoot>(new FakeSceneLease())
                .SetParentScope(new TestParentScope());
            SetPrivateField(scope, "reuseScene", true);

            try
            {
                await scope.LoadIfNeededAsync().AsTask();
                var first = scope.Injector;

                await scope.UnloadAsync().AsTask();
                Assert.That(scope.State, Is.EqualTo(SceneScopeState.Cached));

                await scope.LoadIfNeededAsync().AsTask();

                Assert.That(scope.Injector, Is.Not.SameAs(first),
                    "a reused scene must not keep an injector chained to the previous load");
                var target = new ServiceTarget();
                scope.Injector.Inject(target);
                Assert.That(target.Service.Source, Is.EqualTo("scene"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task SceneScopeKeepsStableStateAndRejectsAConcurrentOperation()
        {
            var gameObject = new GameObject(nameof(BlockingSceneRoot));
            var root = AddPendingSceneRoot<BlockingSceneRoot>(gameObject);
            var scope = new SceneScope<BlockingSceneRoot>()
                .SetParentScope(new TestParentScope());
            var firstLoad = scope.LoadIfNeededAsync().AsTask();

            try
            {
                Assert.That(scope.State, Is.EqualTo(SceneScopeState.Unloaded));
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await scope.LoadIfNeededAsync().AsTask());
                Assert.Throws<InvalidOperationException>(() =>
                    scope.SetParentScope(new TestParentScope()));

                root.CompleteLoading();
                await firstLoad;
                Assert.That(scope.State, Is.EqualTo(SceneScopeState.LoadedInactive));
            }
            finally
            {
                root.CompleteLoading();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SceneScopeRejectsItselfAsParent()
        {
            var scope = new SceneScope<TestSceneRoot>();

            Assert.Throws<ArgumentException>(() => scope.SetParentScope(scope));
        }

        [Test]
        public async Task SceneScopeRequiresAParentAndRecoversAfterValidationFailure()
        {
            var gameObject = new GameObject(nameof(TestSceneRoot));
            AddPendingSceneRoot<TestSceneRoot>(gameObject);
            var scope = new SceneScope<TestSceneRoot>();

            try
            {
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await scope.LoadIfNeededAsync().AsTask());
                Assert.That(scope.State, Is.EqualTo(SceneScopeState.Unloaded));

                scope.SetParentScope(new TestParentScope());
                await scope.LoadIfNeededAsync().AsTask();

                Assert.That(scope.State, Is.EqualTo(SceneScopeState.LoadedInactive));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public async Task CachedSceneCanBePhysicallyReleased()
        {
            var gameObject = new GameObject(nameof(LifecycleSceneRoot));
            var root = AddPendingSceneRoot<LifecycleSceneRoot>(gameObject);
            var sceneLease = new FakeSceneLease();
            var scope = new SceneScope<LifecycleSceneRoot>(sceneLease)
                .SetParentScope(new TestParentScope());
            SetPrivateField(scope, "reuseScene", true);

            try
            {
                await scope.LoadIfNeededAsync().AsTask();
                var loaded = new ServiceTarget();
                scope.Injector.Inject(loaded);
                Assert.That(loaded.Service.Source, Is.EqualTo("scene"));

                await scope.UnloadAsync().AsTask();

                Assert.That(scope.State, Is.EqualTo(SceneScopeState.Cached));
                Assert.That(sceneLease.UnloadCount, Is.Zero);
                Assert.Throws<InvalidOperationException>(() => _ = scope.Injector,
                    "a cached scene has released its injector");

                await scope.ReleaseAsync().AsTask();

                Assert.That(scope.State, Is.EqualTo(SceneScopeState.Unloaded));
                Assert.That(sceneLease.UnloadCount, Is.EqualTo(1));
                Assert.That(sceneLease.HasOwnership, Is.False);
                CollectionAssert.AreEqual(
                    new[] { "load:false", "unload:true", "unload:false" },
                    root.Lifecycle);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SerializableDictionaryKeepsSerializedEntriesAsItsSourceOfTruth()
        {
            var dictionary = new SerializableDictionary<string, int>
            {
                ["runtime"] = 1
            };
            var entriesField = typeof(SerializableDictionary<string, int>).GetField(
                "entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = (List<SerializableDictionary<string, int>.Entry>)entriesField.GetValue(dictionary);

            entries.Clear();
            entries.Add(new SerializableDictionary<string, int>.Entry
            {
                key = "inspector",
                value = 2
            });

            dictionary.OnBeforeSerialize();
            dictionary.OnAfterDeserialize();

            Assert.That(dictionary.ContainsKey("runtime"), Is.False);
            Assert.That(dictionary["inspector"], Is.EqualTo(2));
            Assert.That(dictionary.Remove("inspector"), Is.True);
            entries = (List<SerializableDictionary<string, int>.Entry>)entriesField.GetValue(dictionary);
            Assert.That(entries, Is.Empty);
        }

        [Test]
        public void StoppingMotionCancelsWithoutReportingCompletion()
        {
            var gameObject = new GameObject(nameof(TestMover));
            var mover = gameObject.AddComponent<TestMover>();
            var states = new List<bool>();
            var completionCount = 0;
            var wasMovingAtCompletion = true;
            mover.IsMovingChanged += states.Add;
            mover.ReachedDestination += () =>
            {
                completionCount++;
                wasMovingAtCompletion = mover.IsMoving;
            };

            try
            {
                mover.MoveTo(Vector3.one, Space.World);
                mover.Stop();

                Assert.That(completionCount, Is.Zero);

                mover.MoveTo(Vector3.one, Space.World);
                mover.Finish();

                Assert.That(completionCount, Is.EqualTo(1));
                Assert.That(wasMovingAtCompletion, Is.False);
                CollectionAssert.AreEqual(new[] { true, false, true, false }, states);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SerializableDateTimePreservesDateTimeKind()
        {
            foreach (var kind in new[] { DateTimeKind.Unspecified, DateTimeKind.Utc, DateTimeKind.Local })
            {
                var source = new DateTime(2025, 1, 2, 3, 4, 5, kind);
                var serialized = new SerializableDateTime(source);
                var restored = serialized.ToDateTime();

                Assert.That(restored, Is.EqualTo(source));
                Assert.That(restored.Kind, Is.EqualTo(kind));
            }
        }

        [Test]
        public void RandomHelpersRejectInvalidProbabilitiesAndWeights()
        {
            Assert.That(RandomUtility.NextBoolean(0f), Is.False);
            Assert.That(RandomUtility.NextBoolean(1f), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => RandomUtility.NextBoolean(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new[] { 1 }.ChooseRandom(_ => float.PositiveInfinity));
        }

        [Test]
        public void TrySetLossyScaleRollsBackWhenTheHierarchyCannotRepresentTheTarget()
        {
            var parent = new GameObject("parent");
            var child = new GameObject("child");
            child.transform.SetParent(parent.transform, false);
            parent.transform.localScale = new Vector3(2f, 3f, 4f);

            try
            {
                Assert.That(child.transform.TrySetLossyScale(4f, 6f, 8f), Is.True);
                Assert.That(child.transform.lossyScale.x, Is.EqualTo(4f).Within(0.001f));
                Assert.That(child.transform.lossyScale.y, Is.EqualTo(6f).Within(0.001f));
                Assert.That(child.transform.lossyScale.z, Is.EqualTo(8f).Within(0.001f));

                parent.transform.localScale = new Vector3(0f, 3f, 4f);
                var originalLocalScale = child.transform.localScale;

                Assert.That(child.transform.TrySetLossyScale(x: 2f), Is.False);
                Assert.That(child.transform.localScale, Is.EqualTo(originalLocalScale));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SelectableConversionPreservesComponentIdentityAndReferences()
        {
            var buttonObject = new GameObject("button");
            var holderObject = new GameObject("holder");
            var image = buttonObject.AddComponent<Image>();
            var source = buttonObject.AddComponent<Button>();
            source.targetGraphic = image;
            var holder = holderObject.AddComponent<ButtonReferenceHolder>();
            holder.Button = source;
            var sourceInstanceId = source.GetInstanceID();
            var sourceEntityId = source.GetEntityId();
            var sourceGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(source);

            try
            {
                var replacement = SelectableScriptConverter.Convert<MultiGraphicButton>(
                    source,
                    "Test Button Conversion",
                    button => button.TargetGraphics = new Graphic[] { image });

                Assert.That(replacement, Is.Not.Null);
                Assert.That(replacement.GetInstanceID(), Is.EqualTo(sourceInstanceId));
                Assert.That(
                    GlobalObjectId.GetGlobalObjectIdSlow(replacement),
                    Is.EqualTo(sourceGlobalId));

                var serializedHolder = new SerializedObject(holder);
                var buttonProperty = serializedHolder.FindProperty("button");
                Assert.That(buttonProperty.objectReferenceValue, Is.SameAs(replacement));

                Undo.PerformUndo();
                var restored = EditorUtility.EntityIdToObject(sourceEntityId) as Button;
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.GetInstanceID(), Is.EqualTo(sourceInstanceId));
                serializedHolder.Update();
                Assert.That(buttonProperty.objectReferenceValue, Is.SameAs(restored));

                Undo.PerformRedo();
                var redone = EditorUtility.EntityIdToObject(sourceEntityId) as MultiGraphicButton;
                Assert.That(redone, Is.Not.Null);
                Assert.That(redone.GetInstanceID(), Is.EqualTo(sourceInstanceId));
                serializedHolder.Update();
                Assert.That(buttonProperty.objectReferenceValue, Is.SameAs(redone));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buttonObject);
                UnityEngine.Object.DestroyImmediate(holderObject);
            }
        }

        [Test]
        public async Task BootstrapProgressUsesTaskWeights()
        {
            var gameObject = new GameObject("bootstrap");
            gameObject.SetActive(false);
            var rootScope = gameObject.AddComponent<TestRootScope>();
            var bootstrap = gameObject.AddComponent<TestBootstrap>();
            var firstTask = gameObject.AddComponent<TestBootstrapTask>();
            var secondTask = gameObject.AddComponent<TestBootstrapTask>();
            firstTask.ReportedProgress = 0.5f;
            secondTask.ReportedProgress = 0.5f;
            SetPrivateField(typeof(ABootstrapTask<TestRootScope>), firstTask, "weight", 1f);
            SetPrivateField(typeof(ABootstrapTask<TestRootScope>), secondTask, "weight", 3f);
            SetPrivateField(typeof(ABootstrap<TestRootScope>), bootstrap, "rootScope", rootScope);
            SetPrivateField(
                typeof(ABootstrap<TestRootScope>),
                bootstrap,
                "bootstrapTasks",
                new ABootstrapTask<TestRootScope>[] { firstTask, secondTask });
            var progress = new List<float>();
            bootstrap.ProgressChanged += progress.Add;

            try
            {
                await bootstrap.ExecuteSetupAsync(CancellationToken.None).AsTask();

                Assert.That(progress, Has.Some.EqualTo(0.125f).Within(0.0001f));
                Assert.That(progress, Has.Some.EqualTo(0.25f).Within(0.0001f));
                Assert.That(progress, Has.Some.EqualTo(0.625f).Within(0.0001f));
                Assert.That(progress[^1], Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test, Timeout(5000)]
        public async Task BootstrapRunsIndependentTasksTogether()
        {
            var gameObject = NewBootstrap(out var bootstrap, out var firstTask, out var secondTask);

            // The first task cannot finish until the second one has started, so this only completes
            // if the two really do overlap; run one after the other it deadlocks into the timeout.
            var log = new List<string>();
            var secondStarted = new UniTaskCompletionSource();
            firstTask.Label = "first";
            firstTask.Log = log;
            firstTask.Wait = secondStarted;
            secondTask.Label = "second";
            secondTask.Log = log;
            secondTask.Release = secondStarted;

            try
            {
                await bootstrap.ExecuteSetupAsync(CancellationToken.None).AsTask();

                Assert.That(log, Is.EquivalentTo(
                    new[] { "first start", "second start", "first end", "second end" }));
                Assert.That(bootstrap.Progress, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test, Timeout(5000)]
        public async Task BootstrapHoldsATaskUntilWhatItRequiresIsProvided()
        {
            var gameObject = NewBootstrap(out var bootstrap, out var providerTask, out var consumerTask);

            // Listed last, so only the declaration can put it first.
            var log = new List<string>();
            providerTask.Label = "provider";
            providerTask.Log = log;
            providerTask.ProvidedTypes = new[] { typeof(ProvidedByFirst) };
            consumerTask.Label = "consumer";
            consumerTask.Log = log;
            consumerTask.RequiredTypes = new[] { typeof(ProvidedByFirst) };

            SetPrivateField(
                typeof(ABootstrap<TestRootScope>),
                bootstrap,
                "bootstrapTasks",
                new ABootstrapTask<TestRootScope>[] { consumerTask, providerTask });

            try
            {
                await bootstrap.ExecuteSetupAsync(CancellationToken.None).AsTask();

                Assert.That(log, Is.EqualTo(
                    new[] { "provider start", "provider end", "consumer start", "consumer end" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BootstrapRejectsARequirementNobodyProvides()
        {
            var gameObject = NewBootstrap(out var bootstrap, out var firstTask, out _);
            firstTask.RequiredTypes = new[] { typeof(ProvidedByFirst) };

            try
            {
                var exception = Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await bootstrap.ExecuteSetupAsync(CancellationToken.None).AsTask());

                Assert.That(exception.Message, Does.Contain(nameof(ProvidedByFirst)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BootstrapRejectsTwoTasksProvidingTheSameType()
        {
            var gameObject = NewBootstrap(out var bootstrap, out var firstTask, out var secondTask);
            firstTask.ProvidedTypes = new[] { typeof(ProvidedByFirst) };
            secondTask.ProvidedTypes = new[] { typeof(ProvidedByFirst) };

            try
            {
                var exception = Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await bootstrap.ExecuteSetupAsync(CancellationToken.None).AsTask());

                Assert.That(exception.Message, Does.Contain(nameof(ProvidedByFirst)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BootstrapRejectsACycle()
        {
            var gameObject = NewBootstrap(out var bootstrap, out var firstTask, out var secondTask);
            firstTask.ProvidedTypes = new[] { typeof(ProvidedByFirst) };
            firstTask.RequiredTypes = new[] { typeof(ProvidedBySecond) };
            secondTask.ProvidedTypes = new[] { typeof(ProvidedBySecond) };
            secondTask.RequiredTypes = new[] { typeof(ProvidedByFirst) };

            try
            {
                var exception = Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await bootstrap.ExecuteSetupAsync(CancellationToken.None).AsTask());

                Assert.That(exception.Message, Does.Contain("cycle"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        /// <summary>A disabled bootstrap wired to two blank tasks, in list order.</summary>
        private static GameObject NewBootstrap(
            out TestBootstrap bootstrap,
            out TestBootstrapTask firstTask,
            out TestBootstrapTask secondTask)
        {
            var gameObject = new GameObject("bootstrap");
            gameObject.SetActive(false);
            var rootScope = gameObject.AddComponent<TestRootScope>();
            bootstrap = gameObject.AddComponent<TestBootstrap>();
            firstTask = gameObject.AddComponent<TestBootstrapTask>();
            secondTask = gameObject.AddComponent<TestBootstrapTask>();

            SetPrivateField(typeof(ABootstrap<TestRootScope>), bootstrap, "rootScope", rootScope);
            SetPrivateField(
                typeof(ABootstrap<TestRootScope>),
                bootstrap,
                "bootstrapTasks",
                new ABootstrapTask<TestRootScope>[] { firstTask, secondTask });

            return gameObject;
        }

        [Test]
        public async Task FailedRollbackKeepsOwnershipUntilReleaseSucceeds()
        {
            var gameObject = new GameObject(nameof(FailingLoadSceneRoot));
            AddPendingSceneRoot<FailingLoadSceneRoot>(gameObject);
            var sceneLease = new FakeSceneLease { FailUnload = true };
            var scope = new SceneScope<FailingLoadSceneRoot>(sceneLease)
                .SetParentScope(new TestParentScope());

            try
            {
                var exception = Assert.ThrowsAsync<AggregateException>(async () =>
                    await scope.LoadIfNeededAsync().AsTask());

                Assert.That(exception.InnerExceptions, Has.Count.EqualTo(2));
                Assert.That(scope.State, Is.EqualTo(SceneScopeState.Unloaded));
                Assert.That(sceneLease.HasOwnership, Is.True);
                Assert.That(sceneLease.UnloadCount, Is.EqualTo(1));

                sceneLease.FailUnload = false;
                await scope.ReleaseAsync().AsTask();

                Assert.That(sceneLease.HasOwnership, Is.False);
                Assert.That(sceneLease.UnloadCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PreloadedAddressablesRootIsNotAdoptedWithoutItsHandle()
        {
            var gameObject = new GameObject(nameof(TestSceneRoot));
            AddPendingSceneRoot<TestSceneRoot>(gameObject);
            var sceneLease = new FakeSceneLease();
            var scope = new SceneScope<TestSceneRoot>(sceneLease)
                .SetParentScope(new TestParentScope());
            SetPrivateField(scope, "useAddressable", true);

            try
            {
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await scope.LoadIfNeededAsync().AsTask());
                Assert.That(scope.State, Is.EqualTo(SceneScopeState.Unloaded));
                Assert.That(sceneLease.HasOwnership, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TransitionAlwaysEndsWhenExecutionFails()
        {
            var phases = new List<string>();
            var transition = new FailingTransition(phases);

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await transition.Perform().AsTask());

            CollectionAssert.AreEqual(new[] { "begin", "execute", "end" }, phases);
        }

        [Test]
        public void TransitionPreservesOperationAndCleanupFailures()
        {
            var exception = Assert.ThrowsAsync<AggregateException>(async () =>
                await new DoublyFailingTransition().Perform().AsTask());

            Assert.That(exception.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(exception.InnerExceptions[0].Message, Is.EqualTo("operation"));
            Assert.That(exception.InnerExceptions[1].Message, Is.EqualTo("cleanup"));
            Assert.DoesNotThrowAsync(async () =>
                await new NoOpTransition().Perform().AsTask());
        }

        [Test]
        public async Task TransitionRejectsConcurrentExecution()
        {
            var transition = new BlockingTransition();
            var firstRun = transition.Perform().AsTask();

            try
            {
                Assert.That(transition.IsExecuting, Is.True);
                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await new NoOpTransition().Perform().AsTask());
            }
            finally
            {
                transition.Complete();
                await firstRun;
            }
        }

        public sealed class TestSceneRoot : ASceneRoot, IDependencyResolvable<ServiceTarget>
        {
            private readonly TestService _service = new("scene");

            // Overwrites the root's TestService, leaves RootOnly as the root set it —
            // that split is what the chain-order test reads.
            public void ResolveDependenciesFor(ServiceTarget target) => target.Service = _service;
        }

        public sealed class BlockingSceneRoot : ASceneRoot
        {
            private readonly UniTaskCompletionSource _loadCompletion = new();

            public override UniTask OnSceneLoaded(
                bool isReusing,
                CancellationToken cancellationToken) => _loadCompletion.Task;

            public void CompleteLoading() => _loadCompletion.TrySetResult();
        }

        public sealed class LifecycleSceneRoot : ASceneRoot, IDependencyResolvable<ServiceTarget>
        {
            private readonly TestService _service = new("scene");

            public List<string> Lifecycle { get; } = new();

            public void ResolveDependenciesFor(ServiceTarget target) => target.Service = _service;

            public override UniTask OnSceneLoaded(
                bool isReusing,
                CancellationToken cancellationToken)
            {
                Lifecycle.Add(isReusing ? "load:true" : "load:false");
                return UniTask.CompletedTask;
            }

            public override UniTask OnSceneUnload(
                bool isReusing,
                CancellationToken cancellationToken)
            {
                Lifecycle.Add(isReusing ? "unload:true" : "unload:false");
                return UniTask.CompletedTask;
            }
        }

        public sealed class FailingLoadSceneRoot : ASceneRoot
        {
            public override UniTask OnSceneLoaded(
                bool isReusing,
                CancellationToken cancellationToken) =>
                throw new InvalidOperationException("Expected load failure.");
        }

        public sealed class TestService
        {
            public TestService(string source) => Source = source;
            public string Source { get; }
        }

        public sealed class RootOnlyService
        {
        }

        public sealed class TestMover : AMover
        {
            public void Finish() => CompleteMovement();
        }

        public sealed class ButtonReferenceHolder : MonoBehaviour
        {
            [SerializeField] private Button button;

            public Button Button
            {
                get => button;
                set => button = value;
            }
        }

        public sealed class TestRootScope : ARootScope
        {
        }

        public sealed class TestBootstrap : ABootstrap<TestRootScope>
        {
            protected override UniTask OnBootCompleted(CancellationToken cancellationToken)
            {
                return UniTask.CompletedTask;
            }
        }

        public sealed class TestBootstrapTask : ABootstrapTask<TestRootScope>
        {
            public float ReportedProgress { get; set; }
            public string Label { get; set; } = "task";
            public List<string> Log { get; set; }

            public Type[] RequiredTypes { get; set; } = Array.Empty<Type>();
            public Type[] ProvidedTypes { get; set; } = Array.Empty<Type>();

            /// <summary>Signalled as soon as this task starts, to unblock a sibling's Wait.</summary>
            public UniTaskCompletionSource Release { get; set; }

            /// <summary>Blocks this task until a sibling starts — only reachable if they overlap.</summary>
            public UniTaskCompletionSource Wait { get; set; }

            public override Type[] Requires => RequiredTypes;
            public override Type[] Provides => ProvidedTypes;

            public override async UniTask Execute(
                TestRootScope scope,
                IProgress<float> progress,
                CancellationToken cancellationToken)
            {
                Log?.Add($"{Label} start");
                Release?.TrySetResult();
                if (Wait != null) await Wait.Task;

                progress.Report(ReportedProgress);
                Log?.Add($"{Label} end");
            }
        }

        /// <summary>Stand-ins for whatever a real task hands the scope.</summary>
        private sealed class ProvidedByFirst
        {
        }

        private sealed class ProvidedBySecond
        {
        }

        /// <summary>Target the scopes configure — the scene one refines what the root set.</summary>
        public class ServiceTarget
        {
            public TestService Service { get; set; }
            public RootOnlyService RootOnly { get; set; }
        }

        private sealed class TestParentScope :
            IScope,
            IDependencyResolvable<ServiceTarget>
        {
            private readonly TestService _service = new("root");
            private readonly RootOnlyService _rootOnlyService = new();
            private DependencyInjector _injector;

            public DependencyInjector Injector => _injector ??= new DependencyInjector(this);

            public void ResolveDependenciesFor(ServiceTarget target)
            {
                target.Service = _service;
                target.RootOnly = _rootOnlyService;
            }
        }

        private sealed class FailingTransition : ISceneTransition
        {
            private readonly ICollection<string> _phases;

            public FailingTransition(ICollection<string> phases) => _phases = phases;

            public UniTask BeginAsync(CancellationToken cancellationToken)
            {
                _phases.Add("begin");
                return UniTask.CompletedTask;
            }

            public UniTask ExecuteAsync(CancellationToken cancellationToken)
            {
                _phases.Add("execute");
                throw new InvalidOperationException("Expected test failure.");
            }

            public UniTask EndAsync(CancellationToken cancellationToken)
            {
                _phases.Add("end");
                return UniTask.CompletedTask;
            }
        }

        private sealed class DoublyFailingTransition : ISceneTransition
        {
            public UniTask BeginAsync(CancellationToken cancellationToken) =>
                UniTask.CompletedTask;

            public UniTask ExecuteAsync(CancellationToken cancellationToken) =>
                throw new InvalidOperationException("operation");

            public UniTask EndAsync(CancellationToken cancellationToken) =>
                throw new InvalidOperationException("cleanup");
        }

        private sealed class BlockingTransition : ISceneTransition
        {
            private readonly UniTaskCompletionSource _completion = new();

            public bool IsExecuting { get; private set; }

            public UniTask BeginAsync(CancellationToken cancellationToken) =>
                UniTask.CompletedTask;

            public UniTask ExecuteAsync(CancellationToken cancellationToken)
            {
                IsExecuting = true;
                return _completion.Task;
            }

            public UniTask EndAsync(CancellationToken cancellationToken) =>
                UniTask.CompletedTask;

            public void Complete() => _completion.TrySetResult();
        }

        private sealed class NoOpTransition : ISceneTransition
        {
            public UniTask BeginAsync(CancellationToken cancellationToken) =>
                UniTask.CompletedTask;

            public UniTask ExecuteAsync(CancellationToken cancellationToken) =>
                UniTask.CompletedTask;

            public UniTask EndAsync(CancellationToken cancellationToken) =>
                UniTask.CompletedTask;
        }

        private sealed class FakeSceneLease : ISceneLease
        {
            public bool FailUnload { get; set; }
            public int UnloadCount { get; private set; }
            public bool HasOwnership { get; private set; }

            public void Adopt(Scene scene) => HasOwnership = true;

            public void Track(Scene scene)
            {
            }

            public UniTask LoadAsync(
                string sceneName,
                string addressablePath,
                bool useAddressable)
            {
                HasOwnership = true;
                return UniTask.CompletedTask;
            }

            public UniTask UnloadAsync(
                string sceneName,
                string addressablePath,
                bool useAddressable)
            {
                UnloadCount++;
                if (FailUnload)
                    throw new InvalidOperationException("Expected unload failure.");

                HasOwnership = false;
                return UniTask.CompletedTask;
            }
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
                throw new MissingFieldException(target.GetType().Name, fieldName);

            field.SetValue(target, value);
        }

        private static TSceneRoot AddPendingSceneRoot<TSceneRoot>(GameObject gameObject)
            where TSceneRoot : ASceneRoot
        {
            var sceneRoot = gameObject.AddComponent<TSceneRoot>();
            ASceneRoot.ReturnPending(sceneRoot);
            return sceneRoot;
        }

        private static void SetPrivateField<T>(
            Type ownerType,
            object target,
            string fieldName,
            T value)
        {
            var field = ownerType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
                throw new MissingFieldException(ownerType.Name, fieldName);

            field.SetValue(target, value);
        }
    }
}
