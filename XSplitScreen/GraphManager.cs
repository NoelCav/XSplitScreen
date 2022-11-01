using DoDad.Library.AI;
using DoDad.Library.Graph;
using DoDad.Library.Math;
using DoDad.Library.UI;
using Rewired;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static XSplitScreen.ConfigurationManager;
using static XSplitScreen.ControllerIconManager;
using static XSplitScreen.XSplitScreen;

namespace XSplitScreen
{
    class GraphManager : MonoBehaviour
    {
        #region Variables
        public static GraphManager instance { get; private set; }

        public GraphDisplay display { get; private set; }

        public NodeGraph<Assignment> graph { get; private set; }

        private UnityEvent onGraphUpdated;

        private List<Assignment> changeBuffer;

        private Assignment[][] cachedAssignments;
        #endregion

        #region Initialization & Exit
        public void Initialize()
        {
            InitializeReferences();
            InitializeViewport();
            InitializeListeners();
            InitializeGraph();
            PrintGraph("Initialized");
        }
        private void InitializeReferences()
        {
            if (instance)
                Destroy(gameObject);

            instance = this;

            graph = new NodeGraph<Assignment>(configuration.graphDimensions, true);

            display = gameObject.AddComponent<GraphDisplay>();

            changeBuffer = new List<Assignment>();

            onGraphUpdated = new UnityEvent();
        }
        private void InitializeViewport()
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            //rectTransform.anchorMin = new Vector2(0f, 0.75f);
            //rectTransform.anchorMax = new Vector2(0f, 0.5f);
            display.Initialize();
            display.GetComponent<LayoutElement>().preferredHeight = display.screenDimensions.y * configuration.graphDimensions.y + 50f;
            display.GetComponent<LayoutElement>().preferredWidth = display.screenDimensions.x * configuration.graphDimensions.x + 50f;
            onGraphUpdated.AddListener(display.OnGraphUpdated);
        }
        private void InitializeListeners()
        {
            ControllerIconManager.instance.onStartDragIcon.AddListener(OnStartDragIcon);
            ControllerIconManager.instance.onStopDragIcon.AddListener(OnStopDragIcon);
            ControllerIconManager.instance.onIconAdded.AddListener(OnIconAdded);
            ControllerIconManager.instance.onIconRemoved.AddListener(OnIconRemoved);
        }
        private void InitializeGraph()
        {
            Log.LogDebug($"GraphManager.InitializeGraph");

            int2[] mainScreens = new int2[4]
            {
                new int2(0,1),
                new int2(1,0),
                new int2(1,2),
                new int2(2,1)
            };

            graph.SetNodeType(NodeType.Tertiary);
            graph.SetNodeType(mainScreens, NodeType.Secondary);
            graph.SetNodeType(new int2(1, 1), NodeType.Primary);

            graph.SetNodeData(new Assignment()
            {
                context = int2.negative,
                position = int2.negative,
                displayId = 0,
                profile = "",
                controller = null,
            });

            var data = graph.GetNodeData();

            for (int x = 0; x < data.Length; x++)
            {
                for (int y = 0; y < data.Length; y++)
                {
                    data[x][y].position = new int2(x, y);
                }
            }

            graph.SetNodeData(data);

            LoadPreferencesToGraph();

            onGraphUpdated.Invoke();
        }
        #endregion

        #region Events
        public void OnIconAdded(Icon icon, Assignment assignment)
        {
            ReloadAssignments();
            onGraphUpdated.Invoke();
            Log.LogDebug($"GraphManager.OnIconAdded '{assignment}'");
            PrintGraph("OnIconAdded");
        }
        public void OnIconRemoved(Icon icon, Assignment assignment)
        {
            Unassign(assignment.controller);
            ReloadAssignments();
            PrintGraph("OnIconRemoved.ReloadAssignments");
            PrintGraph("OnIconRemoved.Unassign");
            cachedAssignments = graph.GetNodeData(true);
            onGraphUpdated.Invoke();
            Log.LogDebug($"GraphManager.OnIconRemoved '{assignment}'");
        }
        public void OnStartDragIcon(Icon icon, Assignment assignment)
        {
            cachedAssignments = graph.GetNodeData(true);
            icon.iconMonitorCoroutine = StartCoroutine(IconMonitor(icon));
        }
        public void OnStopDragIcon(Icon icon, Assignment assignment)
        {
            StopCoroutine(icon.iconMonitorCoroutine);

            var newAssignments = graph.GetNodeData(true);

            List<Assignment> changeBuffer = new List<Assignment>();

            for(int x = 0; x < newAssignments.Length; x++)
            {
                for(int y = 0; y < newAssignments[x].Length; y++)
                {
                    if (newAssignments[x][y].isAssigned)
                        changeBuffer.Add(newAssignments[x][y]);
                }
            }

            for(int xX = 0; xX < cachedAssignments.Length; xX++)
            {
                for(int yY = 0; yY < cachedAssignments[xX].Length; yY++)
                {
                    if(cachedAssignments[xX][yY].isAssigned)
                    {
                        bool stillAssigned = false;

                        foreach (Assignment change in changeBuffer)
                        {
                            if(cachedAssignments[xX][yY].Matches(change.controller))
                            {
                                stillAssigned = true;
                                break;
                            }
                        }

                        if(!stillAssigned)
                        {
                            Assignment unassigned = cachedAssignments[xX][yY];
                            unassigned.ClearScreen();

                            changeBuffer.Add(unassigned);
                        }
                    }
                }
            }

            foreach (Assignment change in changeBuffer)
                Log.LogDebug($"changeBuffer entry: '{change}'");

            configuration.PushChanges(changeBuffer);

            return;


            // Notify ConfigurationManager of assignment
            // Update screen's follower

            if(changeBuffer.Count == 0)
            {
                OnPotentialAssignment(icon, assignment);
            }

            changeBuffer.Reverse();

            PrintChangeBuffer();

            configuration.PushChanges(changeBuffer);
            RevertPotentialChanges();
            changeBuffer.Clear();
            icon.SetReassignmentStatus(false);
            PrintGraph("OnStopDragIcon");
        }
        // TODO

        // Need to probably just rewrite it.
        //
        // Potential infinite loop involving events causing crash
        // yeah just rewrite it
        public void OnPotentialAssignment(Icon icon, Assignment newAssignment)
        {
            Log.LogDebug($"GraphManager.OnPotentialAssignment newAssignment = '{newAssignment}'");

            ReloadAssignments();
            Unassign(newAssignment.controller);

            if (newAssignment.isAssigned)
            {
                Assign(icon, newAssignment);
            }

            PrintGraph("OnPotentialAssignment");

            onGraphUpdated.Invoke();
            return;
            ////////
            ///

            Log.LogDebug($" .: GraphManager.OnPotentialAssignment BEGIN :.");
            Log.LogDebug(icon.assignment);
            changeBuffer.Clear();

            RevertPotentialChanges();

            Unassign(icon.assignment.controller);

            //onGraphUpdated.Invoke();

            if (icon.assignment.position.IsPositive())
            {
                Assign(icon, newAssignment);
            }

            // track ALL potential changes and update affected devices during OnStopDragIcon

            PrintChangeBuffer();

            onGraphUpdated.Invoke();
            PrintGraph("OnPotentialAssignment");
            Log.LogDebug($" .: GraphManager.OnPotentialAssignment END :.");
        }
        #endregion

        #region Graph
        public void ReloadAssignments()
        {
            UnassignAll();
            LoadPreferencesToGraph();
        }
        private void LoadPreferencesToGraph()
        {
            NodeType[] types = new NodeType[3]
               {
                NodeType.Primary,
                NodeType.Secondary,
                NodeType.Tertiary
               };

            foreach (NodeType type in types)
            {
                foreach (Assignment assignment in configuration.assignments)
                {
                    if (assignment.isAssigned && assignment.displayId == ((ControllerAssignmentState)ConfigurationManager.instance.stateMachine.GetState(State.State1)).currentDisplay)
                    {
                        if (graph.GetNode(assignment.position).nodeType == type)
                        {
                            foreach (Icon icon in ControllerIconManager.instance.icons)
                            {
                                if (icon.assignment.Matches(assignment) && icon.assignment.isAssigned)
                                {
                                    if (assignment.isAssigned)
                                        Assign(icon, icon.assignment);
                                }
                            }
                        }
                    }
                }
            }
        }
        private void RevertPotentialChanges()
        {
            var data = graph.GetNodeData();

            for (int x = 0; x < data.Length; x++)
            {
                for (int y = 0; y < data[x].Length; y++)
                {
                    data[x][y].ClearAssignment();
                }
            }

            graph.SetNodeData(data);

            InitializeGraph();
        }
        private void Assign(Icon icon, Assignment newAssignment)
        {
            Log.LogDebug($"GraphManager.Assign newAssignment = '{newAssignment}'");
            var node = graph.GetNode(newAssignment.position);

            // If device is already assigned to requested screen do nothing
            if (node.nodeData.data.Matches(newAssignment))
            {
                SetAssignment(newAssignment.position, newAssignment);
                return;
            }

            bool hasAssignments = GraphHasAssignments();

            // If no assignments are present, assign to the center
            if (!hasAssignments)
            {
                SetAssignment(int2.one, newAssignment);
                return;
            }

            // If the requested screen already has a device assigned, replace the device
            if (node.nodeData.data.isAssigned)
            {
                //UnassignToBuffer(node.nodeData.data);
                //Unassign(node.nodeData.data.controller);
                SetAssignment(newAssignment.position, newAssignment);
                return;
            }

            // If the requested node is the center then first unassign everything else
            if (node.nodeType == NodeType.Primary)
            {
                if (hasAssignments)
                    UnassignAll();

                SetAssignment(int2.one, newAssignment);
                return;
            }

            // If an assignment exists in the center, use context as the position instead
            if(graph.GetNode(int2.one).nodeData.data.isAssigned)
            {
                if (graph.GetNode(int2.one).nodeData.data.Matches(newAssignment.controller))
                    return;

                newAssignment.position = newAssignment.context;
                //icon.SetReassignmentStatus(true);
                ShiftRadial(newAssignment.position, true);
            }
            else
            {
                if(node.nodeType == NodeType.Tertiary)
                {
                    ShiftRadial(newAssignment.position, true);
                }
                else if(node.nodeType == NodeType.Secondary)
                {
                    int neighbors = 0;

                    if (node.neighborUp.IsPositive())
                        if(graph.GetNode(node.neighborUp).nodeData.data.isAssigned)
                            neighbors++;
                    if (node.neighborRight.IsPositive())
                        if (graph.GetNode(node.neighborRight).nodeData.data.isAssigned)
                            neighbors++;
                    if (node.neighborDown.IsPositive())
                        if (graph.GetNode(node.neighborDown).nodeData.data.isAssigned)
                            neighbors++;
                    if (node.neighborLeft.IsPositive())
                        if (graph.GetNode(node.neighborLeft).nodeData.data.isAssigned)
                            neighbors++;

                    if(neighbors > 1)
                        ShiftRadial(newAssignment.position, true);
                    else
                        ShiftLinear(newAssignment.position);
                }
            }

            SetAssignment(newAssignment.position, newAssignment);
        }
        private void UnassignAll()
        {
            foreach (Controller controller in ReInput.controllers.Controllers)
            {
                if (controller.type == ControllerType.Mouse)
                    continue;

                Unassign(controller, true);
            }
        }
        private void Unassign(Controller controller, bool simple = false)
        {
            var data = graph.GetNodeData(true);

            for (int x = 0; x < data.Length; x++)
            {
                for (int y = 0; y < data[x].Length; y++)
                {
                    if (data[x][y].Matches(controller))
                    {
                        int2 position = data[x][y].position;

                        var node = graph.GetNode(position);

                        if (!simple)
                        {
                            if (node.nodeType == NodeType.Secondary)
                            {
                                ShiftLinear(position, true);
                            }
                            else
                            {
                                ShiftRadial(position, false);
                            }
                        }// For some reason some controllers aren't being assigned on initialization
                        Log.LogDebug($"GraphManager.Unassign controller = '{controller.name}', position = '{position}', simple = '{true}'");
                        ClearAssignment(position);

                        return;
                    }
                }
            }

            //graph.SetNodeData(data);
        }
        private void ShiftRadial(int2 origin, bool expand)
        {
           var node = graph.GetNode(origin);

            if (expand)
            {
                ShiftNeighbor(node.neighborUp, node.neighborUpShift);
                ShiftNeighbor(node.neighborRight, node.neighborRightShift);
                ShiftNeighbor(node.neighborDown, node.neighborDownShift);
                ShiftNeighbor(node.neighborLeft, node.neighborLeftShift);
            }
            else
            {
                int distantNeighbors = 0;

                if (node.neighborUpShift.IsPositive())
                    if (graph.GetNode(node.neighborUpShift).nodeData.data.isAssigned)
                        distantNeighbors++;
                if (node.neighborRightShift.IsPositive())
                    if (graph.GetNode(node.neighborRightShift).nodeData.data.isAssigned)
                        distantNeighbors++;
                if (node.neighborDownShift.IsPositive())
                    if (graph.GetNode(node.neighborDownShift).nodeData.data.isAssigned)
                        distantNeighbors++;
                if (node.neighborLeftShift.IsPositive())
                    if (graph.GetNode(node.neighborLeftShift).nodeData.data.isAssigned)
                        distantNeighbors++;

                if (distantNeighbors > 1)
                {
                    if(node.nodeData.data.context.IsPositive())
                    {
                        if(node.neighborUp.Equals(node.nodeData.data.context))
                            ShiftNeighbor(node.neighborUpShift, node.neighborUp);
                        else if (node.neighborRight.Equals(node.nodeData.data.context))
                            ShiftNeighbor(node.neighborRightShift, node.neighborRight);
                        else if (node.neighborDown.Equals(node.nodeData.data.context))
                            ShiftNeighbor(node.neighborDownShift, node.neighborDown);
                        else if (node.neighborLeft.Equals(node.nodeData.data.context))
                            ShiftNeighbor(node.neighborLeftShift, node.neighborLeft);
                    }
                }
                else
                {
                    ShiftNeighbor(node.neighborUpShift, node.neighborUp);
                    ShiftNeighbor(node.neighborRightShift, node.neighborRight);
                    ShiftNeighbor(node.neighborDownShift, node.neighborDown);
                    ShiftNeighbor(node.neighborLeftShift, node.neighborLeft);
                }
            }
        }
        private void ShiftLinear(int2 origin, bool reverse = false)
        {
            Direction direction = Direction.None;

            var node = graph.GetNode(origin);

            if(!HasNeighbor(node, Direction.Up))
                direction = reverse ? Direction.Up : Direction.Down;
            else if (!HasNeighbor(node, Direction.Right))
                direction = reverse ? Direction.Right : Direction.Left;
            else if (!HasNeighbor(node, Direction.Down))
                direction = reverse ? Direction.Down : Direction.Up;
            else if (!HasNeighbor(node, Direction.Left))
                direction = reverse ? Direction.Left : Direction.Right;

            if (direction == Direction.None)
                return;

            int2 shiftDirection = int2.zero;

            if (direction == Direction.Up)
                shiftDirection.x = -1;

            if (direction == Direction.Right)
                shiftDirection.y = 1;

            if (direction == Direction.Down)
                shiftDirection.x = 1;

            if (direction == Direction.Left)
                shiftDirection.y = -1;

            bool byColumn = false;

            int startingX = 0;
            int boundX = configuration.graphDimensions.x;
            int xIncrement = 1;

            int startingY = 0;
            int boundY = configuration.graphDimensions.y;
            int yIncrement = 1;

            switch (direction)
            {
                case Direction.Right:
                    startingY = 2;
                    yIncrement = -1;
                    boundY = -1;
                    break;
                case Direction.Left:
                    byColumn = true;
                    break;
                case Direction.Up:
                    byColumn = true;
                    startingX = 0;
                    break;
                case Direction.Down:
                    startingX = 2;
                    xIncrement = -1;
                    boundX = -1;
                    break;
            }

            if (byColumn) 
            {
                for (int wX = startingX; wX != boundX; wX += xIncrement)
                {
                    for (int wY = startingY; wY != boundY; wY += yIncrement)
                    {
                        int2 position = new int2(wX, wY);

                        ShiftNeighbor(position, position.Add(shiftDirection));
                    }
                }
            }
            else 
            {
                for (int hY = startingY; hY != boundY; hY += yIncrement)
                {
                    for (int hX = startingX; hX != boundX; hX += xIncrement)
                    {
                        int2 position = new int2(hX, hY);

                        ShiftNeighbor(position, position.Add(shiftDirection));
                    }
                }
            }
        }
        private bool GraphHasAssignments()
        {
            var data = graph.GetNodeData();

            int2 position = int2.zero;

            for(int x = 0; x < data.Length; x++)
            {
                position.x = x;

                for(int y = 0; y < data[x].Length; y++)
                {
                    position.y = y;

                    if (graph.GetNode(position).nodeData.data.isAssigned)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        private bool HasNeighbor(Node<Assignment> node, Direction direction)
        {
            if(direction == Direction.Up)
                return node.neighborUp.IsPositive();
            else if (direction == Direction.Right)
                return node.neighborRight.IsPositive();
            else if (direction == Direction.Down)
                return node.neighborDown.IsPositive();
            else if (direction == Direction.Left)
                return node.neighborLeft.IsPositive();

            return false;
        }
        private void ShiftNeighbor(int2 origin, int2 destination)
        {
            if (!origin.IsPositive())
                return;

            if(graph.GetNode(origin).nodeData.data.isAssigned)
            {
                if(graph.ValidPosition(destination))
                {
                    ShiftDevice(origin, destination);
                }
                else
                {
                    ClearAssignment(origin);
                }
                // Buffer here?
            }
        }
        private void ShiftDevice(int2 origin, int2 destination)
        {
            // TODO
            // issue with unassign
            // order of execution of change buffer?
            var node = graph.GetNode(origin);

            SetAssignment(destination, node.nodeData.data);
            node.nodeData.data.ClearAssignment();

            //ClearDevice(origin);
        }
        private void ClearAssignment(int2 position)
        {
            var node = graph.GetNode(position);
            node.nodeData.data.ClearAssignment();

            //UnassignToBuffer(node.nodeData.data);
        }
        private void SetAssignment(int2 position, Assignment assignment)
        {
            var node = graph.GetNode(position);
            node.nodeData.data.Load(assignment);
            //AssignToBuffer(node.nodeData.data);
        }
        private void UnassignToBuffer(Assignment assignment)
        {
            Assignment oldAssignment = new Assignment();
            oldAssignment.Initialize();
            oldAssignment.Load(assignment);

            UpdateChangeBuffer(oldAssignment);
            //changeBuffer.Add(oldAssignment);
        }
        private void AssignToBuffer(Assignment assignment)
        {
            UpdateChangeBuffer(assignment);
            //changeBuffer.Add(assignment);
        }
        private void UpdateChangeBuffer(Assignment assignment)
        {
            for(int e = 0; e < changeBuffer.Count; e++)
            {
                if(changeBuffer[e].Matches(assignment.controller))
                {
                    changeBuffer[e] = assignment;
                    return;
                }
            }

            changeBuffer.Add(assignment);
        }
        private void PrintChangeBuffer()
        {
            Log.LogDebug($" - Change Buffer Start -");

            foreach (Assignment assignment in changeBuffer)
            {
                Log.LogDebug($"[{(assignment.position.IsPositive() ? "+" : "-")}] {assignment.controller.name} to {assignment.position}");
            }
            Log.LogDebug($" - Change Buffer End -");
        }
        private void PrintGraph(string title = "")
        {
            // Debug graph assignments (linear shift not working when unassign main in 3 way)
            // Add event listeners for UI updates
            var data = graph.GetGraph();

            string nodeVerticalDivider = " || ";
            string nodeHorizontalDivider = $"--------------------- {title} ---------------------";
            //string template = "[{0}: {1}({2}, {3})]";

            string template = "[{0}: {1}]";

            string row = "";

            Log.LogDebug($"{nodeHorizontalDivider}");

            for(int x = 0; x < data.Length; x++)
            {
                for(int y = 0; y < data[x].Length; y++)
                {
                    //string line = string.Format(template, data[x][y].nodeData.data.position, (data[x][y].nodeData.data.isAssigned ? data[x][y].nodeData.data.controller.name : "none"), data[x][y].nodeData.data.deviceId.ToString(), data[x][y].nodeData.data.isKeyboard.ToString());
                    string id = "0X";

                    if(data[x][y].nodeData.data.isAssigned)
                    {
                        if(data[x][y].nodeData.data.controller.type == ControllerType.Keyboard)
                        {
                            id = "0-";
                        }
                        else
                        {
                            id = $"{data[x][y].nodeData.data.controller.id}+";
                        }
                    }

                    string line = string.Format(template, data[x][y].nodeData.data.position, id);

                    row = $"{row}{((row.Length > 0) ? nodeVerticalDivider : "")}{line}";
                }

                Log.LogDebug(row);
                row = "";
            }

            Log.LogDebug($"{nodeHorizontalDivider}");
        }
        #endregion

        #region Coroutines
        public IEnumerator IconMonitor(Icon icon)
        {
            Assignment potentialAssignment = icon.assignment;

            Vector3 workingVector = Vector3.zero;

            int2 previousPosition = int2.negative;
            int2 previousContext = potentialAssignment.context;

            IconEvent onPotentialAssignment = new IconEvent();

            onPotentialAssignment.AddListener(instance.OnPotentialAssignment);

            while(true)
            {
                if (icon == null || icon.cursorFollower == null)
                    break;

                float maxDistance = 25f;

                float screenDistance = float.MaxValue;
                float contextDistance = float.MaxValue;

                float currentScreenDistance = float.MaxValue;

                bool outOfRange = true;

                foreach (Screen screen in instance.display.screens)
                {
                    workingVector = icon.cursorFollower.targetPosition - screen.transform.position;

                    currentScreenDistance = workingVector.sqrMagnitude / 1000f;

                    if(currentScreenDistance <= maxDistance)
                    {
                        if(currentScreenDistance < screenDistance)
                        {
                            screenDistance = currentScreenDistance;
                            potentialAssignment.position = screen.position;

                            //closestScreen = screen.position;
                        }

                        outOfRange = false;
                    }

                    if (instance.graph.GetNode(screen.position).nodeType != NodeType.Tertiary)
                    {
                        if (currentScreenDistance < contextDistance)
                        {
                            contextDistance = currentScreenDistance;
                            potentialAssignment.context = screen.position;
                        }
                    }
                }

                if(outOfRange)
                {
                    potentialAssignment.position = int2.negative;
                    potentialAssignment.context = int2.negative;
                }

                if((!previousPosition.Equals(potentialAssignment.position) || !previousContext.Equals(potentialAssignment.context)))
                {
                    if(potentialAssignment.position.IsPositive())
                    {
                        bool didAssignment = false;

                        if (!icon.potentialReassignment)
                        {
                            didAssignment = true;
                            onPotentialAssignment.Invoke(icon, potentialAssignment);
                        }
                        else
                        {
                            if(!previousContext.Equals(potentialAssignment.context))
                            {
                                didAssignment = true;
                                onPotentialAssignment.Invoke(icon, potentialAssignment);
                            }
                        }

                        if(didAssignment)
                        {
                            icon.displayFollower.transform.position = icon.displayFollower.targetPosition;
                        }
                    }
                    else
                    {
                        icon.SetReassignmentStatus(false);
                        onPotentialAssignment.Invoke(icon, potentialAssignment);
                    }

                    previousPosition = potentialAssignment.position;
                    previousContext = potentialAssignment.context;
                }

                yield return null;
            }
        }
        #endregion

        #region Definitions
        public class GraphDisplay : MonoBehaviour
        {
            #region Variables
            public readonly int2 screenDimensions = new int2(135, 135);

            private const float dividerFadeSpeed = 20f;

            public List<Screen> screens { get; private set; }
            public Vector3[][] screenPositions { get; private set; }

            private Texture2D texture_display;
            private Texture2D texture_divider;

            private Sprite sprite_display;
            private Sprite sprite_divider;

            private Image monitor;
            private Image[] dividers;

            private bool[] dividerEnabled = new bool[4];
            #endregion

            #region Initialization
            public void Initialize()
            {
                InitializeReferences();
                InitializeUI();
            }
            private void InitializeReferences()
            {
                texture_display = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/display.png");
                texture_divider = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/divider.png");
                sprite_display = Sprite.Create(texture_display, new Rect(Vector2.zero, new Vector2(texture_display.width, texture_display.height)), Vector2.zero);
                sprite_divider = Sprite.Create(texture_divider, new Rect(Vector2.zero, new Vector2(texture_divider.width, texture_divider.height)), Vector2.zero);

                screens = new List<Screen>();

                screenPositions = new Vector3[3][];
                
                for(int e = 0; e < 3; e++)
                    screenPositions[e] = new Vector3[3];

                screenPositions[2][2] = new Vector3(screenDimensions.x, -screenDimensions.y, 0);
                screenPositions[2][1] = new Vector3(0, -screenDimensions.y, 0);
                screenPositions[2][0] = new Vector3(-screenDimensions.x, -screenDimensions.y, 0);
                screenPositions[1][2] = new Vector3(screenDimensions.x, 0, 0);
                screenPositions[1][1] = new Vector3(0, 0, 0);
                screenPositions[1][0] = new Vector3(-screenDimensions.x, 0, 0);
                screenPositions[0][2] = new Vector3(screenDimensions.x, screenDimensions.y, 0);
                screenPositions[0][1] = new Vector3(0, screenDimensions.y, 0);
                screenPositions[0][0] = new Vector3(-screenDimensions.x, screenDimensions.y, 0);
            }
            private void InitializeUI()
            {
                monitor = new GameObject($"(Image) Monitor", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                monitor.transform.SetParent(transform);
                monitor.transform.localScale = Vector3.one;// new Vector3(1.25f, 1.25f, 1.25f);
                monitor.transform.localPosition = Vector3.zero;
                monitor.sprite = sprite_display;
                monitor.SetNativeSize();
                monitor.raycastTarget = false;

                dividers = new Image[4];

                for(int e = 0; e < dividers.Length; e++)
                {
                    dividers[e] = new GameObject($"(Image) Divider {e}", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    dividers[e].transform.SetParent(transform);
                    dividers[e].transform.localScale = Vector3.one;// new Vector3(1.25f, 1.25f, 1.25f);
                    dividers[e].transform.localPosition = Vector3.zero;
                    dividers[e].transform.localRotation = Quaternion.AngleAxis(e * -90f, new Vector3(0, 0, 1));
                    dividers[e].sprite = sprite_divider;
                    dividers[e].SetNativeSize();
                    dividers[e].raycastTarget = false;
                    dividers[e].color = new Color(1, 1, 1, 0);
                }

                for (int e = 0; e < configuration.graphDimensions.x * configuration.graphDimensions.y; e++)
                {
                    Screen newScreen = new GameObject($"(Screen) {Utils.FlatIndexTo2D(e, configuration.graphDimensions.x, false)}", typeof(RectTransform), typeof(Screen)).GetComponent<Screen>();
                    newScreen.transform.SetParent(transform);
                    newScreen.transform.localScale = Vector3.one;
                    newScreen.position = Utils.FlatIndexTo2D(e, configuration.graphDimensions.x, false);
                    newScreen.rectTransform = newScreen.GetComponent<RectTransform>();
                    newScreen.Initialize(screenDimensions);

                    screens.Add(newScreen);
                }
            }
            #endregion

            #region Unity Methods
            public void Update()
            {
                for(int e = 0; e < 4; e++)
                {
                    if(dividerEnabled[e])
                    {
                        dividers[e].color = Color.Lerp(dividers[e].color, new Color(1, 1, 1, 0.6f), Time.unscaledDeltaTime * dividerFadeSpeed);
                    }
                    else
                    {
                        dividers[e].color = Color.Lerp(dividers[e].color, new Color(1, 1, 1, 0), Time.unscaledDeltaTime * dividerFadeSpeed);
                    }
                }
            }
            public void OnDisable()
            {

            }
            #endregion

            #region UI
            public void OnGraphUpdated()
            {
                var data = instance.graph.GetNodeData(true);

                foreach (Icon icon in ControllerIconManager.instance.icons)
                {
                    //if (!icon.assignment.isAssigned)
                    {
                        icon.displayFollower.enabled = false;
                    }
                }

                dividerEnabled[0] = false;
                dividerEnabled[1] = false;
                dividerEnabled[2] = false;
                dividerEnabled[3] = false;

                for (int x = 0; x < data.Length; x++)
                {
                    for (int y = 0; y < data[x].Length; y++)
                    {
                        if(data[x][y].isAssigned)
                        {
                            foreach (Icon icon in ControllerIconManager.instance.icons)
                            {
                                //if (!icon.assignment.isAssigned)
                                //    continue;

                                if(data[x][y].Matches(icon.assignment.controller))
                                {
                                    foreach (Screen screen in screens)
                                    {
                                        if (screen.position.Equals(data[x][y].position))
                                        {
                                            if(!icon.displayFollower.gameObject.activeSelf)
                                            {
                                                if(icon.cursorFollower.gameObject.activeSelf)
                                                    icon.displayFollower.transform.position = icon.cursorFollower.transform.position;
                                            }

                                            icon.displayFollower.target = screen.rectTransform;
                                            icon.displayFollower.enabled = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            var node = instance.graph.GetNode(data[x][y].position);

                            if (node.nodeType == NodeType.Secondary)
                            {
                                if(!instance.graph.GetNode(data[x][y].position).neighborUp.IsPositive())
                                {
                                    dividerEnabled[1] = true;
                                    dividerEnabled[3] = true;
                                }
                                if (!node.neighborRight.IsPositive())
                                {
                                    dividerEnabled[0] = true;
                                    dividerEnabled[2] = true;
                                }
                                if (!node.neighborDown.IsPositive())
                                {
                                    dividerEnabled[1] = true;
                                    dividerEnabled[3] = true;
                                }
                                if (!node.neighborLeft.IsPositive())
                                {
                                    dividerEnabled[0] = true;
                                    dividerEnabled[2] = true;
                                }
                            }
                            else if(node.nodeType == NodeType.Tertiary)
                            {
                                if (x == 0)
                                {
                                    if (y == 0)
                                        dividerEnabled[3] = true;
                                    else
                                        dividerEnabled[1] = true;

                                    dividerEnabled[0] = true;
                                }
                                else
                                {
                                    if (y == 0)
                                        dividerEnabled[3] = true;
                                    else
                                        dividerEnabled[1] = true;

                                    dividerEnabled[2] = true;
                                }

                            }
                        }
                    }
                }


            }
            #endregion
        }
        public class Screen : MonoBehaviour
        {
            public RectTransform rectTransform;

            public int2 position;

            public void Initialize(int2 dimensions)
            {
                rectTransform.localPosition = instance.display.screenPositions[position.x][position.y];
                //gameObject.AddComponent<Image>();
                //gameObject.GetComponent<Image>().color = new Color(1, 1, 1, 0.2f);
                // create follower
                // assign to container in assignmentstate
            }
        }
        public enum Direction
        {
            None,
            Up,
            Right,
            Down,
            Left,
        }
        #endregion
    }
}