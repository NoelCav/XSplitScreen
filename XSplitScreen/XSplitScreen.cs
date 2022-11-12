using BepInEx;
using BepInEx.Configuration;
using MonoMod.Cil;
using R2API;
using R2API.Utils;
using Rewired;
using RoR2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using DoDad.Library;
using DoDad.Library.Math;
using RoR2.UI.MainMenu;
using System.Collections;
using UnityEngine.SceneManagement;
using RoR2.UI;
using RoR2.UI.SkinControllers;
using DoDad.Library.Graph;

namespace XSplitScreen
{
    [BepInDependency(R2API.R2API.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(Library.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [R2APISubmoduleDependency(new string[] { "CommandHelper", "LanguageAPI" })]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)]
    public class XSplitScreen : BaseUnityPlugin
    {
        #region Variables
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "com.DoDad";
        public const string PluginName = "XSplitScreen";
        public const string PluginVersion = "2.0.0";

        public static Configuration configuration { get; private set; }
        public static XSplitScreen instance { get; private set; }
        public static AssetBundle assets { get; private set; }

        private static readonly bool developerMode = true;

        private RectTransform titleButton;
        private RectTransform menuContainer;
        
        private bool readyToCreateUI;
        #endregion

        #region Unity Methods
        public void Awake()
        {
            if (instance)
                Destroy(this);

            Initialize();
        }
        public void OnDestroy()
        {
            CleanupReferences();
        }
        public void Update()
        {
            if(readyToCreateUI)
            {
                CreateUI();
                readyToCreateUI = false;
            }
        }
        #endregion

        #region Initialization & Exit
        private void Initialize()
        {
            instance = this;

            Log.Init(Logger);
            CommandHelper.AddToConsoleWhenReady();

            InitializeLanguage();
            InitializeReferences();

            TogglePersistentHooks(true);
            ToggleUIHooks(true);

            if (developerMode)
                ScreenOnEnter();
        }
        private void InitializeLanguage()
        {
            LanguageAPI.Add(Language.MSG_HOVER_TOKEN, Language.MSG_HOVER_STRING);
            LanguageAPI.Add(Language.MSG_TITLE_BUTTON_TOKEN, Language.MSG_TITLE_BUTTON_STRING);
            LanguageAPI.Add(Language.MSG_SPLITSCREEN_ENABLE_HOVER_TOKEN, Language.MSG_SPLITSCREEN_ENABLE_HOVER_STRING);
            LanguageAPI.Add(Language.MSG_SPLITSCREEN_DISABLE_HOVER_TOKEN, Language.MSG_SPLITSCREEN_DISABLE_HOVER_STRING);
            LanguageAPI.Add(Language.MSG_SPLITSCREEN_ENABLE_TOKEN, Language.MSG_SPLITSCREEN_ENABLE_STRING);
            LanguageAPI.Add(Language.MSG_SPLITSCREEN_DISABLE_TOKEN, Language.MSG_SPLITSCREEN_DISABLE_STRING);
            LanguageAPI.Add(Language.MSG_SPLITSCREEN_CONFIG_HEADER_TOKEN, Language.MSG_SPLITSCREEN_CONFIG_HEADER_STRING);
            LanguageAPI.Add(Language.MSG_SPLITSCREEN_OPEN_DEBUG_TOKEN, Language.MSG_SPLITSCREEN_OPEN_DEBUG_STRING);
            LanguageAPI.Add(Language.MSG_SPLITSCREEN_OPEN_DEBUG_HOVER_TOKEN, Language.MSG_SPLITSCREEN_OPEN_DEBUG_HOVER_STRING);
            LanguageAPI.Add(Language.MSG_DISCORD_LINK_TOKEN, Language.MSG_DISCORD_LINK_STRING);
            LanguageAPI.Add(Language.MSG_DISCORD_LINK_HOVER_TOKEN, Language.MSG_DISCORD_LINK_HOVER_STRING);
            LanguageAPI.Add(Language.MSG_UNSET_TOKEN, Language.MSG_UNSET_STRING);
        }
        private void InitializeReferences()
        {
            if (assets is null)
            {
                try
                {
                    using (Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("XSplitScreen.xsplitscreenbundle"))
                        assets = AssetBundle.LoadFromStream(manifestResourceStream);
                }
                catch(Exception e)
                {
                    Log.LogError(e);
                }
            }

            if (configuration is null)
            {
                configuration = new Configuration(Config);
            }
        }
        private void CleanupReferences()
        {
            configuration?.Destroy();
            configuration = null;

            assets?.Unload(true);
            assets = null;

            instance = null;

            TogglePersistentHooks(false);
            ToggleUIHooks(false);

            if(titleButton != null)
                Destroy(titleButton?.gameObject);

            if(menuContainer != null)
                Destroy(menuContainer?.gameObject);
        }
        #endregion

        #region Hooks
        private void ToggleUIHooks(bool status)
        {
            if(status)
            {
                MainMenuController.instance.currentMenuScreen.onEnter.AddListener(ScreenOnEnter);
                MainMenuController.instance.currentMenuScreen.onExit.AddListener(ScreenOnExit);
            }
            else
            {
                MainMenuController.instance.currentMenuScreen.onEnter.RemoveListener(ScreenOnEnter);
                MainMenuController.instance.currentMenuScreen.onExit.RemoveListener(ScreenOnExit);
            }
        }
        #region Persistent
        private void TogglePersistentHooks(bool status)
        {
            if (status)
            {
                SceneManager.activeSceneChanged += ActiveSceneChanged;
            }
            else
            {
                SceneManager.activeSceneChanged -= ActiveSceneChanged;
            }
        }
        private void ScreenOnEnter()
        {
            StartCoroutine(WaitForMenu());
        }
        private void ScreenOnExit()
        {
            Log.LogDebug("ScreenOnExit");
        }
        private void ActiveSceneChanged(Scene previous, Scene current)
        {
            if (string.Compare(current.name, "title") == 0)
            {
                ToggleUIHooks(true);
                ScreenOnEnter();
            }
            else
            {
                ToggleUIHooks(false);
                ScreenOnExit();
            }
        }
        #endregion

        #endregion

        #region UI
        private void CreateUI()
        {
            Log.LogInfo($"Creating UI");
            CreateMainMenuButton();
            CreateMenu();
        }
        private void CreateMainMenuButton()
        {
            if (titleButton != null)
                return;

            GameObject template = GameObject.Find("GenericMenuButton (Singleplayer)");

            GameObject newXButton = Instantiate(template);

            newXButton.name = "(XButton) XSplitScreen";

            newXButton.transform.SetParent(template.transform.parent);
            newXButton.transform.SetSiblingIndex(1);
            newXButton.transform.localScale = Vector3.one;

            XButtonConverter converter = newXButton.AddComponent<XButtonConverter>();
            converter.Initialize();

            converter.onClickMono.AddListener(OpenMenu);
            converter.hoverToken = Language.MSG_HOVER_TOKEN;
            converter.token = Language.MSG_TITLE_BUTTON_TOKEN;

            titleButton = newXButton.GetComponent<RectTransform>();
        }
        private void CreateMenu()
        {
            if (menuContainer != null)
                return;

            menuContainer = new GameObject("MENU: XSplitScreen", typeof(RectTransform)).GetComponent<RectTransform>();
            menuContainer.SetParent(MainMenuController.instance.transform);
            
            GameObject menu = new GameObject("XSplitScreenMenu", typeof(RectTransform));
            menu.transform.SetParent(menuContainer);

            var screen = menu.AddComponent<XSplitScreenMenu>();
            screen.Initialize();

            menu.gameObject.SetActive(false);
        }
        private void OpenMenu(MonoBehaviour mono)
        {
            RoR2.UI.MainMenu.MainMenuController.instance.SetDesiredMenuScreen(XSplitScreenMenu.instance);
        }
        #endregion

        #region Splitscreen Logic

        #endregion

        #region Coroutines
        IEnumerator WaitForMenu()
        {
            GameObject singleplayerButton = null;

            while(singleplayerButton is null)
            {
                singleplayerButton = GameObject.Find("GenericMenuButton (Singleplayer)");
                yield return null;
            }

            Log.LogDebug($"WaitForMenu done");
            readyToCreateUI = true;

            yield return null;
        }
        #endregion

        #region Definitions
        public class AssignmentEvent : UnityEvent<Controller, Assignment> { }

        [System.Serializable]
        public class Configuration
        {
            #region Variables
            public UnityEvent onConfigurationUpdated { get; private set; }

            public Action<ControllerStatusChangedEventArgs> onControllerConnected;
            public Action<ControllerStatusChangedEventArgs> onControllerDisconnected;

            public List<Assignment> assignments { get; private set; } // TODO private
            public List<Controller> controllers { get; private set; }

            public int2 graphDimensions { get; private set; }

            public bool enabled = false;

            public int localPlayerCount { get; private set; }
            public readonly int maxLocalPlayers = 4;

            private bool devMode = true;

            private List<Preference> preferences;

            private ConfigFile config;

            private ConfigEntry<string> preferencesConfig;
            private ConfigEntry<bool> enabledConfig;
            private ConfigEntry<bool> autoKeyboardConfig;
            private ConfigEntry<int> playerCountConfig;
            #endregion

            #region Unity Methods
            public Configuration(ConfigFile configFile)
            {
                InitializeReferences();
                LoadConfigFile(configFile);
                InitializeAssignments();
                Save();
            }
            public void Destroy()
            {
                ToggleListeners(false);

                if(devMode)
                    ClearSave();

                Save();
            }
            #endregion

            #region Initialization & Exit
            private void ClearSave()
            {
                preferences.Clear();
                assignments.Clear();
                InitializeAssignments();
            }
            private void InitializeReferences()
            {
                assignments = new List<Assignment>();
                controllers = new List<Controller>();

                preferences = new List<Preference>();

                onConfigurationUpdated = new UnityEvent();
                //onAssignmentLoaded = new AssignmentEvent();
                //onAssignmentUnloaded = new AssignmentEvent();
                //onAssignmentUpdate = new AssignmentEvent();

                graphDimensions = new int2(3, 3);

                ToggleListeners(true);
                UpdateActiveControllers();
            }
            private void LoadConfigFile(ConfigFile configFile)
            {
                this.config = configFile;

                try
                {
                    preferencesConfig = configFile.Bind<string>("General", "Assignments", "", "Changes may break the mod!");
                    enabledConfig = configFile.Bind<bool>("General", "Enabled", false, "Should splitscreen automatically enable based on available controllers?");
                    autoKeyboardConfig = configFile.Bind<bool>("General", "UseKeyboard", true, "Automatically enable splitscreen using the keyboard?");
                    playerCountConfig = configFile.Bind<int>("General", "PlayerCount", 1, "Number of local players");
                }
                catch(Exception e)
                {
                    Log.LogError(e);
                }

                if(preferencesConfig.Value.Length > 0)
                {
                    try
                    {
                        Wrapper wrapper = JsonUtility.FromJson<Wrapper>(preferencesConfig.Value);

                        for(int e = 0; e < wrapper.displayId.Length; e++)
                        {
                            preferences.Add(new Preference()
                            {
                                position = new int2(wrapper.positionX[e], wrapper.positionY[e]),
                                context = new int2(wrapper.contextX[e], wrapper.contextY[e]),
                                displayId = wrapper.displayId[e],
                                playerId = wrapper.playerId[e],
                                profileId = wrapper.profileId[e],
                                color = new Color(wrapper.colorR[e], wrapper.colorG[e], wrapper.colorB[e], wrapper.colorA[e]),
                            });
                        }

                        Log.LogInfo($"Loaded {wrapper.displayId.Length} preferences");
                    }
                    catch(Exception e)
                    {
                        Log.LogError(e);
                    }
                }
                else
                {
                    Log.LogInfo($"No preferences found.");
                }
            }
            private void InitializeAssignments()
            {
                Controller[] availableControllers = controllers.Where(c => autoKeyboardConfig.Value ? true : c.type != ControllerType.Keyboard).ToArray();

                if(preferences.Count == 0)
                {
                    Log.LogDebug($"Creating preferences..");

                    for(int e = 0; e < maxLocalPlayers; e++)
                    {
                        if(e == 0)
                        {
                            preferences.Add(new Preference()
                            {
                                position = int2.one,
                                context = new int2(1, 0),
                                displayId = 0,
                                playerId = 0,
                                profileId = 0,
                                color = ColorCatalog.GetMultiplayerColor(e)
                            });
                        }
                        else
                        {
                            preferences.Add(new Preference()
                            {
                                position = int2.negative,
                                context = int2.negative,
                                displayId = -1,
                                playerId = e,
                                profileId = -1,
                                color = ColorCatalog.GetMultiplayerColor(e)
                            });
                        }
                    }
                }

                for (int preferenceId = 0; preferenceId < preferences.Count; preferenceId++)
                {
                    if(preferenceId < availableControllers.Length) // If controllers are available
                    {
                        LoadAssignment(preferenceId, availableControllers[preferenceId]);
                    }
                    else
                    {
                        Log.LogDebug($"Unable to find controller for preference id '{preferenceId}'");
                        LoadAssignment(preferenceId, null);
                    }
                }
            }
            private void ToggleListeners(bool status)
            {
                if (status)
                {
                    ReInput.ControllerConnectedEvent += OnControllerConnected;
                    ReInput.ControllerDisconnectedEvent += OnControllerDisconnected;
                }
                else
                {
                    ReInput.ControllerConnectedEvent -= OnControllerConnected;
                    ReInput.ControllerDisconnectedEvent -= OnControllerDisconnected;
                }
            }
            #endregion

            #region Events
            public void OnControllerConnected(Rewired.ControllerStatusChangedEventArgs args)
            {
                if (args.controllerType == ControllerType.Mouse)
                    return;

                UpdateActiveControllers();
                onControllerConnected.Invoke(args);

                foreach(Assignment assignment in assignments)
                {
                    if (assignment.controller is null)
                    {
                        assignment.Load(args.controller);
                        return;
                    }
                }
            }
            public void OnControllerDisconnected(Rewired.ControllerStatusChangedEventArgs args)
            {
                if (args.controllerType == ControllerType.Mouse)
                    return;

                UpdateActiveControllers();
                Log.LogDebug($"OnControllerDisconnected args: {args != null}");
                onControllerDisconnected.Invoke(args);
            }
            #endregion

            #region Controllers
            public Assignment? GetAssignment(Controller controller)
            {
                foreach(Assignment assignment in assignments)
                {
                    if (assignment.HasController(controller))
                        return assignment;
                }

                return null;
            }
            public bool IsAssigned(Controller controller)
            {
                if (controller is null)
                    return false;

                foreach (Assignment assignment in assignments)
                {
                    if (assignment.HasController(controller))
                    {
                        return true;
                    }
                }

                return false;
            }
            private void UpdateActiveControllers()
            {
                controllers = ReInput.controllers.Controllers.Where(c => c.type != ControllerType.Mouse).ToList();
            }
            #endregion

            #region Assignments
            public void PushChanges(List<Assignment> changes)
            {
                foreach(Assignment change in changes)
                {
                    SetAssignment(change);
                }
            }
            public void SetAssignment(Assignment assignment)
            {
                if (!assignment.position.IsPositive())
                    assignment.controller = null;

                assignments[assignment.playerId] = assignment;
            }
            public bool Save()
            {
                try
                {
                    UpdatePreferences();
                    Wrapper wrapper = new Wrapper(preferences);
                    enabledConfig.Value = enabled;
                    preferencesConfig.Value = JsonUtility.ToJson(wrapper);
                    config.Save();
                    return true;
                }
                catch(Exception e)
                {
                    Log.LogError(e);
                    return false;
                }

            }
            private void LoadAssignment(int preferenceId, Controller controller)
            {
                Assignment newAssignment = new Assignment(controller);

                newAssignment.Load(preferences[preferenceId]);

                assignments.Add(newAssignment);
            }
            private void UpdatePreferences()
            {
                localPlayerCount = 0;

                foreach(Assignment assignment in assignments)
                {
                    for(int e = 0; e < preferences.Count; e++)
                    {
                        if(assignment.Matches(preferences[e]))
                        {
                            var preference = preferences[e];
                            preference.Update(assignment);
                            preferences[e] = preference;
                            break;
                        }
                    }

                    if(assignment.isAssigned)
                        localPlayerCount++;
                }

                onConfigurationUpdated.Invoke();
            }
            #endregion

            #region Definitions
            [System.Serializable]
            private class Wrapper
            {
                //public bool[] isKeyboard;

                //public string[] profile;

                //public int[] deviceId;
                public int[] positionX;
                public int[] positionY;
                public int[] contextX;
                public int[] contextY;
                public int[] displayId;
                public int[] playerId;
                public int[] profileId;
                public float[] colorR;
                public float[] colorG;
                public float[] colorB;
                public float[] colorA;

                public Wrapper(List<Preference> preferences)
                {
                    //isKeyboard = new bool[preferences.Count];
                    //profile = new string[preferences.Count];
                    //deviceId = new int[preferences.Count];
                    positionX = new int[preferences.Count];
                    positionY = new int[preferences.Count];
                    contextX = new int[preferences.Count];
                    contextY = new int[preferences.Count];
                    displayId = new int[preferences.Count];
                    playerId = new int[preferences.Count];
                    profileId = new int[preferences.Count];
                    colorR = new float[preferences.Count];
                    colorG = new float[preferences.Count];
                    colorB = new float[preferences.Count];
                    colorA = new float[preferences.Count];

                    for (int e = 0; e < preferences.Count; e++)
                    {
                        //isKeyboard[e] = preferences[e].isKeyboard;
                        //profile[e] = preferences[e].profile;
                        //deviceId[e] = preferences[e].deviceId;
                        positionX[e] = preferences[e].position.x;
                        positionY[e] = preferences[e].position.y;
                        contextX[e] = preferences[e].context.x;
                        contextY[e] = preferences[e].context.y;
                        displayId[e] = preferences[e].displayId;
                        playerId[e] = preferences[e].playerId;
                        profileId[e] = preferences[e].profileId;
                        colorR[e] = preferences[e].color.r;
                        colorG[e] = preferences[e].color.g;
                        colorB[e] = preferences[e].color.b;
                        colorA[e] = preferences[e].color.a;
                    }
                }
            }
            #endregion
        }
        public struct Preference
        {
            public Color color;

            public int2 position;
            public int2 context;

            public int displayId;
            public int playerId;
            public int profileId;

            public Preference(int playerId)
            {
                position = int2.negative;
                context = int2.negative;
                displayId = -1;
                profileId = -1;
                this.playerId = playerId;
                color = Color.white;
            }
            public override string ToString()
            {
                string newFormat = "Preference(position = '{0}', context = '{1}', displayId = '{2}', playerId = '{3}', profileId = '{4}', color = '{5}')";

                return string.Format(newFormat, position, context, displayId, playerId, profileId, color);
            }
            public bool Matches(Preference preference)
            {
                return preference.playerId == this.playerId;
            }
            public void Update(Assignment assignment)
            {
                position = assignment.position;
                context = assignment.context;
                //deviceId = assignment.deviceId;
                displayId = assignment.displayId;
                playerId = assignment.playerId;
                profileId = assignment.profileId;
                color = assignment.color;
                //profile = assignment.profile;
                //isKeyboard = assignment.isKeyboard;
            }
        }
        public struct Assignment
        {
            public Controller controller; // Group: Assignment

            public Color color; // Group: Assignment

            public int2 position;  // Group: Screen
            public int2 context;  // Group: Assignment

            //public string profile;

            public int displayId;  // Group: Display
            public int playerId; // Group: Assignment
            public int profileId; // Group: Assignment

            public Assignment(Controller controller)
            {
                this.controller = controller;

                position = int2.negative;
                context = int2.negative;
                displayId = -1;
                playerId = -1;
                profileId = -1;
                color = Color.white;
            }
            public int deviceId
            {
                get
                {
                    if (controller is null)
                        return -1;

                    return controller.id;
                }
            }
            public bool isAssigned
            {
                get
                {
                    return position.IsPositive() && playerId > -1;
                }
            }
            public bool isKeyboard
            {
                get
                {
                    if(!(controller is null))
                    {
                        return controller.type == ControllerType.Keyboard;
                    }

                    return false;
                }
            }
            public override string ToString()
            {
                return $"Assignment(position = '{position}', playerId = '{playerId}', profileId = '{playerId}', controller = '{controller != null}')";
            }
            public bool Matches(Preference preference)
            {
                return this.playerId == preference.playerId;
            }
            public bool Matches(Assignment assignment)
            {
                return this.playerId == assignment.playerId;
            }
            public bool HasController(Controller controller)
            {
                if (this.controller is null)
                    return false;

                return this.controller.Equals(controller); //this.controller.id == controller.id && this.controller.type == controller.type;
            }
            public void Load(Preference preference)
            {
                position = preference.position;
                displayId = preference.displayId;
                playerId = preference.playerId;
                profileId = preference.profileId;
                color = preference.color;
            }
            public void Load(Assignment assignment)
            {
                controller = assignment.controller;
                displayId = assignment.displayId;
                playerId = assignment.playerId;
                profileId = assignment.profileId;
                color = assignment.color;
            }
            public void Load(AssignmentManager.Screen screen)
            {
                position = screen.position;
                displayId = ConfigurationManager.ControllerAssignmentState.currentDisplay;
                Log.LogDebug($"Assignment.Load screen: '{this}'");
            }
            public void Load(Controller controller)
            {
                this.controller = controller;
            }
            public void ClearPlayer()
            {
                this.controller = null;
                context = int2.negative;
                playerId = -1;
                profileId = -1;
                color = Color.white;
            }
            public void ClearScreen()
            {
                position = int2.negative;
                displayId = -1;
                //context = int2.negative; // Last known change
            }
            public void ClearAll()
            {
                ClearScreen();
                ClearPlayer();
                playerId = -1;
                profileId = -1;
                color = Color.white;
            }
        }
        public struct Language
        {
            public static readonly string MSG_DISCORD_LINK_HREF = "https://discord.gg/maHhJSv62G";
            public static readonly string MSG_DISCORD_LINK_STRING = "Discord";
            public static readonly string MSG_DISCORD_LINK_TOKEN = "XSPLITSCREEN_DISCORD";
            public static readonly string MSG_DISCORD_LINK_HOVER_STRING = "Join the Discord for support";
            public static readonly string MSG_DISCORD_LINK_HOVER_TOKEN = "XSPLITSCREEN_DISCORD_HOVER";
            public static readonly string MSG_SPLITSCREEN_OPEN_DEBUG_TOKEN = "XSPLITSCREEN_DEBUG_FILE";
            public static readonly string MSG_SPLITSCREEN_OPEN_DEBUG_STRING = "Debug Folder";
            public static readonly string MSG_SPLITSCREEN_OPEN_DEBUG_HOVER_TOKEN = "XSPLITSCREEN_DEBUG_FILE_HOVER";
            public static readonly string MSG_SPLITSCREEN_OPEN_DEBUG_HOVER_STRING = "Open the folder containing the XSplitScreen log";
            public static readonly string MSG_SPLITSCREEN_CONFIG_HEADER_TOKEN = "XSPLITSCREEN_CONFIG_HEADER";
            public static readonly string MSG_SPLITSCREEN_CONFIG_HEADER_STRING = "Assignment";
            public static readonly string MSG_SPLITSCREEN_ENABLE_HOVER_TOKEN = "XSPLITSCREEN_ENABLE_HOVER";
            public static readonly string MSG_SPLITSCREEN_ENABLE_HOVER_STRING = "Turn on XSplitScreen";
            public static readonly string MSG_SPLITSCREEN_DISABLE_HOVER_TOKEN = "XSPLITSCREEN_DISABLE_HOVER";
            public static readonly string MSG_SPLITSCREEN_DISABLE_HOVER_STRING = "Turn off XSplitScreen";
            public static readonly string MSG_SPLITSCREEN_ENABLE_TOKEN = "XSPLITSCREEN_ENABLE";
            public static readonly string MSG_SPLITSCREEN_ENABLE_STRING = "Enable";
            public static readonly string MSG_SPLITSCREEN_DISABLE_TOKEN = "XSPLITSCREEN_DISABLE";
            public static readonly string MSG_SPLITSCREEN_DISABLE_STRING = "Disable";
            public static readonly string MSG_TITLE_BUTTON_TOKEN = "TITLE_XSPLITSCREEN";
            public static readonly string MSG_TITLE_BUTTON_STRING = "Splitscreen";
            public static readonly string MSG_HOVER_TOKEN = "TITLE_XSPLITSCREEN_DESC";
            public static readonly string MSG_HOVER_STRING = "Modify splitscreen settings.";
            public static readonly string MSG_UNSET_STRING = "Unset";
            public static readonly string MSG_UNSET_TOKEN = "XSPLITSCREEN_UNSET";

            public static readonly string MSG_TAG_PLUGIN = "[XSS] {0}";
            public static readonly string MSG_ERROR_SINGLE_LOCAL_PLAYER = "There is only 1 local player signed in.";
            public static readonly string MSG_ERROR_GENERIC = "[{0}] Unable to continue.";
            public static readonly string MSG_ERROR_SIGN_IN_FIRST = "Please sign in to a user profile before configuring XSplitScreen.";
            public static readonly string MSG_ERROR_PLAYER_COUNT = "Unable to set player count to requested number. Disabling splitscreen.";
            public static readonly string MSG_ERROR_NETWORK_ACTIVE = "XSplitScreen must be configured in the main menu.";
            public static readonly string MSG_ERROR_NO_PROFILES = "No profiles detected. Please create a profile before configuring XSplitScreen.";
            public static readonly string MSG_ERROR_INVALID_ARGS = "Invalid arguments. Please type help to see a list of console commands and how to use them.";
            public static readonly string MSG_ERROR_INVALID_PLAYER_RANGE = "A given player index is invalid. Make sure all players are logged in with 'xsplitset'.";
            public static readonly string MSG_INFO_KEYBOARD_ONLY = "Not enough controllers. Only keyboard mode is available.";
            public static readonly string MSG_INFO_PLAYER_COUNT_CLAMPED = "Requested invalid number of players ({0}). Trying '{1}'.";
            public static readonly string MSG_INFO_ENTER = "XSplitScreen loaded. Type help to see how to use the 'xsplitset' command.";
            public static readonly string MSG_INFO_EXIT = "Attempting to exit: your controllers may or may not work until you restart the game.";
            public static readonly string MSG_INFO_KEYBOARD_STATUS = "Keyboard mode requested";
            public static readonly string MSG_INFO_POTENTIAL_PLAYERS = "{0} potential users detected";
        }
        #endregion
    }
}