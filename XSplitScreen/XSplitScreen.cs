using BepInEx;
using R2API.Utils;
using RoR2;
using System;
using UnityEngine;
using Rewired;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Zio;
using Rewired.UI;
using Rewired.Integration.UnityUI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using DoDad.UI;
using R2API;
using RoR2.UI;
using System.Text;
using MonoMod.Cil;
using System.Reflection;
using UnityEngine.UI;
using System.IO;
using DoDad.UI.Components;
using DoDad.Library;
using BepInEx.Configuration;
using System.Linq;
using DoDad.Library.Math;

/// <summary>
/// Influenced by iDeathHD's FixedSplitScreen mod
/// https://thunderstore.io/package/xiaoxiao921/FixedSplitscreen/
/// 
/// This should be major version '0' to indicate a development version but it's my first release ever
/// so I mistakenly used '1' instead
/// </summary>
namespace DoDad
{
    [BepInDependency(R2API.R2API.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(DoDad.Library.Library.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [R2APISubmoduleDependency(new string[] { "CommandHelper", "LanguageAPI" })]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)]
    public class XSplitScreen : BaseUnityPlugin
    {
        #region Variables
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "com.DoDad";
        public const string PluginName = "XSplitScreen";
        public const string PluginVersion = "1.2.2";

        private static readonly int MAX_LOCAL_PLAYERS = 4;
        private static readonly Rect[][] verticalLayouts = new Rect[5][]
        {
            new Rect[0],
            new Rect[1]{ new Rect(0.0f, 0.0f, 1f, 1f) },
            new Rect[2]
            {
              new Rect(0.0f, 0.0f, 0.5f, 1f),
              new Rect(0.5f, 0.0f, 0.5f, 1f)
            },
            new Rect[3]
            {
            new Rect(0.0f, 0.5f, 1f, 0.5f),
            new Rect(0.0f, 0.0f, 0.5f, 0.5f),
            new Rect(0.5f, 0.0f, 0.5f, 0.5f)
            },
            new Rect[4]
            {
            new Rect(0.0f, 0.5f, 0.5f, 0.5f),
            new Rect(0.5f, 0.5f, 0.5f, 0.5f),
            new Rect(0.0f, 0.0f, 0.5f, 0.5f),
            new Rect(0.5f, 0.0f, 0.5f, 0.5f)
            }
        };

        #region Messages
        private static readonly string MSG_DISCORD_LINK_HREF = "https://discord.gg/maHhJSv62G";
        private static readonly string MSG_DISCORD_LINK_STRING = "Discord";
        private static readonly string MSG_DISCORD_LINK_TOKEN = "XSPLITSCREEN_DISCORD";
        private static readonly string MSG_DISCORD_LINK_HOVER_STRING = "Join the Discord for support";
        private static readonly string MSG_DISCORD_LINK_HOVER_TOKEN = "XSPLITSCREEN_DISCORD_HOVER";
        private static readonly string LogFileName = "XSplitScreen-Log.txt";
        private static readonly string MSG_INFO_DEBUG_SAVED = "Log file updated: '{0}'";
        private static readonly string MSG_SPLITSCREEN_OPEN_DEBUG_TOKEN = "XSPLITSCREEN_DEBUG_FILE";
        private static readonly string MSG_SPLITSCREEN_OPEN_DEBUG_STRING = "Debug Folder";
        private static readonly string MSG_SPLITSCREEN_OPEN_DEBUG_HOVER_TOKEN = "XSPLITSCREEN_DEBUG_FILE_HOVER";
        private static readonly string MSG_SPLITSCREEN_OPEN_DEBUG_HOVER_STRING = "Open the folder containing the XSplitScreen log";
        private static readonly string MSG_SPLITSCREEN_CONFIG_HEADER_TOKEN = "XSPLITSCREEN_CONFIG_HEADER";
        private static readonly string MSG_SPLITSCREEN_CONFIG_HEADER_STRING = "Input Assignment";
        private static readonly string MSG_SPLITSCREEN_ENABLE_HOVER_TOKEN = "XSPLITSCREEN_ENABLE_HOVER";
        private static readonly string MSG_SPLITSCREEN_ENABLE_HOVER_STRING = "Turn on XSplitScreen";
        private static readonly string MSG_SPLITSCREEN_DISABLE_HOVER_TOKEN = "XSPLITSCREEN_DISABLE_HOVER";
        private static readonly string MSG_SPLITSCREEN_DISABLE_HOVER_STRING = "Turn off XSplitScreen";
        private static readonly string MSG_SPLITSCREEN_ENABLE_TOKEN = "XSPLITSCREEN_ENABLE";
        private static readonly string MSG_SPLITSCREEN_ENABLE_STRING = "Enable";
        private static readonly string MSG_SPLITSCREEN_DISABLE_TOKEN = "XSPLITSCREEN_DISABLE";
        private static readonly string MSG_SPLITSCREEN_DISABLE_STRING = "Disable";
        private static readonly string MSG_TITLE_BUTTON_TOKEN = "TITLE_XSPLITSCREEN";
        private static readonly string MSG_TITLE_BUTTON_STRING = "XSplitScreen";
        private static readonly string MSG_HOVER_TOKEN = "TITLE_XSPLITSCREEN_DESC";
        private static readonly string MSG_HOVER_STRING = "Modify splitscreen settings.";

        private static readonly string MSG_TAG_PLUGIN = "[XSS] {0}";
        private static readonly string MSG_ERROR_SINGLE_LOCAL_PLAYER = "There is only 1 local player signed in.";
        private static readonly string MSG_ERROR_GENERIC = "[{0}] Unable to continue.";
        private static readonly string MSG_ERROR_SIGN_IN_FIRST = "Please sign in to a user profile before configuring XSplitScreen.";
        private static readonly string MSG_ERROR_PLAYER_COUNT = "Unable to set player count to requested number. Disabling splitscreen.";
        private static readonly string MSG_ERROR_NETWORK_ACTIVE = "XSplitScreen must be configured in the main menu.";
        private static readonly string MSG_ERROR_NO_PROFILES = "No profiles detected. Please create a profile before configuring XSplitScreen.";
        private static readonly string MSG_ERROR_INVALID_ARGS = "Invalid arguments. Please type help to see a list of console commands and how to use them.";
        private static readonly string MSG_ERROR_INVALID_PLAYER_RANGE = "A given player index is invalid. Make sure all players are logged in with 'xsplitset'.";
        private static readonly string MSG_INFO_KEYBOARD_ONLY = "Not enough controllers. Only keyboard mode is available.";
        private static readonly string MSG_INFO_PLAYER_COUNT_CLAMPED = "Requested invalid number of players ({0}). Trying '{1}'.";
        private static readonly string MSG_INFO_ENTER = "XSplitScreen loaded. Type help to see how to use the 'xsplitset' command.";
        private static readonly string MSG_INFO_EXIT = "Attempting to exit: your controllers may or may not work until you restart the game.";
        private static readonly string MSG_INFO_KEYBOARD_STATUS = "Keyboard mode requested";
        private static readonly string MSG_INFO_POTENTIAL_PLAYERS = "{0} potential users detected";
        #endregion

        public static XSplitScreen instance;

        public static UnityEvent OnLocalPlayerCount = new UnityEvent();
        public static SplitScreenUpdated OnSplitScreenUpdated = new SplitScreenUpdated();
        //public static UnityAction DisableMenu;
        
        public static AssetBundle ResourceBundle;
        public static SplitScreenConfiguration Configuration { get { return instance._configuration; } }

        public static bool Enabled => LocalPlayerCount > 1;
        public static int MaxPlayers
        {
            get
            {
                int potentialPlayers = instance.EstimateJoysticks();

                if (potentialPlayers == 1)
                    if (instance._keyboardModeOnly)
                        potentialPlayers++;

                return Mathf.Min(potentialPlayers, MAX_LOCAL_PLAYERS);
            }
        }
        public static int LocalPlayerCount
        {
            protected set
            {
                _localPlayerCount = value;
                OnLocalPlayerCount.Invoke();
            }
            get
            {
                return _localPlayerCount;
            }
        }
        public static bool KeyboardMode => instance._keyboardModeOnly || (_localPlayerCount == 1) || (instance._keyboardOptional && instance._requestKeyboard);

        private static int _localPlayerCount = 1;
        private static bool OverwriteLogFile
        {
            get
            {
                bool value = instance._overwriteLogFile;

                if(value)
                {
                    instance._overwriteLogFile = false;
                }

                return value;
            }
        }

        private SplitScreenConfiguration _configuration;

        private Rect[][] _screenLayouts
        {
            get
            {
                if (_currentLayout == ScreenLayout.Horizontal)
                    return RunCameraManager.screenLayouts;

                return verticalLayouts;
            }
        }
        private ScreenLayout _currentLayout = ScreenLayout.Vertical;
        private HGButton _titleButton;
        private GameObject _controllerAssignmentWindow;

        public RoR2.UI.MPEventSystem _lastEventSystem;
        private Coroutine WaitFormenuLoad;

        private int _playerBeginIndex => KeyboardMode ? 1 : 1;//1 : 2;

        private int _retryCounter = 0;
        private bool _devMode = true;
        private bool _keyboardModeOnly = false;
        private bool _keyboardOptional = false;
        private bool _requestKeyboard = false;
        private bool _enteredMenu = false;
        private bool _mpUpdateOutputThisFrame = false;
        private bool _overwriteLogFile = true;
        // create wrapper class for RiskOfOptions
        // create menu there or in the main menu

        #endregion

        #region Unity Methods
        public void Awake()
        {
            if (instance)
                Destroy(this);

            instance = this;
            OnSplitScreenUpdated = new SplitScreenUpdated();
            LocalPlayerCount = 1; // Should this be called before or after the next listener is added?

            OnLocalPlayerCount.AddListener(UpdateCursorStatus); // Enables or disables the gamepad cursor when player count changes

            Log.Init(Logger);
            CommandHelper.AddToConsoleWhenReady();
            AddLanguageReferences();
            LoadBundle();
            InitializeConfiguration();

            Print(MSG_INFO_ENTER); // Remove?
        }
        private void OnDestroy()
        {
            SetLocalPlayerCount(1);
            //DevModeTriggers(false);
            TogglePersistentHooks(false);
            ResourceBundle.Unload(true);
            CleanupReferences();

            Print(MSG_INFO_EXIT);
        }
        public void Start()
        {
            TogglePersistentHooks(true);
            DevModeTriggers(_devMode);
        }
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                if (_currentLayout == ScreenLayout.Horizontal)
                    _currentLayout = ScreenLayout.Vertical;
                else
                    _currentLayout = ScreenLayout.Horizontal;
            }
        }
        public void LateUpdate()
        {
            _mpUpdateOutputThisFrame = false;
        }
       /*
        public void Update()
        {

            if (Input.GetKeyDown("k"))
            {
                if (MPEventSystem.instancesList == null)
                {
                    Print("No MPEventSystem instances");
                    return;

                }
                foreach(MPEventSystem system in MPEventSystem.instancesList)
                {
                    if(system.currentSelectedGameObject == null)
                        Print($"{system?.localUser?.userProfile?.name}({system?.name}) targeting nothing");
                    else
                        Print($"{system?.localUser?.userProfile?.name}({system?.name}) targeting {system?.currentSelectedGameObject?.name}");
                }
            }
        }*/
        #endregion

        #region Public Methods
        public bool SetLocalPlayerCount(int localPlayerCount, bool overrideMaxPlayers = false)
        {
            int maxPlayers = overrideMaxPlayers ? 4 : MaxPlayers;

            if (localPlayerCount < 1 || localPlayerCount > maxPlayers)
            {
                Print(string.Format(MSG_INFO_PLAYER_COUNT_CLAMPED, localPlayerCount, Mathf.Clamp(localPlayerCount, 1, maxPlayers)), Log.LogLevel.Message);
                localPlayerCount = Mathf.Clamp(localPlayerCount, 1, maxPlayers);
            }

            if (localPlayerCount == LocalPlayerCount)
                return false;

            bool success = true;

            LocalPlayerCount = localPlayerCount;

            success &= LogInProfiles();

            //if(!Enabled)
                success &= ToggleHooks(success);

            success &= ToggleControllers(success);

            if (!success)
            {
                Print(MSG_ERROR_PLAYER_COUNT, Log.LogLevel.Warning);

                _retryCounter++;

                if (_retryCounter > 2)
                    return false;

                return SetLocalPlayerCount(1);
            }
            else
            {
                _retryCounter = 0;
                _lastEventSystem = LocalUserManager.GetFirstLocalUser().eventSystem;
                WriteToLogFile(LogFileName, OutputPlayerInputToLog(false), OverwriteLogFile);
                return success;
            }

        }
        #endregion

        #region Private Methods
        private void InitializeConfiguration()
        {
            // TODO load from config

            if (Configuration == null)
                _configuration = new SplitScreenConfiguration();

            Configuration.Initialize(Config);
        }
        private void LoadBundle()
        {
            if(ResourceBundle == null)
            {
                using (Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("XSplitScreen.xsplitscreenbundle"))
                    ResourceBundle = AssetBundle.LoadFromStream(manifestResourceStream);
            }
        }
        private void AddLanguageReferences()
        {
            LanguageAPI.Add(MSG_HOVER_TOKEN, MSG_HOVER_STRING);
            LanguageAPI.Add(MSG_TITLE_BUTTON_TOKEN, MSG_TITLE_BUTTON_STRING);
            LanguageAPI.Add(MSG_SPLITSCREEN_ENABLE_HOVER_TOKEN, MSG_SPLITSCREEN_ENABLE_HOVER_STRING);
            LanguageAPI.Add(MSG_SPLITSCREEN_DISABLE_HOVER_TOKEN, MSG_SPLITSCREEN_DISABLE_HOVER_STRING);
            LanguageAPI.Add(MSG_SPLITSCREEN_ENABLE_TOKEN, MSG_SPLITSCREEN_ENABLE_STRING);
            LanguageAPI.Add(MSG_SPLITSCREEN_DISABLE_TOKEN, MSG_SPLITSCREEN_DISABLE_STRING);
            LanguageAPI.Add(MSG_SPLITSCREEN_CONFIG_HEADER_TOKEN, MSG_SPLITSCREEN_CONFIG_HEADER_STRING);
            LanguageAPI.Add(MSG_SPLITSCREEN_OPEN_DEBUG_TOKEN, MSG_SPLITSCREEN_OPEN_DEBUG_STRING);
            LanguageAPI.Add(MSG_SPLITSCREEN_OPEN_DEBUG_HOVER_TOKEN, MSG_SPLITSCREEN_OPEN_DEBUG_HOVER_STRING);
            LanguageAPI.Add(MSG_DISCORD_LINK_TOKEN, MSG_DISCORD_LINK_STRING);
            LanguageAPI.Add(MSG_DISCORD_LINK_HOVER_TOKEN, MSG_DISCORD_LINK_HOVER_STRING);
        }
        /// <summary>
        /// Force the creation of the mod menu.
        /// </summary>
        /// <param name="enable"></param>
        private void DevModeTriggers(bool enable)
        {
            if (!enable)
                return;

            Print("DevMode");
            ToggleModMenu(enable);
        }
        /// <summary>
        /// Set to true to react to scene changes
        /// </summary>
        /// <param name="status"></param>
        private void TogglePersistentHooks(bool status)
        {
            if(status)
            {
                On.RoR2.UI.MainMenu.BaseMainMenuScreen.OnEnter += BaseMainMenuScreen_OnEnter;
                SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
                On.RoR2.UI.CursorOpener.Awake += CursorOpener_Awake; // move
            }
            else
            {
                On.RoR2.UI.MainMenu.BaseMainMenuScreen.OnEnter -= BaseMainMenuScreen_OnEnter;
                SceneManager.activeSceneChanged -= SceneManager_activeSceneChanged;
                On.RoR2.UI.CursorOpener.Awake -= CursorOpener_Awake;
            }
        }
        private void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            if(self.body.teamComponent.teamIndex == TeamIndex.Player)
                damageInfo.damage = 0f;

            damageInfo.force = Vector3.zero;
            orig(self, damageInfo);
        }
        /// <summary>
        /// Create or destroy the mod menu.
        /// </summary>
        /// <param name="enable"></param>
        private void ToggleModMenu(bool enable) 
        {
            if(enable)
            {
                if (_titleButton == null)
                {
                    UILayer newLayer = ScriptableObject.CreateInstance<UILayer>(); // TODO Sort priority out
                    newLayer.name = PluginName;
                    newLayer.priority = 10;

                    ModScreen modScreen = ModMenuManager.AddScreen(PluginName, newLayer); 

                    Quaternion forward = Quaternion.identity;

                    forward = Quaternion.AngleAxis(20f, Vector3.up);
                    forward *= Quaternion.AngleAxis(-40f, Vector3.right);

                    modScreen.SetCameraPosition(new Vector3(-10.8f, 601.2f, -424.2f), forward);

                    _titleButton = ModMenuManager.CreateHGButton("XSplitScreen", MSG_TITLE_BUTTON_TOKEN, Menu.Title);
                    _titleButton.hoverToken = MSG_HOVER_TOKEN;
                    _titleButton.updateTextOnHover = true;
                    _titleButton.uiClickSoundOverride = "";
                    _titleButton.submitOnPointerUp = true;
                    _titleButton.onClick.AddListener(OnClickMainTitleButton);

                    /* // Disabled while implementing assignment window
                    HGButton enableSplitScreenButton = ModMenuManager.CreateHGButton("EnableSplitScreen", MSG_TITLE_BUTTON_TOKEN, Menu.None, userControllerScreen);
                    enableSplitScreenButton.hoverToken = MSG_HOVER_TOKEN; // what is this for?
                    enableSplitScreenButton.updateTextOnHover = true;
                    enableSplitScreenButton.submitOnPointerUp = true;
                    enableSplitScreenButton.uiClickSoundOverride = "";
                    enableSplitScreenButton.onClick.AddListener(OnClickToggleSplitScreen);
                    enableSplitScreenButton.defaultFallbackButton = true;
                    enableSplitScreenButton.requiredTopLayer = userControllerScreen.GetComponent<UILayerKey>();

                    DoDad.UI.Components.SplitscreenTextMeshController component = enableSplitScreenButton.gameObject.AddComponent<DoDad.UI.Components.SplitscreenTextMeshController>();
                    component.OnEnabledToken = MSG_SPLITSCREEN_DISABLE_TOKEN;
                    component.OnEnabledHoverToken = MSG_SPLITSCREEN_DISABLE_HOVER_TOKEN;
                    component.OnDisabledToken = MSG_SPLITSCREEN_ENABLE_TOKEN;
                    component.OnDisabledHoverToken = MSG_SPLITSCREEN_ENABLE_HOVER_TOKEN;
                    component.UpdateToken();
                    
                    */

                    HGButton openDebugFolderButton = ModMenuManager.CreateHGButton("OpenDebugFolder", MSG_SPLITSCREEN_OPEN_DEBUG_TOKEN, Menu.None, modScreen);
                    openDebugFolderButton.hoverToken = MSG_SPLITSCREEN_OPEN_DEBUG_HOVER_TOKEN;
                    openDebugFolderButton.updateTextOnHover = true;
                    openDebugFolderButton.submitOnPointerUp = true;
                    openDebugFolderButton.uiClickSoundOverride = "";
                    openDebugFolderButton.onClick.AddListener(OnClickOpenDebugFolder);
                    openDebugFolderButton.defaultFallbackButton = false;
                    openDebugFolderButton.requiredTopLayer = modScreen.GetComponent<UILayerKey>();

                    HGButton openDiscordButton = ModMenuManager.CreateHGButton("OpenDebugFolder", MSG_DISCORD_LINK_TOKEN, Menu.None, modScreen);
                    openDiscordButton.hoverToken = MSG_DISCORD_LINK_HOVER_TOKEN;
                    openDiscordButton.updateTextOnHover = true;
                    openDiscordButton.submitOnPointerUp = true;
                    openDiscordButton.uiClickSoundOverride = "";
                    openDiscordButton.onClick.AddListener(OnClickJoinDiscord);
                    openDiscordButton.defaultFallbackButton = false;
                    openDiscordButton.requiredTopLayer = modScreen.GetComponent<UILayerKey>();

                    CreateAssignmentWindow(modScreen);
                    //CreateControllerAssignmentWindow();

                    WriteToLogFile(LogFileName, OutputPlayerInputToLog(false), OverwriteLogFile);
                }
            }
            else
            {
                if (_titleButton)
                    Destroy(_titleButton?.gameObject);

                if (instance._controllerAssignmentWindow)
                    instance._controllerAssignmentWindow = null;

                ModMenuManager.CleanupReferences();
            }
        }
        /// <summary>
        /// Create the interactable controller assignment window. 
        /// </summary>
        private void CreateAssignmentWindow(ModScreen parent)
        {
            _controllerAssignmentWindow = ModMenuManager.CreatePopupPanel("ControllerAssignment", parent);

            Destroy(_controllerAssignmentWindow.transform.GetChild(0).gameObject);

            RectTransform assignmentTransform = _controllerAssignmentWindow.GetComponent<RectTransform>();
            assignmentTransform.anchorMax = new Vector2(0.9f, 0.8f);
            assignmentTransform.anchorMin = new Vector2(0.1f, 0.5f);

            foreach (LanguageTextMeshController controller in _controllerAssignmentWindow.GetComponentsInChildren<LanguageTextMeshController>(true))
            {
                if (string.Compare(controller.name, "HeaderText") == 0)
                    controller.token = MSG_SPLITSCREEN_CONFIG_HEADER_TOKEN;
            }

            GameObject assignmentManager = _controllerAssignmentWindow.transform.GetChild(1).gameObject;
            _controllerAssignmentWindow.gameObject.AddComponent<SplitscreenConfigurationManager>();
            assignmentManager.SetActive(true);

            //parent.GetComponent<CanvasScaler>().HandleScaleWithScreenSize();
        }
        private void CreateControllerAssignmentWindow()
        {
            if (_controllerAssignmentWindow)
                return;

            ModScreen screen = ModMenuManager.ActiveScreens[PluginName];
            _controllerAssignmentWindow = ModMenuManager.CreatePopupPanel("ControllerAssignment", screen);

            GameObject onEnableObject = _controllerAssignmentWindow.transform.GetChild(0).gameObject;
            onEnableObject.SetActive(Enabled);

            screen.AddObjectOnEnable(onEnableObject);

            foreach(LanguageTextMeshController controller in _controllerAssignmentWindow.GetComponentsInChildren<LanguageTextMeshController>(true))
            {
                if (string.Compare(controller.name, "HeaderText") == 0)
                    controller.token = MSG_SPLITSCREEN_CONFIG_HEADER_TOKEN;
            }
        }
        private bool LogInProfiles(UserProfile[] currentProfiles = null)
        {
            if(!LocalUserManager.isAnyUserSignedIn)
            {
                Print(MSG_ERROR_SIGN_IN_FIRST, Log.LogLevel.Message);
                return false;
            }

            if(currentProfiles == null)
            {
                currentProfiles = new UserProfile[PlatformSystems.saveSystem.loadedUserProfiles.Values.Count];
                PlatformSystems.saveSystem.loadedUserProfiles.Values.CopyTo(currentProfiles, 0);
            }

            if (currentProfiles.Length == 0)
            {
                Print(MSG_ERROR_NO_PROFILES);
                return false;
            }

            // Silence log spam
            On.RoR2.ViewablesCatalog.AddNodeToRoot += ViewablesCatalog_AddNodeToRoot;

            LocalUserManager.ClearUsers();
            LocalUserManager.LocalUserInitializationInfo[] initializationArray = new LocalUserManager.LocalUserInitializationInfo[LocalPlayerCount];

            List<UserProfile> userProfileList = new List<UserProfile>();

            //TODO Select which profiles to load
            for (int index = 0; index < currentProfiles.Length; index++)
            {
                if (index >= LocalPlayerCount)
                    break;

                userProfileList.Add(currentProfiles[index]);
            }

            // Create profiles if there aren't enough
            while (userProfileList.Count < LocalPlayerCount)
            {
                UserProfile newProfile = CopyProfile(userProfileList[0]);

                newProfile.fileName = Guid.NewGuid().ToString();
                newProfile.filePath = (UPath)("/UserProfiles/XSS-Copy-" + newProfile.fileName + ".xml");
                newProfile.name = string.Format("{0} ({1})", userProfileList[0].name, userProfileList.Count + 1);
                newProfile.canSave = true;

                PlatformSystems.saveSystem.loadedUserProfiles.Add(newProfile.fileName, newProfile);
                PlatformSystems.saveSystem.Save(newProfile, true);

                userProfileList.Add(newProfile);
            }

            for (int index = 0; index < LocalPlayerCount; index++)
            {
                initializationArray[index] = new LocalUserManager.LocalUserInitializationInfo()
                {
                    player = ReInput.players.GetPlayer(_playerBeginIndex + index),
                    profile = userProfileList[index]
                };
               // Print($"Added {ReInput.players.GetPlayer(_playerBeginIndex + index).name}");
            }

            LocalUserManager.SetLocalUsers(initializationArray);
            On.RoR2.ViewablesCatalog.AddNodeToRoot -= ViewablesCatalog_AddNodeToRoot;

            return true;
        }
        private bool ToggleHooks(bool valid = true)
        {
            if(!valid)
            {
                Print(MSG_ERROR_GENERIC);
                return false;
            }

            if(Enabled)
            {
                if (_devMode)
                {
                    On.RoR2.UI.SurvivorIconController.Update += SurvivorIconController_Update;
                }
                   // On.RoR2.UI.SurvivorIconController.GetLocalUser += SurvivorIconController_GetLocalUser;
                On.RoR2.UI.SurvivorIconController.UpdateAvailability += SurvivorIconController_UpdateAvailability;

                On.RoR2.UI.ScoreboardController.Awake += ScoreboardController_Awake;
                On.RoR2.UI.RuleChoiceController.FindNetworkUser += RuleChoiceController_FindNetworkUser;
                //On.RoR2.UI.SurvivorIconController.GetLocalUser += SurvivorIconController_GetLocalUser;
               // On.RoR2.UI.CharacterSelectController.Update += CharacterSelectController_Update;
                //On.RoR2.UI.CharacterSelectController.RebuildLocal += CharacterSelectController_RebuildLocal;
                //On.RoR2.UI.CharacterSelectController.OnLoadoutChangedGlobal += CharacterSelectController_OnLoadoutChangedGlobal;
                On.RoR2.LocalCameraEffect.OnUICameraPreCull += LocalCameraEffect_OnUICameraPreCull; // entire, req
                On.RoR2.UI.CombatHealthBarViewer.SetLayoutHorizontal += CombatHealthBarViewer_SetLayoutHorizontal; // entire, req
                On.RoR2.UI.LoadoutPanelController.UpdateDisplayData += LoadoutPanelController_UpdateDisplayData; // entire, req
                On.RoR2.UI.MPButton.Update += MPButton_Update; // entire, req
                On.RoR2.UI.MPButton.OnPointerClick += MPButton_OnPointerClick; // unaffected, req
                On.RoR2.UI.MPButton.InputModuleIsAllowed += MPButton_InputModuleIsAllowed;
                On.RoR2.UI.MPButton.CanBeSelected += MPButton_CanBeSelected;
                //On.RoR2.UI.MPButton.OnPointerExit += MPButton_OnPointerExit;
                On.RoR2.UI.MPEventSystem.ValidateCurrentSelectedGameobject += MPEventSystem_ValidateCurrentSelectedGameobject; // yes
                On.RoR2.UI.MPInputModule.GetMousePointerEventData += MPInputModule_GetMousePointerEventData; // yes
                //On.RoR2.UI.CharacterSelectController.OnEnable += CharacterSelectController_OnEnable; // no
                On.RoR2.CharacterSelectBarController.PickIcon += CharacterSelectBarController_PickIcon; // yes

                On.RoR2.RunCameraManager.Update += RunCameraManager_Update;
                /*
                IL.RoR2.RunCameraManager.Update += (il) =>
                {
                    
                    ILCursor c = new ILCursor(il);
                    c.GotoNext(
                        x => x.MatchLdsfld(typeof(RunCameraManager).GetField("screenLayouts",(BindingFlags)(-1)))
                        x => x.ldloc
                        );
                    Debug.Log(c);

                    c.Next.Operand = _currentLayout;
                };*/
            }
            else
            {
                if(_devMode)
                {
                    On.RoR2.UI.SurvivorIconController.Update -= SurvivorIconController_Update;
                }
                   // On.RoR2.UI.SurvivorIconController.GetLocalUser -= SurvivorIconController_GetLocalUser;
                //Print("DISABLING ALL HOOKS");
                On.RoR2.RunCameraManager.Update -= RunCameraManager_Update;
                On.RoR2.UI.ScoreboardController.Awake -= ScoreboardController_Awake;
                On.RoR2.UI.RuleChoiceController.FindNetworkUser -= RuleChoiceController_FindNetworkUser;
                //On.RoR2.UI.SurvivorIconController.GetLocalUser -= SurvivorIconController_GetLocalUser;
                //On.RoR2.UI.CharacterSelectController.Update -= CharacterSelectController_Update;
                //On.RoR2.UI.CharacterSelectController.RebuildLocal -= CharacterSelectController_RebuildLocal;
                //On.RoR2.UI.CharacterSelectController.OnLoadoutChangedGlobal -= CharacterSelectController_OnLoadoutChangedGlobal;
                On.RoR2.LocalCameraEffect.OnUICameraPreCull -= LocalCameraEffect_OnUICameraPreCull;
                On.RoR2.UI.CombatHealthBarViewer.SetLayoutHorizontal -= CombatHealthBarViewer_SetLayoutHorizontal;
                On.RoR2.UI.LoadoutPanelController.UpdateDisplayData -= LoadoutPanelController_UpdateDisplayData;
                On.RoR2.UI.MPButton.Update -= MPButton_Update;
                On.RoR2.UI.MPButton.OnPointerClick -= MPButton_OnPointerClick;
                On.RoR2.UI.MPButton.InputModuleIsAllowed -= MPButton_InputModuleIsAllowed;
                On.RoR2.UI.MPButton.CanBeSelected -= MPButton_CanBeSelected;
                //On.RoR2.UI.MPButton.OnPointerExit += MPButton_OnPointerExit;
                On.RoR2.UI.MPEventSystem.ValidateCurrentSelectedGameobject -= MPEventSystem_ValidateCurrentSelectedGameobject;
                On.RoR2.UI.MPInputModule.GetMousePointerEventData -= MPInputModule_GetMousePointerEventData;
                //On.RoR2.UI.CharacterSelectController.OnEnable -= CharacterSelectController_OnEnable; 
                On.RoR2.CharacterSelectBarController.PickIcon -= CharacterSelectBarController_PickIcon;
            }
            
            return true;
        }

        private void SurvivorIconController_UpdateAvailability(On.RoR2.UI.SurvivorIconController.orig_UpdateAvailability orig, SurvivorIconController self)
        {
            self.SetBoolAndMarkDirtyIfChanged(ref self.survivorIsUnlocked, SurvivorCatalog.SurvivorIsUnlockedOnThisClient(self.survivorIndex));
            self.SetBoolAndMarkDirtyIfChanged(ref self.survivorRequiredExpansionEnabled, self.survivorDef.CheckRequiredExpansionEnabled((NetworkUser)null));
            self.SetBoolAndMarkDirtyIfChanged(ref self.survivorRequiredEntitlementAvailable, self.survivorDef.CheckUserHasRequiredEntitlement(LocalUserManager.GetFirstLocalUser()));//self.GetLocalUser()));
            self.survivorIsAvailable = self.survivorIsUnlocked && self.survivorRequiredExpansionEnabled && self.survivorRequiredEntitlementAvailable;
        }

        private void RunCameraManager_Update(On.RoR2.RunCameraManager.orig_Update orig, RunCameraManager self)
        {
            bool instance = Stage.instance;
            if (instance)
            {
                int index = 0;
                for (int count = CameraRigController.readOnlyInstancesList.Count; index < count; ++index)
                {
                    if (CameraRigController.readOnlyInstancesList[index].suppressPlayerCameras)
                        return;
                }
            }
            if (instance)
            {
                int index1 = 0;
                System.Collections.ObjectModel.ReadOnlyCollection<NetworkUser> localPlayersList = NetworkUser.readOnlyLocalPlayersList;
                for (int index2 = 0; index2 < localPlayersList.Count; ++index2)
                {
                    NetworkUser networkUser = localPlayersList[index2];
                    CameraRigController cameraRigController = self.cameras[index1];
                    if (!cameraRigController)
                    {
                        cameraRigController = GameObject.Instantiate<GameObject>(LegacyResourcesAPI.Load<GameObject>("Prefabs/Main Camera")).GetComponent<CameraRigController>();
                        self.cameras[index1] = cameraRigController;
                    }
                    cameraRigController.viewer = networkUser;
                    networkUser.cameraRigController = cameraRigController;
                    GameObject networkUserBodyObject = RunCameraManager.GetNetworkUserBodyObject(networkUser);
                    ForceSpectate forceSpectate = InstanceTracker.FirstOrNull<ForceSpectate>();
                    if (forceSpectate)
                    {
                        cameraRigController.nextTarget = forceSpectate.target;
                        cameraRigController.cameraMode = (RoR2.CameraModes.CameraModeBase)RoR2.CameraModes.CameraModePlayerBasic.spectator;
                    }
                    else if (networkUserBodyObject)
                    {
                        cameraRigController.nextTarget = networkUserBodyObject;
                        cameraRigController.cameraMode = (RoR2.CameraModes.CameraModeBase)RoR2.CameraModes.CameraModePlayerBasic.playerBasic;
                    }
                    else if (!cameraRigController.disableSpectating)
                    {
                        cameraRigController.cameraMode = (RoR2.CameraModes.CameraModeBase)RoR2.CameraModes.CameraModePlayerBasic.spectator;
                        if (!cameraRigController.target)
                            cameraRigController.nextTarget = CameraRigControllerSpectateControls.GetNextSpectateGameObject(networkUser, (GameObject)null);
                    }
                    else
                        cameraRigController.cameraMode = (RoR2.CameraModes.CameraModeBase)RoR2.CameraModes.CameraModeNone.instance;
                    ++index1;
                }
                int index3 = index1;
                for (int index2 = index1; index2 < self.cameras.Length; ++index2)
                {
                    ref CameraRigController local = ref self.cameras[index1];
                    if (local != null)
                    {
                        if (local)
                            GameObject.Destroy(self.cameras[index1].gameObject);
                        local = (CameraRigController)null;
                    }
                }
                Rect[] screenLayout = _screenLayouts[index3];
                for (int index2 = 0; index2 < index3; ++index2)
                    self.cameras[index2].viewport = screenLayout[index2];
            }
            else
            {
                for (int index = 0; index < self.cameras.Length; ++index)
                {
                    if (self.cameras[index])
                        GameObject.Destroy(self.cameras[index].gameObject);
                }
            }
        }
        private void ScoreboardController_Awake(On.RoR2.UI.ScoreboardController.orig_Awake orig, ScoreboardController self)
        {
            orig(self);

            self.transform.GetComponentInChildren<PostProcessDuration>().gameObject.SetActive(false);
        }
        private NetworkUser RuleChoiceController_FindNetworkUser(On.RoR2.UI.RuleChoiceController.orig_FindNetworkUser orig, RuleChoiceController self)
        {
            // Fix rule selection
            return _lastEventSystem?.localUser.currentNetworkUser;
        }
        private LocalUser SurvivorIconController_GetLocalUser(On.RoR2.UI.SurvivorIconController.orig_GetLocalUser orig, SurvivorIconController self)
        {
            // Fix entitlement selection
            //return LocalUserManager.GetFirstLocalUser().eventSystem.localUser;
            return _lastEventSystem.localUser == null ? ((MPEventSystem) EventSystem.current).localUser : _lastEventSystem.localUser;
        }
        private bool MPButton_CanBeSelected(On.RoR2.UI.MPButton.orig_CanBeSelected orig, MPButton self)
        {
            if (!self.gameObject.activeInHierarchy)
                return false;

            return true;
        }
        private bool ToggleControllers(bool valid = true)
        {
            if (!valid)
                return false;

            if(!Enabled)
            {
                ReInput.controllers.AutoAssignJoysticks();
                return true;
            }

            List<Controller> joystickList = new List<Controller>();

            foreach (Controller controller in ReInput.controllers.Controllers)
            {
                if((controller.type == ControllerType.Custom || controller.type == ControllerType.Joystick) || KeyboardMode && string.Compare(controller.name.ToLower(), "unknown") != 0)
                    joystickList.Add(controller);
            }

            //if (controllerList.Count - 2 < LocalPlayerCount - 1)
            //{
            //    Print(MSG_ERROR_NO_CONTROLLERS);
            //    return false;
            //}

            joystickList.Reverse();

            LocalUserManager.localUsersList[0].inputPlayer.controllers.ClearAllControllers();

            if(KeyboardMode)
            {
                LocalUserManager.localUsersList[0].inputPlayer.controllers.ClearAllControllers();
                LocalUserManager.localUsersList[0].inputPlayer.controllers.AddController(joystickList[joystickList.Count - 1], false);
                LocalUserManager.localUsersList[0].inputPlayer.controllers.AddController(joystickList[joystickList.Count - 2], false);

                joystickList.RemoveRange(joystickList.Count - 2, 2);
            }

            for(int e = KeyboardMode ? 1 : 0; e < LocalUserManager.readOnlyLocalUsersList.Count; e++)
            {
                if(joystickList.Count == 0)
                {
                    Print(string.Format(MSG_ERROR_GENERIC, "01"), Log.LogLevel.Error);
                    return false;
                }

                LocalUserManager.readOnlyLocalUsersList[e].inputPlayer.controllers.ClearAllControllers();

                LocalUserManager.readOnlyLocalUsersList[e].inputPlayer.controllers.AddController(joystickList[joystickList.Count - 1], false);
                joystickList.RemoveAt(joystickList.Count - 1);
            }

            return true;
            /*
            for (int e = 0; e < LocalUserManager.readOnlyLocalUsersList.Count; e++)
            {
                if (controllerList.Count == 0)
                {
                    Print(MSG_ERROR_NO_CONTROLLERS);
                    return false;
                }

                LocalUserManager.readOnlyLocalUsersList[e].inputPlayer.controllers.ClearAllControllers();

                if (e == 0)
                {
                    if (!DisableKeyboard)
                    {
                        LocalUserManager.readOnlyLocalUsersList[e].inputPlayer.controllers.AddController(controllerList[controllerList.Count - 2], false);
                        LocalUserManager.readOnlyLocalUsersList[e].inputPlayer.controllers.AddController(controllerList[controllerList.Count - 1], false);
                    }

                    controllerList.RemoveRange(controllerList.Count - 2, 2);

                    if (DisableKeyboard)
                    {
                        LocalUserManager.readOnlyLocalUsersList[e].inputPlayer.controllers.AddController(controllerList[controllerList.Count - 1], false);
                        controllerList.RemoveAt(controllerList.Count - 1);
                    }
                }
                else
                {
                    LocalUserManager.readOnlyLocalUsersList[e].inputPlayer.controllers.AddController(controllerList[controllerList.Count - 1], false);
                    controllerList.RemoveAt(controllerList.Count - 1);
                }
            }
            */
            return true;
        }
        
        private int EstimateJoysticks()
        {
            int potentialUsers = 0;

            int totalJoysticks = 0;
            int keyboardMouse = 0;

            _keyboardModeOnly = false;
            _keyboardOptional = false;

            foreach(Controller controller in ReInput.controllers.Controllers)
            {
                if(string.Compare(controller.name.ToLower(), "unknown") != 0)
                {
                    switch (controller.identifier.controllerType)
                    {
                       // case ControllerType.Joystick: // removed to test controller not being detected bug
                        //    totalJoysticks++;
                        //    break;
                        case ControllerType.Keyboard:
                            keyboardMouse++;
                            break;
                        case ControllerType.Mouse:
                            keyboardMouse++;
                            break;
                        default:
                            totalJoysticks++;
                            break;
                    }
                }
            }

            if(totalJoysticks == 1 && keyboardMouse == 2)
            {
                Print(MSG_INFO_KEYBOARD_ONLY, Log.LogLevel.Message);
                _keyboardModeOnly = true;
            }

            if(totalJoysticks > 1 && keyboardMouse == 2)
            {
                _keyboardOptional = true;
            }

            potentialUsers += totalJoysticks;
            /*
            if (potentialUsers > 1 && keyboardMouse == 2)
                KeyboardSplitscreenAvailable = true;
            */
            return potentialUsers;
        }
        private void AddMainMenuCalls()
        {
            //GameObject.Find("GenericMenuButton (Singleplayer)").GetComponent<HGButton>().onClick.AddListener(DisableMenu);
            //GameObject.Find("GenericMenuButton (Logbook)").GetComponent<HGButton>().onClick.AddListener(DisableMenu);
        }
        private void OnClickToggleKeyboard()
        {
            _requestKeyboard = !_requestKeyboard;
        }
        public void CleanupReferences()
        {
            //GameObject.Find("GenericMenuButton (Singleplayer)").GetComponent<HGButton>().onClick.RemoveListener(DisableMenu);
            //GameObject.Find("GenericMenuButton (Logbook)").GetComponent<HGButton>().onClick.RemoveListener(DisableMenu);
            ToggleModMenu(false);
            Configuration.Exit();
        }
        private void SwapProfiles(int firstPlayerIndex, int secondPlayerIndex)
        {
            UserProfile[] currentProfiles = new UserProfile[PlatformSystems.saveSystem.loadedUserProfiles.Values.Count];
            PlatformSystems.saveSystem.loadedUserProfiles.Values.CopyTo(currentProfiles, 0);

            firstPlayerIndex -= 1;
            secondPlayerIndex -= 1;

            UserProfile holdingProfile = currentProfiles[firstPlayerIndex];
            currentProfiles[firstPlayerIndex] = currentProfiles[secondPlayerIndex];
            currentProfiles[secondPlayerIndex] = holdingProfile;

            LogInProfiles(currentProfiles);
        }
        private string OutputPlayerInputToLog(bool output = true)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine($"{PluginName} {PluginVersion} = {Enabled}");
            builder.AppendLine($"MaxPlayers: {MaxPlayers}");
            builder.AppendLine($"LocalPlayerCount: {LocalPlayerCount}");
            builder.AppendLine($"KeyboardMode: {KeyboardMode}");
            builder.AppendLine("");
            builder.AppendLine("Local Users");
            //Print("LocalUsers");
            foreach (LocalUser user in LocalUserManager.localUsersList)
            {
                //Print($" - {user.inputPlayer.name} ({(user.userProfile == null ? ("no user") : user.userProfile.name)})");
                builder.AppendLine($" {user.inputPlayer.name} ({(user.userProfile == null ? ("no user") : user.userProfile.name)})");
                foreach (Controller controller in user.inputPlayer.controllers.Controllers)
                {
                    builder.AppendLine($" - {controller.identifier.controllerType.ToString()} ({controller.name.ToString()})");
                    //Print($" -- {controller.identifier.controllerType.ToString()} ({controller.name.ToString()})");
                }
            }

            builder.AppendLine("ReInput");
            //Print("ReInput Players");
            int count = 1;
            foreach(Controller controller in ReInput.controllers.Controllers)
            {
                builder.AppendLine($"[{count}] {controller.name}, {controller.hardwareIdentifier}, {controller.hardwareName}, {controller.id}, {controller.identifier.controllerType}, {controller.enabled}");
                count++;
            }
            foreach (Player player in ReInput.players.AllPlayers)
            {
                builder.AppendLine($"{player.name}");
                //Print($" - {player.name}");
                foreach (Controller controller in player.controllers.Controllers)
                {
                    builder.AppendLine($" - {controller.templateCount.ToString()} ({controller.name})");
                    builder.AppendLine($" -- {controller.mapTypeString} ({controller.buttonCount})");
                    //Print($" -- {controller.templateCount.ToString()} ({controller.name})");
                    //Print($" --- {controller.mapTypeString} ({controller.buttonCount})");
                }
            }

            if(output)
                Print(builder.ToString());

            return builder.ToString();
        }
        private void WriteToLogFile(string fileName, string text, bool overwrite = false)
        {
            string file = $"{Application.persistentDataPath}/{fileName}";

            if (overwrite)
                System.IO.File.WriteAllText(file, text);
            else
                System.IO.File.AppendAllText(file, text);

            Print(string.Format(MSG_INFO_DEBUG_SAVED, file));
        }
        private string OutputControllersToLog()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("Detected Controllers\n");

            int count = 0;

            foreach (Controller controller in ReInput.controllers.Controllers)
            {
                string profileName = "none";
                int userIndex = -1;

                for(int e = 0; e < LocalPlayerCount; e++)
                {
                    LocalUser user = LocalUserManager.readOnlyLocalUsersList[e];

                    if (user.inputPlayer.controllers.ContainsController(controller.type, controller.id))
                    {
                        profileName = user.userProfile.name;
                        userIndex = e;
                    }
                }

                builder.AppendLine($"[{count}] {controller.name} assigned to user '[{(userIndex > -1 ? userIndex : "")}] {profileName}'");
                count++;
            }

            return builder.ToString();
        }
        #endregion

        #region Event Handlers / Hooks
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
        private void OnClickMainTitleButton()
        {
            RoR2.UI.MainMenu.MainMenuController.instance.SetDesiredMenuScreen(ModMenuManager.ActiveScreens[PluginName]);
        }
        private static void OnClickJoinDiscord()
        {
            Application.OpenURL(MSG_DISCORD_LINK_HREF);
        }
        private static void OnClickOpenDebugFolder()
        {
            Application.OpenURL(Application.persistentDataPath);
        }
        private static void OnClickToggleSplitScreen()
        {
            if (!Enabled)
            {
                int max = MaxPlayers;
                Print(string.Format(MSG_INFO_POTENTIAL_PLAYERS, max));
                XSplitScreen.instance.SetLocalPlayerCount(max);
            }
            else
            {
                XSplitScreen.instance.SetLocalPlayerCount(1);
            }
        }
        private void CursorOpener_Awake(On.RoR2.UI.CursorOpener.orig_Awake orig, CursorOpener self)
        {
            orig(self);
            self._forceCursorForGamepad = true;
        }
        /// <summary>
        /// Enable or disable the cursor
        /// </summary>
        private void UpdateCursorStatus()
        {
            CursorOpener[] openers = GameObject.FindObjectsOfType<CursorOpener>();

            foreach (CursorOpener opener in openers)
            {
                opener.forceCursorForGamePad = Enabled;
            }

            if (!Enabled)
            {
                foreach (MPEventSystem instance in MPEventSystem.instancesList)
                {
                    instance.SetSelectedGameObject(null);
                }
            }
        }
        /// <summary>
        /// If the new scene is 'title' then we need to start a coroutine that will create the mod menu. Afterwards update the cursor status. TODO: Do we need a coroutine to wait until everything is loaded to update the cursor status?
        /// </summary>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
        {
            if (string.Compare(arg1.name, "title") == 0)
            {
                if (WaitFormenuLoad != null)
                    StopCoroutine(WaitFormenuLoad);

                WaitFormenuLoad = StartCoroutine(WaitForMenuCoroutine());
            }
            else
            {
                ToggleModMenu(false);
            }

            UpdateCursorStatus();
        }
        /// <summary>
        /// Notify WaitForMenuCoroutine that we've entered the main menu
        /// </summary>
        /// <param name="orig"></param>
        /// <param name="self"></param>
        /// <param name="mainMenuController"></param>
        private void BaseMainMenuScreen_OnEnter(On.RoR2.UI.MainMenu.BaseMainMenuScreen.orig_OnEnter orig, RoR2.UI.MainMenu.BaseMainMenuScreen self, RoR2.UI.MainMenu.MainMenuController mainMenuController)
        {
            orig(self, mainMenuController);
            _enteredMenu = true;
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
        private void LocalCameraEffect_OnUICameraPreCull(On.RoR2.LocalCameraEffect.orig_OnUICameraPreCull orig, UICamera uiCamera)
        {
            for (int index = 0; index < LocalCameraEffect.instancesList.Count; index++)
            {
                GameObject target = uiCamera.cameraRigController.target;
                LocalCameraEffect instance = LocalCameraEffect.instancesList[index];

                if (instance.targetCharacter == target && uiCamera.cameraRigController.localUserViewer.cachedBody.healthComponent.alive)
                    LocalCameraEffect.instancesList[index].effectRoot.SetActive(true);
                else
                    LocalCameraEffect.instancesList[index].effectRoot.SetActive(false);
            }
        }
        private void CombatHealthBarViewer_SetLayoutHorizontal(On.RoR2.UI.CombatHealthBarViewer.orig_SetLayoutHorizontal orig, RoR2.UI.CombatHealthBarViewer self)
        {
            RoR2.UICamera uiCamera = self.uiCamera;

            if (!uiCamera)
                return;

            self.UpdateAllHealthbarPositions(uiCamera.cameraRigController.sceneCam, uiCamera.camera);
        }
        private void LoadoutPanelController_UpdateDisplayData(On.RoR2.UI.LoadoutPanelController.orig_UpdateDisplayData orig, RoR2.UI.LoadoutPanelController self)
        {
            UserProfile userProfile = _lastEventSystem?.localUser?.userProfile;
            NetworkUser currentNetworkUser = _lastEventSystem?.localUser?.currentNetworkUser;

            BodyIndex bodyIndex = ((UnityEngine.Object)currentNetworkUser) ? currentNetworkUser.bodyIndexPreference : BodyIndex.None;
            self.SetDisplayData(new RoR2.UI.LoadoutPanelController.DisplayData()
            {
                userProfile = userProfile,
                bodyIndex = bodyIndex
            });
        }
        private bool MPButton_InputModuleIsAllowed(On.RoR2.UI.MPButton.orig_InputModuleIsAllowed orig, RoR2.UI.MPButton self, BaseInputModule inputModule)
        {
            return true;
        }
        private void MPButton_Update(On.RoR2.UI.MPButton.orig_Update orig, RoR2.UI.MPButton self)
        {
            if(!self.eventSystem || self.eventSystem.player == null)
                return;

            bool outputMessage = false;

            for (int e = 1; e < RoR2.UI.MPEventSystem.readOnlyInstancesList.Count; e++)
            {
                RoR2.UI.MPEventSystem eventSystem = RoR2.UI.MPEventSystem.readOnlyInstancesList[e] as MPEventSystem;

                if (eventSystem.player.GetButtonDown(4) && !_mpUpdateOutputThisFrame)
                {
                   // Print($"[{self.disableGamepadClick}] {eventSystem.name} is pressing X and object selected is {(eventSystem.currentSelectedGameObject != null ? eventSystem.currentSelectedGameObject.name : "null")}");
                    outputMessage = true;
                }
                if (eventSystem && eventSystem.currentSelectedGameObject == self.gameObject && ((eventSystem.player.GetButtonDown(4) && !self.disableGamepadClick) || eventSystem.player.GetButtonDown(14)))
                {
                   // Print("Invoking click");
                    _lastEventSystem = eventSystem;
                    self.InvokeClick();
                }
            }/*
            foreach(RoR2.UI.MPEventSystem eventSystem in RoR2.UI.MPEventSystem.readOnlyInstancesList)
            {
                if (eventSystem.player.GetButtonDown(4) && !_mpUpdateOutputThisFrame)
                {
                    Print($"[{self.disableGamepadClick}] {eventSystem.name} is pressing X and object selected is {(eventSystem.currentSelectedGameObject != null ? eventSystem.currentSelectedGameObject.name : "null")}");
                    outputMessage = true;
                }
                if(eventSystem && eventSystem.currentSelectedGameObject == self.gameObject && ((eventSystem.player.GetButtonDown(4) && !self.disableGamepadClick) || eventSystem.player.GetButtonDown(14)))
                {
                    Print("Invoking click");
                    _lastEventSystem = eventSystem;
                    self.InvokeClick();
                }
            }*/

            if(outputMessage)
                _mpUpdateOutputThisFrame = true;

            if (!self.defaultFallbackButton || self.eventSystem.currentInputSource != RoR2.UI.MPEventSystem.InputSource.Gamepad || !(self.eventSystem.currentSelectedGameObject == null || !self.CanBeSelected()))
            {
                return;
            }

            //Print("Fallback for " + self.gameObject.name);
            self.Select();
        }
        private void MPButton_OnPointerClick(On.RoR2.UI.MPButton.orig_OnPointerClick orig, RoR2.UI.MPButton self, PointerEventData eventData)
        {
            _lastEventSystem = (eventData.currentInputModule.eventSystem as RoR2.UI.MPEventSystem);
            orig(self, eventData);
        }
        private void CharacterSelectController_OnEnable(On.RoR2.UI.CharacterSelectController.orig_OnEnable orig, RoR2.UI.CharacterSelectController self)
        {
            orig(self);
            //Print("Forcing cursor");
            //self.GetComponent<RoR2.UI.CursorOpener>().forceCursorForGamePad = true;
        }
        private object MPInputModule_GetMousePointerEventData(On.RoR2.UI.MPInputModule.orig_GetMousePointerEventData orig, RoR2.UI.MPInputModule self, int playerId, int mouseIndex)
        { // TODO don't need to replace entire method
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

            if(mouseInputSource.locked || !mouseInputSource.enabled)
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
            data1.button = UnityEngine.EventSystems.PointerEventData.InputButton.Left;

            // Raycast all objects from current position and select the first one
            self.eventSystem.RaycastAll(data1, self.m_RaycastResultCache);
            UnityEngine.EventSystems.RaycastResult firstRaycast = BaseInputModule.FindFirstRaycast(self.m_RaycastResultCache);

            bool foundObject = false;
            bool foundInput = false;
            bool foundHG = false;
            int priority = 0;

            GameObject focusObject = null;

            foreach (RaycastResult raycast in self.m_RaycastResultCache)
            {
                if(self.useCursor)
                {
                    if(raycast.gameObject != null)
                    {
                        TMPro.TMP_InputField input = raycast.gameObject.GetComponent<TMPro.TMP_InputField>();
                        MPButton mpButton = raycast.gameObject.GetComponent<RoR2.UI.MPButton>();
                        HGButton hgButton = mpButton?.GetComponent<HGButton>();

                        if(input != null && priority < 3)
                        {
                            //Print($"Selecting {raycast.gameObject} p3");
                            focusObject = raycast.gameObject;
                            priority = 3;
                        }
                        if(hgButton != null)
                        {
                            if(priority <2)
                            {
                                //Print($"Selecting {raycast.gameObject} p2");
                                focusObject = raycast.gameObject;
                                priority = 2;
                            }
                        }
                        if(mpButton != null)
                        {
                            if(priority < 1)
                            {
                                //Print($"Selecting {raycast.gameObject} p1");
                                focusObject = raycast.gameObject;
                                priority = 1;
                            }
                        }
                        /*
                        TMPro.TMP_InputField input = raycast.gameObject.GetComponent<TMPro.TMP_InputField>();

                        if (input != null)
                        {
                            //Print("FOUND INPUT"); this does nothing
                            foundInput = true;
                            //self.eventSystem.SetSelectedGameObject(raycast.gameObject);
                            foundObject = true;
                        }

                        if (self.eventSystem?.currentSelectedGameObject?.GetComponent<TMPro.TMP_InputField>() != null)
                            foundInput = foundObject = true;

                        MPButton button = raycast.gameObject.GetComponent<RoR2.UI.MPButton>();

                        if (button != null && !foundInput)
                        {
                            if (raycast.gameObject.GetComponent<RoR2.UI.HGButton>() != null)
                            {
                               //Print("FOUND HG");
                                foundHG = true;
                               // self.eventSystem.SetSelectedGameObject(raycast.gameObject);
                                foundObject = true;
                            }

                            if(!foundHG)
                            {
                                //Print("FOUND MP");
                                //self.eventSystem.SetSelectedGameObject(raycast.gameObject);
                                foundObject = true;
                            }
                        */

                        //if(raycast.gameObject.GetComponent<RoR2.UI.MPButton>().requiredTopLayer.representsTopLayer)
                        //{
                        //    foundObject = true;
                        //   self.eventSystem.SetSelectedGameObject(raycast.gameObject);
                        //}
                        //Print($"{(self.eventSystem as MPEventSystem).localUser?.userProfile.name} raycast onto {raycast.gameObject.name}");

                    }
                }
            }

            //if(self.eventSystem.currentSelectedGameObject?.GetComponent<TMPro.TMP_InputField>() != null)

            if (self.eventSystem.currentSelectedGameObject != null && focusObject == null)
                if (self.eventSystem.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>() != null)
                    focusObject = self.eventSystem.currentSelectedGameObject;

            self.eventSystem.SetSelectedGameObject(focusObject); // REMOVED
            //if (!foundObject)
            //    self.eventSystem.SetSelectedGameObject(null);

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

            // No idea what this is doing. Maybe comining all?
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
        private void MPEventSystem_ValidateCurrentSelectedGameobject(On.RoR2.UI.MPEventSystem.orig_ValidateCurrentSelectedGameobject orig, RoR2.UI.MPEventSystem self)
        {
            // REVISION ADD
            return;
            // REVISION ADD

            if (!self.currentSelectedGameObject)
                return;

            MPButton component = self.currentSelectedGameObject.GetComponent<MPButton>();

            if(Enabled) // if hooked then we're always enabled.. right?
            {
                if(component)
                {
                    if (component.interactable)
                        return;
                    //if ((component.interactable) && self.localUser != null)
                    //{
                    //    return;
                    //}
                }
            }
            else
            {
                orig(self);
            }

            self.SetSelectedGameObject(null);
            return;
        }
        private void CharacterSelectBarController_PickIcon(On.RoR2.CharacterSelectBarController.orig_PickIcon orig, CharacterSelectBarController self, RoR2.UI.SurvivorIconController newPickedIcon)
        {
            MPEventSystem lastEventSystem = ((RoR2.UI.MPEventSystem)_lastEventSystem);

            if (_lastEventSystem == null)
            {
                orig(self, newPickedIcon);
                return;
            }

            if (self.pickedIcon == newPickedIcon)
                return;

            self.pickedIcon = newPickedIcon;

            CharacterSelectBarController.SurvivorPickInfoUnityEvent onSurvivorPicked = self.onSurvivorPicked;

            if (onSurvivorPicked == null)
                return;

            onSurvivorPicked.Invoke(new CharacterSelectBarController.SurvivorPickInfo()
            {
                localUser = lastEventSystem.localUser,
                pickedSurvivor = newPickedIcon.survivorDef
            });
        }
        #endregion

        #region Console Commands
        
        [ConCommand(commandName = "xassign", flags = ConVarFlags.None, helpText = "Assign controllers by index. Type 'xdevices' for IDs. Useage: xassign [controller id] [player id]")]
        private static void ConAssign(ConCommandArgs args)
        {
            int controllerId = args.GetArgInt(0);
            int playerId = args.GetArgInt(1);

            bool controllerFound = false;
            bool playerFound = false;

            if (controllerId > -1 && controllerId < ReInput.controllers.Controllers.Count)
                controllerFound = true;

            if(playerId > -1 &&  playerId < LocalPlayerCount)
            {
                playerFound = true;
            }

            if (!controllerFound)
                Print("Controller not found. Type 'xdevices' to see available controllers.");

            if (!playerFound)
                Print("Player not found. Type 'xsplitset #' to add local players.");

            if (!(playerFound && controllerFound))
                return;

            int controllerCount = 0;

            foreach (Controller controller in ReInput.controllers.Controllers) // lazy I know
            {
                if (controllerCount == controllerId)
                {
                    for(int e = 0; e < LocalPlayerCount; e++)
                    {
                        LocalUser user = LocalUserManager.readOnlyLocalUsersList[e];

                        user.inputPlayer.controllers.RemoveController(controller.type, controller.id);

                        if (e == playerId)
                        {
                            Print($"Adding '{controller.name}' to '{user.userProfile.name}'");
                            user.inputPlayer.controllers.AddController(controller, false);
                            user.ApplyUserProfileBindingstoRewiredController(controller);
                        }
                    }
                }

                controllerCount++;
            }
        }
        [ConCommand(commandName = "xdevices", flags = ConVarFlags.None, helpText = "Controller assignment information")]
        private static void ConDeviceStatus(ConCommandArgs args)
        {
           // if (instance._devMode)
            //    instance.OutputPlayerInputToLog(true);

            Print(instance.OutputControllersToLog());
        }
        [ConCommand(commandName = "xsplitswap", flags = ConVarFlags.None, helpText = "Swap players by index. Useage: xsplit # #")]
        private static void ConXSplitSwap(ConCommandArgs args)
        {
            if (!instance.CanConfigure())
                return;

            if(!Enabled)
            {
                Print(MSG_ERROR_SINGLE_LOCAL_PLAYER);
                return;
            }

            if(args.Count == 2)
            {
                int firstPlayerIndex = args.GetArgInt(0);
                int secondPlayerIndex = args.GetArgInt(1);

                if(!instance.IsPlayerIndexValid(firstPlayerIndex) || !instance.IsPlayerIndexValid(secondPlayerIndex))
                {
                    Print(MSG_ERROR_INVALID_PLAYER_RANGE);
                    return;
                }

                instance.SwapProfiles(firstPlayerIndex, secondPlayerIndex);
            }
            else
            {
                Print(MSG_ERROR_INVALID_ARGS);
            }
        }
        [ConCommand(commandName = "xsplitset", flags = ConVarFlags.None, helpText = "Maximum 4 players. Useage: xsplitset [players] [kb]")]
        private static void ConXSplitSet(ConCommandArgs args)
        {
            if(!instance.CanConfigure())
                return;

            int requestedLocalPlayerCount = 2;

            if (args.Count > 0)
                requestedLocalPlayerCount = args.GetArgInt(0);

            instance._requestKeyboard = false;

            if (args.Count > 1)
                if (args.GetArgString(1) == "kb")
                    instance._requestKeyboard = true;

            if (instance._requestKeyboard)
                Print(MSG_INFO_KEYBOARD_STATUS, Log.LogLevel.Debug);

            instance.SetLocalPlayerCount(requestedLocalPlayerCount, true);
        }
        #endregion

        #region Helpers
        internal static void Print(string msg, Log.LogLevel level = Log.LogLevel.UnityDebug)
        {
            msg = level != Log.LogLevel.UnityDebug ? msg : string.Format(MSG_TAG_PLUGIN, msg);

            switch (level)
            {
                case Log.LogLevel.Error:
                    Log.LogError(msg);
                    break;
                case Log.LogLevel.Fatal:
                    Log.LogFatal(msg);
                    break;
                case Log.LogLevel.Info:
                    Log.LogInfo(msg);
                    break;
                case Log.LogLevel.Message:
                    Log.LogMessage(msg);
                    break;
                case Log.LogLevel.Warning:
                    Log.LogWarning(msg);
                    break;
                case Log.LogLevel.Debug:
                    Log.LogDebug(msg);
                    break;
                default:
                    Debug.Log(msg);
                    break;
            }
        }
        private static UserProfile CopyProfile(UserProfile template)
        {
            UserProfile newProfile = new UserProfile();
            SaveSystem.Copy(template, newProfile);
            return newProfile;
        }
        private bool CanConfigure()
        {
            if (string.Compare(SceneManager.GetActiveScene().name, "title") != 0)
            {
                Print(MSG_ERROR_NETWORK_ACTIVE);
                return false;
            }

            return true;
        }
        private bool IsPlayerIndexValid(int index)
        {
            return index > 0 && index <= LocalPlayerCount;
        }
        #endregion

        #region Definitions
        System.Collections.IEnumerator WaitForMenuCoroutine()
        {
            // ModMenuManager depends on this. TODO stop that
            GameObject singleplayerButton = GameObject.Find("GenericMenuButton (Singleplayer)");

            while (!singleplayerButton)
            {
                singleplayerButton = GameObject.Find("GenericMenuButton (Singleplayer)");
                yield return null;
            }

            while (RoR2.UI.MainMenu.MainMenuController.instance == null || !_enteredMenu)
                yield return null;

            while (!ModMenuManager.Ready)
            {
                ModMenuManager.CreateReferences();
                yield return null;
            }

            ToggleModMenu(true);

            _enteredMenu = false;

            yield return null;
        }
        public class SplitScreenUpdated : UnityEvent<SplitScreenConfiguration> { }
        public enum ScreenLayout
        {
            Horizontal,
            Vertical
        }

        [System.Serializable]
        public class SplitScreenConfiguration
        {
            #region Variables
            public ControllerAssignedEvent OnSplitscreenDeviceConnected 
            { 
                get
                {
                    return _controllerAssigned;
                } 
            }
            public List<ControllerAssignment> ControllerAssignments
            {
                get
                { 
                    ControllerAssignment[] list = new ControllerAssignment[_controllerAssignments.Count];

                    _controllerAssignments.CopyTo(list);

                    return list.ToList<ControllerAssignment>();
                }
            }
            public int DefaultPlayerCount
            {
                get
                {
                    return _defaultPlayerCountConfig.Value;
                }
            }

            //public Dictionary<int, ControllerAssignment> ControllerAssignments; // Controller.id

            private ConfigEntry<string> _controllerAssignmentsConfig;
            private ConfigEntry<bool> _startOnLoadConfig;
            private ConfigEntry<int> _defaultPlayerCountConfig;
            private ConfigFile _config;

            private ControllerAssignedEvent _controllerAssigned;
            private List<ControllerAssignment> _controllerAssignments;

            // Should serve as both the desired player count and the indicator for "StartOnLoad"
            #endregion

            #region Initialization & Exit
            public void Initialize(ConfigFile config)
            {
                _config = config;

                _controllerAssignments = new List<ControllerAssignment>();
                _controllerAssigned = new ControllerAssignedEvent();

                try
                {
                    _controllerAssignmentsConfig = config.Bind<string>("General", "Controller Assignment Preferences", "", "Persistent device preferences");
                    _startOnLoadConfig = config.Bind<bool>("General", "Plugin Settings", false, "Should the plugin start on launch?");
                    //_defaultPlayerCountConfig = config.Bind<int>("General", "Plugin Settings", 1, "How many players should be logged in?");
                }
                catch(Exception e)
                {
                    Print("Placeholder Error - Unable to load config file");
                }

                if (_controllerAssignmentsConfig.Value.Length > 0)
                {
                    try
                    {
                        ConfigDataWrapper wrapper = JsonUtility.FromJson<ConfigDataWrapper>(_controllerAssignmentsConfig.Value);

                        for(int index = 0; index < wrapper.AssignedDeviceId.Length; index++)
                        {
                            _controllerAssignments.Add(new ControllerAssignment()
                            {
                                AssignedDeviceId = wrapper.AssignedDeviceId[index],
                                AssignedDisplay = wrapper.AssignedDisplay[index],
                                AssignedProfile = wrapper.AssignedProfile[index],
                                AssignedScreen = wrapper.AssignedScreen[index],
                                IsKeyboard = wrapper.IsKeyboard[index]
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        Print("Placeholder Error - Unable to parse config file");
                    }
                }

                // Create default config file
                
                ReloadEntries();

                ReInput.ControllerConnectedEvent += OnControllerConnected;
            }
            public void Exit()
            {
                ReInput.ControllerConnectedEvent -= OnControllerConnected;

                Save();
            }
            #endregion

            #region Entries
            private void ReloadEntries()
            {
                foreach (Controller controller in ReInput.controllers.Controllers)
                {
                    EnsureAssignmentExists(controller);
                }
            }
            public bool Save()
            {
                bool status = true;

                _startOnLoadConfig.Value = XSplitScreen.Enabled;

                List<ControllerAssignment> list = new List<ControllerAssignment>();

                ConfigDataWrapper wrapper = new ConfigDataWrapper(ControllerAssignments);

                try
                {
                    _controllerAssignmentsConfig.Value = JsonUtility.ToJson(wrapper);
                }
                catch (Exception e)
                {
                    Print("Placeholder Error - Unable to write profile to JSON");
                    status = false;
                }

                _config.Save();
                return status;
            }
            private void EnsureAssignmentExists(Controller controller)
            {
                if (controller.type == ControllerType.Mouse)
                    return;

                bool isKeyboard = false;

                if (controller.type == ControllerType.Keyboard)
                    if (controller.id != 0)
                        return;
                    else
                        isKeyboard = true;

                int defaultDisplay = -1;
                int defaultScreen = -1;
                string defaultProfile = "";

                if (isKeyboard)
                {
                    defaultDisplay = 0;
                    defaultScreen = 0;
                    defaultProfile = LocalUserManager.GetFirstLocalUser().userProfile.fileName;
                }

                var selection = _controllerAssignments.Where(x => x.AssignedDeviceId == controller.id && x.IsKeyboard == isKeyboard);

                if (selection.Count() == 0)
                {
                    _controllerAssignments.Add(new ControllerAssignment()
                    {
                        IsKeyboard = isKeyboard,
                        AssignedDeviceId = controller.id,
                        AssignedDisplay = defaultDisplay,
                        AssignedScreen = defaultScreen,
                        AssignedProfile = ""
                    });

                    Save();
                }

                OnSplitscreenDeviceConnected.Invoke(controller);
            }
            #endregion

            #region Events
            public void OnControllerConnected(Rewired.ControllerStatusChangedEventArgs args)
            {
                EnsureAssignmentExists(args.controller);
            }
            #endregion

            #region Public Accessors
            public void UpdateAssignments(List<ControllerAssignment> newAssignments)
            {

            }
            public bool IsControllerAssigned(Controller controller)
            {
                foreach(ControllerAssignment assignment in ControllerAssignments)
                {
                    if (assignment.AssignedDeviceId == controller.id)
                        if (assignment.IsKeyboard == (controller.type == ControllerType.Keyboard))
                            if(assignment.AssignedDisplay > -1)
                                return true;
                }

                return false;
            }
            #endregion

            #region Definitions
            [Serializable]
            public class ControllerAssignedEvent : UnityEvent<Controller> { }

            [System.Serializable]
            public struct ControllerAssignment
            {
                public string AssignedProfile;
                public int AssignedDeviceId;
                public int AssignedDisplay;
                public int AssignedScreen;
                public bool IsKeyboard;

                public int2 GetId()
                {
                    return new int2(AssignedDisplay, AssignedScreen);
                }
            }

            [System.Serializable]
            private class ConfigDataWrapper
            {
                public ConfigDataWrapper(List<ControllerAssignment> data)
                {
                    IsKeyboard = new bool[data.Count];
                    AssignedProfile = new string[data.Count];
                    AssignedDeviceId = new int[data.Count];
                    AssignedDisplay = new int[data.Count];
                    AssignedScreen = new int[data.Count];

                    for (int e = 0; e < data.Count; e++)
                    {
                        IsKeyboard[e] = data[e].IsKeyboard;
                        AssignedDeviceId[e] = data[e].AssignedDeviceId;
                        AssignedProfile[e] = data[e].AssignedProfile;
                        AssignedDisplay[e] = data[e].AssignedDisplay;
                        AssignedScreen[e] = data[e].AssignedScreen;
                    }
                }

                public bool[] IsKeyboard;
                public string[] AssignedProfile;
                public int[] AssignedDeviceId;
                public int[] AssignedDisplay;
                public int[] AssignedScreen;
            }
            #endregion
        }
        #endregion
    }
}
