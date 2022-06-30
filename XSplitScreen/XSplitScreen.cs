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
using Facepunch.Steamworks;
using UnityEngine.Events;
using DoDad.UI;
using R2API;
using RoR2.UI;

/// <summary>
/// Influenced by iDeathHD's FixedSplitScreen mod
/// https://thunderstore.io/package/xiaoxiao921/FixedSplitscreen/
/// </summary>
namespace DoDad.XSplitScreen
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [R2APISubmoduleDependency(new string[] { "CommandHelper", "LanguageAPI"})]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)]
    public class XSplitScreen : BaseUnityPlugin
    {
        #region Variables
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "DoDad";
        public const string PluginName = "XSplitScreen";
        public const string PluginVersion = "1.0.0";

        private static readonly int MAX_LOCAL_PLAYERS = 4;

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

        private static readonly string MSG_TAG_PLUGIN = "XSS";
        private static readonly string MSG_ERROR_MENU_CONTROLLER_CREATION = "Unable to create XMenuController - only console commands will work.";
        private static readonly string MSG_ERROR_SINGLE_LOCAL_PLAYER = "There is only 1 local player signed in.";
        private static readonly string MSG_ERROR_GENERIC = "Unable to continue.";
        private static readonly string MSG_ERROR_SIGN_IN_FIRST = "Please sign in to a user profile before configuring XSplitScreen.";
        private static readonly string MSG_ERROR_PLAYER_COUNT = "Unable to set player count to requested number.";
        private static readonly string MSG_ERROR_NETWORK_ACTIVE = "XSplitScreen must be configured outside of a lobby.";
        private static readonly string MSG_ERROR_NO_PROFILES = "No profiles detected. Please create a profile before configuring XSplitScreen.";
        private static readonly string MSG_ERROR_INVALID_ARGS = "Invalid arguments. Please type help to see a list of console commands and how to use them.";
        private static readonly string MSG_ERROR_INVALID_PLAYER_RANGE = "A given player index is invalid. Make sure all players are logged in with 'xsplitset'.";
        private static readonly string MSG_ERROR_NO_CONTROLLERS = "Not enough controllers exist to assign to the requested number of players.";
        private static readonly string MSG_INFO_PLAYER_COUNT_CHANGED = "Player count set to: ";
        private static readonly string MSG_INFO_PLAYER_COUNT_CLAMPED = "Number requested is outside the valid range. Player count set to: ";
        private static readonly string MSG_INFO_KEYBOARD_AUTOENABLE = "Enabling keyboard by default. To disable add 'nokb' to 'xsplitplayers'. WARNING: Not fully tested!";
        private static readonly string MSG_INFO_ENTER = "XSplitScreen loaded. Type help to see how to use the 'xsplitset' command.";
        private static readonly string MSG_INFO_EXIT = "Attempting to exit: your controllers may or may not work until you restart the game.";
        private static readonly string MSG_INFO_KEYBOARD_STATUS = "Keyboard and mouse {0}";
        private static readonly string MSG_UI_MAIN_MENU = "Local Players";

        public static XSplitScreen instance;
        public static bool DisableKeyboard
        {
            get
            {
                return instance._disableKeyboard;
            }
            set
            {
                instance._disableKeyboard = value;
                Print(string.Format(MSG_INFO_KEYBOARD_STATUS, value ? "DISABLED" : "ENABLED"));
            }
        }
        public static UnityEvent OnLocalPlayerCount;
        public static bool Enabled => LocalPlayerCount > 1;
        public static int LocalPlayerCount
        {
            set
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

        private static int _localPlayerCount = 1;

        private RoR2.UI.MPEventSystem _lastEventSystem;
        private HGButton _titleButton;
        private GameObject _controllerAssignmentWindow;

        private int _playerBeginIndex = 1;
        private bool _disableKeyboard = false;
        private bool _devMode = true;
        #endregion

        #region Unity Methods
        public void Awake()
        {
            if (instance)
                Destroy(this);

            instance = this;
            LocalPlayerCount = 1;

            Log.Init(Logger);
            CommandHelper.AddToConsoleWhenReady();

            ModMenuManager.CreateReferences();

            TogglePersistentHooks(true);
            Print(MSG_INFO_ENTER);
        }
        private void OnDestroy()
        {
            SetLocalPlayerCount(1);
            DevModeTriggers(false);
            TogglePersistentHooks(false);
            CleanupReferences();
            Print(MSG_INFO_EXIT);
        }
        public void Start()
        {
            DevModeTriggers(true);
        }
        #endregion

        #region Public Methods
        public bool SetLocalPlayerCount(int localPlayerCount, bool forceKeyboardSupport = false)
        {
            if (localPlayerCount < 1 || localPlayerCount > MAX_LOCAL_PLAYERS)
            {
                localPlayerCount = Mathf.Clamp(localPlayerCount, 1, MAX_LOCAL_PLAYERS);
                Print(MSG_INFO_PLAYER_COUNT_CLAMPED + localPlayerCount.ToString());
            }

            if (localPlayerCount == LocalPlayerCount)
                return false;

            bool success = true;

            LocalPlayerCount = localPlayerCount;

            success &= LogInProfiles();

            if(!Enabled)
                success &= ToggleHooks(success);

            success &= ToggleControllers(success);

            if (!success)
                Print(MSG_ERROR_PLAYER_COUNT);

            return success;
        }
        #endregion

        #region Private Methods
        private void DevModeTriggers(bool enable)
        {
            ToggleModMenu(enable);
        }
        private void ToggleModMenu(bool enable) // Call this function when the title screen is loaded, maybe on scene change?
        {
            // _titleButton should be created only when player count is over 1

            if(enable)
            {
                if (_titleButton == null)
                {
                    LanguageAPI.Add(MSG_HOVER_TOKEN, MSG_HOVER_STRING);
                    LanguageAPI.Add(MSG_TITLE_BUTTON_TOKEN, MSG_TITLE_BUTTON_STRING);

                    LanguageAPI.Add(MSG_SPLITSCREEN_ENABLE_HOVER_TOKEN, MSG_SPLITSCREEN_ENABLE_HOVER_STRING);
                    LanguageAPI.Add(MSG_SPLITSCREEN_DISABLE_HOVER_TOKEN, MSG_SPLITSCREEN_DISABLE_HOVER_STRING);
                    LanguageAPI.Add(MSG_SPLITSCREEN_ENABLE_TOKEN, MSG_SPLITSCREEN_ENABLE_STRING);
                    LanguageAPI.Add(MSG_SPLITSCREEN_DISABLE_TOKEN, MSG_SPLITSCREEN_DISABLE_STRING);
                    /*
                     * MSG_SPLITSCREEN_ENABLE_HOVER_TOKEN = "XSPLITSCREEN_ENABLE_HOVER";
        private static readonly string MSG_SPLITSCREEN_ENABLE_HOVER_STRING = "Turn on XSplitScreen";
        private static readonly string MSG_SPLITSCREEN_DISABLE_HOVER_TOKEN = "XSPLITSCREEN_DISABLE_HOVER";
        private static readonly string MSG_SPLITSCREEN_DISABLE_HOVER_STRING = "Turn off XSplitScreen";
        private static readonly string MSG_SPLITSCREEN_ENABLE_TOKEN = "XSPLITSCREEN_ENABLE";
        private static readonly string MSG_SPLITSCREEN_ENABLE_STRING = "Enable";
        private static readonly string MSG_SPLITSCREEN_DISABLE_TOKEN = "XSPLITSCREEN_DISABLE";
        private static readonly string MSG_SPLITSCREEN_DISABLE_STRING = "Disable";
                    */

                    ModScreen userControllerScreen = ModMenuManager.AddScreen(PluginName); // Screen should be created when entering title and destroyed when exiting 
                    Quaternion forward = Quaternion.identity;

                    forward = Quaternion.AngleAxis(20f, Vector3.up);
                    forward *= Quaternion.AngleAxis(-40f, Vector3.right);

                    userControllerScreen.SetCameraPosition(new Vector3(-10.8f, 601.2f, -424.2f), forward);


                    _titleButton = ModMenuManager.CreateHGButton("XSplitScreen", MSG_TITLE_BUTTON_TOKEN, Menu.Title);
                    _titleButton.hoverToken = MSG_HOVER_TOKEN;
                    _titleButton.updateTextOnHover = true;
                    _titleButton.uiClickSoundOverride = "";
                    _titleButton.submitOnPointerUp = true;
                    _titleButton.onClick.AddListener(delegate { RoR2.UI.MainMenu.MainMenuController.instance.SetDesiredMenuScreen(userControllerScreen); });

                    HGButton enableSplitScreenButton = ModMenuManager.CreateHGButton("EnableSplitScreen", MSG_TITLE_BUTTON_TOKEN, Menu.None, userControllerScreen);
                    enableSplitScreenButton.hoverToken = MSG_HOVER_TOKEN;
                    enableSplitScreenButton.updateTextOnHover = true;
                    enableSplitScreenButton.submitOnPointerUp = true;
                    enableSplitScreenButton.uiClickSoundOverride = "";
                    enableSplitScreenButton.onClick.AddListener(OnClickToggleSplitScreen);
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
                Destroy(_titleButton?.gameObject);
                ModMenuManager.CleanupReferences();
            }
        }
        private void CreateControllerAssignmentWindow()
        {
            _controllerAssignmentWindow = ModMenuManager.CreatePopupPanel("ControllerAssignment", ModMenuManager.ActiveScreens[PluginName]);
        }
        private void CleanupReferences()
        {
            ToggleModMenu(false);
        }
        private void TogglePersistentHooks(bool status)
        {
            if(status)
            {
                On.RoR2.UI.MainMenu.BaseMainMenuScreen.OnEnter += BaseMainMenuScreen_OnEnter;
            }
            else
            {
                On.RoR2.UI.MainMenu.BaseMainMenuScreen.OnEnter -= BaseMainMenuScreen_OnEnter;
            }
        }
        private bool ToggleControllers(bool valid = true)
        {
            if (!valid)
                return false;

            if(!Enabled)
            {
                foreach(Controller controller in ReInput.controllers.Controllers)
                {
                    LocalUserManager.GetFirstLocalUser().inputPlayer.controllers.AddController(controller, false);
                }
                

                //ConDeviceStatus(new ConCommandArgs());
                return true;
            }

            //ConDeviceStatus(new ConCommandArgs());

            return true;

            List<Controller> controllerList = new List<Controller>();

            foreach (Controller controller in ReInput.controllers.Controllers)
            {
                controllerList.Add(controller);
            }

            Controller controller1 = controllerList[controllerList.Count - 1];

            if (Enabled)
            {
                if (controllerList.Count - 2 < LocalPlayerCount - 1)
                {
                    Print(MSG_ERROR_NO_CONTROLLERS);
                    return false;
                }

                controllerList.Reverse();

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
            }
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
                On.RoR2.LocalCameraEffect.OnUICameraPreCull += LocalCameraEffect_OnUICameraPreCull; // entire, req
                On.RoR2.UI.CombatHealthBarViewer.SetLayoutHorizontal += CombatHealthBarViewer_SetLayoutHorizontal; // entire, req
                On.RoR2.UI.LoadoutPanelController.UpdateDisplayData += LoadoutPanelController_UpdateDisplayData; // entire, req
                On.RoR2.UI.MPButton.Update += MPButton_Update; // entire, req
                On.RoR2.UI.MPButton.OnPointerClick += MPButton_OnPointerClick; // unaffected, req
                On.RoR2.UI.MPButton.InputModuleIsAllowed += MPButton_InputModuleIsAllowed;
                On.RoR2.UI.MPButton.OnPointerExit += MPButton_OnPointerExit;
                On.RoR2.UI.MPEventSystem.ValidateCurrentSelectedGameobject += MPEventSystem_ValidateCurrentSelectedGameobject; // yes
                On.RoR2.UI.MPInputModule.GetMousePointerEventData += MPInputModule_GetMousePointerEventData; // yes
                On.RoR2.UI.CharacterSelectController.OnEnable += CharacterSelectController_OnEnable; // yes
                On.RoR2.CharacterSelectBarController.PickIcon += CharacterSelectBarController_PickIcon; // yes
            }
            else
            {
                On.RoR2.LocalCameraEffect.OnUICameraPreCull -= LocalCameraEffect_OnUICameraPreCull;
                On.RoR2.UI.CombatHealthBarViewer.SetLayoutHorizontal -= CombatHealthBarViewer_SetLayoutHorizontal;
                On.RoR2.UI.LoadoutPanelController.UpdateDisplayData -= LoadoutPanelController_UpdateDisplayData;
                On.RoR2.UI.MPButton.Update -= MPButton_Update;
                On.RoR2.UI.MPButton.OnPointerClick -= MPButton_OnPointerClick;
                On.RoR2.UI.MPButton.InputModuleIsAllowed -= MPButton_InputModuleIsAllowed;
                On.RoR2.UI.MPButton.OnPointerExit += MPButton_OnPointerExit;
                On.RoR2.UI.MPEventSystem.ValidateCurrentSelectedGameobject -= MPEventSystem_ValidateCurrentSelectedGameobject;
                On.RoR2.UI.MPInputModule.GetMousePointerEventData -= MPInputModule_GetMousePointerEventData;
                On.RoR2.UI.CharacterSelectController.OnEnable -= CharacterSelectController_OnEnable;
                On.RoR2.CharacterSelectBarController.PickIcon -= CharacterSelectBarController_PickIcon;
            }
            
            return true;
        }
        private bool LogInProfiles(UserProfile[] currentProfiles = null)
        {
            if(!LocalUserManager.isAnyUserSignedIn)
            {
                Print(MSG_ERROR_SIGN_IN_FIRST);
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

            }

            LocalUserManager.SetLocalUsers(initializationArray);
            On.RoR2.ViewablesCatalog.AddNodeToRoot -= ViewablesCatalog_AddNodeToRoot;

            return true;
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
        #endregion

        #region Event Handlers / Hooks
        public static void OnClickToggleSplitScreen()
        {
            if(!Enabled)
            {
                XSplitScreen.instance.SetLocalPlayerCount(2);
                instance.CreateControllerAssignmentWindow();
            }
            else
            {
                XSplitScreen.instance.SetLocalPlayerCount(1);
                GameObject.Destroy(instance._controllerAssignmentWindow);
            }
        }
        public static void AddPlayer()
        {
            instance.SetLocalPlayerCount(LocalPlayerCount + 1);
        }
        public static void RemovePlayer()
        {
            instance.SetLocalPlayerCount(LocalPlayerCount - 1);
        }
        
        private void BaseMainMenuScreen_OnEnter(On.RoR2.UI.MainMenu.BaseMainMenuScreen.orig_OnEnter orig, RoR2.UI.MainMenu.BaseMainMenuScreen self, RoR2.UI.MainMenu.MainMenuController mainMenuController)
        {
            orig(self, mainMenuController);

            if (ModMenuManager.ActiveScreens.Count == 0)
                ToggleModMenu(true);
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

            BodyIndex bodyIndex = (bool)((UnityEngine.Object)currentNetworkUser) ? currentNetworkUser.bodyIndexPreference : BodyIndex.None;
            self.SetDisplayData(new RoR2.UI.LoadoutPanelController.DisplayData()
            {
                userProfile = userProfile,
                bodyIndex = bodyIndex
            });
        }
        private void MPButton_OnPointerExit(On.RoR2.UI.MPButton.orig_OnPointerExit orig, RoR2.UI.MPButton self, PointerEventData eventData)
        {
            orig(self, eventData);
            //Print($"OnPointerExit for Input module '{eventData.currentInputModule.gameObject.name}'");
            //Print($"Object {self.name}");
        }
        private bool MPButton_InputModuleIsAllowed(On.RoR2.UI.MPButton.orig_InputModuleIsAllowed orig, RoR2.UI.MPButton self, BaseInputModule inputModule)
        {
            return true;
        }
        private void MPButton_Update(On.RoR2.UI.MPButton.orig_Update orig, RoR2.UI.MPButton self)
        {
            if(!self.eventSystem || self.eventSystem.player == null)
                return;

            if(self.eventSystem.localUser.userProfile.name.Contains("1"))
            {
                Print("Found player 2");
            }
            foreach(RoR2.UI.MPEventSystem eventSystem in RoR2.UI.MPEventSystem.readOnlyInstancesList)
            {
                if(eventSystem && eventSystem.currentSelectedGameObject == self.gameObject && ((eventSystem.player.GetButtonDown(4) && !self.disableGamepadClick) || eventSystem.player.GetButtonDown(14)))
                {
                    _lastEventSystem = eventSystem;
                    self.InvokeClick();
                }
            }

            if (!self.defaultFallbackButton || self.eventSystem.currentInputSource != RoR2.UI.MPEventSystem.InputSource.Gamepad || !(self.eventSystem.currentSelectedGameObject == null || !self.CanBeSelected()))
            {
                return;
            }

            //Print("Fallback for " + self.gameObject.name);
            self.Select();
        }
        private void MPButton_OnPointerClick(On.RoR2.UI.MPButton.orig_OnPointerClick orig, RoR2.UI.MPButton self, PointerEventData eventData)
        {
            _lastEventSystem = (RoR2.UI.MPEventSystem)eventData.currentInputModule.eventSystem;
            orig(self, eventData);
        }
        private void CharacterSelectController_OnEnable(On.RoR2.UI.CharacterSelectController.orig_OnEnable orig, RoR2.UI.CharacterSelectController self)
        {
            orig(self);
            self.GetComponent<RoR2.UI.CursorOpener>().forceCursorForGamePad = true;
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

            foreach (RaycastResult raycast in self.m_RaycastResultCache)
            {
                if(self.useCursor)
                {
                    if(raycast.gameObject != null)
                    {
                        if(raycast.gameObject.GetComponent<RoR2.UI.HGButton>())
                        {
                            foundObject = true;
                            self.eventSystem.SetSelectedGameObject(raycast.gameObject);
                        }
                    }
                }
            }

            if (!foundObject)
                self.eventSystem.SetSelectedGameObject(null);

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
            return;

            if (self.currentSelectedGameObject == null)
                return;

            RoR2.UI.MPButton mpButton = self.currentSelectedGameObject.GetComponent<RoR2.UI.MPButton>();

            if (!mpButton || mpButton.interactable)
            {
                return;
            }

            Print("Setting null: " + self.currentSelectedGameObject?.name);
            self.SetSelectedGameObject(null);
        }
        private void CharacterSelectBarController_PickIcon(On.RoR2.CharacterSelectBarController.orig_PickIcon orig, CharacterSelectBarController self, RoR2.UI.SurvivorIconController newPickedIcon)
        {
            if (self.pickedIcon == newPickedIcon)
                return;

            self.pickedIcon = newPickedIcon;

            CharacterSelectBarController.SurvivorPickInfoUnityEvent onSurvivorPicked = self.onSurvivorPicked;

            if (onSurvivorPicked == null)
                return;

            onSurvivorPicked.Invoke(new CharacterSelectBarController.SurvivorPickInfo()
            {
                localUser = ((RoR2.UI.MPEventSystem)_lastEventSystem).localUser,
                pickedSurvivor = newPickedIcon.survivorDef
            });

        }
        #endregion

        #region Console Commands
        [ConCommand(commandName = "xdevice_status", flags = ConVarFlags.None, helpText = "View who owns what controller")]
        private static void ConDeviceStatus(ConCommandArgs args)
        {
            Print("LocalUsers");
            foreach(LocalUser user in LocalUserManager.localUsersList)
            {
                Print($" - {user.inputPlayer.name} ({(user.userProfile == null ? ("no user") : user.userProfile.name)})");
                foreach (Controller controller in user.inputPlayer.controllers.Controllers)
                {
                    Print($" -- {controller.identifier.controllerType.ToString()} ({controller.identifier.hardwareIdentifier.ToString()}");
                }
            }

            Print("ReInput Players");
            foreach(Player player in ReInput.players.AllPlayers)
            {
                Print($" - {player.name}");
                foreach(Controller controller in player.controllers.Controllers)
                {
                    Print($" -- {controller.identifier.controllerType.ToString()}");
                }
            }
        }
        [ConCommand(commandName = "xsplitswap", flags = ConVarFlags.None, helpText = "Swap players by index. Useage: xsplit # #")]
        private static void ConXSplitSwap(ConCommandArgs args)
        {
            if (!instance.CanConfigure())
                return;

            if(Enabled)
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
        [ConCommand(commandName = "xsplitset", flags = ConVarFlags.None, helpText = "Maximum 4 players. Useage: xsplitset [players] [nokb]")]
        private static void ConXSplitSet(ConCommandArgs args)
        {
            if(!instance.CanConfigure())
                return;

            int requestedLocalPlayerCount = 2;

            if (args.Count > 0)
                requestedLocalPlayerCount = args.GetArgInt(0);

            // keyboard should be enabled or disabled automatically

            instance.SetLocalPlayerCount(requestedLocalPlayerCount);

            return;
            /*
            if (args.Count == 1 || args.Count == 2)
            {
                int requestedLocalPlayerCount = args.GetArgInt(0);

                if (args.Count == 2)
                {
                    string _useKeyboard = args.GetArgString(1);
                    DisableKeyboard = string.Compare(_useKeyboard, "nokb") == 0;
                }
                else
                {
                    Print(MSG_INFO_KEYBOARD_AUTOENABLE);
                    DisableKeyboard = false;
                }

                if (instance.SetLocalPlayerCount(requestedLocalPlayerCount))
                    Print(MSG_INFO_PLAYER_COUNT_CHANGED + requestedLocalPlayerCount.ToString());
                else
                { // Exit on error
                    DisableKeyboard = false;
                    instance.SetLocalPlayerCount(1);
                    Print(MSG_ERROR_PLAYER_COUNT);
                }
            }
            else
            {
                if (args.Count == 0)
                {
                    Print(MSG_INFO_KEYBOARD_AUTOENABLE);
                    DisableKeyboard = false;

                    if (instance.SetLocalPlayerCount(2))
                        Print(MSG_INFO_PLAYER_COUNT_CHANGED + LocalPlayerCount.ToString());
                    else
                    { // Exit on error
                        DisableKeyboard = false;
                        instance.SetLocalPlayerCount(1);
                        Print(MSG_ERROR_PLAYER_COUNT);
                    }

                }
                else
                    Print(MSG_ERROR_INVALID_ARGS);
            }*/
        }
        #endregion

        #region Helpers
        internal static void Print(string msg) 
        { 
            Debug.Log(string.Format("[{0}] {1}", MSG_TAG_PLUGIN, msg));
        }
        private static UserProfile CopyProfile(UserProfile template)
        {
            UserProfile newProfile = new UserProfile();
            SaveSystem.Copy(template, newProfile);
            return newProfile;
        }
        private bool CanConfigure()
        {
            if (PlatformSystems.lobbyManager.isInLobby)
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
    }
}
