using DoDad.Library.UI;
using DoDad.XSplitScreen.Components;
using Rewired;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DoDad.XSplitScreen.Components
{
    public class IconEvent : UnityEvent<Icon> { }

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

        private XSplitScreen.Configuration configuration => XSplitScreen.configuration;

        public bool isAssigned
        {
            get
            {
                var assignment = XSplitScreen.configuration.GetAssignment(controller);

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
        public void Start()
        {
            if (XSplitScreen.configuration.enabled)
                OnSplitScreenEnabled();
        }
        public void Update()
        {
            if (controller is null)
                return;

            if (controller.GetAnyButton())
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

            if (displayFollower.enabled && !hideDisplayImage)
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
            XSplitScreen.configuration?.onSplitScreenEnabled.RemoveListener(OnSplitScreenEnabled);
            XSplitScreen.configuration?.onSplitScreenDisabled.RemoveListener(OnSplitScreenDisabled);
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

            iconImage.sprite = ControllerIconManager.instance.GetDeviceSprite(controller);
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

            displayImage.raycastTarget = status && displayFollower.enabled;
            displayButton.interactable = !configuration.enabled;
        }
        public void UpdateDisplayFollower(RectTransform target)
        {
            if (displayFollower is null)
                return;

            if (target == null)
            {
                displayFollower.enabled = false;
            }
            else
            {
                displayFollower.target = target;
                displayFollower.enabled = true;
            }

            displayImage.raycastTarget = displayFollower.enabled;
            displayButton.interactable = !configuration.enabled;

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
            displayButton.interactable = false;
            cursorFollower.gameObject.SetActive(false);
            hasTemporaryAssignment = false;
        }
        public void OnSplitScreenDisabled()
        {
            iconButton.interactable = true;
            displayButton.interactable = true;
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
            // TODO displayButton.interactable is not properly enabling after returning from another scene while the mod is enabled
            if (hasTemporaryAssignment || XSplitScreen.configuration.enabled)
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

            if (!displayFollower.enabled)
                displayFollower.transform.position = cursorFollower.transform.position;

            displayImage.raycastTarget = false;
            displayButton.interactable = false;
            framesSinceAssignment = 0;

            AssignmentManager.instance.OnAssignController(this);
        }
        #endregion
    }
}
