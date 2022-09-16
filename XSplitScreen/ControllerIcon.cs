using Rewired;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DoDad.Library.UI;
using System.Collections;
using UnityEngine.Events;
using DoDad.Library.Events;

namespace DoDad.UI.Components
{
    public class ControllerIcon : MonoBehaviour
    {
        #region Variables
        public static List<ControllerIcon> activeIcons { get; private set; }
        public static MonoEvent onIconAdded { get; private set; }
        public static MonoEvent onIconRemoved { get; private set; }

        private static readonly float cursorFollowerSpeed = 0.1f;
        private static readonly float iconFollowerSpeed = 0.8f;
        private static readonly float activityTimeout = 0.4f;
        private static readonly float minSpawnTime = 0.3f;

        private static readonly Color restingColor = new Color(1, 1, 1, 0.5f);
        private static readonly Color activityColor = new Color(1, 1, 1, 1);

        public MonoEvent onStartDrag;
        public MonoEvent onStopDrag;

        public Controller controller;
        public Follower cursorFollower { get; private set; }

        public GameObject xMark;
        public GameObject checkMark;
        
        public bool isDragging { get; private set; }
        
        private AssignmentStatus assignmentStatus;

        private Follower iconFollower;
        private ReadyButton button;
        private Image iconImage;

        private IEnumerator waitForPopInCoroutine;
        private RectTransform rectTransform;

        private bool poppedIn;
        #endregion

        #region Unity Methods
        public void Awake()
        {
            rectTransform = gameObject.GetComponent<RectTransform>();

             // TODO initialization methods
            if (activeIcons == null)
            {
                activeIcons = new List<ControllerIcon>();
                onIconAdded = new MonoEvent();
                onIconRemoved = new MonoEvent();
            }

            onStartDrag = new MonoEvent();
            onStopDrag = new MonoEvent();
        }
        public void OnEnable()
        {
            if (controller == null)
                return;

            button = gameObject.GetComponent<ReadyButton>();
            button.image = GetComponent<Image>();

            InitializeFollowers();
            ToggleEvents();
            PopInIconFollower();
            RefreshAssignmentStatus();
            AddActiveIcon();
        }
        public void OnDisable()
        {
            RemoveActiveIcon();

            if (controller == null)
                return;

            ToggleEvents(false);
        }
        public void OnDestroy()
        {
            activeIcons.Clear();
            onIconAdded = null;
            onIconRemoved = null;
        }
        public void Update()
        {
            if (controller == null)
                Destroy(gameObject);
            else
                if (!controller.isConnected)
                    Destroy(gameObject);

            if(cursorFollower)
            {
                if (cursorFollower.gameObject.activeSelf)
                {
                    // Tell DisplayManager the position of uiFollower
                }
            }

            if (!iconImage)
                return;

            if (ReInput.time.unscaledTime - controller.GetLastTimeAnyButtonChanged() < activityTimeout || controller.GetAnyButton())
                iconImage.color = activityColor;
            else
                iconImage.color = restingColor;

            if(checkMark && xMark)
            {
                if(button.hovering)
                {
                    if(assignmentStatus == AssignmentStatus.Assigned)
                    {
                        if(checkMark.activeSelf)
                        {
                            checkMark.SetActive(false);
                            xMark.SetActive(true);
                        }
                    }
                }
                else
                {
                    if(assignmentStatus == AssignmentStatus.Assigned)
                    {
                        if(!checkMark.activeSelf)
                        {
                            checkMark.SetActive(true);
                            xMark.SetActive(false);
                        }
                    }
                    else if(assignmentStatus == AssignmentStatus.Unassigned)
                    {
                        if(checkMark.activeSelf)
                        {
                            checkMark.SetActive(false);
                            xMark.SetActive(false);
                        }
                    }
                }
            }
        }
        #endregion

        #region Update Active Icons
        private void AddActiveIcon()
        {
            activeIcons.Add(this);
            onIconAdded.Invoke(this);
        }
        private void RemoveActiveIcon()
        {
            activeIcons.Remove(this);
            onIconRemoved.Invoke(this);
        }
        #endregion

        #region Public Static Methods
        private static void DestroyReferences()
        {

        }
        #endregion

        #region Logic
        private void ToggleEvents(bool status = true)
        {
            ReadyButton followerButton = cursorFollower.GetComponent<ReadyButton>();

            if (followerButton is null || button is null)
                return;

            switch (status)
            {
                case true:
                    button.onClickMono.AddListener(ButtonOnClick);
                    button.onPointerDown.AddListener(ButtonOnPointerDown);
                    followerButton.onClickMono.AddListener(FollowerOnClick);
                    followerButton.onPointerUp.AddListener(FollowerOnPointerUp);
                    break;
                default:
                    button.onClickMono.RemoveAllListeners();
                    button.onPointerDown.RemoveAllListeners();
                    followerButton.onClickMono.RemoveAllListeners();
                    followerButton.onPointerUp.RemoveAllListeners();
                    break;
            }
        }
        private void SendFollowerToCursor()
        {
            cursorFollower.transform.position = button.eventSystem.currentInputModule.input.mousePosition;
        }
        private void UpdateFollowerInput(MonoBehaviour button)
        {
            cursorFollower.inputModule = button.GetComponent<MPButton>().eventSystem?.currentInputModule?.input;
        }
        
        private void BeginDrag()
        {
            //if (convertMethods == null)
            //    return;
            //if (convertMethods.Count == 0)
            //    return;

            cursorFollower.gameObject.SetActive(true);
            
            //onIconDrag.Invoke(this);

            //foreach(Func<ControllerIcon, int> function in convertMethods)
            //{
                //function.Invoke(this);
            //}

            onStartDrag.Invoke(this);
        }
        private void PopInIconFollower()
        {
            if (iconFollower == null)
                return;

            if (poppedIn)
                return;

            if (waitForPopInCoroutine != null)
                StopCoroutine(waitForPopInCoroutine);

            waitForPopInCoroutine = WaitForPopIn();

            StartCoroutine(waitForPopInCoroutine);
        }
        #endregion

        #region Events
        public void ButtonOnClick(MonoBehaviour button)
        {
            UpdateFollowerInput(button);
            SendFollowerToCursor();
            BeginDrag();
        }
        public void ButtonOnPointerDown(MonoBehaviour button)
        {
            UpdateFollowerInput(button);
            SendFollowerToCursor();
            BeginDrag();
        }
        public void FollowerOnClick(MonoBehaviour button)
        {
            EvaluateDrag();
        }
        public void FollowerOnPointerUp(MonoBehaviour button)
        {
            EvaluateDrag();
        }
        #endregion

        #region Coroutines
        private IEnumerator WaitForPopIn()
        {
            float timer = 0;

            while(timer < minSpawnTime * transform.GetSiblingIndex())
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            iconImage.enabled = true;
            poppedIn = true;

            iconFollower.transform.position = MPEventSystem.current.currentInputModule.input.mousePosition;

            Vector3 currentPosition = MPEventSystem.current.currentInputModule.input.mousePosition;
            Vector3 targetPosition = new Vector3(Screen.width / 2f, Screen.height * 0.8f, 0);

            Vector3 direction = currentPosition - targetPosition;

            Quaternion rotation = Quaternion.Euler(direction);

            rotation *= Quaternion.AngleAxis(UnityEngine.Random.Range(-60f, 60f), Vector3.forward);

            direction = rotation.eulerAngles.normalized;
            direction *= UnityEngine.Random.Range(100, 200);

            iconFollower.velocity = direction;

            yield return null;
        }
        #endregion

        #region Assignment Status
        private void ResetAssignmentStatus()
        {
            // Clear assignment in Display Manager
            Debug.Log("Clearing assignment status");
            SetAssignmentStatus(AssignmentStatus.Unassigned);
        }
        private void UpdateMarks()
        {
            if(assignmentStatus == AssignmentStatus.Assigned)
            {
                checkMark.SetActive(true);
                xMark.SetActive(false);
            }
            else if(assignmentStatus == AssignmentStatus.Unassigned)
            {
                checkMark.SetActive(false);
                xMark.SetActive(false);
            }
        }
        private void EvaluateDrag()
        {
            cursorFollower.gameObject.SetActive(false);
            isDragging = false;

            //DisplayManager.instance.RequestDropAssignment(this, cursorFollower);

            //RefreshAssignmentStatus();

            onStopDrag.Invoke(this);
        }
        public void RefreshAssignmentStatus()
        {
            if (controller.type == ControllerType.Keyboard) // DEVELOPMENT
            {

            }

            bool isAssigned = XSplitScreen.Configuration.IsControllerAssigned(controller);


            if (isAssigned)
                SetAssignmentStatus(AssignmentStatus.Assigned);
            else
                SetAssignmentStatus(AssignmentStatus.Unassigned);
        }
        public void SetAssignmentStatus(AssignmentStatus status)
        {
            assignmentStatus = status;

            UpdateMarks();
        }
        #endregion

        #region Initialization
        private void InitializeFollowers()
        {
            if(iconFollower == null)
            {
                GameObject _iconFollower = new GameObject($"Icon Follower ({controller.name})", typeof(RectTransform));

                iconFollower = _iconFollower.AddComponent<Follower>();

                iconFollower.transform.SetParent(SplitscreenConfigurationManager.ControllerAssignmentState.followerContainer);

                iconFollower.transform.localScale = transform.localScale / 3f;
                iconFollower.transform.position = Vector3.zero;

                iconFollower.target = gameObject.GetComponent<RectTransform>();
                iconFollower.smoothMovement = true;
                iconFollower.movementSpeed = iconFollowerSpeed;
                iconFollower.destroyOnTargetLost = true;

                iconImage = _iconFollower.AddComponent<Image>();

                iconImage.sprite = button.image.sprite;
                iconImage.SetNativeSize();
                iconImage.raycastTarget = false;
                iconImage.enabled = false;

                checkMark.transform.SetParent(iconFollower.transform);
                checkMark.transform.localPosition = Vector3.zero;
                xMark.transform.SetParent(iconFollower.transform);
                xMark.transform.localPosition = Vector3.zero;
            }
            if (cursorFollower == null)
            {
                //
                GameObject _cursorFollower = new GameObject("Cursor Follower", typeof(RectTransform));

                cursorFollower = _cursorFollower.AddComponent<Follower>();

                cursorFollower.transform.SetParent(SplitscreenConfigurationManager.ControllerAssignmentState.followerContainer);

                cursorFollower.transform.localScale = transform.localScale / 2f;

                cursorFollower.shouldFollowMouse = true;
                cursorFollower.smoothMovement = true;
                cursorFollower.movementSpeed = cursorFollowerSpeed;
                cursorFollower.destroyOnTargetLost = false;

                Image cursorImage = _cursorFollower.AddComponent<Image>();

                cursorImage.sprite = button.image.sprite;
                cursorImage.SetNativeSize();

                ReadyButton _button = _cursorFollower.AddComponent<ReadyButton>();

                _button.allowOutsiderOnPointerUp = true;

                cursorFollower.gameObject.SetActive(false);

                // Add a button and register to events 
                //  - Update assignment in the DisplayManager
            }
        }
        #endregion

        #region Definitions
        public enum AssignmentStatus
        {
            Unassigned,
            Assigned
        }
        public class IconDragEvent : UnityEvent<ControllerIcon> { }
        #endregion
    }
}
