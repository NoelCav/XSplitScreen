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
            // this:
            // UI to select available monitor

            public RectTransform followerContainer { get; private set; }
            public ControllerIconManager controllerIcons { get; private set; }
            public AssignmentManager assignmentManager { get; private set; }

            public static int currentDisplay { get; private set; }

            private RectTransform toggleEnableMod;
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

                //graphManager = page.GetComponentInChildren<UserProfileListController>().gameObject.AddComponent<GraphManager>();

                //Destroy(graphManager.GetComponent<UserProfileListController>());
                //Destroy(page.GetComponentInChildren<UserProfileListController>().gameObject);
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
            #endregion
        }
        #endregion
    }
}