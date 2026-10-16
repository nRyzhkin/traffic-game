using System;
using TrafficGame.Presentation.Cameras;
using TrafficGame.Presentation.Localization;
using TrafficGame.Presentation.Settings;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    /// <summary>
    /// Owns the UI Toolkit document: creates the views that live in it, scales it like a uGUI canvas with a reference
    /// resolution in "expand" mode (the reference rectangle always fits on screen; the extra space goes to the longer
    /// side), picking the landscape or portrait reference by aspect ratio. It also applies the light or dark theme,
    /// keeps the HUD inside the screen's safe area and tells map input when the pointer is over the UI.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameUi : MonoBehaviour, IUiHitTest, IThemeView, IMapViewport
    {
        private const string PortraitClass = "portrait";
        private const string DarkClass = "theme-dark";

        /// <summary>How long the curtain takes to cover and to uncover the map (<c>.curtain</c> in Game.uss).</summary>
        private const long CurtainMilliseconds = 200;
        private const long CurtainLiftMilliseconds = 450;

        [Tooltip("UI size the landscape layout is designed for, in UI units.")]
        [SerializeField] private Vector2 _landscapeReference = new(1280f, 720f);
        [Tooltip("UI size the portrait layout is designed for, in UI units.")]
        [SerializeField] private Vector2 _portraitReference = new(390f, 844f);

        // How far the in-level HUD reaches in from the top and bottom edge, in UI units (Game.uss: top bar and goal card;
        // hint, actions and tool bar). The camera keeps what matters out from under it.
        [SerializeField] private Vector2 _landscapeHudTopBottom = new(110f, 150f);
        [SerializeField] private Vector2 _portraitHudTopBottom = new(156f, 210f);

        private PanelSettings _panelSettings;
        private bool? _portrait;
        private VisualElement _root;
        private VisualElement _layoutRoot;
        private VisualElement _safeArea;
        private VisualElement _hud;
        private VisualElement _curtain;
        private Rect _appliedSafeArea;

        public HudView Hud { get; private set; }
        public TooltipView Tooltip { get; private set; }
        public ToolbarView Toolbar { get; private set; }
        public RoadProfileBarView ProfileBar { get; private set; }
        public ElevationView Elevation { get; private set; }
        public SimulationControlsView SimulationControls { get; private set; }
        public ObjectiveView Objective { get; private set; }
        public IntroView Intro { get; private set; }
        public ResultView Result { get; private set; }
        public MenuView Menu { get; private set; }
        public ThemeControlView ThemeControl { get; private set; }

        /// <summary>Screen pixels per UI unit with the current reference resolution.</summary>
        public float ScreenPixelsPerUnit
        {
            get
            {
                var reference = IsPortraitScreen ? _portraitReference : _landscapeReference;
                return Mathf.Min(Screen.width / reference.x, Screen.height / reference.y);
            }
        }

        private static bool IsPortraitScreen => Screen.width < Screen.height;

        /// <summary>The part of the screen the in-level HUD leaves free for the map, safe area included.</summary>
        public ScreenInsets MapInsets
        {
            get
            {
                var hud = (IsPortraitScreen ? _portraitHudTopBottom : _landscapeHudTopBottom) * ScreenPixelsPerUnit;
                var safe = Screen.safeArea;
                return new ScreenInsets(hud.x + Screen.height - safe.yMax, hud.y + safe.yMin, safe.xMin, Screen.width - safe.xMax);
            }
        }

        private void Awake()
        {
            // A copy, so switching the reference at runtime never edits the asset.
            var document = GetComponent<UIDocument>();
            _panelSettings = Instantiate(document.panelSettings);
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.screenMatchMode = PanelScreenMatchMode.Expand;
            document.panelSettings = _panelSettings;
            ApplyReference();
        }

        public void Initialize(ILocalizer localizer, Camera worldCamera)
        {
            if (localizer == null)
                throw new ArgumentNullException(nameof(localizer));

            _root = GetComponent<UIDocument>().rootVisualElement;
            _layoutRoot = _root.Q("root");
            _safeArea = _root.Q("safe-area");
            _hud = _root.Q("hud");
            _curtain = _root.Q("curtain");

            Hud = new HudView(_root, localizer);
            Tooltip = new TooltipView(_root.Q("tooltip"), worldCamera, localizer);
            Toolbar = new ToolbarView(_root, localizer);
            ProfileBar = new RoadProfileBarView(_root, localizer);
            Elevation = new ElevationView(_root, localizer);
            SimulationControls = new SimulationControlsView(_root, localizer);
            Objective = new ObjectiveView(_root, localizer);
            var modal = new ModalLayer(_root.Q("modal"));
            Intro = new IntroView(_root, modal, localizer);
            Result = new ResultView(_root, modal, localizer);
            Menu = new MenuView(_root, localizer);
            ThemeControl = new ThemeControlView(_root.Q("theme-control"));

            _root.RegisterCallback<GeometryChangedEvent>(_ => ApplyLayout());
        }

        public void ApplyTheme(bool dark)
        {
            _layoutRoot ??= GetComponent<UIDocument>().rootVisualElement.Q("root");
            _layoutRoot?.EnableInClassList(DarkClass, dark);
        }

        /// <summary>Shows or hides the in-level HUD (money, speed, tools, hints); the menu hides it. Its bars slide in and out.</summary>
        public void SetHudVisible(bool visible) => Motion.SetShown(_hud, visible);

        /// <summary>
        /// Changes the scene behind a curtain: the map fades out, <paramref name="change"/> runs while it is covered and
        /// the new map fades in. Menus and dialogs stay above the curtain and animate on their own.
        /// </summary>
        public void ChangeScene(Action change)
        {
            if (change == null)
                throw new ArgumentNullException(nameof(change));

            Motion.Show(_curtain);
            _curtain.schedule.Execute(() =>
            {
                change();
                // Lift it from the next update on: building a level makes this frame long.
                _curtain.schedule.Execute(() => Motion.Hide(_curtain, milliseconds: CurtainLiftMilliseconds));
            }).StartingIn(CurtainMilliseconds);
        }

        /// <summary>Lifts the curtain the game starts behind.</summary>
        public void RevealScene() => Motion.Hide(_curtain, milliseconds: CurtainLiftMilliseconds);

        public bool IsOverUi(float2 screenPosition)
        {
            var panel = _root?.panel;
            if (panel == null)
                return false;

            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return panel.Pick(panelPosition) != null;
        }

        private void Update()
        {
            ApplyReference();

            // The safe area changes on rotation without necessarily resizing the panel.
            if (_root != null && Screen.safeArea != _appliedSafeArea)
                ApplyLayout();
        }

        /// <summary>Switches between the landscape and portrait reference resolution when the screen turns.</summary>
        private void ApplyReference()
        {
            var portrait = IsPortraitScreen;
            if (_portrait == portrait || _panelSettings == null)
                return;

            _portrait = portrait;
            var reference = portrait ? _portraitReference : _landscapeReference;
            _panelSettings.referenceResolution = new Vector2Int(Mathf.RoundToInt(reference.x), Mathf.RoundToInt(reference.y));
        }

        private void ApplyLayout()
        {
            ApplySafeArea();
            _layoutRoot.EnableInClassList(PortraitClass, IsPortraitScreen);
        }

        private void ApplySafeArea()
        {
            var panel = _root.panel;
            if (panel == null)
                return;

            _appliedSafeArea = Screen.safeArea;
            var topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(_appliedSafeArea.xMin, Screen.height - _appliedSafeArea.yMax));
            var bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(_appliedSafeArea.xMax, Screen.height - _appliedSafeArea.yMin));
            var size = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height));

            _safeArea.style.left = topLeft.x;
            _safeArea.style.top = topLeft.y;
            _safeArea.style.right = size.x - bottomRight.x;
            _safeArea.style.bottom = size.y - bottomRight.y;
        }

        private void OnDestroy()
        {
            Hud?.Dispose();
            Toolbar?.Dispose();
            ProfileBar?.Dispose();
            Elevation?.Dispose();
            SimulationControls?.Dispose();
            Objective?.Dispose();
        }
    }
}
