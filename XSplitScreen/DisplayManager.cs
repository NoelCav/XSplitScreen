using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
/// <summary>
/// TODO
/// Rewrite how screens are worked. Screens should live under _backgroundMonitor and be updated based on CurrentDisplay
/// Each screen should take ownership of all controllers across configurations in the same spot and
/// subscribe to ChangedDisplay to update which ControllerDraggable is set to visible
/// </summary>
namespace DoDad.UI.Components
{
    class DisplayManager : MonoBehaviour
    {
        public static DisplayManager Instance;
        public static UnityEvent ChangedDisplay = new UnityEvent();

        private static readonly float AssignmentDistanceToScreen = 5f;
        private static readonly int MaxScreensPerDisplay = 4;
        private static readonly Vector2[] ScreenPositions = new Vector2[4]
        {
            new Vector2(-10, 10),
            new Vector2(10,10),
            new Vector2(-10,-10),
            new Vector2(10,-10)
        };
        private static List<DisplayConfiguration> DisplayConfigurations = new List<DisplayConfiguration>();

        public static int CurrentDisplay {
            get
            {
                return Instance._currentDisplay;
            }
            private set
            {
                Instance._currentDisplay = value;
                ChangedDisplay.Invoke();
            }
        }

        private int _currentDisplay = 0;

        private RectTransform _backgroundMonitor;

        private List<RectTransform> _dividers;

        public void Awake()
        {
            if (DisplayManager.Instance != null)
                Destroy(gameObject);

            DisplayManager.Instance = this;
            //_content = gameObject.GetComponentInChildren<UnityEngine.UI.ContentSizeFitter>().GetComponent<RectTransform>();
            //_content.GetComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            //_content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            //foreach (RoR2.UI.HGButton button in _content.GetComponentsInChildren<RoR2.UI.HGButton>())
            //{
            //    Destroy(button.gameObject);
            //}
            Destroy(transform.GetChild(0).gameObject);
            Destroy(transform.GetChild(1).gameObject);

            UnityEngine.UI.HorizontalLayoutGroup layout = gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            InitializeMenu();

            // Screens should be initialized here
            // First create n = MaxScreensPerDisplay then child them to _backgroundMonitor
            // Keep in a list 
        }
        public void UpdateDisplays()
        {
            // TODO initialize values from config

            if(Display.displays.Length < DisplayConfigurations.Count)
            {
                DisplayConfigurations.RemoveRange(Display.displays.Length - 1, DisplayConfigurations.Count - Display.displays.Length);
            }

            for(int e = DisplayConfigurations.Count; e < Display.displays.Length; e++)
            {
                DisplayConfigurations.Add(new DisplayConfiguration());

                for (int i = 0; i < MaxScreensPerDisplay; i++)
                {
                    DisplayConfigurations[e].Screens[i] = CreateScreen();
                    DisplayConfigurations[e].Screens[i].transform.SetParent(_backgroundMonitor);
                }
            }

            if (CurrentDisplay > DisplayConfigurations.Count - 1)
                CurrentDisplay = DisplayConfigurations.Count - 1;
            else
                CurrentDisplay = CurrentDisplay;

        }

        public void ClearDisplays()
        {

        }

        public static void AssignDraggable(ControllerDraggable newDraggable, int displayId, int screenId)
        {

        }
        public static bool EvaluateDraggable(ControllerDraggable newDraggable, out int screenIndex, out DisplayScreen newParent)
        {
            newParent = null;
            screenIndex = -1;

            if(Instance.OverScreen(newDraggable))
            {
                if(Instance.AssignToScreen(newDraggable, out screenIndex, out newParent))
                {
                    return true;
                }
            }

            return false;
        }
        private void InitializeMenu()
        {
            // Create a ScreenManager that monitors draggable slots
            // Check for distance to cursor from each slot
            // Change UI to reflect the desired change
            // Accept draggable into slot
            // Update settings

            Texture2D backgroundTexture = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/display.png");
            Texture2D dividerTexture = XSplitScreen.ResourceBundle.LoadAsset<Texture2D>("Assets/DoDad/Textures/divider.png");

            _backgroundMonitor = ModMenuManager.CreateImage("Monitor").GetComponent<RectTransform>();
            _backgroundMonitor.SetParent(transform);
            _backgroundMonitor.localScale = Vector3.one;

            UnityEngine.UI.Image _backgroundImage = _backgroundMonitor.GetComponent<UnityEngine.UI.Image>();

            _backgroundImage.sprite = Sprite.Create(backgroundTexture, new Rect(Vector2.zero, new Vector2(backgroundTexture.width, backgroundTexture.height)), Vector2.zero);
            _backgroundImage.SetNativeSize();

            _dividers = new List<RectTransform>();

            for(int e = 0; e < MaxScreensPerDisplay; e++)
            {
                _dividers.Add(ModMenuManager.CreateImage("Divider").GetComponent<RectTransform>());

                _dividers[_dividers.Count - 1].SetParent(_backgroundMonitor);
                _dividers[_dividers.Count - 1].localScale = Vector3.one / 2f;
                _dividers[_dividers.Count - 1].Rotate(new Vector3(0, 0, 90 * e));
                _dividers[_dividers.Count - 1].pivot = new Vector2(0.5f, 0);

                UnityEngine.UI.Image image = _dividers[_dividers.Count - 1].GetComponent<UnityEngine.UI.Image>();

                image.sprite = Sprite.Create(dividerTexture, new Rect(Vector2.zero, new Vector2(dividerTexture.width, dividerTexture.height)), Vector2.zero);
                image.SetNativeSize();

                DividerButton button = _dividers[_dividers.Count - 1].gameObject.AddComponent<DividerButton>();

                button.allowAllEventSystems = true;
                button.disableGamepadClick = false;

                button.AssignId(e);
                button.SetStatus(false);
            }
        }
        private bool OverScreen(ControllerDraggable newDraggable)
        {
            // Wrong, cycle ONLY through available screens and check for distance
            foreach(DisplayConfiguration configuration in DisplayConfigurations)
            {
                foreach(DisplayScreen screen in configuration.Screens)
                {
                    Debug.Log("Configuration: " + (configuration == null).ToString());
                    Debug.Log("Screen: " + (screen == null).ToString());
                }
            }
            return false;
        }
        private bool AssignToScreen(ControllerDraggable newDraggable, out int screenIndex, out DisplayScreen newParent)
        {
            screenIndex = -1;
            newParent = null;
            return false;
        }
        private DisplayScreen CreateScreen()
        {
            GameObject newScreenObject = new GameObject($"Display Screen", typeof(RectTransform), typeof(DisplayScreen));
            DisplayScreen newScreen = newScreenObject.GetComponent<DisplayScreen>();
            //newScreen.displayProfileId = displayId;

            return newScreen;
        }
        // A display is a screen (monitor), a screen is a portion of the screen assigned to a player
        internal class DisplayConfiguration
        {
            public int[] AvailableScreens = new int[4] { 1, 0, 0, 0 };
            public Rewired.Controller[] Assignments = new Rewired.Controller[MaxScreensPerDisplay];
            public DisplayScreen[] Screens = new DisplayScreen[MaxScreensPerDisplay]; // This doesn't belong here

            public void UpdateAssignments() // This is not needed, assignments should be set on EvaluateDraggable
            {
                for(int e = 0; e < Screens.Length; e++)
                {
                    Assignments[e] = Screens[e]?.AssignedController?.Controller;
                }
            }
        }
    }

}
