using DoDad.Library.UI;
using Rewired;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static XSplitScreen.XSplitScreen;

namespace XSplitScreen
{
    // TODO

    // On the first launch of the plugin the controller icons should pop out of a chest

    class ControllerIconManager : MonoBehaviour
    {
        #region Variables
        public static ControllerIconManager instance { get; private set; }

        public static bool isAssigning;

        public List<Icon> icons { get; private set; }
        public RectTransform followerContainer { get; private set; }

        public Sprite sprite_Dinput { get; private set; }
        public Sprite sprite_Xinput { get; private set; }
        public Sprite sprite_Keyboard { get; private set; }
        public Sprite sprite_Unknown { get; private set; }
        public Sprite sprite_Checkmark { get; private set; }
        public Sprite sprite_Xmark { get; private set; }

        public IconEvent onStartDragIcon { get; private set; }
        public IconEvent onStopDragIcon { get; private set; }
        public IconEvent onIconAdded { get; private set; }
        public IconEvent onIconRemoved { get; private set; }

        private Texture2D texture_Dinput;
        private Texture2D texture_Xinput;
        private Texture2D texture_Keyboard;
        private Texture2D texture_Unknown;
        private Texture2D texture_Checkmark;
        private Texture2D texture_Xmark;

        private RectTransform iconPrefab;
        private RectTransform iconContainer;
        #endregion

        #region Unity Methods
        public void Awake()
        {
            if (instance)
                Destroy(gameObject);

            Initialize();
        }
        #endregion

        #region Initialization & Exit
        private void Initialize()
        {
            InitializeReferences();
            InitializePrefab();
            InitializeIcons();
            ToggleListeners(true);
        }
        private void InitializeReferences()
        {
            instance = this;

            onStartDragIcon = new IconEvent();
            onStopDragIcon = new IconEvent();
            onIconAdded = new IconEvent();
            onIconRemoved = new IconEvent();

            texture_Dinput = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/dinput.png");
            texture_Xinput = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/xinput.png");
            texture_Keyboard = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/keyboardmouse.png");
            texture_Unknown = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/unknown.png");
            texture_Checkmark = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/checkmark.png");
            texture_Xmark = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/xmark.png");

            sprite_Dinput = Sprite.Create(texture_Dinput, new Rect(Vector2.zero, new Vector2(texture_Dinput.width, texture_Dinput.height)), Vector2.zero);
            sprite_Xinput = Sprite.Create(texture_Xinput, new Rect(Vector2.zero, new Vector2(texture_Xinput.width, texture_Xinput.height)), Vector2.zero);
            sprite_Keyboard = Sprite.Create(texture_Keyboard, new Rect(Vector2.zero, new Vector2(texture_Keyboard.width, texture_Keyboard.height)), Vector2.zero);
            sprite_Unknown = Sprite.Create(texture_Unknown, new Rect(Vector2.zero, new Vector2(texture_Unknown.width, texture_Unknown.height)), Vector2.zero);
            sprite_Checkmark = Sprite.Create(texture_Checkmark, new Rect(Vector2.zero, new Vector2(texture_Checkmark.width, texture_Checkmark.height)), Vector2.zero);
            sprite_Xmark = Sprite.Create(texture_Xmark, new Rect(Vector2.zero, new Vector2(texture_Xmark.width, texture_Xmark.height)), Vector2.zero);

            icons = new List<Icon>();

            iconContainer = new GameObject("Icon Container", typeof(RectTransform)).GetComponent<RectTransform>(); 
            
            var parent = (ConfigurationManager.PageState)ConfigurationManager.instance.stateMachine.GetState(DoDad.Library.AI.State.State1);

            var state = parent as ConfigurationManager.ControllerAssignmentState;

            followerContainer = state.followerContainer;

            iconContainer.SetParent(parent.page);
            iconContainer.SetSiblingIndex(3);
            iconContainer.localScale = Vector3.one;

            LayoutElement element = iconContainer.gameObject.AddComponent<LayoutElement>();

            element.preferredHeight = 64;

            iconContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        private void InitializePrefab()
        {
            // TODO anchorMax and anchorMin are being reset when opening the menu
            iconPrefab = new GameObject("Icon Prefab", typeof(RectTransform)).GetComponent<RectTransform>();

            var parent = (ConfigurationManager.PageState)ConfigurationManager.instance.stateMachine.GetState(DoDad.Library.AI.State.State1);

            iconPrefab.SetParent(parent.page);

            Icon icon = iconPrefab.gameObject.AddComponent<Icon>();

            iconPrefab.gameObject.SetActive(false);
        }
        private void InitializeIcons()
        {
            foreach(Controller controller in configuration.controllers)
            {
                CreateIcon(controller);
            }
        }
        private void ToggleListeners(bool status)
        {
            if(status)
            {
                configuration.onControllerConnected += OnControllerConnected;
                configuration.onControllerDisconnected += OnControllerDisconnected;
                configuration.onConfigurationUpdated.AddListener(OnConfigurationUpdated);
            }
            else
            {
                configuration.onControllerConnected -= OnControllerConnected;
                configuration.onControllerDisconnected -= OnControllerDisconnected;
                configuration.onConfigurationUpdated.RemoveListener(OnConfigurationUpdated);
            }
        }
        #endregion

        #region Icons
        public void OnConfigurationUpdated()
        {
            foreach(Icon icon in icons)
            {
                icon.UpdateStatus();
            }
        }
        public void OnControllerConnected(ControllerStatusChangedEventArgs args)
        {
            CreateIcon(args.controller);
        }
        public void OnControllerDisconnected(ControllerStatusChangedEventArgs args)
        {
            int index = -1;

            for (int e = 0; e < icons.Count; e++)
            {
                if (!icons[e].controller.isConnected)
                {
                    index = e;
                    break;
                }
            }

            if (index > -1)
            {
                Destroy(icons[index].gameObject);
                icons.RemoveAt(index);
            }
        }
        public Sprite GetDeviceSprite(Controller controller)
        {
            Sprite sprite = sprite_Xinput;

            if (controller.name.ToLower().Contains("sony") || controller.name.ToLower().Contains("hyper"))
            {
                sprite = sprite_Dinput;
            }
            else if (controller.name.ToLower().Contains("key"))
            {
                sprite = sprite_Keyboard;
            }

            return sprite;
        }
        public Icon GetIcon(Controller controller)
        {
            if (controller is null)
                return null;

            foreach(Icon icon in icons)
            {
                if (icon.controller.Equals(controller))
                    return icon;
            }

            return null;
        }
        private void CreateIcon(Controller controller)
        {
            Icon icon = Instantiate(iconPrefab).GetComponent<Icon>();

            icon.name = $"(Icon) {controller.name}";
            //icon.assignment = assignment;
            icon.transform.SetParent(iconContainer);
            icon.transform.position = new Vector3(Screen.width, icons.Count > 0 ? icons[icons.Count - 1].transform.position.y : Screen.height, 0);
            icon.Initialize(controller, followerContainer);

            icon.onStartDragIcon.AddListener(OnStartDragIcon);
            icon.onStopDragIcon.AddListener(OnStopDragIcon);

            //UpdateIconDeviceSprite(icon);

            icon.gameObject.SetActive(true);
            icons.Add(icon);
            //onIconAdded.Invoke(icon, assignment);
        }
        #endregion

        #region Events
        public void OnStartDragIcon(Icon icon)
        {
            onStartDragIcon.Invoke(icon);
        }
        public void OnStopDragIcon(Icon icon)
        {
            onStopDragIcon.Invoke(icon);
        }
        #endregion

        #region Definitions
        public class Icon : MonoBehaviour
        {
            #region Variables
            private static readonly Color normalColor = new Vector4(1, 1, 1, 0.4f);
            private static readonly Color buttonPressColor = new Vector4(1, 1, 1, 1f);

            private static readonly float normalAlpha = 0.4f;
            private static readonly float buttonPressAlpha = 1f;
            private static readonly float colorSpeed = 20f;

            private static readonly float iconFollowerSpeed = 0.4f;
            private static readonly float cursorFollowerSpeed = 0.1f;
            private static readonly float activityTimeout = 0.4f;

            public Coroutine iconMonitorCoroutine;

            public Controller controller;

            //public Assignment assignment;

            public Image deviceImage;
            public Image statusImage;
            public Image cursorImage;
            public Image displayImage;

            public Follower iconFollower;
            public Follower cursorFollower;
            public Follower displayFollower; // use only cursorFollower to allow reassignment 

            public XButton button;

            public IconEvent onStartDragIcon { get; private set; }
            public IconEvent onStopDragIcon { get; private set; }

            public bool isAssigned => configuration.IsAssigned(controller);
            public bool potentialReassignment = false;
            public bool showStatusImage = false;
            public bool hasTemporaryAssignment = false;

            private Vector4 targetColor = new Vector4(1, 1, 1, 0);
            private Vector4 statusColor = Color.clear;

            private float activityTimer = 0f;
            #endregion

            #region Unity Methods
            public void Update()
            {
                if (controller is null)
                    return;

                if(controller.GetAnyButton())
                {
                    targetColor.w = buttonPressAlpha;
                    activityTimer = activityTimeout;
                }
                else
                {
                    if (activityTimer > 0)
                        activityTimer -= Time.unscaledDeltaTime;

                    if (activityTimer <= 0)
                    {
                        targetColor.w = normalAlpha;
                    }
                }

                deviceImage.color = Color.Lerp(deviceImage.color, targetColor, Time.unscaledDeltaTime * colorSpeed);

                if(showStatusImage)
                    statusImage.color = Color.Lerp(statusImage.color, Color.white, Time.unscaledDeltaTime * colorSpeed);
                else
                    statusImage.color = Color.Lerp(statusImage.color, Color.clear, Time.unscaledDeltaTime * colorSpeed);

                cursorImage.color = Color.Lerp(cursorImage.color, targetColor, Time.unscaledDeltaTime * colorSpeed);

                if(displayFollower.enabled)
                    displayImage.color = Color.Lerp(displayImage.color, targetColor, Time.unscaledDeltaTime * colorSpeed);
                else
                    displayImage.color = Color.Lerp(displayImage.color, new Vector4(targetColor.x, targetColor.y, targetColor.z, 0f), Time.unscaledDeltaTime * colorSpeed);

            }
            public void OnDestroy()
            {
                Log.LogDebug($"Destroying '{name}'");
                Destroy(displayFollower.gameObject);
                Destroy(iconFollower.gameObject);
                Destroy(cursorFollower.gameObject);
            }
            #endregion

            #region Initialization & Exit
            public void Initialize(Controller controller, RectTransform followerContainer) // Much of this should occur on creation of the prefab, not icon
            {
                onStartDragIcon = new IconEvent();
                onStopDragIcon = new IconEvent();

                this.controller = controller;
                // TODO organize
                displayFollower = new GameObject($"(Display Follower) {controller.name}", typeof(RectTransform), typeof(Image), typeof(Follower), typeof(XButton)).GetComponent<Follower>();
                displayFollower.transform.SetParent(followerContainer);
                displayFollower.transform.localScale = Vector3.one * 0.18f;
                displayFollower.movementSpeed = iconFollowerSpeed;
                displayFollower.smoothMovement = true;
                displayImage = displayFollower.GetComponent<Image>();
                displayImage.color = targetColor;

                displayFollower.enabled = false;

                XButton displayButton = displayFollower.GetComponent<XButton>();
                displayButton.onPointerDown.AddListener(OnPointerDownIcon);
                displayButton.onClickMono.AddListener(OnClickIcon);

                displayButton.onHoverStart.AddListener(OnHoverStart);
                displayButton.onHoverStop.AddListener(OnHoverStop);

                iconFollower = new GameObject($"(Icon Follower) {controller.name}", typeof(RectTransform), typeof(Image), typeof(Follower), typeof(XButton)).GetComponent<Follower>();
                iconFollower.transform.SetParent(followerContainer);
                iconFollower.transform.localScale = Vector3.one * 0.3f;
                iconFollower.target = gameObject.GetComponent<RectTransform>();
                iconFollower.movementSpeed = iconFollowerSpeed;
                iconFollower.destroyOnTargetLost = true;
                iconFollower.smoothMovement = true;
                //iconFollower.transform.localPosition = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
                
                Log.LogDebug($"Icon.Initialize setting iconFollower position to '{iconFollower.transform.localPosition}'");

                button = iconFollower.GetComponent<XButton>();
                button.allowAllEventSystems = true;
                button.onPointerDown.AddListener(OnPointerDownIcon);
                button.onClickMono.AddListener(OnClickIcon);

                button.onHoverStart.AddListener(OnHoverStart);
                button.onHoverStop.AddListener(OnHoverStop);

                GameObject statusObject = new GameObject("(Image) Status", typeof(RectTransform));
                statusObject.transform.SetParent(iconFollower.transform);
                statusObject.transform.localScale = Vector3.one;
                statusObject.transform.localPosition = Vector3.zero;

                deviceImage = iconFollower.GetComponent<Image>();
                deviceImage.color = targetColor;

                statusImage = statusObject.AddComponent<Image>();
                statusImage.raycastTarget = false;
                statusImage.color = Color.clear;

                cursorFollower = new GameObject($"(Cursor Follower) {controller.name}", typeof(RectTransform), typeof(Image), typeof(Follower), typeof(XButton)).GetComponent<Follower>();
                cursorFollower.transform.SetParent(followerContainer);
                cursorFollower.transform.localScale = Vector3.one * 0.24f;
                cursorFollower.shouldFollowMouse = true;
                cursorFollower.movementSpeed = cursorFollowerSpeed;
                cursorFollower.destroyOnTargetLost = true;
                cursorFollower.smoothMovement = true;
                
                button = cursorFollower.GetComponent<XButton>();
                button.onPointerUp.AddListener(OnPointerUpCursor);
                button.onClickMono.AddListener(OnClickCursor);

                button.allowOutsiderOnPointerUp = true;

                cursorImage = cursorFollower.GetComponent<Image>();
                cursorImage.color = targetColor;
                
                cursorFollower.gameObject.SetActive(false);

                targetColor.w = 1;

                deviceImage.sprite = instance.GetDeviceSprite(controller);
                deviceImage.SetNativeSize();

                displayImage.sprite = deviceImage.sprite;
                displayImage.SetNativeSize();

                cursorImage.sprite = deviceImage.sprite;
                cursorImage.SetNativeSize();

                UpdateStatus();
            }
            #endregion

            #region Display & Icon
            public void SetReassignmentStatus(bool status)
            {
                potentialReassignment = status;

                displayImage.raycastTarget = !status;

                if (!isAssigned)
                    displayImage.raycastTarget = false;
            }
            public void UpdateDisplayFollower(RectTransform target)
            {
                if (displayFollower is null)
                    return;

                if(target == null)
                {
                    displayFollower.enabled = false;
                }
                else
                {
                    displayFollower.target = target;
                    displayFollower.enabled = true;
                }
            }
            public void UpdateStatus()
            {
                showStatusImage = isAssigned;
                statusImage.sprite = instance.sprite_Checkmark;
                statusImage.SetNativeSize();

                Assignment? currentAssignment = configuration.GetAssignment(controller);

                if(currentAssignment.HasValue)
                {
                    float tempAlpha = targetColor.w;
                    if (currentAssignment.Value.position.IsPositive())
                        targetColor = currentAssignment.Value.color;
                    else
                        targetColor = Color.white;

                    targetColor.w = tempAlpha;
                }

                //this.assignment = assignment;

                //showStatusImage = assignment.isAssigned;
            }
            #endregion

            #region Events
            public void OnHoverStart(MonoBehaviour mono)
            {
                if (isAssigned)
                {
                    statusImage.sprite = instance.sprite_Xmark;
                    statusImage.SetNativeSize();
                }

                Log.LogDebug($"ControllerIconManager.OnHoverStart {name}: {name}");
            }
            public void OnHoverStop(MonoBehaviour mono)
            {
                if (isAssigned)
                {
                    statusImage.sprite = instance.sprite_Checkmark;
                    statusImage.SetNativeSize();
                }
            }
            public void OnClickIcon(MonoBehaviour mono)
            {
                OnPointerDownIcon(mono);
            }
            public void OnPointerDownIcon(MonoBehaviour mono)
            {
                if (hasTemporaryAssignment)
                    return;

                XButton button = mono as XButton;

                if (button is null)
                    return;

                cursorFollower.inputModule = button.eventSystem?.currentInputModule?.input;

                if (cursorFollower.inputModule is null)
                    return;

                hasTemporaryAssignment = true;
                showStatusImage = false;

                cursorFollower.transform.position = cursorFollower.inputModule.mousePosition;
                cursorFollower.gameObject.SetActive(true);

                onStartDragIcon.Invoke(this);
            }
            public void OnClickCursor(MonoBehaviour mono)
            {
                OnPointerUpCursor(mono);
            }
            public void OnPointerUpCursor(MonoBehaviour mono)
            {
                cursorFollower.gameObject.SetActive(false);
                onStopDragIcon.Invoke(this);
                hasTemporaryAssignment = false;
            }
            #endregion
        }
        #endregion

        #region Definitions
        public class IconEvent : UnityEvent<Icon> { }
        #endregion
    }
}