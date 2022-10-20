using DoDad.Library.AI;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace XSplitScreen
{
    // TODO
    // create buttons to switch between different pages
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
            public GraphManager graphManager { get; private set; }

            public int currentDisplay { get; private set; }
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
            #endregion

            #region Initialization & Exit
            private void InitializePage()
            {
                page = Instantiate(instance.basePageObjectPrefab).GetComponent<RectTransform>();
                page.SetParent(gameObject.transform);
                page.name = "(Page) Controller Assignment";
                page.transform.localScale = Vector3.one;

                followerContainer = new GameObject("Follower Container", typeof(RectTransform)).GetComponent<RectTransform>();
                followerContainer.SetParent(page);

                controllerIcons = gameObject.AddComponent<ControllerIconManager>();

                graphManager = new GameObject("Graph Manager", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement)).AddComponent<GraphManager>();

                //var element = graphManager.GetComponent<UnityEngine.UI.LayoutElement>();

                graphManager.transform.SetParent(page);
                graphManager.transform.localScale = Vector3.one;
                graphManager.transform.localPosition = Vector3.zero;
                graphManager.transform.SetSiblingIndex(4);
                graphManager.Initialize();
                Log.LogDebug($"Graph Manager created");

                Destroy(page.GetChild(5).gameObject);
                //graphManager = page.GetComponentInChildren<UserProfileListController>().gameObject.AddComponent<GraphManager>();

                //Destroy(graphManager.GetComponent<UserProfileListController>());
                //Destroy(page.GetComponentInChildren<UserProfileListController>().gameObject);

                page.gameObject.SetActive(false);
            }
            #endregion
        }
        #endregion
    }
}