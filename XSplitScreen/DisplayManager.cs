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
    // TODO
    // Rewrite SplitscreenConfigurationManager to store screens as int2
    // Pass along to this class
    // Finish sorting & grid behaviour
    // ??
    public class DisplayManager : MonoBehaviour
    {
        #region Variables
        public static DisplayManager instance { get; private set; }

        public static int rowSize { get; private set; }

        private static readonly float maxIconDropDistance = 5f;
        private static readonly int2 rowDimensions = new int2(150, 150);

        public List<DisplayConfiguration> displayConfigurations { get; private set; }

        public RectTransform[] screenSlots { get; private set; }

        public int currentDisplay { get; private set; }

        private RectTransform display;
        private RectTransform screenSlotContainer;

        private Texture2D textureDisplay;
        private Texture2D textureScreenDivider;


        private Vector2[] screenSlotPositions;

        public static readonly int mainSlotMask = 1 << (int)DisplayConfiguration.ScreenPosition.HigherUp |
            1 << (int)DisplayConfiguration.ScreenPosition.MiddleLeft |
            1 << (int)DisplayConfiguration.ScreenPosition.MiddleCenter |
            1 << (int)DisplayConfiguration.ScreenPosition.MiddleRight |
            1 << (int)DisplayConfiguration.ScreenPosition.LowerDown;

        private Dictionary<ControllerIcon, FollowerDistanceMonitor> distanceMonitors;

        private bool initialized = false;
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
            InitializeSlots();
        }
        public void Update()
        {
            UpdateIconListeners();
        }
        #endregion

        #region Initialization
        private void InitializeReferences()
        {
            screenSlotPositions = new Vector2[9] {
            new Vector2(-rowDimensions.x, -rowDimensions.y), new Vector2(0, -rowDimensions.y), new Vector2(rowDimensions.x,-rowDimensions.y),
            new Vector2(-rowDimensions.x, 0), new Vector2(0, 0), new Vector2(rowDimensions.x,0),
            new Vector2(-rowDimensions.x, rowDimensions.y), new Vector2(0, rowDimensions.y), new Vector2(rowDimensions.x,rowDimensions.y) };

            rowSize = Library.Math.Utils.Minimum2dGridSize(screenSlotPositions.Length);

            displayConfigurations = new List<DisplayConfiguration>();
            textureDisplay = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/display.png");
            textureScreenDivider = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/divider.png");
            distanceMonitors = new Dictionary<ControllerIcon, FollowerDistanceMonitor>();
            screenSlots = new RectTransform[9];
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

            screenSlotContainer = new GameObject("Screen Slot Container", typeof(RectTransform)).GetComponent<RectTransform>();
            screenSlotContainer.SetParent(display);
            screenSlotContainer.localScale = Vector3.one;
            screenSlotContainer.localPosition = Vector3.zero;

        }
        private void InitializeDisplayConfiguration()
        {
            if (displayConfigurations.Count != 0)
                displayConfigurations.Clear();
            // Fix index out of range
            for(int e = 0; e < Display.displays.Length; e++)
            {
                List<ControllerAssignment> selected = XSplitScreen.Configuration.ControllerAssignments.Where(x => x.deviceId == e).ToList();

                displayConfigurations.Add(new DisplayConfiguration(selected));
            }
        }
        private void InitializeSlots()
        {
            // the current config uses a coordinate system for slots with x being vertical
            // need to rewrite it so that x is horizontal
            for(int e = 0; e < screenSlotPositions.Length; e++)
            {
                screenSlots[e] = Library.UI.Utils.CreateImage($"Slot {e.ToString()}").GetComponent<RectTransform>();
                screenSlots[e].SetParent(screenSlotContainer);
                screenSlots[e].localScale = Vector3.one;
                screenSlots[e].localPosition = screenSlotPositions[e];
            }
        }
        private void InitializeIconListeners()
        {
            if(ControllerIcon.activeIcons.Count > 0)
            {
                foreach(ControllerIcon icon in ControllerIcon.activeIcons)
                {
                    OnIconAdded(icon);
                }
            }

            ControllerIcon.onIconAdded.AddListener(OnIconAdded);
            ControllerIcon.onIconRemoved.AddListener(OnIconRemoved);
        }
        #endregion

        #region Public Methods
        public bool ChangeDisplay(int newDisplay)
        {
            // change display
            return false;
        }
        public void RequestDropAssignment(ControllerIcon icon)
        {
            bool isKeyboard = icon.controller.type == ControllerType.Keyboard;

            float range = maxIconDropDistance * maxIconDropDistance;

            int closestIndex = distanceMonitors[icon].GetClosestSlotIndex(range);
            int mainIndex = distanceMonitors[icon].GetClosestMainSlotIndex(range);

            Debug.Log($"closestIndex: {closestIndex}");
            Debug.Log($"mainSlot: '{mainIndex}'");

            ControllerAssignment newAssignment = new ControllerAssignment();

            newAssignment.deviceId = icon.controller.id;
            newAssignment.isKeyboard = isKeyboard;
            newAssignment.displayId = currentDisplay;
            newAssignment.profile = "";
            newAssignment.screenId = closestIndex;

            UnassignFromDisplay(newAssignment);

            if (closestIndex > -1)
                AssignToDisplay(newAssignment, mainIndex);
        }
        #endregion

        #region Events
        public void OnIconAdded(MonoBehaviour behaviour)
        {
            ControllerIcon icon;

            try
            {
                icon = (ControllerIcon)behaviour;
            }
            catch (Exception e)
            {
                return;
            }

            if (icon == null)
                return;
            
            distanceMonitors.Add(icon, new FollowerDistanceMonitor(screenSlots, icon.cursorFollower));

            icon.onStartDrag.AddListener(OnStartDrag);
            icon.onStopDrag.AddListener(OnStopDrag);
        }
        public void OnIconRemoved(MonoBehaviour behaviour)
        {
            ControllerIcon icon;

            try
            {
                icon = (ControllerIcon)behaviour;
            }
            catch (Exception e)
            {
                return;
            }

            if (icon == null)
                return;

            distanceMonitors.Remove(icon);

            icon.onStartDrag.RemoveListener(OnStartDrag);
            icon.onStopDrag.RemoveListener(OnStopDrag);
        }
        public void OnStartDrag(MonoBehaviour behaviour)
        {
            ControllerIcon icon;

            try
            {
                icon = (ControllerIcon)behaviour;
            }
            catch(Exception e)
            {
                return;
            }

            if (icon == null)
                return;

            Debug.Log("Dragging!");
            distanceMonitors[icon].Start();
            // monitor distance
        }
        public void OnStopDrag(MonoBehaviour behaviour)
        {
            ControllerIcon icon;

            try
            {
                icon = (ControllerIcon)behaviour;
            }
            catch (Exception e)
            {
                return;
            }

            if (icon == null)
                return;

            Debug.Log("Not Dragging!");
            distanceMonitors[icon].Stop();
            RequestDropAssignment(icon);
            // stop monitoring distance
            // evaluate drop
        }
        public int OnIconDrag(ControllerIcon icon)
        {
            // Check for end of drag and remove icon from the list + shut down coroutines
            //followerCoroutines.Add(icon, new IEnumerator[screenHotSpots.Length]);

            //if(!distances.ContainsKey(icon.cursorFollower))
            //    distances.Add(icon.cursorFollower, new float[screenHotSpots.Length]);

            //for(int e = 0; e < screenHotSpots.Length; e++)
            //{
             //   followerCoroutines[icon][e] = CalculateDistance(icon.cursorFollower, e);
            //    StartCoroutine(followerCoroutines[icon][e]);
            //}

            return 0;
        }
        #endregion

        #region Controller Icons
        private void UpdateIconListeners()
        {
            if (ControllerIcon.onIconAdded == null)
                return;

            if (initialized)
                return;

            InitializeIconListeners();

            initialized = true;

            return;
        }
        #endregion

        #region Assignment
        private void AssignToDisplay(ControllerAssignment newAssignment, int mainSlot)
        {
            if (newAssignment.displayId < Display.displays.Length && newAssignment.displayId > -1)
            {
                displayConfigurations[newAssignment.displayId].AssignDevice(newAssignment, mainSlot);
            }
        }
        private void UnassignFromDisplay(ControllerAssignment oldAssignment)
        {
            for(int e = 0; e < displayConfigurations.Count; e++)
            {
                displayConfigurations[e].Unassign(oldAssignment);
            }
        }
        #endregion

        #region Definitions
        private class FollowerDistanceMonitor
        {
            public IEnumerator[] coroutines;
            public float[] distances;
            public bool[] mainSlot;
            public bool[] center;

            public FollowerDistanceMonitor(RectTransform[] slots, Follower follower)
            {
                distances = new float[slots.Length];
                coroutines = new IEnumerator[slots.Length];
                center = new bool[slots.Length];
                this.mainSlot = new bool[slots.Length];

                for (int e = 0; e < slots.Length; e++)
                {
                    coroutines[e] = CalculateDistance(e, follower, this);
                }

                foreach (int index in DisplayConfiguration.mainSlotIndices)
                    mainSlot[index] = true;

                center[DisplayConfiguration.centerSlotIndex] = true;
            }

            public void Start()
            {
                foreach(IEnumerator enumerator in coroutines)
                {
                    DisplayManager.instance.StartCoroutine(enumerator);
                }
            }
            public void Stop()
            {
                foreach (IEnumerator enumerator in coroutines)
                {
                    DisplayManager.instance.StopCoroutine(enumerator);
                }
            }
            public int GetClosestSlotIndex(float maximumRange)
            {
                int index = -1;

                float currentDistance = float.MaxValue;

                for (int e = 0; e < distances.Length; e++)
                {
                    float distance = distances[e] / 100f;

                    if (distance > maximumRange)
                        continue;

                    if (distance < currentDistance)
                    {
                        currentDistance = distance;
                        index = e;
                    }
                }

                return index;
            }
            public int GetClosestMainSlotIndex(float maximumRange)
            {
                int index = -1;

                float currentDistance = float.MaxValue;

                for (int e = 0; e < distances.Length; e++)
                {
                    if (!mainSlot[e])
                        continue;

                    float distance = distances[e] / 100f;

                    if (distance > maximumRange)
                        continue;

                    if (distance < currentDistance)
                    {
                        currentDistance = distance;
                        index = e;
                    }
                }

                return index;
            }
            
            #region Coroutines
            private IEnumerator CalculateDistance(int slotIndex, Follower follower, FollowerDistanceMonitor monitor)
            {
                RectTransform slot = DisplayManager.instance.screenSlots[slotIndex];
                RectTransform followerRect = follower.GetComponent<RectTransform>();

                Vector3 distance;

                float measureTime = 0.1f;
                float elapsedTime = 0f;

                while (true && follower != null && monitor != null)
                {
                    if(elapsedTime < measureTime)
                    {
                        elapsedTime += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    distance = slot.position - followerRect.localPosition;

                    monitor.distances[slotIndex] = distance.sqrMagnitude;

                    elapsedTime = 0;

                    yield return null;
                }
            }
            #endregion
        }
        public class DisplayConfiguration
        {
            #region Variables
            public static readonly int2[] mainSlotPositions = new int2[4]
            {
                new int2(0,1),
                new int2(1,0),
                new int2(1,2),
                new int2(2,1)
            };
            public static readonly int2 centerSlotPosition = new int2(1,1);

            public static readonly int[] mainSlotIndices = new int[4] { 1, 3, 5, 7 };
            public static readonly int centerSlotIndex = 4;
            public UnityEvent onDisplayUpdated { get; private set; }

            private SlotConfiguration[][] screenLayout;
            //private ScreenConfig[] screens = new ScreenConfig[9];

            //private int[] availableScreens = new int[9] { -1, -1, -1, -1, -1, -1, -1, -1, -1 };
            //private string[] assignedProfiles = new string[9] { "", "", "", "", "", "", "", "", "", };

            //private bool[] isKeyboard = new bool[9] { false, false, false, false, false, false, false, false, false };
            #endregion

            #region Public Methods
            public DisplayConfiguration(List<ControllerAssignment> assignments)
            {
                onDisplayUpdated = new UnityEvent();

                screenLayout = new SlotConfiguration[rowSize][];

                for (int row = 0; row < rowSize; row++)
                {
                    screenLayout[row] = new SlotConfiguration[rowSize];
                }

                int2 slotPosition = new int2(-1, -1);

                for(int x = 0; x < rowSize; x++)
                {
                    for(int y = 0; y < rowSize; y++)
                    {
                        slotPosition.x = x;
                        slotPosition.y = y;

                        bool isMain = false;
                        bool isCenter = false;

                        foreach(int2 mainSlotPosition in mainSlotPositions)
                        {
                            if (mainSlotPosition.Equals(slotPosition))
                            {
                                isMain = true;
                            }
                        }

                        if (centerSlotPosition.Equals(slotPosition))
                            isCenter = true;

                        screenLayout[slotPosition.x][slotPosition.y] = new SlotConfiguration(slotPosition, IsMainSlot(slotPosition), IsCenterSlot(slotPosition));
                        screenLayout[slotPosition.x][slotPosition.y].Validate();
                    }
                }
                /*
                for(int screenIndex = 0; screenIndex < screenLayout.Length * screenLayout.Length; screenIndex++)
                {
                    position.x++;

                    if(position.x == rowSize)
                    {
                        position.x = 0;
                        position.y--;

                        if (position.y == -1)
                            break; ;
                    }

                    screenLayout[position.x][position.y] = new SlotConfiguration(position, screenIndex, 
                        IsMainSlot(screenIndex), IsCenter(screenIndex));
                    screenLayout[position.x][position.y].Validate();
                }*/

                Initialize(assignments);

                DebugGrid();
                // TODO Initialize based on the current saved assignments
                // No shifting, just set them to the last saved configuration

                // Then fixed device assignment from the top down
                // Only dropping an icon should assign or unassign devices

                // ASSIGNMENT
                //
                // If current assignment is identical to requested assignment do nothing
                //
                // If grid empty change assignment to center
                // 
                // Else
                // 
                // If the grid has center then change assignment to main hotspot
                //
                // MOVEMENT
                //
                // If the requested assignment is empty
                //
                // - If the requested assignment is a main hotspot
                // -- If the requested assignment has neighbors
                // --- Shift neighbors away
                // -- Else
                // --- Shift ALL assignments a single direction
                //
                // If deviceId < 0 shift opposite direction of above
                // - If unassigning a main hotspot, it will never have neighbors
                // 
                // - Else
                //
                // - Shift all neighbors away
                // -- If deviceId < 0 shift opposite of above
                // 
                // Update the requested assignment values
                // 
                // Send "OnDeviceAssigned" and "OnDeviceUnassigned" events
                // ControllerIcon should update display status
            }
            public void AssignDevice(ControllerAssignment newAssignment, int mainSlot)
            {
                int2 screenPosition = Get2dIndex(newAssignment.screenId, rowSize, false);
                int2 centerPosition = Get2dIndex(centerSlotIndex, rowSize, false);

                if (screenPosition.IsPositive())
                {
                    ControllerAssignment existingAssignment = screenLayout[screenPosition.x][screenPosition.y].assignment;

                    if (existingAssignment.Matches(newAssignment))
                        return;

                    if (existingAssignment.deviceId > -1) // If assignment exists just replace it
                    {
                        SetValue(screenPosition, newAssignment);

                        DebugGrid();
                        // onDisplayUpdated
                        return;
                    }

                    if(IsEmpty())
                    {
                        SetCenter(newAssignment);
                        DebugGrid();
                        return;
                    }
                    else
                    {
                        if (IsAssigned(centerPosition))
                        {
                            Debug.Log($"Center position is assigned: {centerPosition}");
                            newAssignment.screenId = mainSlot;
                        }
                    }

                    int2 newPosition = Get2dIndex(newAssignment.screenId, rowSize, false);
                    Debug.Log($"updated Position: '{newPosition}'");

                    if (IsMainSlot(newPosition))
                    {
                        if(HasAssignedNeighbor(newPosition))
                        {
                            Debug.Log("ShiftRadial");
                            ShiftRadial(newPosition, newAssignment.deviceId > -1);
                        }
                        else
                        {
                            Debug.Log("ShiftLinear");
                            DirectionalPointer direction = DirectionalPointer.Self;

                            if (NeighborExists(newPosition, DirectionalPointer.Up))
                                direction = newAssignment.deviceId > -1 ? DirectionalPointer.Down : DirectionalPointer.Up;
                            if (NeighborExists(newPosition, DirectionalPointer.Right))
                                direction = newAssignment.deviceId > -1 ? DirectionalPointer.Left : DirectionalPointer.Right;
                            if (NeighborExists(newPosition, DirectionalPointer.Down))
                                direction = newAssignment.deviceId > -1 ? DirectionalPointer.Up : DirectionalPointer.Down;
                            if (NeighborExists(newPosition, DirectionalPointer.Left))
                                direction = newAssignment.deviceId > -1 ? DirectionalPointer.Right : DirectionalPointer.Left;

                            Reset(newPosition);
                            ShiftLinear(direction);
                        }
                    }
                    else
                    {
                        Debug.Log("Normal ShiftRadial");
                        ShiftRadial(newPosition, newAssignment.deviceId > -1);
                    }
                    Debug.Log($"Final SetValue: {newPosition}");
                    SetValue(newPosition, newAssignment);
                    DebugGrid();
                }

            }
            public void Unassign(ControllerAssignment oldAssignment)
            {
                for(int x = 0; x < screenLayout.Length; x++)
                {
                    for(int y = 0; y < screenLayout.Length; y++)
                    {
                        if(screenLayout[x][y].assignment.Matches(oldAssignment))
                        {
                            screenLayout[x][y].assignment.Reset();
                            // Notify unassigned
                        }
                    }
                }
            }
            public void Save()
            {
                onDisplayUpdated.Invoke();
            }
            #endregion

            #region Assignments
            private void Initialize(List<ControllerAssignment> assignments)
            {
                foreach(ControllerAssignment assignment in assignments)
                {
                    int2 position = DoDad.Library.Math.Utils.FlatIndexTo2D(assignment.screenId, rowSize, false);

                    if (!position.IsPositive())
                        continue;

                    screenLayout[position.x][position.y].assignment = assignment;
                }
            }
            private void SetCenter(ControllerAssignment newAssignment)
            {
                int2 centerPosition = Get2dIndex(centerSlotIndex, rowSize, false);

                screenLayout[centerPosition.x][centerPosition.y].Load(newAssignment);
            }
            private bool NeighborExists(int2 origin, DirectionalPointer direction)
            {
                SlotConfiguration slot = screenLayout[origin.x][origin.y];

                switch (direction)
                {
                    case DirectionalPointer.Up:
                        return slot.neighborUp.IsPositive();
                    case DirectionalPointer.Right:
                        return slot.neighborRight.IsPositive();
                    case DirectionalPointer.Down:
                        return slot.neighborDown.IsPositive();
                    case DirectionalPointer.Left:
                        return slot.neighborLeft.IsPositive();
                    default:
                        return false;
                }
            }
            private bool HasAssignedNeighbor(int2 origin)
            {
                if (!origin.IsPositive())
                    return false;

                if (IsAssigned(screenLayout[origin.x][origin.y].neighborUp))
                    return true;
                if (IsAssigned(screenLayout[origin.x][origin.y].neighborRight))
                    return true;
                if (IsAssigned(screenLayout[origin.x][origin.y].neighborDown))
                    return true;
                if (IsAssigned(screenLayout[origin.x][origin.y].neighborLeft))
                    return true;

                return false;
            }
            private bool IsEmpty()
            {
                for(int x = 0; x < screenLayout.Length; x++)
                {
                    for(int y = 0; y < screenLayout.Length; y++)
                    {
                        if (screenLayout[x][y].isAssigned)
                            return false;
                    }
                }

                return true;
            }
            private bool IsMainSlot(int2 position)
            {
                for (int e = 0; e < mainSlotPositions.Length; e++)
                {
                    if (mainSlotPositions[e].Equals(position))
                        return true;
                }

                return false;
            }
            private bool IsCenterSlot(int2 position)
            {
                return centerSlotPosition.Equals(position);
            }
            private bool IsCenter(int flatIndex)
            {
                return flatIndex == centerSlotIndex;
            }
            private void DebugGrid()
            {
                string rowTemplate = "[{0} = {1} {2}] | [{3} = {4} {5}] | [{6} = {7} {8}]";
                string rowDivider = "----------------------------------";

                int y = 0;
                for(int x = screenLayout.Length - 1; x > -1; x--)
                {
                    string output = rowTemplate;

                    output = string.Format(output, $"{x}, {y}", screenLayout[x][y].assignment.deviceId, screenLayout[x][y].assignment.isKeyboard,
                        $"{x}, {y+1}", screenLayout[x][y+1].assignment.deviceId, screenLayout[x][y + 1].assignment.isKeyboard,
                        $"{x}, {y+2}", screenLayout[x][y+2].assignment.deviceId, screenLayout[x][y + 2].assignment.isKeyboard);
                    Debug.Log(output);
                    Debug.Log(rowDivider);
                }
            }
            private void SetValue(int2 position, ControllerAssignment newAssignment)
            {
                screenLayout[position.x][position.y].Load(newAssignment);
            }
            private void ShiftRadial(int2 origin, bool expand) // TODO do this better
            {
                SlotConfiguration slot = screenLayout[origin.x][origin.y];

                if(expand)
                {
                    Debug.Log("Expanding");
                    ShiftNeighbor(slot.neighborUp, slot.neighborUpShift);
                    ShiftNeighbor(slot.neighborRight, slot.neighborRightShift);
                    ShiftNeighbor(slot.neighborDown, slot.neighborDownShift);
                    ShiftNeighbor(slot.neighborLeft, slot.neighborLeftShift);
                }
                else
                {
                    Debug.Log("Contracting");
                    ShiftNeighbor(slot.neighborUpShift, slot.neighborUp);
                    ShiftNeighbor(slot.neighborRightShift, slot.neighborRight);
                    ShiftNeighbor(slot.neighborDownShift, slot.neighborDown);
                    ShiftNeighbor(slot.neighborLeftShift, slot.neighborLeft);
                }
            }
            private void ShiftLinear(DirectionalPointer direction)
            {
                if (direction == DirectionalPointer.Self)
                    return;

                int2 shiftDirection = new int2(0, 0);

                if (direction == DirectionalPointer.Up)
                    shiftDirection.y = 1;

                if (direction == DirectionalPointer.Right)
                    shiftDirection.x = 1;

                if (direction == DirectionalPointer.Down)
                    shiftDirection.y = -1;

                if (direction == DirectionalPointer.Left)
                    shiftDirection.x = -1;

                for(int x = 0; x < screenLayout.Length; x++)
                {
                    for(int y = 0; y < screenLayout.Length; y++)
                    {
                        if(screenLayout[x][y].isMainSlot || screenLayout[x][y].isCenter)
                        {
                            int2 mainSlot = new int2(x, y);
                            
                            ShiftNeighbor(mainSlot, mainSlot.Add(shiftDirection));
                        }
                    }
                }
            }
            private void ShiftNeighbor(int2 origin, int2 destination)
            {
                if (origin.IsPositive())
                {
                    if (IsAssigned(origin))
                    {
                        if (destination.IsPositive())
                        {
                            Debug.Log($"Shifting {origin} to {destination}");
                            Shift(origin, destination);
                        }
                        else
                        {
                            Debug.Log($"Resetting {origin}");
                            Reset(origin);
                        }
                    }
                }
            }
            private bool IsAssigned(int2 position)
            {
                if(position.IsPositive())
                    return screenLayout[position.x][position.y].isAssigned;

                return false;
            }
            private void Reset(int2 position)
            {
                screenLayout[position.x][position.y].Reset();
            }
            private void Shift(int2 origin, int2 destination)
            {
                screenLayout[destination.x][destination.y].Load(screenLayout[origin.x][origin.y].assignment);
                Reset(origin);
            }
            #endregion

            #region Events
            public void DisplayUpdated()
            {
                // TODO notify listeners (all ControllerIcons)
            }
            #endregion

            #region Helpers
            private int2 Get2dIndex(int flatIndex, int rowSize, bool useWidth)
            {
                Debug.Log($"Get2dIndex(flatIndex {flatIndex}, rowSize {rowSize}, useWidth {useWidth})");

                return Library.Math.Utils.FlatIndexTo2D(flatIndex, rowSize, useWidth);

                int2 newIndex;

                if (useWidth)
                    newIndex = new int2(flatIndex % rowSize, flatIndex / rowSize);
                else
                    newIndex = new int2(flatIndex / rowSize, flatIndex % rowSize);

                return newIndex;

            }
            #endregion

            #region Definitions
            public struct SlotConfiguration
            {
                public ControllerAssignment assignment;

                public bool isAssigned
                {
                    get
                    {
                        return assignment.deviceId > -1;
                    }
                }
                public bool isMainSlot { get; private set; }
                public bool isCenter { get; private set; }
                public int2 neighborUp { get; private set; }
                public int2 neighborRight { get; private set; }
                public int2 neighborDown { get; private set; }
                public int2 neighborLeft { get; private set; }
                public int2 neighborUpShift { get; private set; }
                public int2 neighborRightShift { get; private set; }
                public int2 neighborDownShift { get; private set; }
                public int2 neighborLeftShift { get; private set; }

                // Should probably fully convert to in2 coords
                public SlotConfiguration(int2 position, bool isMainSlot, bool isCenter)
                {
                    neighborUp = new int2(position.x, position.y + 1);
                    neighborRight = new int2(position.x + 1, position.y);
                    neighborDown = new int2(position.x, position.y - 1);
                    neighborLeft = new int2(position.x - 1, position.y);

                    neighborUpShift = new int2(neighborUp.x, neighborUp.y + 1);
                    neighborRightShift = new int2(neighborRight.x + 1, neighborRight.y);
                    neighborDownShift = new int2(neighborDown.x, neighborDown.y - 1);
                    neighborLeftShift = new int2(neighborLeft.x - 1, neighborLeft.y);

                    assignment = new ControllerAssignment();
                    assignment.deviceId = -1;
                    assignment.profile = "";
                    assignment.isKeyboard = false;
                    assignment.screenId = position.FlatIndex(rowSize, true);

                    this.isMainSlot = isMainSlot;
                    this.isCenter = isCenter;
                }
                public void Validate()
                {
                    neighborUp = Validate(neighborUp);
                    neighborRight = Validate(neighborRight);
                    neighborDown = Validate(neighborDown);
                    neighborLeft = Validate(neighborLeft);

                    neighborUpShift = Validate(neighborUpShift);
                    neighborRightShift = Validate(neighborRightShift);
                    neighborDownShift = Validate(neighborDownShift);
                    neighborLeftShift = Validate(neighborLeftShift);
                }
                public void Load(ControllerAssignment newAssignment)
                {
                    Debug.Log($"Loading new assignment: {newAssignment.deviceId}");
                    assignment.deviceId = newAssignment.deviceId;
                    assignment.isKeyboard = newAssignment.isKeyboard;
                }
                public void Reset()
                {
                    assignment.deviceId = -1;
                    assignment.isKeyboard = false;
                }
                private int2 Validate(int2 position)
                {
                    if (position.x > -1 && position.x < DisplayManager.rowSize &&
                        position.y > -1 && position.y < DisplayManager.rowSize)
                        return position;

                    position = new int2(-1, -1);

                    return position;
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
