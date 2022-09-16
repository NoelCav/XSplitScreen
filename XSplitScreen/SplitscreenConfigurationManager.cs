using DoDad;
using Rewired;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using DoDad.Library.AI;
using DoDad.Library.UI;

namespace DoDad.UI.Components
{
    public class SplitscreenConfigurationManager : UnityEngine.MonoBehaviour
    {
        public static SplitscreenConfigurationManager instance;

        private static readonly string MSG_TAG = "[XSplitScreen.Components.ControllerAssignmentManager] {0}";

        public GameObject basePageObjectPrefab 
        { 
            get
            {
                return _basePageObjectPrefab;
            }
        }

        private GameObject _basePageObjectPrefab;

        private StateMachine stateMachine;
        // TODO
        // Must create page switching before moving on with creation of DisplayManager

        #region Unity Methods
        public void Awake()
        {
            if (instance)
                Destroy(gameObject);

            instance = this;

            _basePageObjectPrefab = transform.GetChild(0).gameObject;
            Destroy(basePageObjectPrefab.transform.GetChild(0).gameObject);
            Destroy(basePageObjectPrefab.transform.GetChild(1).gameObject);

            basePageObjectPrefab.SetActive(false);

            InitializeStateMachine();

            ReadyButton.Preset_AllowAllEventSystems = true;
            ReadyButton.ApplyStaticPresets = true;
        }
        #endregion

        #region Initialization
        private void InitializeStateMachine()
        {
            Dictionary<State, BaseState> states = new Dictionary<State, BaseState>();

            states.Add(State.State1, new ControllerAssignmentState(gameObject));

            stateMachine = gameObject.AddComponent<StateMachine>();
            stateMachine.SetStates(states);
        }
        #endregion

        #region Helpers
        private static void Print(object msg, Log.LogLevel level = Log.LogLevel.UnityDebug)
        {
            msg = level != Log.LogLevel.UnityDebug ? msg : string.Format(MSG_TAG, msg);

            switch (level)
            {
                case Log.LogLevel.Error:
                    Log.LogError(msg);
                    break;
                case Log.LogLevel.Fatal:
                    Log.LogFatal(msg);
                    break;
                case Log.LogLevel.Info:
                    Log.LogInfo(msg);
                    break;
                case Log.LogLevel.Message:
                    Log.LogMessage(msg);
                    break;
                case Log.LogLevel.Warning:
                    Log.LogWarning(msg);
                    break;
                case Log.LogLevel.Debug:
                    Log.LogDebug(msg);
                    break;
                default:
                    Debug.Log(msg);
                    break;
            }
        }
        #endregion

        #region Definitions
        #region BaseStates
        public class ControllerAssignmentState : BaseState
        {
            // TODO Create the DisplayManager and finished controller assignment
            #region Variables
            public static DisplayManager displayManager;

            public static RectTransform followerContainer;

            private Sprite sprite_Dinput;
            private Sprite sprite_Xinput;
            private Sprite sprite_Keyboard;
            private Sprite sprite_Unknown;
            private Sprite sprite_Checkmark;
            private Sprite sprite_Xmark;

            private Texture2D texture_Dinput;
            private Texture2D texture_Xinput;
            private Texture2D texture_Keyboard;
            private Texture2D texture_Unknown;
            private Texture2D texture_Checkmark;
            private Texture2D texture_Xmark;

            private RectTransform controllerIconContainer;

            private GameObject controllerIconPrefab;
            private GameObject page;
            #endregion

            #region Base Methods
            public ControllerAssignmentState(GameObject gameObject) : base(gameObject)
            {
            }
            public override void Start()
            {
                page.SetActive(true);
                gameObject.GetComponentInParent<UnityEngine.UI.CanvasScaler>().HandleConstantPhysicalSize();
                gameObject.GetComponentInParent<UnityEngine.UI.CanvasScaler>().HandleScaleWithScreenSize();
            }
            public override void Stop()
            {
                page.SetActive(false);
            }
            public override State Tick()
            {
                return State.NoStateChange;
            }
            public override void Initialize()
            {
                InitializeReferences();
                InitializePage();
                BuildIconPrefab();
                InitializeIcons();
                AddListeners();
            }
            public override void Exit()
            {
                DestroyReferences();
                DestroyIcons(); // Is this really necessary?
            }
            #endregion

            #region Initialization & Exit
            private void InitializeReferences()
            {
                texture_Dinput = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/dinput.png");
                texture_Xinput = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/xinput.png");
                texture_Keyboard = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/keyboardmouse.png");
                texture_Unknown = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/unknown.png");
                texture_Checkmark = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/checkmark.png");
                texture_Xmark = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/xmark.png");

                sprite_Dinput = Sprite.Create(texture_Dinput, new Rect(Vector2.zero, new Vector2(texture_Dinput.width, texture_Dinput.height)), Vector2.zero);
                sprite_Xinput = Sprite.Create(texture_Xinput, new Rect(Vector2.zero, new Vector2(texture_Xinput.width, texture_Xinput.height)), Vector2.zero);
                sprite_Keyboard = Sprite.Create(texture_Keyboard, new Rect(Vector2.zero, new Vector2(texture_Keyboard.width, texture_Keyboard.height)), Vector2.zero);
                sprite_Unknown = Sprite.Create(texture_Unknown, new Rect(Vector2.zero, new Vector2(texture_Unknown.width, texture_Unknown.height)), Vector2.zero);
                sprite_Checkmark = Sprite.Create(texture_Checkmark, new Rect(Vector2.zero, new Vector2(texture_Checkmark.width, texture_Checkmark.height)), Vector2.zero);
                sprite_Xmark = Sprite.Create(texture_Xmark, new Rect(Vector2.zero, new Vector2(texture_Xmark.width, texture_Xmark.height)), Vector2.zero);
            }
            private void InitializePage()
            {
                page = Instantiate(SplitscreenConfigurationManager.instance.basePageObjectPrefab);
                page.transform.SetParent(gameObject.transform);
                page.name = "Controller Assignment Page"; // TODO use language token?
                page.transform.localScale = Vector3.one;
                // Create controller icon container

                controllerIconContainer = Instantiate(page.transform.GetChild(2).gameObject).GetComponent<RectTransform>();
                controllerIconContainer.name = "Controller Icon Container";
                controllerIconContainer.SetParent(page.transform);
                controllerIconContainer.SetSiblingIndex(3);  // 3
                controllerIconContainer.localScale = Vector3.one;

                Destroy(controllerIconContainer.transform.GetChild(0).gameObject);
                Destroy(controllerIconContainer.transform.GetChild(1).gameObject);

                controllerIconContainer.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();

                displayManager = page.GetComponentInChildren<UserProfileListController>().gameObject.AddComponent<DisplayManager>();
                displayManager.name = "Display Manager";

                Destroy(displayManager.GetComponent<UserProfileListController>());

                followerContainer = new GameObject("Follower Container", typeof(RectTransform)).GetComponent<RectTransform>();
                followerContainer.transform.SetParent(transform);

                page.SetActive(false);
            }
            private void BuildIconPrefab()
            {
                // Shouldn't all of this be inside ControllerIcon initialization?
                controllerIconPrefab = Utils.CreateImage("Controller Icon Prefab");
                controllerIconPrefab.transform.SetParent(transform);
                controllerIconPrefab.AddComponent<ReadyButton>();

                ControllerIcon icon = controllerIconPrefab.AddComponent<ControllerIcon>();
                icon.enabled = false;

                Image prefabImage = controllerIconPrefab.GetComponent<Image>();
                prefabImage.color = Vector4.zero;
                prefabImage.sprite = Instantiate(sprite_Unknown);
                prefabImage.SetNativeSize();
                prefabImage.enabled = true;

                icon.checkMark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
                icon.checkMark.transform.SetParent(controllerIconPrefab.transform);
                icon.checkMark.transform.localScale = Vector3.one / 1.5f;

                icon.xMark = new GameObject("XMark", typeof(RectTransform), typeof(Image));
                icon.xMark.transform.SetParent(controllerIconPrefab.transform);
                icon.xMark.transform.localScale = Vector3.one / 1.5f;

                Image checkmarkImage = icon.checkMark.GetComponent<Image>();
                checkmarkImage.sprite = Instantiate(sprite_Checkmark);
                checkmarkImage.SetNativeSize();
                checkmarkImage.raycastTarget = false;

                Image xmarkImage = icon.xMark.GetComponent<Image>();
                xmarkImage.sprite = Instantiate(sprite_Xmark);
                xmarkImage.SetNativeSize();
                xmarkImage.raycastTarget = false;

                icon.SetAssignmentStatus(ControllerIcon.AssignmentStatus.Unassigned);
            }
            private void AddListeners()
            {
                XSplitScreen.Configuration.OnSplitscreenDeviceConnected.AddListener(OnControllerAssigned);
            }
            private void InitializeIcons()
            {
                foreach (XSplitScreen.SplitScreenConfiguration.ControllerAssignment assignment in XSplitScreen.Configuration.ControllerAssignments)
                {
                    foreach (Controller controller in ReInput.controllers.Controllers)
                    {
                        if (controller.type == ControllerType.Mouse)
                            continue;

                        if (controller.id == assignment.deviceId && assignment.isKeyboard == (controller.type == ControllerType.Keyboard))
                        {
                            CreateIcon(assignment, controller);
                            break;
                        }
                    }
                }
            }
            private void DestroyReferences()
            {
                Destroy(sprite_Dinput);
                Destroy(sprite_Xinput);
                Destroy(sprite_Keyboard);
                Destroy(sprite_Unknown);
                Destroy(sprite_Checkmark);
                Destroy(sprite_Xmark);
            }
            private void DestroyIcons()
            {
                foreach (ControllerIcon icon in controllerIconContainer.GetComponentsInChildren<ControllerIcon>())
                {
                    Destroy(icon.gameObject);
                }
            }
            #endregion

            #region Icons
            private ControllerIcon CreateIcon(XSplitScreen.SplitScreenConfiguration.ControllerAssignment assignment, Controller controller)
            {
                Vector3 scale = Vector3.one / 2f;

                ControllerIcon icon = Instantiate(controllerIconPrefab).GetComponent<ControllerIcon>();

                icon.gameObject.name = "Controller Icon";
                icon.transform.SetParent(controllerIconContainer);
                icon.transform.localScale = scale;
                icon.controller = controller;

                Image image = icon.GetComponent<Image>();

                Sprite spriteType = sprite_Unknown;

                if (controller.name.ToLower().Contains("xinput") || controller.name.ToLower().Contains("xbox"))
                    spriteType = sprite_Xinput;
                if (controller.name.ToLower().Contains("sony") || controller.name.ToLower().Contains("dinput"))
                    spriteType = sprite_Dinput;
                if (controller.type == ControllerType.Keyboard)
                    spriteType = sprite_Keyboard;

                image.color = Vector4.zero;
                image.sprite = Instantiate(spriteType);
                image.SetNativeSize();
                image.enabled = true;

                icon.enabled = true;

                if (assignment.displayId > -1)
                    icon.SetAssignmentStatus(ControllerIcon.AssignmentStatus.Assigned);

                // Notify DisplayManager of new assignment

                return icon;
            }
            #endregion

            #region Events
            public void OnControllerAssigned(Controller newController)
            {
                if (newController.type == ControllerType.Mouse)
                    return;

                foreach (XSplitScreen.SplitScreenConfiguration.ControllerAssignment assignment in XSplitScreen.Configuration.ControllerAssignments)
                {
                    if (newController.id == assignment.deviceId && assignment.isKeyboard == (newController.type == ControllerType.Keyboard))
                    {
                        CreateIcon(assignment, newController);
                    }
                }
            }
            #endregion
        }
        #endregion
        public enum ConfigurationPage
        {
            ControllerAssignment,
            ProfileAssignment,
            SettingsAssignment,
        }
        #endregion
    }
}
