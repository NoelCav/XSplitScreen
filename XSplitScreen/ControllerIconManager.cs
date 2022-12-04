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
    // On the first launch of the plugin the controller icons should pop out of a chest?

    public class ControllerIconManager : MonoBehaviour
    {
        #region Variables
        public static ControllerIconManager instance { get; private set; }

        public static bool isAssigning;

        public List<Icon> icons { get; private set; }
        public RectTransform followerContainer { get; private set; }

        // TODO move references to appropriate area
        public Sprite sprite_Dinput { get; private set; }
        public Sprite sprite_Xinput { get; private set; }
        public Sprite sprite_Keyboard { get; private set; }
        public Sprite sprite_Unknown { get; private set; }
        public Sprite sprite_Xmark { get; private set; }
        public Sprite sprite_Lock { get; private set; }
        public Sprite sprite_Gear { get; private set; }

        public IconEvent onStartDragIcon { get; private set; }
        public IconEvent onStopDragIcon { get; private set; }
        public IconEvent onIconAdded { get; private set; }
        public IconEvent onIconRemoved { get; private set; }

        private Texture2D texture_Dinput;
        private Texture2D texture_Xinput;
        private Texture2D texture_Keyboard;
        private Texture2D texture_Unknown;
        private Texture2D texture_Xmark;
        private Texture2D texture_Lock;
        private Texture2D texture_Gear;

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
        public void OnDestroy()
        {
            instance = null;
            ToggleListeners(false);
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
            texture_Xmark = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/xmark.png");
            texture_Lock = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/lock.png");
            texture_Gear = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/gear.png");

            sprite_Dinput = Sprite.Create(texture_Dinput, new Rect(Vector2.zero, new Vector2(texture_Dinput.width, texture_Dinput.height)), Vector2.zero);
            sprite_Xinput = Sprite.Create(texture_Xinput, new Rect(Vector2.zero, new Vector2(texture_Xinput.width, texture_Xinput.height)), Vector2.zero);
            sprite_Keyboard = Sprite.Create(texture_Keyboard, new Rect(Vector2.zero, new Vector2(texture_Keyboard.width, texture_Keyboard.height)), Vector2.zero);
            sprite_Unknown = Sprite.Create(texture_Unknown, new Rect(Vector2.zero, new Vector2(texture_Unknown.width, texture_Unknown.height)), Vector2.zero);
            sprite_Xmark = Sprite.Create(texture_Xmark, new Rect(Vector2.zero, new Vector2(texture_Xmark.width, texture_Xmark.height)), Vector2.zero);
            sprite_Lock = Sprite.Create(texture_Lock, new Rect(Vector2.zero, new Vector2(texture_Lock.width, texture_Lock.height)), Vector2.zero);
            sprite_Gear = Sprite.Create(texture_Gear, new Rect(Vector2.zero, new Vector2(texture_Gear.width, texture_Gear.height)), Vector2.zero);

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
            if (configuration is null)
                return;

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

        }
        public void OnControllerConnected(ControllerStatusChangedEventArgs args)
        {
            foreach (Icon icon in icons)
                if (icon.controller.Equals(args.controller))
                    return;

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
                sprite = sprite_Dinput;
            else if (controller.name.ToLower().Contains("key"))
                sprite = sprite_Keyboard;

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
            icon.transform.SetParent(iconContainer);
            icon.transform.position = new Vector3(Screen.width, icons.Count > 0 ? icons[icons.Count - 1].transform.position.y : Screen.height, 0);
            icon.Initialize(controller, followerContainer);

            icon.onStartDragIcon.AddListener(OnStartDragIcon);
            icon.onStopDragIcon.AddListener(OnStopDragIcon);

            icon.gameObject.SetActive(true);
            icons.Add(icon);
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
            private static readonly float colorSpeed = 10f;//20f;

            private static readonly float iconFollowerSpeed = 0.4f;
            private static readonly float cursorFollowerSpeed = 0.1f;
            private static readonly float activityTimeout = 0.4f;

            public Coroutine iconMonitorCoroutine;

            public Controller controller;

            public Image statusImage; // TODO remove this
            public Image iconImage;
            public Image cursorImage;
            public Image displayImage;

            public Follower iconFollower;
            public Follower cursorFollower;
            public Follower displayFollower; // use only displayFollower to allow reassignment 

            public XButton iconButton;
            public XButton cursorButton;
            public XButton displayButton;

            public IconEvent onStartDragIcon { get; private set; }
            public IconEvent onStopDragIcon { get; private set; }

            public bool isAssigned
            {
                get
                {
                    var assignment = configuration.GetAssignment(controller);

                    if (!assignment.HasValue)
                        return false;

                    return assignment.Value.isAssigned;
                }
            }
            public bool potentialReassignment = false;
            public bool showStatusImage = false;
            public bool hasTemporaryAssignment = false;
            public bool hideDisplayImage = false;

            private Vector4 targetColor = new Vector4(1, 1, 1, 0);
            private Vector4 statusColor = Color.clear;

            private int framesSinceAssignment = 0;
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

                iconImage.color = Color.Lerp(iconImage.color, targetColor, Time.unscaledDeltaTime * colorSpeed);

                cursorImage.color = Color.Lerp(cursorImage.color, targetColor, Time.unscaledDeltaTime * colorSpeed);

                if(displayFollower.enabled && !hideDisplayImage)
                    displayImage.color = Color.Lerp(displayImage.color, targetColor, Time.unscaledDeltaTime * colorSpeed);
                else
                    displayImage.color = Color.Lerp(displayImage.color, new Vector4(targetColor.x, targetColor.y, targetColor.z, 0f), Time.unscaledDeltaTime * colorSpeed);

            }
            public void LateUpdate()
            {
                framesSinceAssignment++;

                if (framesSinceAssignment == 2)
                    displayButton.interactable = true;
            }
            public void OnDestroy()
            {
                Destroy(displayFollower.gameObject);
                Destroy(iconFollower.gameObject);
                Destroy(cursorFollower.gameObject);
                configuration?.onSplitScreenEnabled.RemoveListener(OnSplitScreenEnabled);
                configuration?.onSplitScreenDisabled.RemoveListener(OnSplitScreenDisabled);
            }
            #endregion

            #region Initialization & Exit
            public void Initialize(Controller controller, RectTransform followerContainer) // Much of this should occur on creation of the prefab, not icon
            {
                onStartDragIcon = new IconEvent();
                onStopDragIcon = new IconEvent();

                configuration.onSplitScreenEnabled.AddListener(OnSplitScreenEnabled);
                configuration.onSplitScreenDisabled.AddListener(OnSplitScreenDisabled);

                this.controller = controller;
                // TODO organize
                displayFollower = new GameObject($"(Display Follower) {controller.name}", typeof(RectTransform), typeof(Image), typeof(Follower), typeof(XButton)).GetComponent<Follower>();
                displayFollower.transform.SetParent(followerContainer);
                displayFollower.transform.localScale = Vector3.one * (controller.type == ControllerType.Keyboard ? 0.27f : 0.19f);
                displayFollower.movementSpeed = iconFollowerSpeed * 0.4f;
                displayFollower.smoothMovement = true;
                displayFollower.target = GetComponent<RectTransform>();
                displayFollower.CatchUp();

                displayImage = displayFollower.GetComponent<Image>();
                displayImage.color = targetColor;

                displayFollower.enabled = false;

                displayButton = displayFollower.GetComponent<XButton>();
                displayButton.onPointerDown.AddListener(OnPointerDownIcon); //LAST
                displayButton.onClickMono.AddListener(OnClickIcon); //LAST

                displayButton.onHoverStart.AddListener(OnHoverStart);
                displayButton.onHoverStop.AddListener(OnHoverStop);

                iconFollower = new GameObject($"(Icon Follower) {controller.name}", typeof(RectTransform), typeof(Image), typeof(Follower), typeof(XButton)).GetComponent<Follower>();
                iconFollower.transform.SetParent(followerContainer);
                iconFollower.transform.localScale = Vector3.one * (controller.type == ControllerType.Keyboard ? 0.35f : 0.3f);
                iconFollower.target = gameObject.GetComponent<RectTransform>();
                iconFollower.movementSpeed = iconFollowerSpeed;
                iconFollower.destroyOnTargetLost = true;
                iconFollower.smoothMovement = true;
                
                iconButton = iconFollower.GetComponent<XButton>();
                iconButton.allowAllEventSystems = true;
                iconButton.onPointerDown.AddListener(OnPointerDownIcon);
                iconButton.onClickMono.AddListener(OnClickIcon);

                iconButton.onHoverStart.AddListener(OnHoverStart);
                iconButton.onHoverStop.AddListener(OnHoverStop);

                GameObject statusObject = new GameObject("(Image) Status", typeof(RectTransform));
                statusObject.transform.SetParent(iconFollower.transform);
                statusObject.transform.localScale = Vector3.one;
                statusObject.transform.localPosition = Vector3.zero;

                iconImage = iconFollower.GetComponent<Image>();
                iconImage.color = targetColor;

                cursorFollower = new GameObject($"(Cursor Follower) {controller.name}", typeof(RectTransform), typeof(Image), typeof(Follower), typeof(XButton)).GetComponent<Follower>();
                cursorFollower.transform.SetParent(followerContainer);
                cursorFollower.transform.localScale = Vector3.one * 0.24f;
                cursorFollower.shouldFollowMouse = true;
                cursorFollower.movementSpeed = cursorFollowerSpeed;
                cursorFollower.destroyOnTargetLost = true;
                cursorFollower.smoothMovement = true;
                
                cursorButton = cursorFollower.GetComponent<XButton>();
                cursorButton.allowAllEventSystems = true;
                cursorButton.allowOutsiderOnPointerUp = true;
                cursorButton.onPointerUp.AddListener(OnPointerUpCursor);
                cursorButton.onClickMono.AddListener(OnClickCursor);

                cursorButton.allowOutsiderOnPointerUp = true;

                cursorImage = cursorFollower.GetComponent<Image>();
                cursorImage.color = targetColor;
                
                cursorFollower.gameObject.SetActive(false);

                targetColor.w = 1;

                iconImage.sprite = instance.GetDeviceSprite(controller);
                iconImage.SetNativeSize();

                displayImage.sprite = iconImage.sprite;
                displayImage.SetNativeSize();

                cursorImage.sprite = iconImage.sprite;
                cursorImage.SetNativeSize();

                UpdateStatus();
            }
            #endregion

            #region Display & Icon
            public void ToggleDisplayImage(bool status)
            {
                hideDisplayImage = !status;

                if (hideDisplayImage)
                    displayImage.raycastTarget = false;
                else
                {
                    if (displayFollower.enabled)
                        displayImage.raycastTarget = true;
                }
            }
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

                displayImage.raycastTarget = displayFollower.enabled;

                UpdateStatus();
            }
            public void UpdateStatus()
            {
                showStatusImage = isAssigned;

                Assignment? currentAssignment = configuration.GetAssignment(controller);

                float tempAlpha = targetColor.w;

                targetColor = Color.white;

                if (currentAssignment.HasValue)
                {
                    if (currentAssignment.Value.position.IsPositive())
                        targetColor = currentAssignment.Value.color;
                }

                targetColor.w = tempAlpha;
            }
            #endregion

            #region Events
            public void OnSplitScreenEnabled()
            {
                iconButton.interactable = false;
                displayFollower.GetComponent<XButton>().interactable = false;
                cursorFollower.gameObject.SetActive(false);
                hasTemporaryAssignment = false;
            }
            public void OnSplitScreenDisabled()
            {
                iconButton.interactable = true;
                displayFollower.GetComponent<XButton>().interactable = true;
            }
            public void OnHoverStart(MonoBehaviour mono)
            {

            }
            public void OnHoverStop(MonoBehaviour mono)
            {

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
                Log.LogDebug($"Icon.OnPointerUpCursor: mono.name = '{mono.name}'");
                cursorFollower.gameObject.SetActive(false);
                onStopDragIcon.Invoke(this);
                hasTemporaryAssignment = false;

                if (!displayFollower.enabled)
                    displayFollower.transform.position = cursorFollower.transform.position;

                displayImage.raycastTarget = false;
                displayButton.interactable = false;
                framesSinceAssignment = 0;

                AssignmentManager.instance.OnAssignController(this);
            }
            #endregion
        }
        public class IconEvent : UnityEvent<Icon> { }
        #endregion
    }
}