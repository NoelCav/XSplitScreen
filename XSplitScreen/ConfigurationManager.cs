using DoDad.Library.AI;
using RoR2.UI;
using RoR2.UI.MainMenu;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace XSplitScreen
{
    // TODO
    // create buttons to switch between different pages
    // state machine no longer needed due to defunct logic
    class ConfigurationManager : MonoBehaviour
    {
        #region Variables
        public static ConfigurationManager instance;

        public GameObject basePageObjectPrefab { get; private set; }

        public StateMachine stateMachine { get; private set; }
        #endregion

        #region Unity Methods
        public void Awake()
        {
            if (instance)
                Destroy(gameObject);

            instance = this;

            Initialize();
        }
        #endregion

        #region Initialization
        private void Initialize()
        {
            InitializeReferences();
            InitializeStateMachine();
        }
        private void InitializeReferences()
        {
            basePageObjectPrefab = transform.GetChild(1).gameObject;

            Destroy(basePageObjectPrefab.transform.GetChild(0).gameObject);
            Destroy(basePageObjectPrefab.transform.GetChild(1).gameObject);
            basePageObjectPrefab.name = "(Page) Prefab";
            basePageObjectPrefab.SetActive(false);
        }
        private void InitializeStateMachine()
        {
            Dictionary<State, BaseState> states = new Dictionary<State, BaseState>();

            states.Add(State.State1, new ControllerAssignmentState(gameObject));

            stateMachine = gameObject.AddComponent<StateMachine>();
            stateMachine.SetStates(states);
        }
        #endregion

        #region StateMachine Definitions
        public abstract class PageState : BaseState
        {
            public RectTransform page;

            public PageState(GameObject gameObject) : base(gameObject) { }
        }
        public class ControllerAssignmentState : PageState
        {
            #region Variables
            public RectTransform followerContainer { get; private set; }
            public ControllerIconManager controllerIcons { get; private set; }
            public AssignmentManager assignmentManager { get; private set; }

            public static int currentDisplay { get; private set; }

            private RectTransform toggleEnableMod;
            private RectTransform displayControl;

            private LanguageTextMeshController currentDisplayText;
            private GameObject leftArrow;
            private GameObject rightArrow;
            #endregion

            #region Base Methods
            public ControllerAssignmentState(GameObject gameObject) : base(gameObject)
            {
                this.gameObject = gameObject;
            }
            public override void Initialize()
            {
                InitializePage();
            }
            public override void Start()
            {
                page.gameObject.SetActive(true);
                gameObject.GetComponentInParent<UnityEngine.UI.CanvasScaler>().HandleConstantPhysicalSize();
                gameObject.GetComponentInParent<UnityEngine.UI.CanvasScaler>().HandleScaleWithScreenSize();
                UpdateToggle(XSplitScreen.configuration.enabled);
            }
            public override State Tick()
            {
                return State.NullState;
            }
            public override void Stop()
            {
                page.gameObject.SetActive(false);
            }
            public override void Exit()
            {
                base.Exit();
                Destroy(page.gameObject);
            }
            #endregion

            #region Initialization & Exit
            private void InitializePage()
            {
                currentDisplay = 0;

                page = Instantiate(instance.basePageObjectPrefab).GetComponent<RectTransform>();
                page.SetParent(gameObject.transform);
                page.name = "(Page) Controller Assignment";
                page.transform.localScale = Vector3.one;

                followerContainer = new GameObject("Follower Container", typeof(RectTransform)).GetComponent<RectTransform>();
                followerContainer.SetParent(page);

                controllerIcons = gameObject.AddComponent<ControllerIconManager>();

                assignmentManager = new GameObject("Assignment Manager", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement)).AddComponent<AssignmentManager>();

                //var element = graphManager.GetComponent<UnityEngine.UI.LayoutElement>();

                assignmentManager.transform.SetParent(page);
                assignmentManager.transform.localScale = Vector3.one;
                assignmentManager.transform.localPosition = Vector3.zero;
                assignmentManager.transform.SetSiblingIndex(4);
                assignmentManager.Initialize();

                Destroy(page.GetChild(5).gameObject);
                Destroy(page.GetChild(6).GetChild(0).gameObject);

                //GameObject leftArrowPrefab = .gameObject;
                //GameObject rightArrowPrefab = GameObject.Find("Right Arrow");

                displayControl = new GameObject("(Container) Display Control", typeof(RectTransform)).GetComponent<RectTransform>();
                displayControl.SetParent(page.GetChild(6));

                displayControl.transform.localPosition = new Vector3(0, 71.5f, 0);
                displayControl.transform.localScale = Vector3.one;

                // TODO find the location of the sprite and get rid of this
                GameObject rightArrowPrefab = ((SubmenuMainMenuScreen)MainMenuController.instance.settingsMenuScreen).submenuPanelPrefab.transform.GetChild(3).GetChild(2).GetChild(5).GetChild(0).GetChild(2).GetChild(0).GetChild(3).GetChild(3).GetChild(1).gameObject;
                rightArrow = Instantiate(rightArrowPrefab, displayControl.transform);

                GameObject leftArrowPrefab = rightArrowPrefab.transform.parent.GetChild(2).gameObject;
                leftArrow = Instantiate(leftArrowPrefab, displayControl.transform);

                rightArrow.GetComponentInChildren<HGButton>().gameObject.AddComponent<XButtonConverter>();
                rightArrow.transform.localPosition = new Vector3(100f, 0, 0);

                XButtonConverter rightConverter = rightArrow.GetComponent<XButtonConverter>();

                rightConverter.Initialize(0);
                rightConverter.onClickMono.AddListener(OnChangeDisplay);
                rightConverter.interactable = Display.displays.Length > 1;

                leftArrow.GetComponentInChildren<HGButton>().gameObject.AddComponent<XButtonConverter>();
                leftArrow.transform.localPosition = new Vector3(-100f, 0, 0);

                XButtonConverter leftConverter = leftArrow.GetComponent<XButtonConverter>();

                leftConverter.Initialize(0);
                leftConverter.onClickMono.AddListener(OnChangeDisplay);
                leftConverter.interactable = false;

                GameObject controlTextPrefab = rightArrowPrefab.transform.parent.GetChild(3).gameObject;
                GameObject controlText = Instantiate(controlTextPrefab, displayControl.transform);
                controlText.transform.localPosition = new Vector3(0, 65f, 0);

                currentDisplayText = controlText.GetComponent<LanguageTextMeshController>();
                UpdateDisplayText();

                GameObject togglePrefab = MainMenuController.instance.multiplayerMenuScreen.GetComponentInChildren<MPToggle>(true).gameObject;

                toggleEnableMod = new GameObject($"(Toggle) Enable Splitscreen", typeof(RectTransform)).GetComponent<RectTransform>();
                toggleEnableMod.transform.SetParent(page.GetChild(6));
                toggleEnableMod.transform.localPosition = Vector3.zero;
                toggleEnableMod.transform.localScale = Vector3.one;

                GameObject toggle = Instantiate(togglePrefab, toggleEnableMod.transform);
                toggle.name = "(Toggle) Control";
                toggle.transform.localPosition = new Vector3(-60,0,0);
                toggle.transform.localScale = Vector3.one * 1.5f;
                toggle.SetActive(true);
                toggle.GetComponent<MPToggle>().isOn = XSplitScreen.configuration.enabled;
                toggle.GetComponent<MPToggle>().onValueChanged.AddListener(OnToggleEnableMod);

                GameObject label = Instantiate(togglePrefab.transform.parent.GetChild(1).gameObject);
                label.transform.SetParent(toggleEnableMod.transform);
                label.transform.localPosition = Vector3.zero;
                label.transform.localScale = Vector3.one;
                label.name = "(TextMesh) Label";

                UpdateToggle(XSplitScreen.configuration.enabled);

                page.gameObject.SetActive(false);
            }
            #endregion

            #region Event Listeners
            public void OnToggleEnableMod(bool status)
            {
                bool success = XSplitScreen.configuration.SetEnabled(status);

                UpdateToggle(XSplitScreen.configuration.enabled);

                if (success)
                {
                    Log.LogDebug($"Activated");
                }
                else
                {
                    Log.LogDebug($"Not activated");
                }

                // if SetEnabled is false then the configuration is invalid
                // ping incomplete UI elements
            }
            public void OnChangeDisplay(MonoBehaviour mono)
            {
                return;

                int direction = mono.name.Contains("Right") ? 1 : -1;

                int display = Mathf.Clamp(direction + currentDisplay, 0, Display.displays.Length - 1);

                if (display == currentDisplay)
                    return;

                currentDisplay = display;

                UpdateDisplayText();
                UpdateDisplayArrows();

                assignmentManager.OnUpdateDisplay();
                // -1 or 1 to cycle
                // disable arrows
                // send event to assignmentmanager
            }
            #endregion

            #region UI
            private void UpdateToggle(bool status)
            {
                toggleEnableMod.transform.GetChild(0).GetComponent<MPToggle>().onValueChanged.RemoveAllListeners();
                toggleEnableMod.transform.GetChild(0).GetComponent<MPToggle>().isOn = status;
                toggleEnableMod.transform.GetChild(0).GetComponent<MPToggle>().onValueChanged.AddListener(OnToggleEnableMod);

                LanguageTextMeshController controllerEnableMod = toggleEnableMod.transform.GetChild(1).GetComponent<LanguageTextMeshController>();

                if (status)
                {
                    controllerEnableMod.token = XSplitScreen.Language.MSG_SPLITSCREEN_DISABLE_TOKEN;
                }
                else
                {
                    controllerEnableMod.token = XSplitScreen.Language.MSG_SPLITSCREEN_ENABLE_TOKEN;
                }

            }
            private void UpdateDisplayText()
            {
                currentDisplayText.token = (currentDisplay + 1).ToString();
            }
            private void UpdateDisplayArrows()
            {
                XButton leftButton = leftArrow.GetComponent<XButton>();
                XButton rightButton = rightArrow.GetComponent<XButton>();

                leftButton.interactable = true;
                //rightButton.interactable = true;

                if (currentDisplay == 0)
                {
                    leftButton.interactable = false;
                }
                else if (currentDisplay >= Display.displays.Length - 1)
                {
                    rightButton.interactable = false;
                }
            }
            #endregion
        }
        #endregion
    }
}