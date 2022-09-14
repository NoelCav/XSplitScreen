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
using System.Linq;

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

        public static int hotspotCount { get; private set; }

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

        public static readonly int mainHotSpotMask = 1 << (int)DisplayConfiguration.ScreenPosition.HigherUp |
            1 << (int)DisplayConfiguration.ScreenPosition.MiddleLeft |
            1 << (int)DisplayConfiguration.ScreenPosition.MiddleCenter |
            1 << (int)DisplayConfiguration.ScreenPosition.MiddleRight |
            1 << (int)DisplayConfiguration.ScreenPosition.LowerDown;

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
        public void Update()
        {
            UpdateIconListeners();
        }
        #endregion

        #region Initialization
        private void InitializeReferences()
        {
            screenHotspotPositions = new Vector2[9] {
            new Vector2(-rowDimensions.x, rowDimensions.y), new Vector2(0, rowDimensions.y), new Vector2(rowDimensions.x,rowDimensions.y),
            new Vector2(-rowDimensions.x, 0), new Vector2(0, 0), new Vector2(rowDimensions.x,0),
            new Vector2(-rowDimensions.x, -rowDimensions.y), new Vector2(0, -rowDimensions.y), new Vector2(rowDimensions.x,-rowDimensions.y) };

            hotspotCount = screenHotspotPositions.Length;
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
            // Check for end of drag and remove icon from the list + shut down coroutines
            followerCoroutines.Add(icon, new IEnumerator[screenHotSpots.Length]);

            if(!distances.ContainsKey(icon.follower))
                distances.Add(icon.follower, new float[screenHotSpots.Length]);

            for(int e = 0; e < screenHotSpots.Length; e++)
            {
                followerCoroutines[icon][e] = CalculateDistance(icon.follower, e);
                StartCoroutine(followerCoroutines[icon][e]);
            }

            return 0;
        }
        #endregion

        #region Controller Icons
        private void UpdateIconListeners()
        {
            List<ControllerIcon> removables = new List<ControllerIcon>();

            foreach(KeyValuePair<ControllerIcon, IEnumerator[]> keyPair in followerCoroutines)
            {
                if(!keyPair.Key.isDragging)
                {
                    removables.Add(keyPair.Key);
                }
            }

            foreach(ControllerIcon removable in removables)
            {
                foreach(IEnumerator enumerator in followerCoroutines[removable])
                {
                    StopCoroutine(enumerator);
                }
                
                followerCoroutines.Remove(removable);
            }
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
            #region Variables
            public UnityEvent onDisplayUpdated { get; private set; }

            private ScreenConfig[] screens = new ScreenConfig[9];

            private int[] availableScreens = new int[9] { -1, -1, -1, -1, -1, -1, -1, -1, -1 };
            private string[] assignedProfiles = new string[9] { "", "", "", "", "", "", "", "", "", };

            private bool[] isKeyboard = new bool[9] { false, false, false, false, false, false, false, false, false };
            #endregion

            #region Public Methods
            public DisplayConfiguration()
            {
                onDisplayUpdated = new UnityEvent();
                
                for(int e = 0; e < screens.Length; e++)
                {
                    screens[e] = new ScreenConfig(e);
                }
            }
            public void AssignDevice(bool isKeyboard, int deviceId, int screenId, int screenSectionId)
            {
                // Duplicate ALL movements to isKeyboard!!!!!!! or make an intbool class to store info
                if (screens[screenId].Matches(deviceId, isKeyboard))
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

                if (requestedPosition == ScreenPosition.None)
                    return;

                if(IsEmptyScreen())
                    requestedPosition = ScreenPosition.MiddleCenter;
                else
                {
                    ScreenPosition screenSectionPosition = (ScreenPosition)screenSectionId;

                    int assignedDevice = GetAssignment((int)ScreenPosition.MiddleCenter);

                    if (assignedDevice > -1)
                    {
                        requestedPosition = screenSectionPosition;

                        if (assignedDevice == deviceId)
                        {
                            if (isKeyboard == this.isKeyboard[(int)ScreenPosition.MiddleCenter])
                            {
                                Debug.Log("Matching devices");
                                return;
                            }
                        }
                    }
                }


                if (HasAssignment((int)requestedPosition))
                {
                    Debug.Log($"Assigned: {screens[(int)requestedPosition].deviceId}");
                    SetValue(requestedPosition, deviceId);
                    DisplayUpdated();
                    DebugGrid();
                    return;
                }

                int upperIndex = (int)requestedPosition - 3;
                int lowerIndex = (int)requestedPosition + 3;
                int leftIndex = (int)requestedPosition - 1;
                int rightIndex = (int)requestedPosition + 1;

                upperIndex = upperIndex >= screens.Length ? -1 : upperIndex;
                lowerIndex = lowerIndex >= screens.Length ? -1 : lowerIndex;
                leftIndex = leftIndex >= screens.Length ? -1 : leftIndex;
                rightIndex = rightIndex >= screens.Length ? -1 : rightIndex;

                ScreenPosition upperPosition = (ScreenPosition)Mathf.Clamp(upperIndex, -1, 8);
                ScreenPosition lowerPosition = (ScreenPosition)Mathf.Clamp(lowerIndex, -1, 8);
                ScreenPosition leftPosition = (ScreenPosition)Mathf.Clamp(leftIndex, -1, 8);
                ScreenPosition rightPosition = (ScreenPosition)Mathf.Clamp(rightIndex, -1, 8);

                int upperShiftPositionInt = (int)upperPosition - 3;
                int lowerShiftPositionInt = (int)lowerPosition + 3;
                int leftShiftPositionInt = (int)leftPosition - 1;
                int rightShiftPositionInt = (int)rightPosition + 1;

                upperShiftPositionInt = upperShiftPositionInt >= screens.Length ? -1 : upperShiftPositionInt;
                lowerShiftPositionInt = lowerShiftPositionInt >= screens.Length ? -1 : lowerShiftPositionInt;
                rightShiftPositionInt = rightShiftPositionInt >= screens.Length ? -1 : rightShiftPositionInt;
                leftShiftPositionInt = leftShiftPositionInt >= screens.Length ? -1 : leftShiftPositionInt;

                ScreenPosition upperShiftPosition = (ScreenPosition)Mathf.Clamp(upperShiftPositionInt, -1, 8);
                ScreenPosition lowerShiftPosition = (ScreenPosition)Mathf.Clamp(lowerShiftPositionInt, -1, 8);
                ScreenPosition leftShiftPosition = (ScreenPosition)Mathf.Clamp(leftShiftPositionInt, -1, 8);
                ScreenPosition rightShiftPosition = (ScreenPosition)Mathf.Clamp(rightShiftPositionInt, -1, 8);

                // MainHotspots need to shift all main hotspots to the opposite side

                // Convert to multidimensional array
                // # # #
                // # # #
                // # # #
                // screenAssignments[x][y].deviceId

                if(IsLayerInMask((int)requestedPosition, DisplayManager.mainHotSpotMask))
                {
                    Debug.Log($"{requestedPosition} is main hotspot");
                    bool isInserting = deviceId > -1;

                    if (requestedPosition == ScreenPosition.MiddleCenter)
                    {
                        ClearScreens();

                        if(isInserting)
                            SetValue(ScreenPosition.MiddleCenter, deviceId);

                        DisplayUpdated();

                        Debug.Log("Inserted to middle");
                        DebugGrid();
                        return;
                    }

                    int shiftDirectionIndex = -1;

                    ScreenPosition shiftDirection = ScreenPosition.None;

                    if (requestedPosition == ScreenPosition.MiddleRight)
                        shiftDirection = ScreenPosition.MiddleLeft;
                    if (requestedPosition == ScreenPosition.MiddleLeft)
                        shiftDirection = ScreenPosition.MiddleRight;
                    if (requestedPosition == ScreenPosition.HigherUp)
                        shiftDirection = ScreenPosition.LowerDown;
                    if (requestedPosition == ScreenPosition.LowerDown)
                        shiftDirection = ScreenPosition.HigherUp;

                    if (shiftDirection == ScreenPosition.MiddleLeft)
                        shiftDirectionIndex = -1;
                    else if (shiftDirection == ScreenPosition.HigherUp)
                        shiftDirectionIndex = -3;
                    else if (shiftDirection == ScreenPosition.MiddleRight)
                        shiftDirectionIndex = 1;
                    else if (shiftDirection == ScreenPosition.LowerDown)
                        shiftDirectionIndex = 3;

                    // Remember to send unassign event to ControllerIcon
                    for (int e = 0; e < availableScreens.Length; e++)
                    {
                        Debug.Log($"Checking screen {(ScreenPosition)e}");

                        if ((ScreenPosition)e == requestedPosition)
                            continue;

                        if (IsLayerInMask(e, DisplayManager.mainHotSpotMask))
                        {
                            int shiftIndex = e + shiftDirectionIndex;
                            Debug.Log($"Shifting hotspot '{(ScreenPosition)e}' to '{(ScreenPosition)shiftIndex}'");

                            if (shiftIndex > -1 && shiftIndex < availableScreens.Length)
                            {
                                if(isInserting)
                                {
                                    ShiftValue((ScreenPosition)e, (ScreenPosition)shiftIndex);
                                    SetValue((ScreenPosition)e, -1);
                                }
                                else
                                {
                                    ShiftValue((ScreenPosition)shiftIndex, (ScreenPosition)e);
                                    SetValue((ScreenPosition)shiftIndex, -1);
                                }
                            }
                        }
                    }

                    SetValue(requestedPosition, deviceId);
                    Debug.Log("Shifted main hotspots");
                    DebugGrid();
                }
                else
                {
                    if (deviceId > -1)
                    {
                        Debug.Log("With device");

                        ShiftValue(upperPosition, upperShiftPosition);
                        SetValue(upperPosition, -1);

                        ShiftValue(lowerPosition, lowerShiftPosition);
                        SetValue(lowerPosition, -1);

                        ShiftValue(leftPosition, leftShiftPosition);
                        SetValue(leftPosition, -1);

                        ShiftValue(rightPosition, rightShiftPosition);
                        SetValue(rightPosition, -1);
                    }
                    else
                    {
                        ShiftValue(upperShiftPosition, upperPosition);
                        SetValue(upperShiftPosition, -1);

                        ShiftValue(lowerShiftPosition, lowerPosition);
                        SetValue(lowerShiftPosition, -1);

                        ShiftValue(leftShiftPosition, leftPosition);
                        SetValue(leftShiftPosition, -1);

                        ShiftValue(rightShiftPosition, rightPosition);
                        SetValue(rightShiftPosition, -1);
                    }

                    SetValue(requestedPosition, deviceId);

                    Debug.Log("Shifted non-main hotspot");
                    DebugGrid();
                }
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
            public bool IsEmptyScreen()
            {
                for (int e = 0; e < availableScreens.Length; e++)
                    if (availableScreens[e] > -1)
                    {
                        return false;
                    }

                return true;
            }
            public void Save()
            {
                onDisplayUpdated.Invoke();
            }
            #endregion

            #region Assignments
            private void DebugGrid()
            {
                Debug.Log($"[{availableScreens[0]}] [{availableScreens[1]}] [{availableScreens[2]}] ");
                Debug.Log($"[{availableScreens[3]}] [{availableScreens[4]}] [{availableScreens[5]}] ");
                Debug.Log($"[{availableScreens[6]}] [{availableScreens[7]}] [{availableScreens[8]}] ");
            }
            private bool IsLayerInMask(int layer, int layerMask)
            {
                return Library.Bitshift.Helpers.IsLayerInMask(layer, layerMask);
            }
            private void ShiftHotspot(int hotspotId, bool main = false)
            {

            }
            private void ShiftValue(ScreenPosition valueIndex, ScreenPosition targetIndex)
            {
                if (valueIndex == ScreenPosition.None || targetIndex == ScreenPosition.None)
                    return;

                availableScreens[(int)targetIndex] = availableScreens[(int)valueIndex];
            }
            private void ClearScreens()
            {
                for (int e = 0; e < availableScreens.Length; e++)
                {
                    availableScreens[e] = -1;
                }
            }
            private void SetValue(ScreenPosition valueIndex, int value)
            {
                if (valueIndex == ScreenPosition.None)
                    return;

                availableScreens[(int)valueIndex] = value;
            }
            private int GetAssignment(int screenId)
            {
                if (screenId < 0 || screenId > availableScreens.Length)
                    return -1;

                return availableScreens[screenId];
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
                if (screenId < 0 || screenId >= availableScreens.Length)
                    return false;

                return availableScreens[screenId] > -1;
            }
            #endregion

            #region Events
            public void DisplayUpdated()
            {
                // TODO notify listeners (all ControllerIcons)
            }
            #endregion

            #region Definitions
            public struct ScreenConfig
            {
                public int deviceId;
                public string profile;
                public bool isKeyboard;

                public ScreenPosition upperPosition { get; private set; }
                public ScreenPosition lowerPosition { get; private set; }
                public ScreenPosition leftPosition { get; private set; }
                public ScreenPosition rightPosition { get; private set; }

                public ScreenPosition upperShiftPosition { get; private set; }
                public ScreenPosition lowerShiftPosition { get; private set; }
                public ScreenPosition leftShiftPosition { get; private set; }
                public ScreenPosition rightShiftPosition { get; private set; }

                public int2 neighborUp { get; private set; };
                public int2 neighborRight { get; private set; };
                public int2 neighborDown { get; private set; };
                public int2 neighborLeft { get; private set; };
                public int2 neighborUpShift { get; private set; };
                public int2 neighborRightShift { get; private set; };
                public int2 neighborDownShift { get; private set; };
                public int2 neighborLeftShift { get; private set; };


                public ScreenConfig(int index, int deviceId = -1, string profile = "", bool isKeyboard = false)
                {
                    int2 index2 = new int2(0, 0);

                    this.deviceId = deviceId;
                    this.profile = profile;
                    this.isKeyboard = isKeyboard;

                    neighborUp = new int2(index2.x, index2.y + 1);
                    neighborRight = new int2(index2.x + 1, index2.y);
                    neighborDown = new int2(index2.x, index2.y - 1);
                    neighborLeft = new int2(index2.x - 1, index2.y);

                    neighborUpShift = new int2(neighborUp.x, neighborUp.y + 1);
                    neighborRightShift = new int2(neighborRight.x + 1, neighborRight.y);
                    neighborDownShift = new int2(neighborDown.x, neighborDown.y - 1);
                    neighborLeftShift = new int2(neighborLeft.x - 1, neighborLeft.y);

                    int upperIndex = index - 3;
                    int lowerIndex = index + 3;
                    int leftIndex = index - 1;
                    int rightIndex = index + 1;

                    // Clamp upper limit
                    upperIndex = upperIndex >= DisplayManager.hotspotCount ? -1 : upperIndex;
                    lowerIndex = lowerIndex >= DisplayManager.hotspotCount ? -1 : lowerIndex;
                    leftIndex = leftIndex >= DisplayManager.hotspotCount ? -1 : leftIndex;
                    rightIndex = rightIndex >= DisplayManager.hotspotCount ? -1 : rightIndex;

                    // Clamp lower limit
                    upperPosition = (ScreenPosition)Mathf.Clamp(upperIndex, -1, 8);
                    lowerPosition = (ScreenPosition)Mathf.Clamp(lowerIndex, -1, 8);
                    leftPosition = (ScreenPosition)Mathf.Clamp(leftIndex, -1, 8);
                    rightPosition = (ScreenPosition)Mathf.Clamp(rightIndex, -1, 8);

                    // Repeat for upper shift
                    int upperShiftPositionInt = (int)upperPosition - 3;
                    int lowerShiftPositionInt = (int)lowerPosition + 3;
                    int leftShiftPositionInt = (int)leftPosition - 1;
                    int rightShiftPositionInt = (int)rightPosition + 1;

                    upperShiftPositionInt = upperShiftPositionInt >= DisplayManager.hotspotCount ? -1 : upperShiftPositionInt;
                    lowerShiftPositionInt = lowerShiftPositionInt >= DisplayManager.hotspotCount ? -1 : lowerShiftPositionInt;
                    rightShiftPositionInt = rightShiftPositionInt >= DisplayManager.hotspotCount ? -1 : rightShiftPositionInt;
                    leftShiftPositionInt = leftShiftPositionInt >= DisplayManager.hotspotCount ? -1 : leftShiftPositionInt;

                    upperShiftPosition = (ScreenPosition)Mathf.Clamp(upperShiftPositionInt, -1, 8);
                    lowerShiftPosition = (ScreenPosition)Mathf.Clamp(lowerShiftPositionInt, -1, 8);
                    leftShiftPosition = (ScreenPosition)Mathf.Clamp(leftShiftPositionInt, -1, 8);
                    rightShiftPosition = (ScreenPosition)Mathf.Clamp(rightShiftPositionInt, -1, 8);
                }

                public bool Matches(int deviceId, bool isKeyboard)
                {
                    return (deviceId == this.deviceId && isKeyboard == this.isKeyboard);
                }

                private int Validate(int2 position)
                {
                    if (position.x < 0)
                        position.x = -1;
                    if(position.x > )
                }
            }
            public enum ScreenPosition
            {
                None = -1,
                HigherLeft = 0,
                HigherUp = 1,
                HigherRight = 2,
                MiddleLeft = 3,
                MiddleCenter = 4,
                MiddleRight = 5,
                LowerLeft = 6,
                LowerDown = 7,
                LowerRight = 8
            }
            #endregion
        }
        #endregion
    }

}
