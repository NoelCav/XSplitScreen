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
    class AssignmentManager : MonoBehaviour
    {
        #region Variables
        public static AssignmentManager instance { get; private set; }

        public GraphDisplay display { get; private set; }

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
            Log.LogDebug($"AssignmentManager.InitializeGraph");

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
                    Log.LogDebug($"AssignmentManager.LoadAssignments {assignment}");
                    graph.GetNode(assignment.position).nodeData.data.Load(assignment);
                }
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
                for(int y = 0; y < data[x].Length; y++)
                {
                    //string line = string.Format(template, data[x][y].nodeData.data.position, (data[x][y].nodeData.data.isAssigned ? data[x][y].nodeData.data.controller.name : "none"), data[x][y].nodeData.data.deviceId.ToString(), data[x][y].nodeData.data.isKeyboard.ToString());
                    string id = "0X";

                    if(data[x][y].nodeData.data.controller != null)
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

        #region Definitions
        public class GraphDisplay : MonoBehaviour
        {
            #region Variables
            public readonly int2 screenDimensions = new int2(135, 135);

            private const float dividerFadeSpeed = 20f;

            public List<Screen> screens { get; private set; }
            public Vector3[][] screenPositions { get; private set; }

            private Texture2D texture_display;
            private Texture2D texture_display_center;
            private Texture2D texture_divider;
            private Texture2D texture_plus;

            private Sprite sprite_display;
            private Sprite sprite_display_center;
            private Sprite sprite_divider;
            private Sprite sprite_plus;

            private Image monitor;
            private Image center;
            private Image[] dividers;

            private bool[] dividerEnabled = new bool[4];
            private bool centerEnabled;
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
                texture_divider = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/divider.png");
                texture_plus = assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/plus.png");
                sprite_display = Sprite.Create(texture_display, new Rect(Vector2.zero, new Vector2(texture_display.width, texture_display.height)), Vector2.zero);
                sprite_display_center = Sprite.Create(texture_display_center, new Rect(Vector2.zero, new Vector2(texture_display_center.width, texture_display_center.height)), Vector2.zero);
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
                    Screen newScreen = new GameObject($"(Screen) {Utils.FlatIndexTo2D(e, configuration.graphDimensions.x, false)}", typeof(RectTransform), typeof(Screen), typeof(Image), typeof(XButton)).GetComponent<Screen>();
                    newScreen.transform.SetParent(transform);
                    newScreen.transform.localScale = Vector3.one;
                    newScreen.position = Utils.FlatIndexTo2D(e, configuration.graphDimensions.x, false);
                    newScreen.rectTransform = newScreen.GetComponent<RectTransform>();

                    // TODO
                    // Move "AddPlayer" image and button to child of newScreen
                    // Attach listener
                    // Create screen UI to edit local player
                    // Finish logic for assignments
                    XButton screenButton = newScreen.GetComponent<XButton>();
                    screenButton.onClickMono.AddListener(OnClickScreenAddPlayer);

                    Image screenImage = newScreen.GetComponent<Image>();
                    screenImage.sprite = sprite_plus;
                    screenImage.SetNativeSize();
                    screenImage.raycastTarget = true;

                    newScreen.Initialize();
                    screens.Add(newScreen);
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
                        dividers[e].color = Color.Lerp(dividers[e].color, new Color(1, 1, 1, 0.6f), Time.unscaledDeltaTime * dividerFadeSpeed);
                    else
                        dividers[e].color = Color.Lerp(dividers[e].color, new Color(1, 1, 1, 0), Time.unscaledDeltaTime * dividerFadeSpeed);
                }

                if (centerEnabled)
                    center.color = Color.Lerp(center.color, new Color(1, 1, 1, 0.6f), Time.unscaledDeltaTime * dividerFadeSpeed);
                else
                    center.color = Color.Lerp(center.color, new Color(1, 1, 1, 0), Time.unscaledDeltaTime * dividerFadeSpeed);
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
                                }
                                break;
                        }

                        screen.LoadAssignment(node.nodeData.data);
                    }
                }
            }
            public void OnClickScreenAddPlayer(MonoBehaviour mono)
            {
                return;
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
            public void OnGraphUpdated()
            {
                return;

                var data = instance.graph.GetNodeData(true);

                foreach (Icon icon in ControllerIconManager.instance.icons)
                {
                    icon.displayFollower.enabled = false;
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
                            // Check for assigned screen
                            // If assigned show customization panel
                            // Needs to reflect the data
                            // Add a (+) button to each unassigned screen position
                            // this allows manual setting of layouts instead of the grid algorithm
                            /*
                            foreach (Icon icon in ControllerIconManager.instance.icons)
                            {
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
                            */
                        }
                    }
                }


            }
            #endregion

            #region Display
            public void UpdateDisplayFollowers()
            {
                foreach (Screen screen in screens)
                {
                    screen.targetColor.w = 1;

                    foreach (Assignment assignment in configuration.assignments)
                    {
                        if (assignment.position.Equals(screen.position) && assignment.displayId == ControllerAssignmentState.currentDisplay)
                        {
                            screen.targetColor.w = 0;

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
            private static readonly float colorSpeed = 20f;

            public RectTransform rectTransform;

            public int2 position;

            public bool showAddPlayerButton = false;

            private Image addPlayerImage;

            public Vector4 targetColor = new Vector4(1, 1, 1, 0);
            public Vector4 disabledColor = new Vector4(1, 1, 1, 0);
            #endregion

            #region Unity Methods
            public void Update()
            {
                if(showAddPlayerButton)
                    addPlayerImage.color = Color.Lerp(addPlayerImage.color, targetColor, Time.unscaledDeltaTime * colorSpeed);
                else
                    addPlayerImage.color = Color.Lerp(addPlayerImage.color, disabledColor, Time.unscaledDeltaTime * colorSpeed);
            }
            #endregion

            #region Initialization
            public void Initialize()
            {
                rectTransform.localPosition = instance.display.screenPositions[position.x][position.y];

                InitializeReferences();
            }
            private void InitializeReferences()
            {
                addPlayerImage = gameObject.GetComponent<Image>();
                addPlayerImage.color = disabledColor;
            }
            #endregion

            #region Player
            public void LoadAssignment(Assignment assignment)
            {
                if(assignment.isAssigned)
                {

                }
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