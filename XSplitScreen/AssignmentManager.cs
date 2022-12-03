using DoDad.Library.AI;
using DoDad.Library.Graph;
using DoDad.Library.Math;
using DoDad.Library.UI;
using Rewired;
using RoR2;
using RoR2.UI;
using RoR2.UI.MainMenu;
using RoR2.UI.SkinControllers;
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
    public class AssignmentManager : MonoBehaviour
    {
        #region Variables
        public static AssignmentManager instance { get; private set; }

        public ScreenDisplay display { get; private set; }

        public NodeGraph<Assignment> graph { get; private set; }

        private UnityEvent onGraphUpdated;

        private List<Assignment> changeBuffer;

        private Assignment[][] cachedAssignments;

        private int catchUpFrameCount = 3;
        #endregion

        #region Unity Methods
        public void LateUpdate()
        {
            if (catchUpFrameCount > -1)
                catchUpFrameCount--;

            if (catchUpFrameCount == 0)
                display.CatchUpFollowers();
        }
        public void OnDestroy()
        {
            ToggleListeners(false);
        }
        #endregion

        #region Initialization & Exit
        public void Initialize()
        {
            InitializeReferences();
            InitializeGraph();
            InitializeViewport();
            ToggleListeners(true);
            //PrintGraph("Initialized");
        }
        private void InitializeReferences()
        {
            if (instance)
                Destroy(gameObject);

            instance = this;

            graph = new NodeGraph<Assignment>(configuration.graphDimensions, true);

            display = gameObject.AddComponent<ScreenDisplay>();

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
            //onGraphUpdated.AddListener(display.OnGraphUpdated);
        }
        private void ToggleListeners(bool status)
        {
            if (configuration is null)
                return;

            if(status)
            {
                configuration.onControllerConnected += OnControllerConnected;
            }
            else
            {
                configuration.onControllerConnected -= OnControllerConnected;
            }
            //ControllerIconManager.instance.onStartDragIcon.AddListener(OnStartDragIcon);
            //ControllerIconManager.instance.onStopDragIcon.AddListener(OnStopDragIcon);
            //ControllerIconManager.instance.onIconAdded.AddListener(OnIconAdded);
            //ControllerIconManager.instance.onIconRemoved.AddListener(OnIconRemoved);

            //configuration.onAssignmentLoaded.AddListener(OnAssignmentLoaded);
        }
        private void InitializeGraph()
        {
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

            graph.SetNodeData(new Assignment(null));

            var data = graph.GetNodeData();

            for (int x = 0; x < data.Length; x++)
            {
                for (int y = 0; y < data.Length; y++)
                {
                    data[x][y].position = new int2(x, y);
                }
            }

            graph.SetNodeData(data);

            LoadAssignments();

            onGraphUpdated.Invoke();
        }
        #endregion

        #region Events
        public void OnUpdateDisplay()
        {
            InitializeGraph();
            ScreenDisplay.instance.OnConfigurationUpdated();
        }
        public void OnAssignController(Icon icon)
        {
            Screen closestScreen = null;
            float screenDistance = float.MaxValue;
            float maxDistance = 22f;

            foreach(Screen screen in display.screens)
            {
                if (!screen.showPlayerPane)
                    continue;

                float currentDistance = (icon.cursorFollower.transform.position - screen.transform.position).sqrMagnitude / 1000f;

                if (currentDistance < screenDistance)
                {
                    closestScreen = screen;
                    screenDistance = currentDistance;
                }
            }

            var assignment = configuration.GetAssignment(icon.controller);

            if (assignment.HasValue)
                AssignController(null, assignment.Value.position);

            if (screenDistance <= maxDistance)
                AssignController(icon.controller, closestScreen.position);
        }
        public void OnBeginDragController()
        {
            // show player pane slots
        }
        public void OnClickScreenAddPlayer(Screen screen)
        {
            Log.LogDebug($"Adding player to '{screen.name}'");
            AddPlayer(screen);
        }
        public void OnClickScreenRemovePlayer(Screen screen)
        {
            RemovePlayer(screen);
        }
        public void OnControllerConnected(ControllerStatusChangedEventArgs args)
        {
            display.UpdateDisplayFollowers();
            display.CatchUpFollowers();
        }
        
        #endregion

        #region Assignments
        public void AssignController(Controller controller, int2 destination)
        {
            // TODO
            // when controller is assigned to different display, the assignment isn't being cleared during reassignment
            if (!graph.ValidPosition(destination))
                return;

            var nodeData = graph.GetNodeData(destination);

            if (!nodeData.isAssigned)
                return;

            changeBuffer.Clear();

            nodeData.controller = controller;

            graph.SetNodeData(destination, nodeData);

            PushToBuffer(nodeData);
            PushToConfiguration();
            //PrintGraph("Assign Controller");
        }
        public void UnassignController(int2 origin)
        {

        }
        public void SetProfile(int profileId, int2 destination)
        {
            var nodeData = graph.GetNodeData(destination);

            if (!nodeData.isAssigned)
                return;

            changeBuffer.Clear();

            nodeData.profileId = profileId;

            graph.SetNodeData(destination, nodeData);

            PushToBuffer(nodeData);
            PushToConfiguration();
        }
        private void LoadAssignments()
        {
            foreach(Assignment assignment in configuration.assignments)
            {
                if(assignment.displayId == ControllerAssignmentState.currentDisplay && assignment.position.IsPositive())
                {
                    graph.GetNode(assignment.position).nodeData.data.Load(assignment);
                }
            }

            PrintGraph("Loaded Assignments");
        }
        private void AddPlayer(Screen screen)
        {
            if (configuration.localPlayerCount == configuration.maxLocalPlayers)
                return;

            changeBuffer.Clear();

            var newPlayerIndex = -1;

            foreach(Assignment assignment in configuration.assignments)
            {
                if (!assignment.isAssigned)
                {
                    newPlayerIndex = assignment.playerId;
                    break;
                }
            }

            if (newPlayerIndex < 0)
                return;

            // Duplicate existing assignment and update the position to match the requested screen
            var duplicateAssignment = configuration.assignments[newPlayerIndex];
            duplicateAssignment.Load(screen);

            // Load new assignment to the graph
            AssignToGraph(duplicateAssignment, screen.position);

            PushToConfiguration();
            PrintGraph("AddPlayer");
        }
        private void RemovePlayer(Screen screen)
        {
            Log.LogDebug($"AssignmentManager.RemovePlayer: configuration.localPlayerCount = '{configuration.localPlayerCount}'");
            if (configuration.assignedPlayerCount == 1)
                return;

            changeBuffer.Clear();
            UnassignFromGraph(screen.position);
            configuration.PushChanges(changeBuffer);
            configuration.Save();
            PrintGraph("RemovePlayer");
        }
        private void AssignToGraph(Assignment assignment, int2 destination)
        {
            if (!graph.ValidPosition(destination))
                return;

            SetAssignment(assignment, destination);
            ShiftRadial(destination, true);
        }
        private void UnassignFromGraph(int2 destination)
        {
            if (!graph.ValidPosition(destination))
                return;

            ClearAssignment(destination);

            var node = graph.GetNode(destination);

            // TODO
            // Bug: Controller not auto assigning?

            if (node.nodeType == NodeType.Secondary)
                ShiftLinear(destination, true);
            else
                ShiftRadial(destination, false);
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
                var neighborUpShift = graph.GetNode(node.neighborUpShift);
                var neighborRightShift = graph.GetNode(node.neighborRightShift);
                var neighborDownShift = graph.GetNode(node.neighborDownShift);
                var neighborLeftShift = graph.GetNode(node.neighborLeftShift);

                int lowestPlayerId = int.MaxValue;
                int2 neighborOrigin = int2.negative;
                int2 neighborDestination = int2.negative;

                if (neighborUpShift != null)
                    if (neighborUpShift.nodeData.data.playerId > -1)
                        if (neighborUpShift.nodeData.data.playerId < lowestPlayerId)
                        {
                            neighborOrigin = node.neighborUpShift;
                            neighborDestination = node.neighborUp;
                            lowestPlayerId = neighborUpShift.nodeData.data.playerId;
                        }
                if (neighborRightShift != null)
                    if (neighborRightShift.nodeData.data.playerId > -1)
                        if (neighborRightShift.nodeData.data.playerId < lowestPlayerId)
                        {
                            neighborOrigin = node.neighborRightShift;
                            neighborDestination = node.neighborRight;
                            lowestPlayerId = neighborRightShift.nodeData.data.playerId;
                        }
                if (neighborDownShift != null)
                    if (neighborDownShift.nodeData.data.playerId > -1)
                        if (neighborDownShift.nodeData.data.playerId < lowestPlayerId)
                        {
                            neighborOrigin = node.neighborDownShift;
                            neighborDestination = node.neighborDown;
                            lowestPlayerId = neighborDownShift.nodeData.data.playerId;
                        }
                if (neighborLeftShift != null)
                    if (neighborLeftShift.nodeData.data.playerId > -1)
                        if (neighborLeftShift.nodeData.data.playerId < lowestPlayerId)
                        {
                            neighborOrigin = node.neighborLeftShift;
                            neighborDestination = node.neighborLeft;
                            lowestPlayerId = neighborLeftShift.nodeData.data.playerId;
                        }

                ShiftNeighbor(neighborOrigin, neighborDestination);
                //ShiftNeighbor(node.neighborUpShift, node.neighborUp);
                //ShiftNeighbor(node.neighborRightShift, node.neighborRight);
                //ShiftNeighbor(node.neighborDownShift, node.neighborDown);
                //ShiftNeighbor(node.neighborLeftShift, node.neighborLeft);
            }
        }
        private void ShiftLinear(int2 origin, bool reverse)
        {
            Log.LogDebug($"AssignmentManager.ShiftLinear: origin = '{origin}'");
            Direction direction = Direction.None;

            var node = graph.GetNode(origin);

            if (!HasNeighbor(node, Direction.Up))
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

                        //ShiftNeighbor(position, position.Add(shiftDirection));
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

                        //ShiftNeighbor(position, position.Add(shiftDirection));
                        ShiftNeighbor(position, position.Add(shiftDirection));
                    }
                }
            }
        }
        private bool HasNeighbor(Node<Assignment> node, Direction direction)
        {
            if (direction == Direction.Up)
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
            if (!graph.ValidPosition(origin) || !graph.ValidPosition(destination))
                return;

            if(graph.GetNode(origin).nodeData.data.isAssigned)
            {
                if (graph.ValidPosition(destination))
                    ShiftAssignment(origin, destination);
                else
                    ClearAssignment(origin);
            }
        }
        private void ShiftAssignment(int2 origin, int2 destination)
        {
            var nodeData = graph.GetNodeData(origin);

            ClearAssignment(origin);

            SetAssignment(nodeData, destination);
        }
        private void ClearAssignment(int2 destination)
        {
            var nodeData = graph.GetNodeData(destination);

            if(nodeData.isAssigned)
            {
                var unassigned = nodeData;
                unassigned.ClearScreen();
                PushToBuffer(unassigned);

                nodeData.ClearPlayer();
                graph.SetNodeData(destination, nodeData);
            }
        }
        private void SetAssignment(Assignment assignment, int2 destination)
        {
            var nodeData = graph.GetNodeData(destination);
            nodeData.Load(assignment);

            // Save data to the graph and push changes to buffer
            graph.SetNodeData(destination, nodeData);

            PushToBuffer(nodeData);
        }
        private void PushToBuffer(Assignment assignment)
        {
            bool updated = false;

            for(int e = 0; e < changeBuffer.Count; e++)
            {
                if (changeBuffer[e].Matches(assignment))
                {
                    changeBuffer[e] = assignment;
                    updated = true;
                }
            }

            if (!updated)
            {
                changeBuffer.Add(assignment);
            }
        }
        private void PushToConfiguration()
        {
            configuration.PushChanges(changeBuffer);
            // Save changes
            configuration.Save();
        }
        private void PrintGraph(string title = "")
        {
            var data = graph.GetGraph();

            string nodeVerticalDivider = " || ";
            string nodeHorizontalDivider = $"--------------------- {title} ---------------------";

            string template = "[{0}: {1}]";

            string row = "";

            Log.LogDebug($"{nodeHorizontalDivider}");

            for(int x = 0; x < data.Length; x++)
            {
                for (int y = 0; y < data[x].Length; y++)
                {
                    string id = "x";

                    if (data[x][y].nodeData.data.controller != null)
                    {
                        if (data[x][y].nodeData.data.controller.type == ControllerType.Keyboard)
                        {
                            id = "0-";
                        }
                        else
                        {
                            id = $"{data[x][y].nodeData.data.controller.id}+";
                        }
                    }

                    id = $"{(data[x][y].nodeData.data.playerId < 0 ? "x" : data[x][y].nodeData.data.playerId)}p{id}";

                    string line = string.Format(template, data[x][y].nodeData.data.position, id);

                    row = $"{row}{((row.Length > 0) ? nodeVerticalDivider : "")}{line}";
                }

                Log.LogDebug(row);
                row = "";
            }

            Log.LogDebug($"{nodeHorizontalDivider}");
        }
        #endregion

        #region Definitions
        public class ScreenDisplay : MonoBehaviour
        {
            #region Variables
            public static ScreenDisplay instance { get; private set; }

            public readonly int2 screenDimensions = new int2(129, 129);//135, 135);
            
            private readonly Vector4 disabledColor = new Vector4(1, 1, 1, 0);

            private const float dividerFadeSpeed = 20f;

            public List<Screen> screens { get; private set; }
            public Sprite sprite_display { get; private set; }
            public Sprite sprite_display_screen { get; private set; }
            public Sprite sprite_display_center { get; private set; }
            public Sprite sprite_divider { get; private set; }
            public Sprite sprite_plus { get; private set; }

            public Vector3[][] screenPositions { get; private set; }

            private Texture2D texture_display;
            private Texture2D texture_display_center;
            private Texture2D texture_display_screen;
            private Texture2D texture_divider;
            private Texture2D texture_plus;

            private Image monitor;
            private Image center;
            private Image[] dividers;

            private PlayerPane[] panes;

            private bool[] dividerEnabled = new bool[4];
            private bool centerEnabled;

            private Color defaultColor = new Color(1, 1, 1, 0.1f);
            #endregion

            #region Unity Methods
            public void Update()
            {
                for(int e = 0; e < 4; e++)
                {
                    if(dividerEnabled[e])
                        dividers[e].color = Color.Lerp(dividers[e].color, defaultColor, Time.unscaledDeltaTime * dividerFadeSpeed);
                    else
                        dividers[e].color = Color.Lerp(dividers[e].color, disabledColor, Time.unscaledDeltaTime * dividerFadeSpeed);
                }

                if (centerEnabled)
                    center.color = Color.Lerp(center.color, defaultColor, Time.unscaledDeltaTime * dividerFadeSpeed);
                else
                    center.color = Color.Lerp(center.color, disabledColor, Time.unscaledDeltaTime * dividerFadeSpeed);
            }
            public void OnDestroy()
            {
                ToggleListeners(false);

                instance = null;
            }
            #endregion

            #region Initialization
            public void Initialize()
            {
                InitializeReferences();
                InitializeUI();
                ToggleListeners(true);
                //Log.LogDebug($"AssignmentManager.ScreenDisplay: initialized");
            }
            private void InitializeReferences()
            {
                if (instance)
                    Destroy(instance.gameObject);

                instance = this;

                texture_display = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/display.png");
                texture_display_center = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/display_center.png");
                texture_display_screen = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/display_screen.png");
                texture_divider = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/divider.png");
                texture_plus = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/plus.png");
                sprite_display = Sprite.Create(texture_display, new Rect(Vector2.zero, new Vector2(texture_display.width, texture_display.height)), Vector2.zero);
                sprite_display_center = Sprite.Create(texture_display_center, new Rect(Vector2.zero, new Vector2(texture_display_center.width, texture_display_center.height)), Vector2.zero);
                sprite_display_screen = Sprite.Create(texture_display_screen, new Rect(Vector2.zero, new Vector2(texture_display_screen.width, texture_display_screen.height)), Vector2.zero);
                sprite_divider = Sprite.Create(texture_divider, new Rect(Vector2.zero, new Vector2(texture_divider.width, texture_divider.height)), Vector2.zero);
                sprite_plus = Sprite.Create(texture_plus, new Rect(Vector2.zero, new Vector2(texture_plus.width, texture_plus.height)), Vector2.zero);

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
                monitor = new GameObject($"(Image) Monitor", typeof(RectTransform), typeof(Image), typeof(XButton)).GetComponent<Image>();
                monitor.transform.SetParent(transform);
                monitor.transform.localScale = Vector3.one;// new Vector3(1.25f, 1.25f, 1.25f);
                monitor.transform.localPosition = Vector3.zero;
                monitor.sprite = sprite_display;
                monitor.SetNativeSize();
                monitor.raycastTarget = false;

                center = new GameObject($"(Image) Center", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                center.transform.SetParent(monitor.transform);
                center.transform.localScale = Vector3.one;
                center.transform.localPosition = Vector3.zero;
                center.sprite = sprite_display_center;
                center.SetNativeSize();
                center.raycastTarget = false;
                center.color = new Color(1, 1, 1, 0);

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

                for (int e = 0; e < configuration.graphDimensions.length; e++)
                {
                    Screen newScreen = new GameObject($"(Screen) {Utils.FlatIndexTo2D(e, configuration.graphDimensions.x, false)}", typeof(RectTransform), typeof(Screen)).GetComponent<Screen>();
                    newScreen.transform.SetParent(transform);
                    newScreen.transform.localScale = Vector3.one;
                    newScreen.position = Utils.FlatIndexTo2D(e, configuration.graphDimensions.x, false);
                    newScreen.rectTransform = newScreen.GetComponent<RectTransform>();

                    GameObject addPlayerGameObject = new GameObject($"(XButton) Add Player", typeof(RectTransform), typeof(Image), typeof(XButton));
                    addPlayerGameObject.transform.SetParent(newScreen.transform);
                    addPlayerGameObject.transform.localScale = Vector3.one;
                    addPlayerGameObject.transform.localPosition = Vector3.zero;

                    XButton addPlayerButton = addPlayerGameObject.GetComponent<XButton>();
                    addPlayerButton.onClickMono.AddListener(OnClickScreenAddPlayer);
                    addPlayerButton.transform.localScale = Vector3.one * 0.75f;

                    Image screenImage = addPlayerGameObject.GetComponent<Image>();
                    screenImage.sprite = sprite_plus;
                    screenImage.SetNativeSize();
                    screenImage.raycastTarget = true;

                    newScreen.Initialize();
                    screens.Add(newScreen);
                }

                // Create PlayerPanes

                panes = new PlayerPane[4];

                for(int e = 0; e < 4; e++)
                {
                    panes[e] = new GameObject($"(PlayerPane) Player '{e}'", typeof(RectTransform), typeof(PlayerPane), typeof(Follower)).GetComponent<PlayerPane>();
                    panes[e].transform.SetParent(transform);
                    panes[e].transform.localScale = Vector3.one;
                    panes[e].Initialize();
                }

                OnConfigurationUpdated();
            }
            private void ToggleListeners(bool status)
            {
                if(status)
                {
                    configuration?.onConfigurationUpdated?.AddListener(OnConfigurationUpdated);
                }
                else
                {
                    configuration?.onConfigurationUpdated?.RemoveListener(OnConfigurationUpdated);
                }
            }
            #endregion

            #region Event Handlers
            public void OnConfigurationUpdated()
            {
                // Update followers 
                UpdateDisplayFollowers();

                foreach (Screen screen in screens)
                    screen.showAddPlayerButton = false;

                bool[] shouldCatchUpPanePosition = new bool[4];

                for(int e = 0; e < panes.Length; e++)
                {
                    shouldCatchUpPanePosition[e] = !panes[e].HasAssignment();
                    panes[e].ClearAssignment();
                }

                // Update buttons then dividers

                var data = AssignmentManager.instance.graph.GetGraph();

                var center = AssignmentManager.instance.graph.GetNodeData(int2.one);

                bool hasAssignment = false;

                int x = 0;
                int y = 0;

                for(x = 0; x < data.Length; x++)
                {
                    for(y = 0; y < data[x].Length; y++)
                    {
                        if(data[x][y].nodeData.data.isAssigned)
                        {
                            hasAssignment = true;
                            break;
                        }
                    }

                    if (hasAssignment)
                        break;
                }

                dividerEnabled[0] = false;
                dividerEnabled[1] = false;
                dividerEnabled[2] = false;
                dividerEnabled[3] = false;
                centerEnabled = true;

                bool canAddPlayer = configuration.assignedPlayerCount < configuration.maxLocalPlayers;

                Log.LogDebug($"ScreenDisplay.OnConfigurationUpdated: configuration.assignedPlayerCount = '{configuration.assignedPlayerCount}'");

                for (x = 0; x < data.Length; x++)
                {
                    for(y = 0; y < data[x].Length; y++)
                    {
                        var node = data[x][y];

                        var neighborUp = AssignmentManager.instance.graph.GetNode(node.neighborUp);
                        var neighborRight = AssignmentManager.instance.graph.GetNode(node.neighborRight);
                        var neighborDown = AssignmentManager.instance.graph.GetNode(node.neighborDown);
                        var neighborLeft = AssignmentManager.instance.graph.GetNode(node.neighborLeft);

                        int screenIndex = Utils.FlatIndexFrom2D(node.nodeData.data.position, configuration.graphDimensions.x, false);

                        Screen screen = screens[screenIndex];

                        //screen.showButton = !node.nodeData.data.isAssigned;

                        switch(node.nodeType)
                        {
                            case NodeType.Primary:
                                if(node.nodeData.data.isAssigned)
                                {
                                    screens[Utils.FlatIndexFrom2D(node.neighborUp, configuration.graphDimensions.x, false)].showAddPlayerButton = canAddPlayer;
                                    screens[Utils.FlatIndexFrom2D(node.neighborRight, configuration.graphDimensions.x, false)].showAddPlayerButton = canAddPlayer;
                                    screens[Utils.FlatIndexFrom2D(node.neighborDown, configuration.graphDimensions.x, false)].showAddPlayerButton = canAddPlayer;
                                    screens[Utils.FlatIndexFrom2D(node.neighborLeft, configuration.graphDimensions.x, false)].showAddPlayerButton = canAddPlayer;
                                    centerEnabled = false;
                                }
                                else
                                    if(!hasAssignment)
                                        screen.showAddPlayerButton = canAddPlayer;
                                break;
                            case NodeType.Secondary:
                                if(node.nodeData.data.isAssigned)
                                {
                                    if(neighborUp is null || neighborDown is null) // Column
                                    {
                                        screens[Utils.FlatIndexFrom2D(node.neighborLeft, configuration.graphDimensions.x, false)].showAddPlayerButton = canAddPlayer;
                                        screens[Utils.FlatIndexFrom2D(node.neighborRight, configuration.graphDimensions.x, false)].showAddPlayerButton = canAddPlayer;
                                        dividerEnabled[1] = true;
                                        dividerEnabled[3] = true;
                                    }
                                    else // Row
                                    {
                                        screens[Utils.FlatIndexFrom2D(node.neighborUp, configuration.graphDimensions.x, false)].showAddPlayerButton = canAddPlayer;
                                        screens[Utils.FlatIndexFrom2D(node.neighborDown, configuration.graphDimensions.x, false)].showAddPlayerButton = canAddPlayer;
                                        dividerEnabled[0] = true;
                                        dividerEnabled[2] = true;
                                    }
                                }
                                break;
                            default:
                                if(node.nodeData.data.isAssigned)
                                {
                                    if(neighborUp is null) // Top
                                    {
                                        dividerEnabled[0] = true;
                                    }
                                    else // Bottom
                                    {
                                        dividerEnabled[2] = true;
                                    }

                                    if(neighborLeft is null)
                                    {
                                        dividerEnabled[3] = true;
                                    }
                                    else
                                    {
                                        dividerEnabled[1] = true;
                                    }
                                }
                                break;
                        }

                        screen.LoadAssignment(node.nodeData.data);

                        if(node.nodeData.data.isAssigned)
                            panes[node.nodeData.data.playerId].LoadAssignment(node.nodeData.data, screen, shouldCatchUpPanePosition[node.nodeData.data.playerId]);
                    }
                }
            }
            public void OnClickScreenAddPlayer(MonoBehaviour mono)
            {
                if (configuration.enabled)
                    return;

                Screen screen = (mono as XButton).transform.parent.GetComponent<Screen>();

                if(screen != null)
                    AssignmentManager.instance.OnClickScreenAddPlayer(screen);
            }
            #endregion

            #region Display
            public void UpdateDisplayFollowers()
            {
                foreach (Icon icon in ControllerIconManager.instance.icons)
                    icon.UpdateDisplayFollower(null);

                foreach (Screen screen in screens)
                {
                    if (screen is null)
                        return;

                    screen.addPlayerTargetColor.w = 1;

                    foreach (Assignment assignment in configuration.assignments)
                    {
                        if (assignment.position.Equals(screen.position) && assignment.displayId == ControllerAssignmentState.currentDisplay)
                        {
                            screen.addPlayerTargetColor.w = 0;

                            foreach (Icon icon in ControllerIconManager.instance.icons)
                            {
                                if (assignment.HasController(icon.controller))
                                {
                                    icon.UpdateDisplayFollower(screen.gameObject.GetComponent<RectTransform>());
                                }
                            }
                        }
                    }
                }
            }
            public void CatchUpFollowers()
            {
                foreach(Icon icon in ControllerIconManager.instance.icons)
                {
                    icon.displayFollower.CatchUp();
                    icon.iconFollower.CatchUp(); // TODO meaningless call?
                }
            }
            #endregion
        }
        public class Screen : MonoBehaviour
        {
            #region Variables
            public static readonly float fadeSpeed = 30f;

            public RectTransform rectTransform;

            public int2 position;

            public bool showAddPlayerButton
            {
                get
                {
                    return showAddPlayerButtonBool;
                }
                set
                {
                    addPlayerImage.raycastTarget = showAddPlayerButtonBool = value;
                }
            }
            private bool showAddPlayerButtonBool = false;

            public bool showPlayerPane
            {
                get
                {
                    return showPlayerPaneBool;
                }
                set
                {
                    showPlayerPaneBool = value;

                    //playerPaneBackground.raycastTarget = showPlayerPaneBool = value;
                }
            }
            private bool showPlayerPaneBool = false;

            private Image addPlayerImage;

            public Vector4 addPlayerTargetColor = new Vector4(1, 1, 1, 0);
            
            private readonly Vector4 disabledColor = new Vector4(1, 1, 1, 0);
            #endregion

            #region Unity Methods
            public void Update()
            {
                if(showAddPlayerButton && configuration.localPlayerCount < configuration.maxLocalPlayers)
                    addPlayerImage.color = Color.Lerp(addPlayerImage.color, addPlayerTargetColor, Time.unscaledDeltaTime * fadeSpeed);
                else
                    addPlayerImage.color = Color.Lerp(addPlayerImage.color, disabledColor, Time.unscaledDeltaTime * fadeSpeed);
            }
            #endregion

            #region Initialization
            public void Initialize()
            {
                rectTransform.localPosition = instance.display.screenPositions[position.x][position.y];

                InitializeReferences();
                InitializeUI();
            }
            private void InitializeReferences()
            {
                addPlayerImage = transform.GetChild(0).GetComponent<Image>();
                addPlayerImage.color = disabledColor;
            }
            private void InitializeUI()
            {

            }
            #endregion

            #region Player
            public void LoadAssignment(Assignment assignment)
            {
                showPlayerPane = assignment.isAssigned;
            }
            #endregion
        }
        public class PlayerPane : MonoBehaviour
        {
            #region Variables
            private readonly Vector4 disabledColor = new Vector4(1, 1, 1, 0);
            private readonly string colorFormatString = "0:0.00";
            private readonly Vector2 colorMinMax = new Vector2(0, 1);

            private HGTextMeshProUGUI colorValueText;

            private Image backgroundImage;
            private Image removeIcon;
            private Image settingsIcon;

            private Follower follower;

            private MPDropdown profileDropdown;

            private Slider colorSlider;

            private Assignment assignment;

            private bool settingsOpen = false;
            #endregion

            #region Unity Methods
            public void Start()
            {
                if (follower.enabled)
                    follower.CatchUp();
            }
            public void Update()
            {
                if (follower.enabled)
                {
                    backgroundImage.color = Color.Lerp(backgroundImage.color, Color.white, Time.unscaledDeltaTime * Screen.fadeSpeed);
                }
                else
                {
                    backgroundImage.color = Color.Lerp(backgroundImage.color, disabledColor, Time.unscaledDeltaTime * Screen.fadeSpeed);
                }
            }
            public void OnDestroy()
            {
                configuration?.onSplitScreenEnabled.RemoveListener(OnSplitScreenEnabled);
                configuration?.onSplitScreenDisabled.RemoveListener(OnSplitScreenDisabled);
            }
            #endregion

            #region Initialize
            public void Initialize()
            {
                configuration?.onSplitScreenEnabled.AddListener(OnSplitScreenEnabled);
                configuration?.onSplitScreenDisabled.AddListener(OnSplitScreenDisabled);

                GameObject playerPaneBackgroundObject = new GameObject("(Image) Background", typeof(RectTransform), typeof(Image));
                playerPaneBackgroundObject.transform.SetParent(transform);
                playerPaneBackgroundObject.transform.localScale = Vector3.one * 0.445f;//0.425f;
                playerPaneBackgroundObject.transform.localPosition = Vector3.zero;

                backgroundImage = playerPaneBackgroundObject.GetComponent<Image>();
                backgroundImage.sprite = instance.display.sprite_display_screen;
                backgroundImage.SetNativeSize();
                backgroundImage.raycastTarget = false;
                backgroundImage.color = disabledColor;

                follower = gameObject.GetComponent<Follower>();
                follower.smoothMovement = true;
                follower.movementSpeed = .2f;//.45f;

                InitializePane();
            }
            private void InitializePane()
            {
                // TODO create slider for custom color
                // fix player names
                // ??

                GameObject prefab = MainMenuController.instance.settingsMenuScreen.GetComponentInChildren<SubmenuMainMenuScreen>(true).submenuPanelPrefab.GetComponentInChildren<MPDropdown>(true).gameObject;

                profileDropdown = Instantiate(prefab, transform).GetComponent<MPDropdown>();
                //profileDropdown.gameObject.GetComponent<ButtonSkinController>().skinData = GameObject.Find("NakedButton (Back)").GetComponent<ButtonSkinController>().skinData;
                profileDropdown.name = "ProfileDropdown";
                profileDropdown.transform.localPosition = new Vector3(101.5f, -75.1f);
                profileDropdown.transform.localScale = Vector3.one * .79f;
                profileDropdown.gameObject.SetActive(true);
                profileDropdown.allowAllEventSystems = true;
                profileDropdown.GetComponent<Image>().SetNativeSize();
                profileDropdown.template.gameObject.GetComponentInChildren<HGTextMeshProUGUI>().fontSize = 32;
                profileDropdown.template.gameObject.GetComponentInChildren<HGTextMeshProUGUI>().overflowMode = TMPro.TextOverflowModes.Truncate;
                //profileDropdown.template.gameObject.GetComponentInChildren<PanelSkinController>().skinData = GameObject.Find("NakedButton (Profile)").GetComponent<ButtonSkinController>().skinData;
                //Log.LogDebug($"PlayerPane.InitializePane: button = '{GameObject.Find("NakedButton (Profile)")}'");
                profileDropdown.transform.GetChild(0).gameObject.GetComponentInChildren<HGTextMeshProUGUI>().fontSize = 32;
                profileDropdown.transform.GetChild(0).gameObject.GetComponentInChildren<HGTextMeshProUGUI>().overflowMode = TMPro.TextOverflowModes.Truncate;

                removeIcon = new GameObject("(XButton) Remove", typeof(RectTransform), typeof(Image), typeof(XButton)).GetComponent<Image>();
                removeIcon.transform.SetParent(transform);
                removeIcon.transform.localScale = Vector3.one * 0.18f;//0.2f;
                removeIcon.transform.localPosition = new Vector3(75f, 75f, 0f);
                removeIcon.sprite = ControllerIconManager.instance.sprite_Xmark;
                removeIcon.SetNativeSize();
                removeIcon.GetComponent<XButton>().onClickMono.AddListener(OnRemovePlayer);

                settingsIcon = new GameObject("(XButton) Settings", typeof(RectTransform), typeof(Image), typeof(XButton)).GetComponent<Image>();
                settingsIcon.transform.SetParent(transform);
                settingsIcon.transform.localScale = Vector3.one * 0.08f;
                settingsIcon.transform.localPosition = new Vector3(-75f, 75f, 0f);
                settingsIcon.sprite = ControllerIconManager.instance.sprite_Gear;
                settingsIcon.SetNativeSize();
                settingsIcon.GetComponent<XButton>().onClickMono.AddListener(OnToggleSettings);

                GameObject sliderPrefab = MainMenuController.instance.settingsMenuScreen.GetComponentInChildren<SubmenuMainMenuScreen>(true).submenuPanelPrefab.GetComponentInChildren<SettingsSlider>(true).gameObject;

                colorSlider = Instantiate(sliderPrefab.transform.GetChild(4).gameObject, transform).GetComponentInChildren<Slider>();
                colorSlider.transform.localScale = Vector3.one;
                colorSlider.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 0);
                colorSlider.name = "(Slider) Color";

                colorSlider.minValue = colorMinMax.x;
                colorSlider.maxValue = colorMinMax.y;
                colorSlider.onValueChanged.AddListener(OnSliderValueChange);

                //Destroy(colorSlider.GetComponent<Image>());
                //Destroy(colorSlider.GetComponent<ConsoleFunctions>());
                //Destroy(colorSlider.GetComponent<LayoutElement>());
                //Destroy(colorSlider.GetComponent<ButtonSkinController>());
                //Destroy(colorSlider.GetComponent<HGButton>());
                //Destroy(colorSlider.transform.GetChild(0).gameObject);
                //Destroy(colorSlider.transform.GetChild(1).gameObject);
                //Destroy(colorSlider.transform.GetChild(2).gameObject);
                //Destroy(colorSlider.transform.GetChild(3).gameObject);
                
            }
            #endregion

            #region Assignment
            public bool HasAssignment()
            {
                return assignment.playerId > -1;
            }
            public void LoadAssignment(Assignment assignment, Screen screen, bool resetPosition = false)
            {
                if(false) // TODO This isn't working properly. When a player is unassigned the pane needs to move back to the center!
                {
                    follower.target = transform.parent.GetComponent<RectTransform>();
                    follower.CatchUp();
                }

                follower.target = screen.GetComponent<RectTransform>();
                follower.enabled = true;
                this.assignment = assignment;

                UpdateUI();
            }
            public void ClearAssignment()
            {
                follower.enabled = false;
                UpdateUI();
            }
            private void UpdateUI()
            {
                profileDropdown.gameObject.SetActive(follower.enabled);
                removeIcon.gameObject.SetActive(follower.enabled);
                settingsIcon.gameObject.SetActive(follower.enabled);

                SetSettingsOpen(false);

                SetUILock(configuration.enabled);
            }
            #endregion

            #region Event Handlers
            public void OnSliderValueChange(float value)
            {

            }

            public void OnToggleSettings(MonoBehaviour mono)
            {
                settingsOpen = !settingsOpen;

                SetSettingsOpen(settingsOpen);
            }
            public void OnSplitScreenEnabled()
            {
                SetUILock(true);
            }
            public void OnSplitScreenDisabled()
            {
                SetUILock(false);
            }
            public void OnProfileSelected(int profileId)
            {
                instance.SetProfile(profileId - 1, assignment.position);
                //MPEventSystem.current.SetSelectedGameObject(null);
            }
            public void OnRemovePlayer(MonoBehaviour mono)
            {
                instance.OnClickScreenRemovePlayer(ScreenDisplay.instance.screens[Utils.FlatIndexFrom2D(assignment.position, configuration.graphDimensions.x, false)]);
                //instance.RemovePlayer()
            }
            #endregion

            #region UI
            private void SetUILock(bool status)
            {
                if (status)
                {
                    SetSettingsOpen(false);
                    removeIcon.sprite = ControllerIconManager.instance.sprite_Lock;
                    removeIcon.GetComponent<XButton>().interactable = false;
                    profileDropdown.interactable = false;
                    settingsIcon.GetComponent<XButton>().interactable = false;
                }
                else
                {
                    removeIcon.sprite = ControllerIconManager.instance.sprite_Xmark;
                    removeIcon.GetComponent<XButton>().interactable = true;
                    profileDropdown.interactable = true;
                    settingsIcon.GetComponent<XButton>().interactable = true;
                }
            }
            private void UpdateProfileDropdown()
            {
                if (follower.enabled && !settingsOpen)
                {
                    profileDropdown.onValueChanged.RemoveAllListeners();

                    List<string> options = new List<string>();

                    options.Add(" - Select Profile -");

                    int id = 0;

                    foreach (KeyValuePair<string, UserProfile> keyPair in PlatformSystems.saveSystem.loadedUserProfiles)
                    {
                        //options.Add($"Profile {id + 1}");
                        id++;
                        options.Add(keyPair.Value.name);
                    }

                    profileDropdown.ClearOptions();
                    profileDropdown.AddOptions(options);

                    if (assignment.profileId > -1)
                        profileDropdown.value = assignment.profileId + 1;

                    profileDropdown.gameObject.SetActive(true);
                    profileDropdown.onValueChanged.AddListener(OnProfileSelected);
                }
                else
                {
                    profileDropdown.gameObject.SetActive(false);
                }
            }
            private void UpdateRemoveIcon()
            {
                if (configuration.localPlayerCount == 1 || settingsOpen)
                    removeIcon.enabled = false;
                else
                    removeIcon.enabled = true;
            }
            private void SetSettingsOpen(bool status)
            {
                //Log.LogDebug($"PlayerPane.SetSettingsOpen: name = '{name}', status = '{status}'");

                if (status)
                {
                    settingsIcon.sprite = removeIcon.sprite;
                    colorSlider.transform.parent.gameObject.SetActive(false);

                    foreach (Icon icon in ControllerIconManager.instance.icons)
                    {
                        if (icon.controller.Equals(assignment.controller))
                        {
                            icon.ToggleDisplayImage(false);
                            break;
                        }
                    }
                }
                else
                {
                    settingsIcon.sprite = ControllerIconManager.instance.sprite_Gear;
                    colorSlider.transform.parent.gameObject.SetActive(false);

                    foreach (Icon icon in ControllerIconManager.instance.icons)
                    {
                        if (icon.controller.Equals(assignment.controller))
                        {
                            icon.ToggleDisplayImage(true);
                            break;
                        }
                    }
                }

                settingsOpen = status;

                UpdateProfileDropdown();
                UpdateRemoveIcon();
            }
            private void UpdateSettingsUI(bool status)
            {

            }
            #endregion
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