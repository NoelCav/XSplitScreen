using DoDad.Library.UI;
using Rewired;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using DoDad.Library.Math;
using static DoDad.XSplitScreen.SplitScreenConfiguration;
using System.Collections;

/// <summary>
/// TODO
/// Rewrite how screens are worked. Screens should live under _backgroundMonitor and be updated based on CurrentDisplay
/// Each screen should take ownership of all controllers across configurations in the same spot and
/// subscribe to ChangedDisplay to update which ControllerDraggable is set to visible
/// </summary>
namespace DoDad.UI.Components
{
    public class DisplayManager : MonoBehaviour
    {
        #region Variables
        public static DisplayManager instance { get; private set; }

        private static readonly float maximumHotspotDistance = 50f;
        private static readonly int2 rowDimensions = new int2(150, 150);

        public Dictionary<RectTransform, float[]> distances;

        private Dictionary<ControllerIcon, IEnumerator[]> followerCoroutines;
        private List<DisplayConfiguration> displayConfigurations;

        private RectTransform display;
        private RectTransform screenHotspotContainer;

        private Texture2D textureDisplay;
        private Texture2D textureScreenDivider;

        private RectTransform[] screenHotSpots = new RectTransform[9];

        private Vector2[] screenHotspotPositions = new Vector2[9] {
        new Vector2(-100, 100), new Vector2(0, 100), new Vector2(100,100),
        new Vector2(-100, 0), new Vector2(0, 0), new Vector2(100,0),
        new Vector2(-100, -100), new Vector2(0, -100), new Vector2(100,-100) };

        public static readonly int mainHotSpotMask = 1 << (int)DisplayConfiguration.ScreenPosition.UpperCenter |
            1 << (int)DisplayConfiguration.ScreenPosition.MiddleLeft |
            1 << (int)DisplayConfiguration.ScreenPosition.MiddleCenter |
            1 << (int)DisplayConfiguration.ScreenPosition.MiddleRight |
            1 << (int)DisplayConfiguration.ScreenPosition.LowerCenter;

        private int currentDisplay;

        #endregion

        #region Unity Methods
        public void Awake()
        {
            if (instance != null)
                Destroy(gameObject);

            instance = this;

            InitializeReferences();
            InitializeMenu();
            InitializeDisplayConfiguration();
            InitializeHotspots();

            ControllerIcon.AddDisplayListener(this);
        }
        #endregion

        #region Initialization
        private void InitializeReferences()
        {
            screenHotspotPositions = new Vector2[9] {
            new Vector2(-rowDimensions.x, rowDimensions.y), new Vector2(0, rowDimensions.y), new Vector2(rowDimensions.x,rowDimensions.y),
            new Vector2(-rowDimensions.x, 0), new Vector2(0, 0), new Vector2(rowDimensions.x,0),
            new Vector2(-rowDimensions.x, -rowDimensions.y), new Vector2(0, -rowDimensions.y), new Vector2(rowDimensions.x,-rowDimensions.y) };

            displayConfigurations = new List<DisplayConfiguration>();
            textureDisplay = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/display.png");
            textureScreenDivider = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/divider.png");
            followerCoroutines = new Dictionary<ControllerIcon, IEnumerator[]>();
            distances = new Dictionary<RectTransform, float[]>();
        }
        private void InitializeMenu()
        {
            Destroy(transform.GetChild(0).gameObject);
            Destroy(transform.GetChild(1).gameObject);

            UnityEngine.UI.HorizontalLayoutGroup layout = gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            display = DoDad.Library.UI.Utils.CreateImage("Display").GetComponent<RectTransform>();
            display.SetParent(transform);
            display.localScale = Vector3.one;

            UnityEngine.UI.Image _backgroundImage = display.GetComponent<UnityEngine.UI.Image>();

            _backgroundImage.sprite = Sprite.Create(textureDisplay, new Rect(Vector2.zero, new Vector2(textureDisplay.width, textureDisplay.height)), Vector2.zero);
            _backgroundImage.SetNativeSize();

            screenHotspotContainer = new GameObject("Screen Hotspot Container", typeof(RectTransform)).GetComponent<RectTransform>();
            screenHotspotContainer.SetParent(display);
            screenHotspotContainer.localScale = Vector3.one;
            screenHotspotContainer.localPosition = Vector3.zero;

        }
        private void InitializeDisplayConfiguration()
        {
            if (displayConfigurations.Count != 0)
                displayConfigurations.Clear();

            for(int e = 0; e < Display.displays.Length; e++)
            {
                displayConfigurations.Add(new DisplayConfiguration());
            }

            foreach (XSplitScreen.SplitScreenConfiguration.ControllerAssignment assignment in XSplitScreen.Configuration.ControllerAssignments)
            {
                if(assignment.AssignedDisplay > -1)
                    AssignToDisplay(assignment.IsKeyboard, assignment.AssignedDeviceId, assignment.AssignedDisplay, assignment.AssignedScreen, -1);
            }


        }
        private void InitializeHotspots()
        {
            for(int e = 0; e < screenHotspotPositions.Length; e++)
            {
                screenHotSpots[e] = Utils.CreateImage($"Hotspot {e.ToString()}").GetComponent<RectTransform>();
                screenHotSpots[e].SetParent(screenHotspotContainer);
                screenHotSpots[e].localScale = Vector3.one;
                screenHotSpots[e].localPosition = screenHotspotPositions[e];
            }
        }
        #endregion

        #region Public Methods
        public void RequestDropAssignment(ControllerIcon icon, Follower follower)
        {
            bool isKeyboard = icon.controller.type == ControllerType.Keyboard;

            int closestIndex = -1;
            int mainIndex = -1;

            float minDistance = float.MaxValue;
            float minMainDistance = float.MaxValue;
            float range = maximumHotspotDistance * maximumHotspotDistance;

            RectTransform rectTransform = follower.GetComponent<RectTransform>();

            // Find minimum distance to a hotspot
            for (int e = 0; e < distances[rectTransform].Length; e++)
            {
                if (distances[rectTransform][e] < minDistance)
                {
                    minDistance = distances[rectTransform][e];
                    closestIndex = e;

                    if(Library.Bitshift.Helpers.IsLayerInMask(e, mainHotSpotMask))
                    {
                        if(distances[rectTransform][e] < minMainDistance)
                        {
                            minMainDistance = distances[rectTransform][e];
                            mainIndex = e;
                        }
                    }
                }
            }

            DisplayConfiguration.ScreenPosition requestedPosition = (DisplayConfiguration.ScreenPosition)closestIndex;

            if (closestIndex == -1)
                UnassignFromDisplay(isKeyboard, icon.controller.id);
            else
                AssignToDisplay(isKeyboard, icon.controller.id, currentDisplay, closestIndex, mainIndex);
        }
        #endregion

        #region Events
        public int OnIconDrag(ControllerIcon icon)
        {
            followerCoroutines.Add(icon, new IEnumerator[screenHotSpots.Length]);
            distances.Add(icon.follower, new float[screenHotSpots.Length]);

            for(int e = 0; e < screenHotSpots.Length; e++)
            {
                followerCoroutines[icon][e] = CalculateDistance(icon.follower, e);
                StartCoroutine(followerCoroutines[icon][e]);
            }

            return 0;
        }
        #endregion

        #region Assignment
        private void AssignToDisplay(bool isKeyboard, int deviceId, int displayId, int screenId, int screenSectionId)
        {
            if (displayId <= Display.displays.Length - 1 && displayId > -1)
            {
                UnassignFromDisplay(isKeyboard, deviceId);
                displayConfigurations[displayId].AssignDevice(isKeyboard, deviceId, screenId, screenSectionId);
            }
        }
        private void UnassignFromDisplay(bool isKeyboard, int deviceId)
        {
            for(int e = 0; e < displayConfigurations.Count; e++)
            {
                displayConfigurations[e].Unassign(isKeyboard, deviceId);
            }
        }
        #endregion

        #region Coroutines
        private IEnumerator CalculateDistance(RectTransform follower, int hotspotId)
        {
            Vector3 position = screenHotSpots[hotspotId].position;

            Vector3 distance;

            int maxFrames = 5;
            int currentFrame = 5;

            while (true)
            {
                if(currentFrame < maxFrames)
                {
                    currentFrame++;
                    yield return null;
                }

                distance = position - follower.localPosition;

                distances[follower][hotspotId] = distance.sqrMagnitude;

                currentFrame = 0;

                yield return null;
            }
        }
        #endregion

        #region Definitions
        public class DisplayConfiguration
        {
            public UnityEvent onDisplayUpdated { get; private set; }

            private int[] availableScreens = new int[9] { -1, -1, -1, -1, -1, -1, -1, -1, -1 };
            private string[] assignedProfiles = new string[9] { "", "", "", "", "", "", "", "", "", };

            private bool[] isKeyboard = new bool[9] { false, false, false, false, false, false, false, false, false };

            public DisplayConfiguration()
            {
                onDisplayUpdated = new UnityEvent();
            }
            public void AssignDevice(bool isKeyboard, int deviceId, int screenId, int screenSectionId)
            {
                if (availableScreens[screenId] == deviceId && this.isKeyboard[screenId] == isKeyboard)
                    return;

                // Hotspots should not be assigned to out of order. Main hotspots must be assigned first.
                ScreenPosition requestedPosition = ScreenPosition.None;

                try
                {
                    requestedPosition = (ScreenPosition)screenId;
                }
                catch(Exception e)
                {
                    Debug.Log("Placeholder Error - unable to parse screenId");
                }
                Debug.Log($"Before assign '{deviceId.ToString()}' to '{requestedPosition.ToString()}'");

                if (requestedPosition == ScreenPosition.None)
                    return;

                // Screen has single controller assigned
                if(availableScreens[(int)ScreenPosition.MiddleCenter] > -1)
                    requestedPosition = (ScreenPosition)screenSectionId;
                // Upper half division

                int upperIndex = (int)requestedPosition - 3;
                int lowerIndex = (int)requestedPosition + 3;
                int leftIndex = (int)requestedPosition - 1;
                int rightIndex = (int)requestedPosition + 1;

                upperIndex = upperIndex >= availableScreens.Length ? -1 : upperIndex;
                lowerIndex = lowerIndex >= availableScreens.Length ? -1 : lowerIndex;
                leftIndex = leftIndex >= availableScreens.Length ? -1 : leftIndex;
                rightIndex = rightIndex >= availableScreens.Length ? -1 : rightIndex;

                // Get neighbor indices
                // If they're all valid, we're the center piece
                // - Check for corner pieces
                // - If corner pieces exist,
                // idk figure it out
                bool hasMainAssignment = HasAssignment(upperIndex);
                hasMainAssignment |= HasAssignment(lowerIndex);
                hasMainAssignment |= HasAssignment(leftIndex);
                hasMainAssignment |= HasAssignment(rightIndex);

                if(!hasMainAssignment)
                {
                    // Split tracking into main and corner hotspots
                    // if a main assignment exists, 
                }

                ScreenPosition upperPosition = (ScreenPosition)Mathf.Clamp(upperIndex, -1, 8);
                ScreenPosition lowerPosition = (ScreenPosition)Mathf.Clamp(lowerIndex, -1, 8);
                ScreenPosition leftPosition = (ScreenPosition)Mathf.Clamp(leftIndex, -1, 8);
                ScreenPosition rightPosition = (ScreenPosition)Mathf.Clamp(rightIndex, -1, 8);

                int upperShiftPositionInt = (int)upperPosition - 3;
                int lowerShiftPositionInt = (int)lowerPosition + 3;
                int leftShiftPositionInt = (int)leftPosition - 1;
                int rightShiftPositionInt = (int)rightPosition + 1;

                upperShiftPositionInt = upperShiftPositionInt >= availableScreens.Length ? -1 : upperShiftPositionInt;
                lowerShiftPositionInt = lowerShiftPositionInt >= availableScreens.Length ? -1 : lowerShiftPositionInt;
                rightShiftPositionInt = rightShiftPositionInt >= availableScreens.Length ? -1 : rightShiftPositionInt;
                leftShiftPositionInt = leftShiftPositionInt >= availableScreens.Length ? -1 : leftShiftPositionInt;

                ScreenPosition upperShiftPosition = (ScreenPosition)Mathf.Clamp(upperShiftPositionInt, -1, 8);
                ScreenPosition lowerShiftPosition = (ScreenPosition)Mathf.Clamp(lowerShiftPositionInt, -1, 8);
                ScreenPosition leftShiftPosition = (ScreenPosition)Mathf.Clamp(leftShiftPositionInt, -1, 8);
                ScreenPosition rightShiftPosition = (ScreenPosition)Mathf.Clamp(rightShiftPositionInt, -1, 8);

                if (deviceId > -1)
                {
                    ShiftValue(upperPosition, upperShiftPosition);
                    SetValue(upperPosition, -1);

                    ShiftValue(lowerPosition, lowerShiftPosition);
                    SetValue(lowerPosition, -1);

                    ShiftValue(leftPosition, lowerShiftPosition);
                    SetValue(leftPosition, -1);

                    ShiftValue(rightPosition, lowerShiftPosition);
                    SetValue(rightPosition, -1);
                }
                else
                {
                    ShiftValue(upperShiftPosition, upperPosition);
                    SetValue(upperShiftPosition, -1);

                    ShiftValue(lowerShiftPosition, lowerPosition);
                    SetValue(lowerShiftPosition, -1);

                    ShiftValue(leftShiftPosition, lowerPosition);
                    SetValue(leftShiftPosition, -1);

                    ShiftValue(rightShiftPosition, lowerPosition);
                    SetValue(rightShiftPosition, -1);
                }
                Debug.Log($"After '{deviceId.ToString()}' to '{requestedPosition.ToString()}'");
                Debug.Log($"[{availableScreens[0]}] [{availableScreens[1]}] [{availableScreens[2]}] ");
                Debug.Log($"[{availableScreens[3]}] [{availableScreens[4]}] [{availableScreens[5]}] ");
                Debug.Log($"[{availableScreens[6]}] [{availableScreens[7]}] [{availableScreens[8]}] ");
            }
            public void Unassign(bool isKeyboard, int deviceId)
            {
                for(int e = 0; e < availableScreens.Length; e++)
                {
                    if(availableScreens[e] == deviceId)
                    {
                        if(this.isKeyboard[e] == isKeyboard)
                        {
                            AssignDevice(false, -1, e, -1);
                            break;
                        }
                    }
                }
            }
            public List<ControllerAssignment> GetAssignments()
            {
                List<ControllerAssignment> assignmentList = new List<ControllerAssignment>();

                for (int e = 0; e < availableScreens.Length; e++)
                {
                    if(availableScreens[e] > -1)
                    {
                        assignmentList.Add(new ControllerAssignment()
                        {
                            AssignedDeviceId = availableScreens[e],
                             AssignedProfile = assignedProfiles[e],
                              AssignedDisplay = -1,
                               AssignedScreen = e,
                                IsKeyboard = isKeyboard[e]
                        });
                    }
                }

                return assignmentList;
            }
            public void Save()
            {
                onDisplayUpdated.Invoke();
            }
            private void ShiftValue(ScreenPosition valueIndex, ScreenPosition targetIndex)
            {
                if (valueIndex == ScreenPosition.None)
                    return;

                availableScreens[(int)targetIndex] = availableScreens[(int)valueIndex];
            }
            private void SetValue(ScreenPosition valueIndex, int value)
            {
                if (valueIndex == ScreenPosition.None)
                    return;

                availableScreens[(int)valueIndex] = value;
            }
            private int GetValue(int index)
            {
                return availableScreens[index];
            }
            private bool HasValue(bool isKeyboard, int value)
            {
                for(int e = 0; e < availableScreens.Length; e++)
                {
                    if (availableScreens[e] == value && isKeyboard == this.isKeyboard[e])
                        return true;
                }

                return false;
            }
            private bool HasAssignment(int screenId)
            {
                if (screenId < 0 || screenId > availableScreens.Length)
                    return false;

                return availableScreens[screenId] > -1;
            }
            public enum ScreenPosition
            {
                None = -1,
                UpperLeft = 0,
                UpperCenter = 1,
                UpperRight = 2,
                MiddleLeft = 3,
                MiddleCenter = 4,
                MiddleRight = 5,
                LowerLeft = 6,
                LowerCenter = 7,
                LowerRight = 8
            }
        }
        #endregion
    }

}
