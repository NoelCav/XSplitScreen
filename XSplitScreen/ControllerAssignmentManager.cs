using DoDad;
using Rewired;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DoDad.UI.Components
{
    class ControllerAssignmentManager : UnityEngine.MonoBehaviour
    {
        public static ControllerAssignmentManager Instance;

        public RectTransform displayManager;
        public RectTransform controllerDraggables;
        public RectTransform controllerFollowers;

        private static readonly string MSG_TAG = "[XSplitScreen.Components.ControllerAssignmentManager] {0}";

        private bool _initialized = false;

        private RectTransform _selectionPanel;
        //private RectTransform _controllerPanel;
        private RectTransform _contentArea;

        private Sprite _sprite_Dinput;
        private Sprite _sprite_Xinput;
        private Sprite _sprite_Keyboard;
        //private Sprite _sprite_Mouse;

        private Texture2D _icon_Dinput;
        private Texture2D _icon_Xinput;
        private Texture2D _icon_Keyboard;
        private Texture2D _icon_Mouse;

        private GameObject _controllerPrefab;

        private List<ImageFollower> _imageFollowers = new List<ImageFollower>();

        private RectTransform[] _configurationWindows;
        // TODO

        // OnControllerConnected create new draggable then send to parent

        #region Unity Methods
        void Awake()
        {
            if (Instance)
                Destroy(gameObject);

            Instance = this;
            _initialized = true;

            _selectionPanel = transform.GetChild(0).GetComponent<RectTransform>();
            _contentArea = _selectionPanel.transform.GetChild(3).GetComponent<RectTransform>();

            // Create controller area

            controllerDraggables = Instantiate(_selectionPanel.GetChild(2).gameObject).GetComponent<RectTransform>();
            controllerDraggables.name = "Controller Draggables";
            controllerDraggables.SetParent(_selectionPanel);
            controllerDraggables.SetSiblingIndex(3);  // 3
            controllerDraggables.localScale = Vector3.one;

            Destroy(controllerDraggables.transform.GetChild(0).gameObject);
            Destroy(controllerDraggables.transform.GetChild(1).gameObject);

            displayManager = gameObject.GetComponentInChildren<UserProfileListController>().GetComponent<RectTransform>();
            displayManager.name = "Display Manager";
            displayManager.gameObject.AddComponent<DisplayManager>();

            Destroy(displayManager.GetComponent<UserProfileListController>());

            var layout = controllerDraggables.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();

            _icon_Dinput = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/dinput.png");
            _icon_Xinput = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/xinput.png");
            _icon_Keyboard = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/keyboardmouse.png");
            //_icon_Mouse = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/mouse.png");

            _sprite_Dinput = Sprite.Create(_icon_Dinput, new Rect(Vector2.zero, new Vector2(_icon_Dinput.width, _icon_Dinput.height)), Vector2.zero);
            _sprite_Xinput = Sprite.Create(_icon_Xinput, new Rect(Vector2.zero, new Vector2(_icon_Xinput.width, _icon_Xinput.height)), Vector2.zero);
            _sprite_Keyboard = Sprite.Create(_icon_Keyboard, new Rect(Vector2.zero, new Vector2(_icon_Keyboard.width, _icon_Keyboard.height)), Vector2.zero);
            //_sprite_Mouse = Sprite.Create(_icon_Mouse, new Rect(Vector2.zero, new Vector2(_icon_Mouse.width, _icon_Mouse.height)), Vector2.zero);

            _controllerPrefab = DoDad.UI.ModMenuManager.CreateDraggable("Controller Prefab");
            _controllerPrefab.GetComponent<Image>().color = Vector4.zero;

            GameObject controllerFollowers = new GameObject("Controller Followers", typeof(RectTransform));
            controllerFollowers.transform.SetParent(transform);

            this.controllerFollowers = controllerFollowers.GetComponent<RectTransform>();


            //GameObject controllerDraggables = new GameObject("Controller Draggables", typeof(RectTransform));
            //controllerDraggables.transform.SetParent(transform);

            //_controllerDraggables = controllerDraggables.GetComponent<RectTransform>();

            gameObject.GetComponentInParent<UnityEngine.UI.CanvasScaler>().HandleConstantPhysicalSize();
            gameObject.GetComponentInParent<UnityEngine.UI.CanvasScaler>().HandleScaleWithScreenSize();
        }
        void OnDestroy()
        {
            Destroy(_controllerPrefab);

            Destroy(_sprite_Dinput);
            Destroy(_sprite_Xinput);
            Destroy(_sprite_Keyboard);
            //Destroy(_sprite_Mouse);

            Destroy(_icon_Dinput);
            Destroy(_icon_Xinput);
            Destroy(_icon_Keyboard);
            Destroy(_icon_Mouse);

            if(_initialized)
                Instance = null;
        }
        private void OnEnable()
        {

            // Ensure DisplayManager is initialized if possible
            // Send new draggables to the display manager to sort

            RebuildControllerElements();
            RebuildDisplayElements();

            ReInput.ControllerConnectedEvent += RebuildControllerElements;
            ReInput.ControllerDisconnectedEvent += RebuildControllerElements;
            Display.onDisplaysUpdated += RebuildDisplayElements;
        }
        private void OnDisable()
        {
            ReInput.ControllerConnectedEvent -= RebuildControllerElements;
            ReInput.ControllerDisconnectedEvent -= RebuildControllerElements;
            Display.onDisplaysUpdated -= RebuildDisplayElements;

            ClearControllers();
            ClearDisplays();
        }
        #endregion

        #region Rebuild Elements
        private void RebuildControllerElements(Rewired.ControllerStatusChangedEventArgs args = null)
        {
            int childIndex = 0;

            foreach (ImageFollower follower in _imageFollowers)
            {
                if (follower.Target == null)
                    follower.Excess = true;
            }

            _imageFollowers.RemoveAll(x => x.Excess == true);

            foreach (Controller controller in ReInput.controllers.Controllers)
            {
                if (controller.type == ControllerType.Mouse)
                    continue;

                bool hasFollower = false;

                foreach(ImageFollower follower in _imageFollowers)
                {
                    if(follower.Target?.Controller == controller)
                    {
                        hasFollower = true;
                        follower.Target.ChildId = childIndex;
                    }
                }

                if (!hasFollower)
                {
                    ControllerDraggable draggable = CreateDraggable();
                    
                    if(HasPreference(controller))
                    {
                        // Get displayId and screenId from preference
                        // Get the appropriate screen RectTransform from DisplayManager
                        // Assign draggable to it
                    }

                    draggable.Controller = controller;
                    draggable.ChildId = childIndex;

                    _imageFollowers.Add(CreateFollower(draggable.Controller.type));

                    _imageFollowers[_imageFollowers.Count - 1].SetTarget(draggable);

                    draggable.enabled = true;
                }

                childIndex++;
            }
        }
        private void RebuildDisplayElements()
        {
            displayManager.GetComponent<DisplayManager>().UpdateDisplays();
        }
        #endregion

        #region Helpers
        private bool HasPreference(Controller controller)
        {
            if (XSplitScreen.Configuration.ControllerAssignments.ContainsKey(controller.deviceInstanceGuid.ToString()))
                if (XSplitScreen.Configuration.ControllerAssignments[controller.deviceInstanceGuid.ToString()].AssignedDisplay > -1)
                    return true;

            return false;
        }
        private ControllerDraggable CreateDraggable()
        {
            Vector3 scale = Vector3.one / 2f;

            GameObject draggable = Instantiate(_controllerPrefab);
            draggable.name = "Controller Draggable";
            draggable.transform.SetParent(controllerDraggables);
            draggable.transform.localScale = scale;

            draggable.GetComponent<UnityEngine.UI.Image>().sprite = Instantiate(_sprite_Keyboard);
            draggable.GetComponent<UnityEngine.UI.Image>().SetNativeSize();

            MPButton button = draggable.GetComponent<MPButton>();

            button.allowAllEventSystems = true;

            return (ControllerDraggable)button;
        }
        private ImageFollower CreateFollower( ControllerType type)
        {
            Sprite sprite = null;

            switch (type)
            {
                case ControllerType.Keyboard:
                    sprite = Instantiate(_sprite_Keyboard);
                    break;
                //case ControllerType.Mouse:
                //    sprite = Instantiate(_sprite_Mouse);
                //    break;
                case ControllerType.Joystick:
                    sprite = Instantiate(_sprite_Xinput);
                    break;
                case ControllerType.Custom:
                    sprite = Instantiate(_sprite_Dinput);
                    break;
            }

            float scale = 0.18f;

            GameObject follower = new GameObject("Image Follower", typeof(RectTransform));
            follower.SetActive(false);

            follower.transform.SetParent(controllerFollowers);
            follower.transform.localScale = new Vector3(scale, scale, scale);

            Image image = follower.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.SetNativeSize();
            image.raycastTarget = false;

            ImageFollower imageFollower = follower.AddComponent<ImageFollower>();
            //imageFollower.target = target;
            //imageFollower.SetTarget(target);

            follower.SetActive(true);

            return imageFollower;
        }
        private void ClearDisplays()
        {
            displayManager.GetComponent<DisplayManager>().ClearDisplays();
        }
        private void ClearControllers()
        {
            foreach(ControllerDraggable child in controllerDraggables.GetComponentsInChildren<ControllerDraggable>())
            {
                Destroy(child.gameObject);
            }

            foreach (ImageFollower child in controllerFollowers.GetComponentsInChildren<ImageFollower>())
            {
                Destroy(child.gameObject);
            }
        }
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
    }
}
