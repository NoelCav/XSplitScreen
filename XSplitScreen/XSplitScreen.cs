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

            Destroy(titleButton?.gameObject);
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
            // OnConfiguration event
            // Create the menu during ToggleUI
            public AssignmentEvent onAssignmentUpdate { get; private set; }
            public List<Assignment> assignments { get; private set; } // TODO private

            public int2 graphDimensions { get; private set; }

            public bool enabled = false;

            private List<Preference> preferences;

            private ConfigFile config;

            private ConfigEntry<string> preferencesConfig;
            private ConfigEntry<bool> enabledConfig;
            #endregion

            #region Initialization & Exit
            public Configuration(ConfigFile configFile)
            {
                InitializeReferences();
                LoadConfigFile(configFile);
                InitializeAssignments();
            }
            private void InitializeReferences()
            {
                assignments = new List<Assignment>();
                preferences = new List<Preference>();

                onAssignmentUpdate = new AssignmentEvent();
                graphDimensions = new int2(3, 3);

                ToggleListeners(true);
            }
            private void LoadConfigFile(ConfigFile configFile)
            {
                this.config = configFile;

                try
                {
                    preferencesConfig = configFile.Bind<string>("General", "Assignments", "", "Persistent assignments");
                    enabledConfig = configFile.Bind<bool>("General", "Enabled", false, "Should the program start on launch");
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

                        for(int e = 0; e < wrapper.deviceId.Length; e++)
                        {
                            preferences.Add(new Preference()
                            {
                                deviceId = wrapper.deviceId[e],
                                displayId = wrapper.displayId[e],
                                profile = wrapper.profile[e],
                                position = new int2(wrapper.positionX[e], wrapper.positionY[e]),
                                context = new int2(wrapper.contextX[e], wrapper.contextY[e]),
                                isKeyboard = wrapper.isKeyboard[e],
                            });
                        }

                        Log.LogInfo($"Loaded {wrapper.deviceId.Length} preferences");
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
                foreach (Controller controller in ReInput.controllers.Controllers)
                {
                    LoadAssignment(controller);
                }
            }
            public void Destroy()
            {
                ToggleListeners(false);

                Save();
            }
            #endregion

            #region Events
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
            public void OnControllerConnected(Rewired.ControllerStatusChangedEventArgs args)
            {
                if (args.controller.type == ControllerType.Mouse)
                    return;

                LoadAssignment(args.controller);
            }
            public void OnControllerDisconnected(Rewired.ControllerStatusChangedEventArgs args)
            {
                int index = -1;

                for(int e = 0; e < assignments.Count; e++)
                {
                    if(assignments[e].deviceId == args.controllerId && assignments[e].isKeyboard == (args.controllerType == ControllerType.Keyboard))
                    {
                        index = e;
                        break;
                    }
                }

                if (index > -1)
                {
                    Assignment oldAssignment = assignments[index];

                    assignments.RemoveAt(index);

                    onAssignmentUpdate.Invoke(null, oldAssignment);
                }
            }
            #endregion

            #region Assignments
            public void PushChanges(List<Assignment> changes)
            {
                Log.LogDebug($" - Configuration.OnPushChanges -");
                
                int counter = 0;

                foreach (Assignment change in changes)
                {
                    for(int e = 0; e < assignments.Count; e++)
                    {
                        if(assignments[e].Matches(change.controller))
                        {
                            assignments[e] = change;
                            counter++;
                            Log.LogDebug($"[{counter}] {(change.position.IsPositive() ? "+" : "-")} {change.controller.name} to {change.position}");
                            onAssignmentUpdate.Invoke(change.controller, change);
                        }
                    }
                }

                return;

                for (int e = 0; e < assignments.Count; e++)
                {
                    foreach (Assignment change in changes)
                    {
                        if (change.controller is null)
                            continue;

                        if(assignments[e].Matches(change.controller))
                        {
                            counter++;
                            Log.LogDebug($"[{counter}] {(change.position.IsPositive() ? "+" : "-")} {change.controller.name} to {change.position}");
                            //Log.LogDebug($"Loading change: {change}");
                            // need to handle unloaded preferences somewhere
                            assignments[e] = change;
                            onAssignmentUpdate.Invoke(change.controller, change);
                        }
                    }
                }
            }
            public void Save()
            {
                try
                {
                    UpdatePreferences();
                    Wrapper wrapper = new Wrapper(preferences);
                    enabledConfig.Value = enabled;
                    preferencesConfig.Value = JsonUtility.ToJson(wrapper);
                    config.Save();
                }
                catch(Exception e)
                {
                    Log.LogError(e);
                }

            }
            private void LoadAssignment(Controller controller)
            {
                if (controller.type == ControllerType.Mouse)
                    return;

                Assignment assignment = GetPreference(controller);

                assignments.Add(assignment);

                onAssignmentUpdate.Invoke(controller, assignment);
            }

            #region Helpers
            private Assignment GetPreference(Controller controller)
            {
                Assignment newAssignment = new Assignment(controller);

                bool foundPreference = false;

                foreach(Preference preference in preferences)
                {
                    if (preference.Matches(controller))
                    {
                        foundPreference = true;
                        newAssignment.Load(preference);
                    }
                }

                if(!foundPreference)
                    CreatePreference(controller);

                return newAssignment;
            }
            private void CreatePreference(Controller controller)
            {
                Preference newPreference = new Preference()
                {
                    deviceId = controller.id,
                    isKeyboard = controller.type == ControllerType.Keyboard,
                    position = int2.negative,
                    context = int2.negative,
                    displayId = -1,
                    profile = "",
                };

                if(newPreference.isKeyboard)
                {
                    newPreference.position = int2.one;
                    newPreference.context = new int2(1, 0);
                    newPreference.profile = LocalUserManager.GetFirstLocalUser().userProfile.fileName;
                    newPreference.displayId = 0;
                }

                preferences.Add(newPreference);

                Log.LogMessage($"Created new preference: id = '{controller.id}' ({controller.type})");
            }
            private void UpdatePreferences()
            {
                foreach(Assignment assignment in assignments)
                {
                    for(int e = 0; e < preferences.Count; e++)
                    {
                        if(assignment.Matches(preferences[e]))
                        {
                            preferences[e].Update(assignment);
                            break;
                        }
                    }
                }
            }
            #endregion
            #endregion

            #region Definitions
            [System.Serializable]
            private class Wrapper
            {
                public bool[] isKeyboard;

                public string[] profile;

                public int[] deviceId;
                public int[] displayId;
                public int[] positionX;
                public int[] positionY;
                public int[] contextX;
                public int[] contextY;

                public Wrapper(List<Preference> preferences)
                {
                    isKeyboard = new bool[preferences.Count];
                    profile = new string[preferences.Count];
                    deviceId = new int[preferences.Count];
                    displayId = new int[preferences.Count];
                    positionX = new int[preferences.Count];
                    positionY = new int[preferences.Count];
                    contextX = new int[preferences.Count];
                    contextY = new int[preferences.Count];

                    for (int e = 0; e < preferences.Count; e++)
                    {
                        isKeyboard[e] = preferences[e].isKeyboard;
                        profile[e] = preferences[e].profile;
                        deviceId[e] = preferences[e].deviceId;
                        displayId[e] = preferences[e].displayId;
                        positionX[e] = preferences[e].position.x;
                        positionY[e] = preferences[e].position.y;
                        contextX[e] = preferences[e].context.x;
                        contextY[e] = preferences[e].context.y;
                    }
                }
            }
            #endregion
        }
        public struct Preference
        {
            public int2 position;
            public int2 context;

            public int deviceId;
            public int displayId;

            public string profile;

            public bool isKeyboard;

            public bool Matches(Controller controller)
            {
                return controller.id == deviceId && isKeyboard == (controller.type == ControllerType.Keyboard);
            }
            public void Update(Assignment assignment)
            {
                position = assignment.position;
                context = assignment.context;
                deviceId = assignment.deviceId;
                displayId = assignment.displayId;
                profile = assignment.profile;
                isKeyboard = assignment.isKeyboard;
            }
        }
        public struct Assignment
        {
            public Controller controller;

            public int2 position; // The assigned screen
            public int2 context; // Which side of the screen the player intended the assignment to be on

            public string profile;

            public int displayId; // The desired monitor

            public Assignment(Controller controller = null)
            {
                this.controller = controller;

                position = int2.negative;
                context = int2.negative;
                displayId = -1;
                profile = "";
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
                    if (controller == null)
                        return false;

                    return position.IsPositive();
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
                string newFormat = "Assignment(deviceId = '{0}', isKeyboard = '{1}', displayId = '{2}', position = '{3}', context = '{4}, has controller = '{5}')";

                return string.Format(newFormat, deviceId, isKeyboard, displayId, position, context, controller != null);
            }
            public bool Matches(Preference preference)
            {
                return preference.deviceId == deviceId && preference.isKeyboard == isKeyboard;
            }
            public bool Matches(Assignment assignment)
            {
                return assignment.deviceId == deviceId && assignment.isKeyboard == isKeyboard;
            }
            public bool Matches(Controller controller)
            {
                if (this.controller == null)
                    return false;

                return this.controller.id == controller.id && this.controller.type == controller.type;
            }
            public void Load(Preference preference)
            {
                position = preference.position;
                context = preference.context;
                profile = preference.profile;
                displayId = preference.displayId;
            }
            public void Load(Assignment assignment)
            {
                this.controller = assignment.controller;
                profile = assignment.profile;
                context = assignment.context;
            }
            public void ClearAssignmentData()
            {
                this.controller = null;
                profile = "";
                context = int2.negative;
            }
            public void ClearScreenData()
            {
                position = int2.negative;
                context = int2.negative;
            }
            public void Initialize()
            {
                controller = null;
                ClearScreenData();
                displayId = -1;
                profile = "";
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
            public static readonly string MSG_TITLE_BUTTON_STRING = "XSplitScreen";
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