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

/// <summary>
/// Influenced by iDeathHD's FixedSplitScreen mod
/// https://thunderstore.io/package/xiaoxiao921/FixedSplitscreen/
/// </summary>
namespace DoDad
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [R2APISubmoduleDependency(new string[] { "CommandHelper", "LanguageAPI" })]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)]
    public class XSplitScreen : BaseUnityPlugin
    {
        #region Variables
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "DoDad";
        public const string PluginName = "XSplitScreen";
        public const string PluginVersion = "1.1.1";

        private static readonly int MAX_LOCAL_PLAYERS = 4;

        private static readonly string MSG_SPLITSCREEN_CONFIG_HEADER_TOKEN = "XSPLITSCREEN_CONFIG_HEADER";
        private static readonly string MSG_SPLITSCREEN_CONFIG_HEADER_STRING = "Under Construction";
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

        public static XSplitScreen instance;
        /// <summary>
        /// Called when the user clicks on Logbook or Singleplayer (should be any scene change)
        /// </summary>
        public static UnityAction DisableMenu;
        public static UnityEvent OnLocalPlayerCount;
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
                if (OnLocalPlayerCount == null)
                    OnLocalPlayerCount = new UnityEvent();

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

        private Coroutine WaitFormenuLoad;
        private RoR2.UI.MPEventSystem _lastEventSystem;
        private HGButton _titleButton;
        private GameObject _controllerAssignmentWindow;

        private int _playerBeginIndex => KeyboardMode ? 1 : 1;//1 : 2;
        private int _retryCounter = 0;
        private bool _disableKeyboard = false;
        private bool _devMode = false;
        private bool _keyboardModeOnly = false;
        private bool _keyboardOptional = false;
        private bool _requestKeyboard = false;
        private bool _enteredMenu = false;
        private bool _mpUpdateOutputThisFrame = false;
        #endregion

        #region Unity Methods
        public void Awake()
        {
            if (instance)
                Destroy(this);

            instance = this;
            //DisableMenu = new UnityAction(XSplitScreen.instance.CleanupReferences);
            LocalPlayerCount = 1;
            OnLocalPlayerCount.AddListener(UpdateCursorStatus);

            Log.Init(Logger);
            CommandHelper.AddToConsoleWhenReady();

            Print(MSG_INFO_ENTER);
        }
        private void OnDestroy()
        {
            SetLocalPlayerCount(1);
            //DevModeTriggers(false);
            TogglePersistentHooks(false);
            CleanupReferences();
            Print(MSG_INFO_EXIT);
        }
        public void Start()
        {
            TogglePersistentHooks(true);
            
            
            
            DevModeTriggers(_devMode);
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
        public bool SetLocalPlayerCount(int localPlayerCount, bool forceKeyboardSupport = false)
        {
            int maxPlayers = MaxPlayers;

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
            OutputPlayerInputToLog();
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
                return success;
            }
        }
        #endregion

        #region Private Methods
        private void DevModeTriggers(bool enable)
        {
            if (!enable)
                return;

            Print("DevMode");
            ToggleModMenu(enable);
            //AddMainMenuCalls();
        }
        private void TogglePersistentHooks(bool status)
        {
            if(status)
            {
                On.RoR2.UI.MainMenu.BaseMainMenuScreen.OnEnter += BaseMainMenuScreen_OnEnter;
                //On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
                SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
                On.RoR2.UI.CursorOpener.Awake += CursorOpener_Awake; // move
            }
            else
            {
                On.RoR2.UI.MainMenu.BaseMainMenuScreen.OnEnter -= BaseMainMenuScreen_OnEnter;
                //On.RoR2.HealthComponent.TakeDamage -= HealthComponent_TakeDamage;
                SceneManager.activeSceneChanged -= SceneManager_activeSceneChanged;
                On.RoR2.UI.CursorOpener.Awake -= CursorOpener_Awake;
            }
        }

        private void CursorOpener_Awake(On.RoR2.UI.CursorOpener.orig_Awake orig, CursorOpener self)
        {
            orig(self);
            self._forceCursorForGamepad = true;
        }

        private void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo damageInfo)
        {
            if(self.body.teamComponent.teamIndex == TeamIndex.Player)
                damageInfo.damage = 0f;

            damageInfo.force = Vector3.zero;
            orig(self, damageInfo);
        }

        private void ToggleModMenu(bool enable) // Call this function when the title screen is loaded, maybe on scene change?
        {
            if(enable)
            {
                if (_titleButton == null)
                {
                    // TODO move this
                    LanguageAPI.Add(MSG_HOVER_TOKEN, MSG_HOVER_STRING);
                    LanguageAPI.Add(MSG_TITLE_BUTTON_TOKEN, MSG_TITLE_BUTTON_STRING);

                    LanguageAPI.Add(MSG_SPLITSCREEN_ENABLE_HOVER_TOKEN, MSG_SPLITSCREEN_ENABLE_HOVER_STRING);
                    LanguageAPI.Add(MSG_SPLITSCREEN_DISABLE_HOVER_TOKEN, MSG_SPLITSCREEN_DISABLE_HOVER_STRING);
                    LanguageAPI.Add(MSG_SPLITSCREEN_ENABLE_TOKEN, MSG_SPLITSCREEN_ENABLE_STRING);
                    LanguageAPI.Add(MSG_SPLITSCREEN_DISABLE_TOKEN, MSG_SPLITSCREEN_DISABLE_STRING);
                    LanguageAPI.Add(MSG_SPLITSCREEN_CONFIG_HEADER_TOKEN, MSG_SPLITSCREEN_CONFIG_HEADER_STRING);

                    UILayer newLayer = ScriptableObject.CreateInstance<UILayer>();
                    newLayer.name = PluginName;
                    newLayer.priority = 10;

                    ModScreen userControllerScreen = ModMenuManager.AddScreen(PluginName, newLayer); 

                    Quaternion forward = Quaternion.identity;

                    forward = Quaternion.AngleAxis(20f, Vector3.up);
                    forward *= Quaternion.AngleAxis(-40f, Vector3.right);

                    userControllerScreen.SetCameraPosition(new Vector3(-10.8f, 601.2f, -424.2f), forward);

                    _titleButton = ModMenuManager.CreateHGButton("XSplitScreen", MSG_TITLE_BUTTON_TOKEN, Menu.Title);
                    _titleButton.hoverToken = MSG_HOVER_TOKEN;
                    _titleButton.updateTextOnHover = true;
                    _titleButton.uiClickSoundOverride = "";
                    _titleButton.submitOnPointerUp = true;
                    _titleButton.onClick.AddListener(OnClickMainTitleButton);

                    HGButton enableSplitScreenButton = ModMenuManager.CreateHGButton("EnableSplitScreen", MSG_TITLE_BUTTON_TOKEN, Menu.None, userControllerScreen);
                    enableSplitScreenButton.hoverToken = MSG_HOVER_TOKEN;
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
                On.RoR2.UI.ScoreboardController.Awake += ScoreboardController_Awake;
                On.RoR2.UI.RuleChoiceController.FindNetworkUser += RuleChoiceController_FindNetworkUser;
                On.RoR2.UI.SurvivorIconController.GetLocalUser += SurvivorIconController_GetLocalUser;
                On.RoR2.UI.SurvivorIconController.Update += SurvivorIconController_Update;
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
                On.RoR2.UI.CharacterSelectController.OnEnable += CharacterSelectController_OnEnable; // no
                On.RoR2.CharacterSelectBarController.PickIcon += CharacterSelectBarController_PickIcon; // yes
            }
            else
            {
                //Print("DISABLING ALL HOOKS");
                On.RoR2.UI.ScoreboardController.Awake -= ScoreboardController_Awake;
                On.RoR2.UI.RuleChoiceController.FindNetworkUser -= RuleChoiceController_FindNetworkUser;
                On.RoR2.UI.SurvivorIconController.GetLocalUser -= SurvivorIconController_GetLocalUser;
                On.RoR2.UI.SurvivorIconController.Update -= SurvivorIconController_Update;
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
                On.RoR2.UI.CharacterSelectController.OnEnable -= CharacterSelectController_OnEnable; 
                On.RoR2.CharacterSelectBarController.PickIcon -= CharacterSelectBarController_PickIcon;
            }
            
            return true;
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
            return _lastEventSystem.localUser;
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
                if(controller.type == ControllerType.Joystick || KeyboardMode)
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
                switch(controller.identifier.controllerType)
                {
                    case ControllerType.Joystick:
                        totalJoysticks++;
                        break;
                    case ControllerType.Keyboard:
                        keyboardMouse++;
                        break;
                    case ControllerType.Mouse:
                        keyboardMouse++;
                        break;
                    default:
                        break;
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
            builder.AppendLine($"MAX_LOCAL_PLAYERS: {MAX_LOCAL_PLAYERS}");
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
        #endregion

        #region Event Handlers / Hooks
        private void OnClickMainTitleButton()
        {
            CreateControllerAssignmentWindow();
            RoR2.UI.MainMenu.MainMenuController.instance.SetDesiredMenuScreen(ModMenuManager.ActiveScreens[PluginName]);
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
        private void UpdateCursorStatus()
        {
            CursorOpener[] openers = GameObject.FindObjectsOfType<CursorOpener>();

            foreach (CursorOpener opener in openers)
            {
                //Print($"Setting {opener.name} to true");
                opener.forceCursorForGamePad = Enabled;
            }

            if (!XSplitScreen.Enabled)
            {
                foreach (MPEventSystem instance in MPEventSystem.instancesList)
                {
                   // Print($"UpdateCursorStatus setting SelectedGameObject to null");
                    instance.SetSelectedGameObject(null);
                }
            }
        }
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
        [ConCommand(commandName = "xlog", flags = ConVarFlags.None, helpText = "Print device status to log")]
        private static void ConXLog(ConCommandArgs args)
        {
            string file = $"{Application.persistentDataPath}/XSplitScreen-Debug.txt";
            System.IO.File.AppendAllText(file, instance.OutputPlayerInputToLog(false));
            Print($"File saved to '{file}");
        }

        [ConCommand(commandName = "xdevice_status", flags = ConVarFlags.None, helpText = "Controller assignment information")]
        private static void ConDeviceStatus(ConCommandArgs args)
        {
            instance.OutputPlayerInputToLog();
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

            instance.SetLocalPlayerCount(requestedLocalPlayerCount);
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
        #endregion
    }
}
