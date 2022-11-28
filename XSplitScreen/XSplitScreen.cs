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
using UnityEngine.EventSystems;
using Rewired.Integration.UnityUI;
using Rewired.UI;

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
        public static Input input { get; private set; }
        public static GameObject buttonTemplate { get; private set; }

        private static readonly bool developerMode = true;

        private static Coroutine WaitForMenuRoutine;
        private static Coroutine WaitForRewiredRoutine;

        private RectTransform titleButton;
        private RectTransform menuContainer;

        private bool readyToInitializePlugin;
        private bool readyToCreateUI;

        private int createUIFrameBuffer = 5;
        #endregion

        #region Unity Methods
        public void Awake()
        {
            if (instance)
                Destroy(this);

            WaitForRewiredRoutine = StartCoroutine(WaitForRewired());
            //Initialize(); // Moved to LateUpdate() 11/27/22 - Scene switch bug
        }
        public void OnDestroy()
        {
            CleanupReferences();

            int c;

            SetEnabled(false, out c);
        }
        public void LateUpdate()
        {
            if (readyToCreateUI)
            {
                if (MainMenuController.instance.currentMenuScreen.Equals(MainMenuController.instance.titleMenuScreen))
                {
                    createUIFrameBuffer--;

                    if (createUIFrameBuffer < 1)
                    {
                        if (CreateUI())
                        {
                            readyToCreateUI = false;
                            createUIFrameBuffer = 5;
                        }
                    }
                }
            }

            if (readyToInitializePlugin)
                Initialize();
        }
        #endregion

        #region Initialization & Exit
        private void Initialize()
        {
            instance = this;
            readyToInitializePlugin = false;

            Log.Init(Logger);
            CommandHelper.AddToConsoleWhenReady();

            InitializeLanguage();
            InitializeReferences();

            TogglePersistentListeners(true);
            //ToggleUIListeners(true); // Disabled 11/27/22 - Scene switch bug

            if (developerMode)
            {
                if (WaitForMenuRoutine != null)
                    StopCoroutine(WaitForMenuRoutine);

                WaitForMenuRoutine = StartCoroutine(WaitForMenu());
            }

            // crashing when reconnect controller on scene switch
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
                catch (Exception e)
                {
                    Log.LogError(e);
                }
            }

            if (configuration is null)
                configuration = new Configuration(Config);

            if (input is null)
                input = new Input();
        }
        private void CleanupReferences()
        {
            configuration?.Destroy();
            configuration = null;

            assets?.Unload(true);
            assets = null;

            instance = null;

            TogglePersistentListeners(false);
            //ToggleUIListeners(false); // Disabled 11/27/22 - Scene switch bug

            if (titleButton != null)
                Destroy(titleButton?.gameObject);

            if (menuContainer != null)
                Destroy(menuContainer?.gameObject);

            if (buttonTemplate != null)
                Destroy(buttonTemplate);
        }
        #endregion

        #region Hooks & Event Handlers
        private void ToggleUIListeners(bool status)
        {
            if (MainMenuController.instance?.titleMenuScreen is null)
                return;

            if (status)
            {
                MainMenuController.instance.titleMenuScreen.onEnter.AddListener(ScreenOnEnter);
                MainMenuController.instance.titleMenuScreen.onExit.AddListener(ScreenOnExit);
            }
            else
            {
                MainMenuController.instance.titleMenuScreen.onEnter.RemoveListener(ScreenOnEnter);
                MainMenuController.instance.titleMenuScreen.onExit.RemoveListener(ScreenOnExit);
            }
        }

        #region Persistent
        private void TogglePersistentListeners(bool status)
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
        private void ActiveSceneChanged(Scene previous, Scene current)
        {
            Log.LogDebug($"XSplitScreen.ActiveSceneChanged");

            if (string.Compare(current.name, "title") == 0)
            {
                if (WaitForMenuRoutine != null)
                    StopCoroutine(WaitForMenuRoutine);

                WaitForMenuRoutine = StartCoroutine(WaitForMenu());
                Log.LogDebug($"XSplitScreen.ActiveSceneChanged: Started coroutine");
                //ToggleUIListeners(true); // as below
                //ScreenOnEnter(); // Disabled 11/27/22 - Scene switch bug
            }
            else
            {
                //ToggleUIListeners(false); // as below
                //ScreenOnExit(); // Disabled 11/27/22 - Scene switch bug
            }
        }
        private void ScreenOnEnter()
        {
            Log.LogDebug($"XSplitScreen.ScreenOnEnter");

            //if (WaitForMenu != null) // Disabled 11/27/22 - Scene switch bug
            //    StopCoroutine(WaitForMenuRoutine); // Disabled 11/27/22 - Scene switch bug

            //WaitForMenuRoutine = StartCoroutine(WaitForMenu()); // Disabled 11/27/22 - Scene switch bug
        }
        private void ScreenOnExit()
        {
            Log.LogDebug("ScreenOnExit");
        }
        #endregion

        #region Splitscreen
        private void ToggleSplitScreenHooks(bool status)
        {
            input.UpdateCurrentEventSystem(LocalUserManager.GetFirstLocalUser().eventSystem);

            if (status)
            {
                On.RoR2.UI.CursorOpener.Awake += CursorOpener_Awake;
                On.RoR2.UI.MPButton.Update += MPButton_Update;
                On.RoR2.UI.MPButton.OnPointerClick += MPButton_OnPointerClick;
                On.RoR2.UI.MPButton.InputModuleIsAllowed += MPButton_InputModuleIsAllowed;
                On.RoR2.UI.MPInputModule.GetMousePointerEventData += MPInputModule_GetMousePointerEventData;
                On.RoR2.UI.SurvivorIconController.Update += SurvivorIconController_Update;
                On.RoR2.UI.MPEventSystem.ValidateCurrentSelectedGameobject += MPEventSystem_ValidateCurrentSelectedGameobject;
                On.RoR2.CharacterSelectBarController.PickIcon += CharacterSelectBarController_PickIcon;
            }
            else
            {
                On.RoR2.UI.CursorOpener.Awake -= CursorOpener_Awake;
                On.RoR2.UI.MPButton.Update -= MPButton_Update;
                On.RoR2.UI.MPButton.OnPointerClick -= MPButton_OnPointerClick;
                On.RoR2.UI.MPButton.InputModuleIsAllowed -= MPButton_InputModuleIsAllowed;
                On.RoR2.UI.MPInputModule.GetMousePointerEventData -= MPInputModule_GetMousePointerEventData;
                On.RoR2.UI.SurvivorIconController.Update -= SurvivorIconController_Update;
                On.RoR2.UI.MPEventSystem.ValidateCurrentSelectedGameobject -= MPEventSystem_ValidateCurrentSelectedGameobject;
                On.RoR2.CharacterSelectBarController.PickIcon -= CharacterSelectBarController_PickIcon;
            }
        }

        #region UI Hooks
        private void CursorOpener_Awake(On.RoR2.UI.CursorOpener.orig_Awake orig, CursorOpener self)
        {
            orig(self);
            self._forceCursorForGamepad = configuration is null ? false : configuration.enabled;
        }
        private void MPButton_Update(On.RoR2.UI.MPButton.orig_Update orig, RoR2.UI.MPButton self)
        {
            if (!self.eventSystem || self.eventSystem.player == null)
                return;

            for (int e = 1; e < MPEventSystem.readOnlyInstancesList.Count; e++)
            {
                MPEventSystem eventSystem = MPEventSystem.readOnlyInstancesList[e];

                if (!eventSystem)
                    continue;

                if (eventSystem.currentSelectedGameObject == self.gameObject &&
                    (eventSystem.player.GetButtonDown(4) || eventSystem.player.GetButtonDown(14)))
                {
                    input.UpdateCurrentEventSystem(eventSystem);
                    self.InvokeClick();
                }
            }
        }
        private void MPButton_OnPointerClick(On.RoR2.UI.MPButton.orig_OnPointerClick orig, RoR2.UI.MPButton self, PointerEventData eventData)
        {
            input.UpdateCurrentEventSystem(eventData.currentInputModule.eventSystem);
            orig(self, eventData);
        }
        private bool MPButton_InputModuleIsAllowed(On.RoR2.UI.MPButton.orig_InputModuleIsAllowed orig, RoR2.UI.MPButton self, BaseInputModule inputModule)
        {
            return true;

            if (self.allowAllEventSystems)
                return true;

            if (self.eventSystem)
            {
                if (self.eventSystem == input.currentEventSystem)
                    return true;
            }

            return false;
        }
        private object MPInputModule_GetMousePointerEventData(On.RoR2.UI.MPInputModule.orig_GetMousePointerEventData orig, RoR2.UI.MPInputModule self, int playerId, int mouseIndex)
        {
            IMouseInputSource mouseInputSource = self.GetMouseInputSource(playerId, mouseIndex);

            if (mouseInputSource == null)
                return null;

            PlayerPointerEventData data1;

            // If pointer event data was created? or already exists?
            int num = self.GetPointerData(playerId, mouseIndex, -1, out data1, true, PointerEventType.Mouse) ? 1 : 0;

            data1.Reset();

            // if pointer data exists, set mouse position to current position? to calculate delta later I think
            if (num != 0)
                data1.position = self.input.mousePosition;

            Vector2 mousePosition = self.input.mousePosition;

            if (mouseInputSource.locked || !mouseInputSource.enabled)
            {
                data1.position = new Vector2(-1f, -1f);
                data1.delta = Vector2.zero;
            }
            else
            {
                data1.delta = mousePosition - data1.position;
                data1.position = mousePosition;
            }

            data1.scrollDelta = mouseInputSource.wheelDelta;
            data1.button = PointerEventData.InputButton.Left;

            // Raycast all objects from current position and select the first one
            self.eventSystem.RaycastAll(data1, self.m_RaycastResultCache);
            RaycastResult firstRaycast = BaseInputModule.FindFirstRaycast(self.m_RaycastResultCache);

            GameObject focusObject = null;
            
            int priority = 0;

            bool logOutput = false;

            foreach (RaycastResult raycast in self.m_RaycastResultCache)
            {
                if (self.useCursor)
                {
                    if (raycast.gameObject != null)
                    {
                        TMPro.TMP_InputField input = raycast.gameObject.GetComponent<TMPro.TMP_InputField>();
                        MPButton mpButton = raycast.gameObject.GetComponent<RoR2.UI.MPButton>();
                        HGButton hgButton = raycast.gameObject.GetComponent<HGButton>();

                        if (input != null && priority < 3)
                        {
                            if(logOutput)
                                Debug.Log($"MPInputModule_GetMousePointerEventData: '{playerId}' selecting '{raycast.gameObject}' p3");

                            focusObject = raycast.gameObject;
                            priority = 3;
                        }
                        if (hgButton != null)
                        {
                            if (priority < 2)
                            {
                                if (logOutput)
                                    Debug.Log($"MPInputModule_GetMousePointerEventData: '{playerId}' selecting '{raycast.gameObject}' p3");

                                focusObject = raycast.gameObject;
                                priority = 2;
                            }
                        }
                        if (mpButton != null)
                        {
                            if (priority < 1)
                            {
                                if (logOutput)
                                    Debug.Log($"MPInputModule_GetMousePointerEventData: '{playerId}' selecting '{raycast.gameObject}' p3");

                                focusObject = raycast.gameObject;
                                priority = 1;
                            }
                        }
                    }
                }
            }
            if (self.eventSystem.currentSelectedGameObject != null && focusObject == null)
                if (self.eventSystem.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>() != null)
                    focusObject = self.eventSystem.currentSelectedGameObject;

            
            self.eventSystem.SetSelectedGameObject(focusObject);

            data1.pointerCurrentRaycast = firstRaycast;
            self.UpdateHover(self.m_RaycastResultCache);
            self.m_RaycastResultCache.Clear();

            PlayerPointerEventData data2;
            self.GetPointerData(playerId, mouseIndex, -2, out data2, true, PointerEventType.Mouse);
            self.CopyFromTo(data1, data2);

            data2.button = PointerEventData.InputButton.Right;

            PlayerPointerEventData data3;
            self.GetPointerData(playerId, mouseIndex, -3, out data3, true, PointerEventType.Mouse);
            self.CopyFromTo(data1, data3);
            data3.button = PointerEventData.InputButton.Middle;

            for (int index = 3; index < mouseInputSource.buttonCount; index++)
            {
                PlayerPointerEventData data4;
                self.GetPointerData(playerId, mouseIndex, index - 2147483520, out data4, true, PointerEventType.Mouse);
                self.CopyFromTo(data1, data4);
                data4.button = ~PointerEventData.InputButton.Left;
            }

            self.m_MouseState.SetButtonState(0, self.StateForMouseButton(playerId, mouseIndex, 0), data1);
            self.m_MouseState.SetButtonState(1, self.StateForMouseButton(playerId, mouseIndex, 1), data2);
            self.m_MouseState.SetButtonState(2, self.StateForMouseButton(playerId, mouseIndex, 2), data3);

            for (int index = 3; index < mouseInputSource.buttonCount; index++)
            {
                PlayerPointerEventData data4;
                self.GetPointerData(playerId, mouseIndex, index - 2147483520, out data4, false, PointerEventType.Mouse);
                self.m_MouseState.SetButtonState(index, self.StateForMouseButton(playerId, mouseIndex, index), data4);
            }

            return self.m_MouseState;
        }
        private void SurvivorIconController_Update(On.RoR2.UI.SurvivorIconController.orig_Update orig, SurvivorIconController self)
        {
            // Fix debug spam
            if (EventSystem.current == null)
                return;

            MPEventSystem system = EventSystem.current as MPEventSystem;

            if (system == null)
                return;

            orig(self);
        }
        private void MPEventSystem_ValidateCurrentSelectedGameobject(On.RoR2.UI.MPEventSystem.orig_ValidateCurrentSelectedGameobject orig, RoR2.UI.MPEventSystem self)
        {
            if (!self.currentSelectedGameObject)
                return;

            MPButton component = self.currentSelectedGameObject.GetComponent<MPButton>();

            if (!component || component.CanBeSelected())
                return;

            self.SetSelectedGameObject(null);
        }
        private void CharacterSelectBarController_PickIcon(On.RoR2.CharacterSelectBarController.orig_PickIcon orig, CharacterSelectBarController self, RoR2.UI.SurvivorIconController newPickedIcon)
        {
            Log.LogDebug($"XSplitScreen.CharacterSelectBarController_PickIcon");

            if (self.pickedIcon == newPickedIcon)
                return;

            self.pickedIcon = newPickedIcon;

            CharacterSelectBarController.SurvivorPickInfoUnityEvent onSurvivorPicked = self.onSurvivorPicked;
            if (onSurvivorPicked == null)
                return;

            onSurvivorPicked.Invoke(new CharacterSelectBarController.SurvivorPickInfo()
            {
                localUser = input.currentEventSystem.localUser,
                pickedSurvivor = newPickedIcon.survivorDef
            });
        }
        private void ViewablesCatalog_AddNodeToRoot(On.RoR2.ViewablesCatalog.orig_AddNodeToRoot orig, ViewablesCatalog.Node node)
        {
            node.SetParent(ViewablesCatalog.rootNode);
            foreach (ViewablesCatalog.Node descendant in node.Descendants())
            {
                if (!ViewablesCatalog.fullNameToNodeMap.ContainsKey(descendant.fullName))
                    ViewablesCatalog.fullNameToNodeMap.Add(descendant.fullName, descendant);
            }
        }
        #endregion

        #endregion

        #endregion

        #region UI
        private bool CreateUI()
        {
            Log.LogInfo($"XSplitScreen.CreateUI: Attempting to create UI");

            if (CreateMainMenuButton())
            {
                CreateMenu();
                return true;
            }

            return false;
        }
        private bool CreateMainMenuButton()
        {
            if (titleButton != null)
                return false;

            GameObject template = GameObject.Find("GenericMenuButton (Singleplayer)");

            if (template is null)
                return false;

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

            return true;
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
        public void UpdateCursorStatus(bool status)
        {
            CursorOpener[] openers = FindObjectsOfType<CursorOpener>();

            foreach (CursorOpener opener in openers)
            {
                Log.LogDebug($"XSplitScreen.UpdateCursorStatus: {opener.name} = '{status}'");
                opener.forceCursorForGamePad = status;
            }

            if (!status)
            {
                foreach (MPEventSystem instance in MPEventSystem.instancesList)
                {
                    instance.SetSelectedGameObject(null);
                }
            }
        }
        private bool SetEnabled(bool status, out int localPlayerCount)
        {
            if (configuration != null)
                localPlayerCount = configuration.localPlayerCount;
            else
                localPlayerCount = 0;

            UserProfile[] profiles = new UserProfile[PlatformSystems.saveSystem.loadedUserProfiles.Values.Count];
            PlatformSystems.saveSystem.loadedUserProfiles.Values.CopyTo(profiles, 0);

            if (profiles.Length == 0)
                return false;

            if (!LogInUsers(profiles, status, out localPlayerCount))
                return false;

            AssignControllers(status);

            ToggleSplitScreenHooks(status);
            
            // Hook everything 
            // MPButton: maybe adding a component will fix it? will XButton function as a child?
            return true;
        }
        private bool LogInUsers(UserProfile[] profiles, bool status, out int localPlayerCount)
        {
            List<LocalUserManager.LocalUserInitializationInfo> localUsers = new List<LocalUserManager.LocalUserInitializationInfo>();

            localPlayerCount = 1;

            if (!status)
            {
                localUsers.Add(new LocalUserManager.LocalUserInitializationInfo()
                {
                    player = ReInput.players.GetPlayer(0),
                    profile = PlatformSystems.saveSystem.loadedUserProfiles.First().Value,
                });
            }
            else
            {
                foreach (Assignment assignment in configuration.assignments)
                {
                    if (assignment.isAssigned)
                    {
                        localUsers.Add(new LocalUserManager.LocalUserInitializationInfo()
                        {
                            player = ReInput.players.GetPlayer(assignment.playerId + 1),
                            profile = profiles[assignment.profileId],
                        });
                    }
                }
            }

            try
            {
                // Silence log spam
                On.RoR2.ViewablesCatalog.AddNodeToRoot += ViewablesCatalog_AddNodeToRoot;

                LocalUserManager.ClearUsers();
                LocalUserManager.SetLocalUsers(localUsers.ToArray());

                On.RoR2.ViewablesCatalog.AddNodeToRoot -= ViewablesCatalog_AddNodeToRoot;

                localPlayerCount = localUsers.Count;

                return true;
            }
            catch (Exception e)
            {
                Log.LogWarning(e);

                if(localUsers.Count > 1)
                    LogInUsers(null, false, out localPlayerCount);

                return false;
            }
        }
        private void AssignControllers(bool status)
        {
            if (!status || configuration is null)
            {
                Log.LogDebug($"XSplitScreen.AssignControllers: Auto Assigned");
                ReInput.controllers.AutoAssignJoysticks();
                return;
            }

            foreach (Assignment assignment in configuration.assignments)
            {
                if (!assignment.isAssigned)
                    continue;

                int playerIndex = assignment.playerId;
                LocalUserManager.readOnlyLocalUsersList[playerIndex].inputPlayer.controllers.ClearAllControllers();

                if (assignment.controller.type == ControllerType.Keyboard)
                {
                    foreach (Controller controller in ReInput.controllers.Controllers)
                    {
                        if (controller.type == ControllerType.Mouse)
                        {
                            Log.LogDebug($"Assigning mouse");
                            LocalUserManager.readOnlyLocalUsersList[playerIndex].inputPlayer.controllers.AddController(controller, false);
                            break;
                        }
                    }
                }

                LocalUserManager.readOnlyLocalUsersList[playerIndex].inputPlayer.controllers.AddController(assignment.controller, false);
                Log.LogDebug($"XSplitScreen.AssignControllers: Assigning '{assignment.controller.name}' to playerIndex '{playerIndex}'");
            }
        }
        #endregion

        #region Coroutines
        IEnumerator WaitForRewired()
        {
            while (!ReInput.initialized)
                yield return null;

            readyToInitializePlugin = true;

            yield return null;
        }
        IEnumerator WaitForMenu()
        {
            while (MainMenuController.instance == null)
                yield return null;

            GameObject singleplayerButton = null;

            while(singleplayerButton is null)
            {
                singleplayerButton = GameObject.Find("GenericMenuButton (Singleplayer)");
                yield return null;
            }

            buttonTemplate = Instantiate(singleplayerButton);
            buttonTemplate.SetActive(false);

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
            public UnityEvent onSplitScreenEnabled { get; private set; }
            public UnityEvent onSplitScreenDisabled { get; private set; }

            public Action<ControllerStatusChangedEventArgs> onControllerConnected;
            public Action<ControllerStatusChangedEventArgs> onControllerDisconnected;

            public List<Assignment> assignments { get; private set; } // TODO private
            public List<Controller> controllers { get; private set; }

            public int2 graphDimensions { get; private set; }

            public bool enabled { get; private set; }

            public int localPlayerCount { get; private set; }
            public readonly int maxLocalPlayers = 4;

            private bool developerMode = false;

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

                if(developerMode)
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
                onSplitScreenEnabled = new UnityEvent();
                onSplitScreenDisabled = new UnityEvent();
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
                    onSplitScreenEnabled.AddListener(OnSplitScreenEnabled);
                    onSplitScreenDisabled.AddListener(OnSplitScreenDisabled);
                }
                else
                {
                    ReInput.ControllerConnectedEvent -= OnControllerConnected;
                    ReInput.ControllerDisconnectedEvent -= OnControllerDisconnected;
                    onSplitScreenEnabled.RemoveListener(OnSplitScreenEnabled);
                    onSplitScreenDisabled.RemoveListener(OnSplitScreenDisabled);
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
            private bool VerifyConfiguration()
            {
                if (PlatformSystems.saveSystem.loadedUserProfiles.Values.Count == 0)
                    return false;

                foreach (Assignment assignment in configuration.assignments)
                {
                    if (assignment.isAssigned)
                    {
                        if (assignment.profileId == -1 || assignment.profileId >= PlatformSystems.saveSystem.loadedUserProfiles.Values.Count
                            || assignment.controller == null || assignment.displayId == -1 ||
                            assignment.displayId >= Display.displays.Length)
                            return false;
                    }
                }

                return true;
            }
            #endregion

            #region Splitscreen
            public bool SetEnabled(bool status)
            {
                if (status)
                {
                    if (VerifyConfiguration())
                    {
                        int playerCount;

                        if (instance.SetEnabled(status, out playerCount))
                        {
                            localPlayerCount = playerCount;
                            enabled = localPlayerCount > 1 ? true : false;

                            if (enabled)
                                onSplitScreenEnabled.Invoke();
                            else
                                onSplitScreenDisabled.Invoke();

                            return true;
                        }
                    }
                }
                else
                {
                    int c;

                    instance.SetEnabled(false, out c);
                    localPlayerCount = 1;
                    enabled = false;
                    onSplitScreenDisabled.Invoke();
                }

                return false;
            }
            private void OnSplitScreenEnabled()
            {
                instance.UpdateCursorStatus(true);
            }
            private void OnSplitScreenDisabled()
            {
                instance.UpdateCursorStatus(false);
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
        public class Input
        {
            public MPEventSystem currentEventSystem { get; private set; }
            public bool clickedThisFrame;

            public void UpdateCurrentEventSystem(EventSystem eventSystem)
            {
                Log.LogDebug($"XSplitScreen.Input.UpdateCurrentEventSystem: EventSystem is null: '{eventSystem == null}'");

                if (eventSystem != null)
                    UpdateCurrentEventSystem(eventSystem as MPEventSystem);
                else
                    currentEventSystem = null;
            }
            public void UpdateCurrentEventSystem(MPEventSystem eventSystem)
            {
                Log.LogDebug($"XSplitScreen.Input.UpdateCurrentEventSystem: MPEventSystem is null: '{eventSystem == null}'");
                if (currentEventSystem != null)
                    currentEventSystem.SetSelectedGameObject(null);

                currentEventSystem = eventSystem;
            }
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