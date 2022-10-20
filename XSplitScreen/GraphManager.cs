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
        #endregion

        #region Initialization & Exit
        public void Initialize()
        {
            InitializeReferences();
            InitializeViewport();
            InitializeListeners();
            InitializeAssignments();
            PrintGraph("Initialized");
        }
        private void InitializeReferences()
        {
            if (instance)
                Destroy(gameObject);

            instance = this;

            graph = new NodeGraph<Assignment>(configuration.graphDimensions, true);

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
        }
        private void InitializeAssignments()
        {
            // Assign only ONE at a time
            // Create new followers after potential updates
            // If device is being unassigned then unassign from graph
            // AND if device is NOT assigned to the graph then reset the graph to saved assignments

            foreach (Assignment assignment in configuration.assignments)
            {
                if(assignment.isAssigned && assignment.displayId == ((ConfigurationManager.ControllerAssignmentState)ConfigurationManager.instance.stateMachine.GetState(State.State1)).currentDisplay)
                {
                    graph.GetNode(assignment.position).nodeData.data.Load(assignment);
                    //Log.LogDebug($"Loaded assignment for {assignment.controller} ({assignment})");
                }
            }

            onGraphUpdated.Invoke();
        }
        #endregion

        #region Events
        public void OnStartDragIcon(Icon icon)
        {
            icon.iconMonitorCoroutine = StartCoroutine(IconMonitor(icon));
        }
        public void OnStopDragIcon(Icon icon)
        {
            StopCoroutine(icon.iconMonitorCoroutine);

            // Notify ConfigurationManager of assignment
            // Update screen's follower
            Log.LogDebug($" - OnStopDragIcon -");
            changeBuffer.Reverse();
            Log.LogDebug($" - Change Buffer -");

            foreach (Assignment assignment in changeBuffer)
            {
                Log.LogDebug($"[{(assignment.position.IsPositive() ? "+" : "-")}] {assignment.controller.name} to {assignment.position}");
            }
            Log.LogDebug($" ---");
            configuration.PushChanges(changeBuffer);
            RevertPotentialChanges();
            changeBuffer.Clear();
            PrintGraph("OnStopDragIcon");
        }
        public void OnPotentialAssignment(Icon icon)
        {
            Log.LogDebug($"");
            Log.LogDebug($"{icon.name} needs potential assignment to '{icon.assignment.position}' with context '{icon.assignment.context}'");

            changeBuffer.Clear();

            RevertPotentialChanges();

            Log.LogDebug($" - OnPotentialAssignment.Unassign - ");

            Unassign(icon.assignment.controller);

            if (icon.assignment.position.IsPositive())
            {
                Log.LogDebug($" - OnPotentialAssignment.Assign - ");
                Assign(icon);
            }

            // track ALL potential changes and update affected devices during OnStopDragIcon
            Log.LogDebug($" - Change Buffer -");

            foreach (Assignment assignment in changeBuffer)
            {
                Log.LogDebug($"[{(assignment.position.IsPositive() ? "+" : "-")}] {assignment.controller.name} to {assignment.position}");
            }
            Log.LogDebug($" ---");
            PrintGraph("OnPotentialAssignment");

            onGraphUpdated.Invoke();
        }
        #endregion

        #region Graph
        private void RevertPotentialChanges()
        {
            //Log.LogDebug($"Reverting potential changes");
            var data = graph.GetNodeData();

            for (int x = 0; x < data.Length; x++)
            {
                for (int y = 0; y < data[x].Length; y++)
                {
                    data[x][y].ClearAssignmentData();
                }
            }

            graph.SetNodeData(data);

            InitializeAssignments();
        }
        private void CommitChanges()
        {
            // TODO

            // When unassigning a device the changes are not being saved to the graph
            // Node assignment device should be cleared, then check for existing assignments (which should be false right now)
            // Then assign to requested position and add the change to buffer
        }
        private void Assign(Icon icon)
        {
            Log.LogDebug($"GraphManager.Assign '{icon.assignment.controller.name}' to '{icon.assignment.position}'");

            var node = graph.GetNode(icon.assignment.position);

            if (node.nodeData.data.Matches(icon.assignment))
                return;

            if (node.nodeData.data.isAssigned)
            {
                Log.LogDebug($"Unassigning existing assignment");
                UnassignToBuffer(node.nodeData.data);
                SetDevice(int2.one, icon.assignment);
                //node.nodeData.data.Load(icon.assignment);
                return;
            }

            if (node.nodeType == NodeType.Primary)
            {
                if (graph.GetNode(int2.one).nodeData.data.Matches(icon.assignment.controller))
                    return;

                if (GraphHasAssignments())
                    UnassignAll();

                Log.LogDebug($"Assigning to center");
                //icon.assignment.context = new int2(1, 1);
                SetDevice(int2.one, icon.assignment);
                return;
            }

            if(graph.GetNode(int2.one).nodeData.data.isAssigned)
            {
                if (graph.GetNode(int2.one).nodeData.data.Matches(icon.assignment.controller))
                    return;

                icon.assignment.position = icon.assignment.context;
                icon.potentialReassignment = true;

                ShiftRadial(icon.assignment.position, true);
            }
            else
            {
                if(node.nodeType == NodeType.Tertiary)
                {
                    ShiftRadial(icon.assignment.position, true);
                }
                else if(node.nodeType == NodeType.Secondary)
                {
                    ShiftLinear(icon.assignment.position);
                }
            }

            SetDevice(icon.assignment.position, icon.assignment);
            // if graph has center assignment
            // if center assignment is the same device
            // // do nothing
            // set position to context
        }
        private void UnassignAll()
        {
            Log.LogDebug($" - UnassignAll -");
            var data = graph.GetNodeData();

            for (int x = 0; x < data.Length; x++)
            {
                for (int y = 0; y < data[x].Length; y++)
                {
                    if(data[x][y].isAssigned)
                    {
                        Log.LogDebug($"- {data[x][y].controller.name} at {new int2(x,y)}");
                        ClearDevice(new int2(x, y));
                    }
                }
            }
        }
        private void Unassign(Controller controller)
        {
            //Log.LogDebug($"GraphManager.Unassign controller: {controller.name}");
            var data = graph.GetNodeData();

            for (int x = 0; x < data.Length; x++)
            {
                for (int y = 0; y < data[x].Length; y++)
                {
                    if (data[x][y].Matches(controller))
                    {
                        int2 position = new int2(x, y);
                        var node = graph.GetNode(position);

                        if(node.nodeType == NodeType.Secondary)
                        {
                            ShiftLinear(position, true);
                        }
                        else
                        {
                            ShiftRadial(position, false);
                        }

                        //Log.LogDebug($"GraphManager.Unassign {node.nodeData.data}");
                        ClearDevice(position);
                        //Log.LogDebug($"After: {node.nodeData.data}");
                    }
                }
            }

            graph.SetNodeData(data);
        }
        private void ShiftRadial(int2 origin, bool expand)
        {
            Log.LogDebug($"ShiftRadial for {origin} expand: {expand}");
            Node<Assignment> slot = graph.GetNode(origin);

            if (expand)
            {
                ShiftNeighbor(slot.neighborUp, slot.neighborUpShift);
                ShiftNeighbor(slot.neighborRight, slot.neighborRightShift);
                ShiftNeighbor(slot.neighborDown, slot.neighborDownShift);
                ShiftNeighbor(slot.neighborLeft, slot.neighborLeftShift);
            }
            else
            {
                ShiftNeighbor(slot.neighborUpShift, slot.neighborUp);
                ShiftNeighbor(slot.neighborRightShift, slot.neighborRight);
                ShiftNeighbor(slot.neighborDownShift, slot.neighborDown);
                ShiftNeighbor(slot.neighborLeftShift, slot.neighborLeft);
            }
        }
        private void ShiftLinear(int2 origin, bool reverse = false)
        {
            Direction direction = Direction.None;

            var node = graph.GetNode(origin);

            if(!HasNeighbor(node, Direction.Up))
                direction = !reverse ? Direction.Down : Direction.Up;
            else if (!HasNeighbor(node, Direction.Right))
                direction = !reverse ? Direction.Left : Direction.Right;
            else if (!HasNeighbor(node, Direction.Down))
                direction = !reverse ? Direction.Up : Direction.Down;
            else if (!HasNeighbor(node, Direction.Left))
                direction = !reverse ? Direction.Right : Direction.Left;

            Log.LogDebug($"ShiftLinear for {origin} has direction: {direction} !reverse: {!reverse}");

            if (direction == Direction.None)
                return;

            int2 shiftDirection = int2.zero;

            if (direction == Direction.Up)
                shiftDirection.x = 1;

            if (direction == Direction.Right)
                shiftDirection.y = 1;

            if (direction == Direction.Down)
                shiftDirection.x = -1;

            if (direction == Direction.Left)
                shiftDirection.y = -1;

            bool byWidth = false;

            int startingX = 0;
            int boundX = configuration.graphDimensions.x;
            int xIncrement = 1;

            int startingY = 0;
            int boundY = configuration.graphDimensions.y;
            int yIncrement = 1;

            switch (direction)
            {
                case Direction.Right:
                    byWidth = true;
                    startingY = 2;
                    yIncrement = -1;
                    boundY = -1;
                    break;
                case Direction.Left:
                    break;
                case Direction.Up:
                    byWidth = true;
                    startingX = 2;
                    xIncrement = -1;
                    boundX = -1;
                    break;
                case Direction.Down:
                    break;
            }

            if (byWidth) // Row by row
            {
                for (int wX = startingX; wX != boundX; wX += xIncrement)
                {
                    for (int wY = startingY; wY != boundY; wY += yIncrement)
                    {
                        int2 position = new int2(wX, wY);

                        //if (graph.GetNode(position).nodeType != NodeType.Tertiary)
                            ShiftNeighbor(position, position.Add(shiftDirection));
                    }
                }
            }
            else // Column by column
            {
                for (int hY = startingY; hY != boundY; hY += yIncrement)
                {
                    for (int hX = startingX; hX != boundX; hX += xIncrement)
                    {
                        int2 position = new int2(hX, hY);

                        //if (graph.GetNode(position).nodeType != NodeType.Tertiary)
                            ShiftNeighbor(position, position.Add(shiftDirection));
                    }
                }
            }
        }
        private bool GraphHasAssignments()
        {
            var data = graph.GetNodeData();

            for(int x = 0; x < data.Length; x++)
            {
                for(int y = 0; y < data[x].Length; y++)
                {
                    if (data[x][y].isAssigned)
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

                    //Log.LogDebug($"Clearing device from ShiftNeighbor");
                    ClearDevice(origin);
                }
            }
        }
        private void ShiftDevice(int2 origin, int2 destination)
        {
            //Log.LogDebug($"Clearing device from ShiftDevice");
            // TODO
            // issue with unassign
            // order of execution of change buffer?

            SetDevice(destination, graph.GetNode(origin).nodeData.data);
            ClearDevice(origin);
        }
        private void ClearDevice(int2 position)
        {
            var node = graph.GetNode(position);

            if (!node.nodeData.data.isAssigned)
                return;

            UnassignToBuffer(node.nodeData.data);

            node.nodeData.data.ClearAssignmentData();
        }
        private void SetDevice(int2 position, Assignment assignment)
        {
            var node = graph.GetNode(position);
            node.nodeData.data.Load(assignment);
            AssignToBuffer(node.nodeData.data);
        }
        private void UnassignToBuffer(Assignment assignment)
        {
            Assignment oldAssignment = new Assignment();
            oldAssignment.Initialize();
            oldAssignment.Load(assignment);
            Log.LogDebug($"UnassignToBuffer adding {assignment.controller.name}");
            changeBuffer.Add(oldAssignment);
        }
        private void AssignToBuffer(Assignment assignment)
        {
            Log.LogDebug($"AssignToBuffer adding {assignment.controller.name}");
            changeBuffer.Add(assignment);
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

            Debug.Log($"{nodeHorizontalDivider}");

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
                            id = $"{data[x][y].nodeData.data.controller.id}C";
                        }
                    }

                    string line = string.Format(template, data[x][y].nodeData.data.position, id);

                    row = $"{row}{((row.Length > 0) ? nodeVerticalDivider : "")}{line}";
                }

                Debug.Log(row);
                row = "";
            }

            Debug.Log($"{nodeHorizontalDivider}");
            return;
            for (int x = data.Length - 1; x > -1; x--)
            {
                //position.x = x;

                for (int y = 0; y < data[x].Length; y++)
                {
                    //position.y = y;
                    //string line = string.Format(template, position, (data[x][y].nodeData.data.isAssigned), data[x][y].nodeData.data.deviceId.ToString(), data[x][y].nodeData.data.isKeyboard.ToString());
                    //row = $"{row}{((row.Length > 0) ? nodeVerticalDivider : "")}{line}";
                }

                //if (x != graph.Length - 1)
                //    Debug.Log(nodeHorizontalDivider);

                Debug.Log(row);
                row = "";
            }

        }
        #endregion

        #region Coroutines
        public IEnumerator IconMonitor(Icon icon)
        {
            Vector3 workingVector = Vector3.zero;

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

                int2 previousPosition = icon.assignment.position;
                int2 previousContext = icon.assignment.context;

                foreach (Screen screen in instance.display.screens)
                {
                    workingVector = icon.cursorFollower.targetPosition - screen.transform.position;

                    currentScreenDistance = workingVector.sqrMagnitude / 1000f;

                    if(currentScreenDistance <= maxDistance)
                    {
                        if(currentScreenDistance < screenDistance)
                        {
                            screenDistance = currentScreenDistance;
                            icon.assignment.position = screen.position;

                            //closestScreen = screen.position;
                        }

                        outOfRange = false;
                    }

                    if (instance.graph.GetNode(screen.position).nodeType != NodeType.Tertiary)
                    {
                        if (currentScreenDistance < contextDistance)
                        {
                            contextDistance = currentScreenDistance;
                            icon.assignment.context = screen.position;
                        }
                    }
                }

                if(outOfRange)
                {
                    icon.assignment.position = int2.negative;
                    icon.assignment.context = int2.negative;
                }

                if((!previousPosition.Equals(icon.assignment.position) || !previousContext.Equals(icon.assignment.context)))
                {
                    if(icon.assignment.position.IsPositive())
                    {
                        if(!icon.potentialReassignment)
                            onPotentialAssignment.Invoke(icon);
                        else
                        {
                            if(!previousContext.Equals(icon.assignment.context))
                                onPotentialAssignment.Invoke(icon);
                        }
                    }
                    else
                    {
                        icon.potentialReassignment = false;
                        onPotentialAssignment.Invoke(icon);
                    }
                }

                yield return null;
            }
        }
        #endregion

        #region Definitions
        public class GraphDisplay : MonoBehaviour
        {
            public readonly int2 screenDimensions = new int2(150, 150);

            private const float dividerFadeSpeed = 10f;

            public List<Screen> screens { get; private set; }
            public Vector3[][] screenPositions { get; private set; }

            private Texture2D texture_display;
            private Texture2D texture_divider;

            private Sprite sprite_display;
            private Sprite sprite_divider;

            private Image monitor;
            private Image[] dividers;

            private bool[] dividerEnabled = new bool[4];

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
            #endregion

            #region UI
            public void OnGraphUpdated()
            {
                var data = instance.graph.GetGraph();

                foreach (Icon icon in ControllerIconManager.instance.icons)
                {
                    //if (!icon.assignment.isAssigned)
                    {

                        icon.displayFollower.gameObject.SetActive(false);
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
                        if(data[x][y].nodeData.data.isAssigned)
                        {
                            foreach (Icon icon in ControllerIconManager.instance.icons)
                            {
                                if (!icon.assignment.isAssigned)
                                    continue;

                                if(data[x][y].nodeData.data.Matches(icon.assignment.controller))
                                {
                                    foreach (Screen screen in screens)
                                    {
                                        if (screen.position.Equals(data[x][y].nodeData.data.position))
                                        {
                                            if(!icon.displayFollower.gameObject.activeSelf)
                                            {
                                                if(icon.cursorFollower.gameObject.activeSelf)
                                                    icon.displayFollower.transform.position = icon.cursorFollower.transform.position;
                                            }

                                            icon.displayFollower.target = screen.rectTransform;
                                            icon.displayFollower.gameObject.SetActive(true);
                                            break;
                                        }
                                    }
                                }
                            }

                            if(data[x][y].nodeType == NodeType.Secondary)
                            {
                                if(!data[x][y].neighborUp.IsPositive())
                                {
                                    dividerEnabled[1] = true;
                                    dividerEnabled[3] = true;
                                }
                                if (!data[x][y].neighborRight.IsPositive())
                                {
                                    dividerEnabled[0] = true;
                                    dividerEnabled[2] = true;
                                }
                                if (!data[x][y].neighborDown.IsPositive())
                                {
                                    dividerEnabled[1] = true;
                                    dividerEnabled[3] = true;
                                }
                                if (!data[x][y].neighborLeft.IsPositive())
                                {
                                    dividerEnabled[0] = true;
                                    dividerEnabled[2] = true;
                                }
                            }
                            else if(data[x][y].nodeType == NodeType.Tertiary)
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