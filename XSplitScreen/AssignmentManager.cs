using DoDad.Library.AI;
using DoDad.Library.Graph;
using DoDad.Library.Math;
using DoDad.Library.UI;
using Rewired;
using RoR2;
using RoR2.UI;
using RoR2.UI.MainMenu;
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
        #endregion

        #region Initialization & Exit
        public void Initialize()
        {
            InitializeReferences();
            InitializeGraph();
            InitializeViewport();
            ToggleListeners(true);
            PrintGraph("Initialized");
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
        public void OnClickScreenAddPlayer(Screen screen)
        {
            Log.LogDebug($"Adding player to '{screen.name}'");
            AddPlayer(screen);
        }
        public void OnControllerConnected(ControllerStatusChangedEventArgs args)
        {
            display.UpdateDisplayFollowers();
            display.CatchUpFollowers();
        }
        public void OnClickScreen(Screen screen)
        {
            Log.LogDebug($"Clicked on '{screen.name}'");
        }
        public void OnAssignmentLoaded(Controller controller, Assignment assignment)
        {

        }
        public void OnAssignmentUnloaded(Controller controller, Assignment assignment)
        {

        }
        public void OnIconAdded(Icon icon)
        {
        }
        public void OnIconRemoved(Icon icon)
        {
        }
        public void OnStartDragIcon(Icon icon)
        {
        }
        public void OnStopDragIcon(Icon icon)
        {
        }
        // TODO

        // Need to probably just rewrite it.
        //
        // Potential infinite loop involving events causing crash
        // yeah just rewrite it
        #endregion

        #region Graph
        private void LoadAssignments()
        {
            foreach(Assignment assignment in configuration.assignments)
            {
                if(assignment.displayId == ControllerAssignmentState.currentDisplay && assignment.position.IsPositive())
                {
                    graph.GetNode(assignment.position).nodeData.data.Load(assignment);
                }
            }
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

            configuration.PushChanges(changeBuffer);
            // Save changes
            configuration.Save();

            PrintGraph("AddPlayer");
        }
        private void RemovePlayer(Screen screen)
        {
            if (configuration.localPlayerCount == 1)
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
            ShiftRadial(destination, false);
            // TODO
            // Create X button on the player pane to unassign player
            // Determine node type and either ShiftRadial or ShiftLinear
        }
        private void ShiftRadial(int2 origin, bool expand)
        {
            var node = graph.GetNode(origin);

            if(expand)
            {
                ShiftNeighbor(node.neighborUp, node.neighborUpShift);
                ShiftNeighbor(node.neighborRight, node.neighborRightShift);
                ShiftNeighbor(node.neighborDown, node.neighborDownShift);
                ShiftNeighbor(node.neighborLeft, node.neighborLeftShift);
            }
            else
            {
                ShiftNeighbor(node.neighborUpShift, node.neighborUp);
                ShiftNeighbor(node.neighborRightShift, node.neighborRight);
                ShiftNeighbor(node.neighborDownShift, node.neighborDown);
                ShiftNeighbor(node.neighborLeftShift, node.neighborLeft);
            }
        }
        private void ShiftNeighbor(int2 origin, int2 destination)
        {
            if (!origin.IsPositive())
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
                for (int y = 0; y < data[x].Length; y++)
                {
                    //string line = string.Format(template, data[x][y].nodeData.data.position, (data[x][y].nodeData.data.isAssigned ? data[x][y].nodeData.data.controller.name : "none"), data[x][y].nodeData.data.deviceId.ToString(), data[x][y].nodeData.data.isKeyboard.ToString());
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
            public readonly int2 screenDimensions = new int2(135, 135);
            
            private readonly Vector4 disabledColor = new Vector4(1, 1, 1, 0);

            private const float dividerFadeSpeed = 2f;

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

            #region Initialization
            public void Initialize()
            {
                InitializeReferences();
                InitializeUI();
                ToggleListeners(true);
            }
            private void InitializeReferences()
            {
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
            }
            #endregion

            #region Event Handlers
            public void OnConfigurationUpdated()
            {
                // Update followers 
                UpdateDisplayFollowers();

                foreach (Screen screen in screens)
                    screen.showAddPlayerButton = false;

                for(int e = 0; e < panes.Length; e++)
                    panes[e].ClearAssignment();

                // Update buttons then dividers

                var data = instance.graph.GetGraph();

                var center = instance.graph.GetNodeData(int2.one);

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

                for (x = 0; x < data.Length; x++)
                {
                    for(y = 0; y < data[x].Length; y++)
                    {
                        var node = data[x][y];

                        var neighborUp = instance.graph.GetNode(node.neighborUp);
                        var neighborRight = instance.graph.GetNode(node.neighborRight);
                        var neighborDown = instance.graph.GetNode(node.neighborDown);
                        var neighborLeft = instance.graph.GetNode(node.neighborLeft);

                        int screenIndex = Utils.FlatIndexFrom2D(node.nodeData.data.position, configuration.graphDimensions.x, false);

                        Screen screen = screens[screenIndex];

                        //screen.showButton = !node.nodeData.data.isAssigned;
                        switch(node.nodeType)
                        {
                            case NodeType.Primary:
                                if(node.nodeData.data.isAssigned)
                                {
                                    screens[Utils.FlatIndexFrom2D(node.neighborUp, configuration.graphDimensions.x, false)].showAddPlayerButton = true;
                                    screens[Utils.FlatIndexFrom2D(node.neighborRight, configuration.graphDimensions.x, false)].showAddPlayerButton = true;
                                    screens[Utils.FlatIndexFrom2D(node.neighborDown, configuration.graphDimensions.x, false)].showAddPlayerButton = true;
                                    screens[Utils.FlatIndexFrom2D(node.neighborLeft, configuration.graphDimensions.x, false)].showAddPlayerButton = true;
                                    centerEnabled = false;
                                }
                                else
                                    if(!hasAssignment)
                                        screen.showAddPlayerButton = true;
                                break;
                            case NodeType.Secondary:
                                if(node.nodeData.data.isAssigned)
                                {
                                    if(neighborUp is null || neighborDown is null) // Column
                                    {
                                        screens[Utils.FlatIndexFrom2D(node.neighborLeft, configuration.graphDimensions.x, false)].showAddPlayerButton = true;
                                        screens[Utils.FlatIndexFrom2D(node.neighborRight, configuration.graphDimensions.x, false)].showAddPlayerButton = true;
                                        dividerEnabled[1] = true;
                                        dividerEnabled[3] = true;
                                    }
                                    else // Row
                                    {
                                        screens[Utils.FlatIndexFrom2D(node.neighborUp, configuration.graphDimensions.x, false)].showAddPlayerButton = true;
                                        screens[Utils.FlatIndexFrom2D(node.neighborDown, configuration.graphDimensions.x, false)].showAddPlayerButton = true;
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
                            panes[node.nodeData.data.playerId].LoadAssignment(node.nodeData.data, screen);
                    }
                }
            }
            public void OnClickScreenAddPlayer(MonoBehaviour mono)
            {
                Screen screen = (mono as XButton).transform.parent.GetComponent<Screen>();

                if(screen != null)
                    instance.OnClickScreenAddPlayer(screen);
            }
            public void OnPointerUpScreen(MonoBehaviour mono)
            {
                Log.LogDebug($"GraphDisplay.OnPointerDownScreen = '{mono.name}'");
                return;
                XButton button = mono as XButton;

                if (button is null)
                    return;

                if (button.eventSystem is null || button.eventSystem.currentInputModule is null || button.eventSystem.currentInputModule.input is null)
                    return;

                Screen closestScreen = null;

                Vector3 mousePosition = button.eventSystem.currentInputModule.input.mousePosition;
                Vector3 workingVector = Vector3.zero;

                float maxDistance = 25f;

                float screenDistance = float.MaxValue;
                float currentScreenDistance = float.MaxValue;

                foreach (Screen screen in instance.display.screens)
                {
                    workingVector = mousePosition - screen.transform.position;

                    currentScreenDistance = workingVector.sqrMagnitude / 1000f;

                    if (currentScreenDistance <= maxDistance)
                    {
                        if (currentScreenDistance < screenDistance)
                        {
                            screenDistance = currentScreenDistance;
                            closestScreen = screen;
                        }
                    }
                }

                if (closestScreen is null)
                    return;

                instance.OnClickScreen(closestScreen);
            }
            #endregion

            #region Display
            public void UpdateDisplayFollowers()
            {
                foreach (Screen screen in screens)
                {
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
                    Log.LogDebug($"'{icon.iconFollower.name}' catching up to '{icon.iconFollower.target?.name}' at '{icon.iconFollower.targetPosition}'");
                    icon.displayFollower.CatchUp();
                    icon.iconFollower.CatchUp(); // TODO meaningless call?
                }
            }
            #endregion
        }
        public class Screen : MonoBehaviour
        {
            #region Variables
            public static readonly float fadeSpeed = 20f;

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
                ToggleListeners(true);
            }
            private void InitializeReferences()
            {
                addPlayerImage = transform.GetChild(0).GetComponent<Image>();
                addPlayerImage.color = disabledColor;
            }
            private void InitializeUI()
            {
                // Create 'PlayerPaneManager' and move panes around just like controller icons

            }
            private void ToggleListeners(bool status)
            {
                if(status)
                {

                }
                else
                {

                }
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

            private Image background;

            private Follower follower;

            private MPDropdown profileDropdown;
            #endregion

            #region Unity Methods
            public void Update()
            {
                if (follower.enabled)
                {
                    background.color = Color.Lerp(background.color, Color.white, Time.unscaledDeltaTime * Screen.fadeSpeed);
                }
                else
                {
                    background.color = Color.Lerp(background.color, disabledColor, Time.unscaledDeltaTime * Screen.fadeSpeed);
                }
            }
            #endregion

            #region Initialize
            public void Initialize()
            {
                GameObject playerPaneBackgroundObject = new GameObject("(Image) Background", typeof(RectTransform), typeof(Image));
                playerPaneBackgroundObject.transform.SetParent(transform);
                playerPaneBackgroundObject.transform.localScale = Vector3.one * 0.4f;
                playerPaneBackgroundObject.transform.localPosition = Vector3.zero;

                background = playerPaneBackgroundObject.GetComponent<Image>();
                background.sprite = instance.display.sprite_display_screen;
                background.SetNativeSize();
                background.raycastTarget = false;
                background.color = disabledColor;

                follower = gameObject.GetComponent<Follower>();
                follower.smoothMovement = true;
                follower.movementSpeed = 0.75f;

                InitializePane();
            }
            private void InitializePane()
            {
                Log.LogDebug($"PlayerPane.InitializePane: MainMenuController.instance.settingsMenuScreen = {MainMenuController.instance.settingsMenuScreen.gameObject.name}");

                GameObject prefab = MainMenuController.instance.settingsMenuScreen.GetComponentInChildren<SubmenuMainMenuScreen>(true).submenuPanelPrefab.GetComponentInChildren<MPDropdown>(true).gameObject;

                profileDropdown = Instantiate(prefab, transform).GetComponent<MPDropdown>();
                profileDropdown.transform.localPosition = Vector3.zero + new Vector3(101.5f, -75.1f);
                profileDropdown.transform.localScale = Vector3.one * .79f;
                profileDropdown.gameObject.SetActive(true);
                profileDropdown.allowAllEventSystems = true;
                profileDropdown.GetComponent<Image>().SetNativeSize();
                profileDropdown.template.gameObject.GetComponentInChildren<HGTextMeshProUGUI>().fontSize = 32;
                profileDropdown.template.gameObject.GetComponentInChildren<HGTextMeshProUGUI>().overflowMode = TMPro.TextOverflowModes.Truncate;
                profileDropdown.transform.GetChild(0).gameObject.GetComponentInChildren<HGTextMeshProUGUI>().fontSize = 32;
                profileDropdown.transform.GetChild(0).gameObject.GetComponentInChildren<HGTextMeshProUGUI>().overflowMode = TMPro.TextOverflowModes.Truncate;

                List<string> options = new List<string>();

                int id = 1;
                foreach(KeyValuePair<string, UserProfile> keyPair in PlatformSystems.saveSystem.loadedUserProfiles)
                {
                    options.Add($"Profile {id}");
                    id++;
                    //options.Add(keyPair.Value.name);
                }

                profileDropdown.ClearOptions();
                profileDropdown.AddOptions(options);
                //profil
            }
            #endregion

            #region Assignment
            public void LoadAssignment(Assignment assignment, Screen screen)
            {
                follower.target = screen.GetComponent<RectTransform>();
                follower.enabled = true;
                profileDropdown.gameObject.SetActive(true);
            }
            public void ClearAssignment()
            {
                follower.enabled = false;
                profileDropdown.gameObject.SetActive(false);
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