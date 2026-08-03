using UnityEngine;
using UnityEngine.Serialization;

namespace Hlight.Foundation
{
    /// <summary>
    /// Fits this object's <see cref="RectTransform"/> anchors inside the current
    /// screen safe area, with optional per-axis and top-inset adjustments.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("UI/Safe Area Fitter")]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        public enum SimulationDevice
        {
            None,
            IPhoneX,
            IPhoneXsMax,
            Pixel3XLLandscapeLeft,
            Pixel3XLLandscapeRight
        }

        /// <summary>
        /// Safe-area profile used while running in the Unity Editor.
        /// This value has no effect in a player build.
        /// </summary>
        public static SimulationDevice EditorSimulationDevice { get; set; }
            = SimulationDevice.None;

        [Header("Safe Area Axes")]
        [SerializeField]
        [FormerlySerializedAs("ConformX")]
        [Tooltip("Apply the safe-area insets on the horizontal axis.")]
        private bool _applyHorizontalInsets = true;

        [SerializeField]
        [FormerlySerializedAs("ConformY")]
        [Tooltip("Apply the safe-area insets on the vertical axis.")]
        private bool _applyVerticalInsets = true;

        [SerializeField]
        [FormerlySerializedAs("NotSetAnchorMin")]
        [Tooltip("Keep the current Anchor Min value and update only Anchor Max. Enable this only when another layout rule controls Anchor Min.")]
        private bool _preserveAnchorMin = true;

        [Header("Top Inset")]
        [SerializeField]
        [FormerlySerializedAs("CustomTop")]
        [Tooltip("Reduce the top safe-area inset instead of applying it completely.")]
        private bool _reduceTopInset;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Fraction of the top inset to ignore. 0 keeps the full inset; 1 ignores it completely. The default is 0.5.")]
        private float _topInsetReduction = 0.5f;

        [Header("Diagnostics")]
        [SerializeField]
        [FormerlySerializedAs("Logging")]
        [Tooltip("Log every safe-area change and any rejected invalid screen state.")]
        private bool _logChanges;

        private RectTransform _rectTransform;
        private DrivenRectTransformTracker _drivenTracker;

        private Rect _lastSafeAreaPixels;
        private Vector2Int _lastScreenSize;
        private ScreenOrientation _lastOrientation;
        private bool _hasAppliedState;
        private bool _refreshRequested = true;
        private bool _hasLoggedInvalidState;

    #if UNITY_EDITOR
        private static readonly SimulationProfile IPhoneXProfile = new SimulationProfile(
            portrait: new Rect(0f, 102f / 2436f, 1f, 2202f / 2436f),
            landscape: new Rect(
                132f / 2436f,
                63f / 1125f,
                2172f / 2436f,
                1062f / 1125f));

        private static readonly SimulationProfile IPhoneXsMaxProfile = new SimulationProfile(
            portrait: new Rect(0f, 102f / 2688f, 1f, 2454f / 2688f),
            landscape: new Rect(
                132f / 2688f,
                63f / 1242f,
                2424f / 2688f,
                1179f / 1242f));

        private static readonly SimulationProfile Pixel3XLLandscapeLeftProfile = new SimulationProfile(
            portrait: new Rect(0f, 0f, 1f, 2789f / 2960f),
            landscape: new Rect(0f, 0f, 2789f / 2960f, 1f));

        private static readonly SimulationProfile Pixel3XLLandscapeRightProfile = new SimulationProfile(
            portrait: new Rect(0f, 0f, 1f, 2789f / 2960f),
            landscape: new Rect(171f / 2960f, 0f, 2789f / 2960f, 1f));
    #endif

        private void OnEnable()
        {
            if (!TryGetComponent(out _rectTransform))
            {
                Debug.LogError(
                    $"{nameof(SafeAreaFitter)} requires a {nameof(RectTransform)} on '{name}'.",
                    this);

                enabled = false;
                return;
            }

            RequestRefresh();
            RefreshIfNeeded();
        }

        private void Update()
        {
            RefreshIfNeeded();
        }

        private void OnDisable()
        {
            _drivenTracker.Clear();
        }

    #if UNITY_EDITOR
        private void OnValidate()
        {
            _topInsetReduction = Mathf.Clamp01(_topInsetReduction);
            RequestRefresh();
        }
    #endif

        /// <summary>
        /// Forces the component to read and apply the safe area immediately.
        /// </summary>
        [ContextMenu("Refresh Safe Area")]
        public void RefreshNow()
        {
            if (_rectTransform == null && !TryGetComponent(out _rectTransform))
            {
                return;
            }

            RequestRefresh();
            RefreshIfNeeded();
        }

        private void RequestRefresh()
        {
            _refreshRequested = true;
        }

        private void RefreshIfNeeded()
        {
            if (_rectTransform == null)
            {
                return;
            }

            var screenSize = new Vector2Int(Screen.width, Screen.height);
            ScreenOrientation orientation = Screen.orientation;
            Rect safeAreaPixels = GetSafeAreaPixels(screenSize);

            bool stateChanged = !_hasAppliedState
                                || _refreshRequested
                                || safeAreaPixels != _lastSafeAreaPixels
                                || screenSize != _lastScreenSize
                                || orientation != _lastOrientation;

            if (!stateChanged)
            {
                return;
            }

            if (!TryApplySafeArea(safeAreaPixels, screenSize))
            {
                // Keep retrying because some devices briefly report an invalid
                // screen size or safe area during startup and orientation changes.
                _refreshRequested = true;
                return;
            }

            _lastSafeAreaPixels = safeAreaPixels;
            _lastScreenSize = screenSize;
            _lastOrientation = orientation;
            _hasAppliedState = true;
            _refreshRequested = false;
            _hasLoggedInvalidState = false;
        }

        private static Rect GetSafeAreaPixels(Vector2Int screenSize)
        {
    #if UNITY_EDITOR
            if (EditorSimulationDevice != SimulationDevice.None
                && TryGetSimulationProfile(EditorSimulationDevice, out SimulationProfile profile))
            {
                Rect normalizedSafeArea = profile.GetSafeArea(screenSize.y >= screenSize.x);

                return new Rect(
                    normalizedSafeArea.x * screenSize.x,
                    normalizedSafeArea.y * screenSize.y,
                    normalizedSafeArea.width * screenSize.x,
                    normalizedSafeArea.height * screenSize.y);
            }
    #endif

            return Screen.safeArea;
        }

        private bool TryApplySafeArea(Rect safeAreaPixels, Vector2Int screenSize)
        {
            if (screenSize.x <= 0 || screenSize.y <= 0)
            {
                LogInvalidStateOnce(safeAreaPixels, screenSize, "Screen dimensions must be greater than zero.");
                return false;
            }

            Rect adjustedSafeArea = ApplyAxisSettings(safeAreaPixels, screenSize);

            Vector2 anchorMin = new Vector2(
                adjustedSafeArea.xMin / screenSize.x,
                adjustedSafeArea.yMin / screenSize.y);

            Vector2 anchorMax = new Vector2(
                adjustedSafeArea.xMax / screenSize.x,
                adjustedSafeArea.yMax / screenSize.y);

            if (_reduceTopInset)
            {
                anchorMax.y = Mathf.Lerp(anchorMax.y, 1f, _topInsetReduction);
            }

            if (!TrySanitizeAnchors(ref anchorMin, ref anchorMax))
            {
                LogInvalidStateOnce(safeAreaPixels, screenSize, "Calculated anchors are invalid.");
                return false;
            }

            DrivenTransformProperties drivenProperties = DrivenTransformProperties.AnchorMax;
            if (!_preserveAnchorMin)
            {
                drivenProperties |= DrivenTransformProperties.AnchorMin;
            }

            _drivenTracker.Clear();
            _drivenTracker.Add(this, _rectTransform, drivenProperties);

            if (!_preserveAnchorMin)
            {
                _rectTransform.anchorMin = anchorMin;
            }

            _rectTransform.anchorMax = anchorMax;

            if (_logChanges)
            {
                Debug.Log(
                    $"Applied safe area to '{name}'. "
                    + $"Pixels: {FormatRect(adjustedSafeArea)}; "
                    + $"Anchors: min={anchorMin}, max={anchorMax}; "
                    + $"Screen: {screenSize.x}x{screenSize.y}.",
                    this);
            }

            return true;
        }

        private Rect ApplyAxisSettings(Rect safeAreaPixels, Vector2Int screenSize)
        {
            if (!_applyHorizontalInsets)
            {
                safeAreaPixels.x = 0f;
                safeAreaPixels.width = screenSize.x;
            }

            if (!_applyVerticalInsets)
            {
                safeAreaPixels.y = 0f;
                safeAreaPixels.height = screenSize.y;
            }

            return safeAreaPixels;
        }

        private static bool TrySanitizeAnchors(ref Vector2 anchorMin, ref Vector2 anchorMax)
        {
            if (!IsFinite(anchorMin) || !IsFinite(anchorMax))
            {
                return false;
            }

            anchorMin.x = Mathf.Clamp01(anchorMin.x);
            anchorMin.y = Mathf.Clamp01(anchorMin.y);
            anchorMax.x = Mathf.Clamp01(anchorMax.x);
            anchorMax.y = Mathf.Clamp01(anchorMax.y);

            return anchorMin.x <= anchorMax.x && anchorMin.y <= anchorMax.y;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                   && !float.IsInfinity(value.x)
                   && !float.IsNaN(value.y)
                   && !float.IsInfinity(value.y);
        }

        private void LogInvalidStateOnce(Rect safeAreaPixels, Vector2Int screenSize, string reason)
        {
            if (!_logChanges || _hasLoggedInvalidState)
            {
                return;
            }

            _hasLoggedInvalidState = true;
            Debug.LogWarning(
                $"{nameof(SafeAreaFitter)} skipped an invalid screen state on '{name}'. "
                + $"Reason: {reason} Safe area: {FormatRect(safeAreaPixels)}; "
                + $"Screen: {screenSize.x}x{screenSize.y}.",
                this);
        }

        private static string FormatRect(Rect rect)
        {
            return $"x={rect.x:0.##}, y={rect.y:0.##}, w={rect.width:0.##}, h={rect.height:0.##}";
        }

    #if UNITY_EDITOR
        private static bool TryGetSimulationProfile(
            SimulationDevice device,
            out SimulationProfile profile)
        {
            switch (device)
            {
                case SimulationDevice.IPhoneX:
                    profile = IPhoneXProfile;
                    return true;

                case SimulationDevice.IPhoneXsMax:
                    profile = IPhoneXsMaxProfile;
                    return true;

                case SimulationDevice.Pixel3XLLandscapeLeft:
                    profile = Pixel3XLLandscapeLeftProfile;
                    return true;

                case SimulationDevice.Pixel3XLLandscapeRight:
                    profile = Pixel3XLLandscapeRightProfile;
                    return true;

                default:
                    profile = default;
                    return false;
            }
        }

        private readonly struct SimulationProfile
        {
            private readonly Rect _portraitSafeArea;
            private readonly Rect _landscapeSafeArea;

            public SimulationProfile(Rect portrait, Rect landscape)
            {
                _portraitSafeArea = portrait;
                _landscapeSafeArea = landscape;
            }

            public Rect GetSafeArea(bool isPortrait)
            {
                return isPortrait ? _portraitSafeArea : _landscapeSafeArea;
            }
        }
    #endif
    }
}
