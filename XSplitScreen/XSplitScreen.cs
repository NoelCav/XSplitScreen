using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.UI;
using On.RoR2.UI;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using Rewired;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using BepInEx.Configuration;
using Zio;
using Rewired.UI;
using Rewired.Integration.UnityUI;
using UnityEngine.EventSystems;
using Facepunch.Steamworks;
using UnityEngine.Events;

/// <summary>
/// Influenced by iDeathHD's FixedSplitScreen mod
/// https://thunderstore.io/package/xiaoxiao921/FixedSplitscreen/
/// </summary>
namespace DoDad.XSplitScreen
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [R2APISubmoduleDependency(new string[] { "CommandHelper" })]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)]
    public class XSplitScreen : BaseUnityPlugin
    {
        #region Variables
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "DoDad";
        public const string PluginName = "XSplitScreen";
        public const string PluginVersion = "0.0.1";

        private static readonly int MAX_LOCAL_PLAYERS = 4;

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

        private RoR2.UI.MPEventSystem _lastEventSystem;
        private XMenuController _mainMenuLocalPlayerCountController;

        public static UnityEvent OnLocalPlayerCount;

        private static int _localPlayerCount = 1;
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

        private int _playerBeginIndex => 1;
        private bool _disableKeyboard = false;
        private bool _enabled => LocalPlayerCount > 1;
        private bool _devMode = false;
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

            TogglePersistentHooks(true);


            print(MSG_INFO_ENTER);
        }
        private void OnDestroy()
        {
            SetLocalPlayerCount(1, true);
            TogglePersistentHooks(false);
            CleanupReferences();

            print(MSG_INFO_EXIT);
        }
        #endregion

        #region Public Methods
        public bool SetLocalPlayerCount(int localPlayerCount, bool exit = false)
        {
            if (localPlayerCount < 1 || localPlayerCount > MAX_LOCAL_PLAYERS)
            {
                localPlayerCount = Mathf.Clamp(localPlayerCount, 1, MAX_LOCAL_PLAYERS);
                Print(MSG_INFO_PLAYER_COUNT_CLAMPED + localPlayerCount.ToString());
            }

            bool success = true;
            bool hooked = LocalPlayerCount > 1;

            LocalPlayerCount = localPlayerCount;

            if(!exit)
                success &= LogInProfiles();

            if(LocalPlayerCount == 1 || !hooked)
            {
                success &= ToggleHooks(success);
            }

            success &= ToggleControllers(success);

            return success;
        }
        #endregion

        #region Private Methods
        private void CleanupReferences()
        {
            if(_mainMenuLocalPlayerCountController != null)
            {
                _mainMenuLocalPlayerCountController.Destroy();
            }
        }
        private void TogglePersistentHooks(bool status)
        {
            if(_devMode)
            {
                if (_mainMenuLocalPlayerCountController == null)
                    _mainMenuLocalPlayerCountController = new XMenuController();

                _mainMenuLocalPlayerCountController?.Initialize();
            }

            if(status)
            {
                SceneManager.sceneLoaded += SceneManager_sceneLoaded;

                //On.RoR2.UI.MainMenu.BaseMainMenuScreen.Awake += BaseMainMenuScreen_Awake;
                On.RoR2.UI.MainMenu.BaseMainMenuScreen.OnEnter += BaseMainMenuScreen_OnEnter;
            }
            else
            {
                SceneManager.sceneLoaded -= SceneManager_sceneLoaded;

                //On.RoR2.UI.MainMenu.BaseMainMenuScreen.Awake -= BaseMainMenuScreen_Awake;
                On.RoR2.UI.MainMenu.BaseMainMenuScreen.OnEnter -= BaseMainMenuScreen_OnEnter;
            }
        }
        private bool ToggleControllers(bool valid = true)
        {
            if (!valid)
                return false;

            List<Controller> controllerList = new List<Controller>();

            foreach (Controller controller in ReInput.controllers.Controllers)
            {
                controllerList.Add(controller);
            }

            Controller controller1 = controllerList[controllerList.Count - 1];


            if (_enabled)
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
            else
            {

            }
            /*

            if(index == 0)
            {
                foreach (Controller controller in initializationArray[index].player.controllers.Controllers)
                {
                    controllerList.Add(controller);
                    //ReInput.controllers.RemoveControllerFromAllPlayers(controller);
                    Log.LogWarning($"Found '{controller.identifier.controllerType.ToString()}' attached to '{initializationArray[index].player.name}");
                }

                initializationArray[index].player.controllers.ClearControllersOfType(ControllerType.Joystick);
                initializationArray[index].player.controllers.ClearControllersOfType(ControllerType.Custom);
            }
            else
            {
                Log.LogWarning($"Looking at player '{index.ToString()}' who has '{initializationArray[index].player.controllers.joystickCount.ToString()}' joysticks");

                initializationArray[index].player.controllers.ClearControllersOfType(ControllerType.Joystick);
                initializationArray[index].player.controllers.ClearControllersOfType(ControllerType.Custom);
                initializationArray[index].player.controllers.ClearControllersOfType(ControllerType.Keyboard);
                initializationArray[index].player.controllers.ClearControllersOfType(ControllerType.Mouse);

                if (index + 1 < controllerList.Count)
                {
                    initializationArray[index].player.controllers.AddController(controllerList[index + 1], false);
                    Log.LogWarning($"Reassigned controller '{controllerList[index + 1].identifier.controllerType.ToString()}' to player '{initializationArray[index].player.name}'");
                }
                else
                {
                    Log.LogWarning("WARNING: Not enough controllers for players.");
                }
            }
            */
            return true;
        }
        private bool ToggleHooks(bool valid = true)
        {
            if(!valid)
            {
                Print(MSG_ERROR_GENERIC);
                return false;
            }

            if(_enabled)
            {
                //On.RoR2.NetworkPlayerName.GetResolvedName += NetworkPlayerName_GetResolvedName;
                //On.RoR2.NetworkUser.UpdateUserName += NetworkUser_UpdateUserName;
                //On.RoR2.NetworkUser.GetNetworkPlayerName += NetworkUser_GetNetworkPlayerName;
                On.RoR2.LocalCameraEffect.OnUICameraPreCull += LocalCameraEffect_OnUICameraPreCull;
                On.RoR2.UI.CombatHealthBarViewer.SetLayoutHorizontal += CombatHealthBarViewer_SetLayoutHorizontal;
                On.RoR2.UI.LoadoutPanelController.UpdateDisplayData += LoadoutPanelController_UpdateDisplayData; // yes
                On.RoR2.UI.MPButton.Update += MPButton_Update; // yes
                On.RoR2.UI.MPButton.OnPointerClick += MPButton_OnPointerClick; // yes
                On.RoR2.UI.MPEventSystem.ValidateCurrentSelectedGameobject += MPEventSystem_ValidateCurrentSelectedGameobject; // yes
                On.RoR2.UI.MPInputModule.GetMousePointerEventData += MPInputModule_GetMousePointerEventData; // yes
                On.RoR2.UI.CharacterSelectController.OnEnable += CharacterSelectController_OnEnable; // yes
                On.RoR2.CharacterSelectBarController.PickIcon += CharacterSelectBarController_PickIcon; // yes
            }
            else
            {
                //On.RoR2.NetworkPlayerName.GetResolvedName -= NetworkPlayerName_GetResolvedName;
                //On.RoR2.NetworkUser.UpdateUserName -= NetworkUser_UpdateUserName;
                //On.RoR2.NetworkUser.GetNetworkPlayerName -= NetworkUser_GetNetworkPlayerName;
                On.RoR2.LocalCameraEffect.OnUICameraPreCull -= LocalCameraEffect_OnUICameraPreCull;
                On.RoR2.UI.MainMenu.BaseMainMenuScreen.Awake -= BaseMainMenuScreen_Awake;
                On.RoR2.UI.CombatHealthBarViewer.SetLayoutHorizontal -= CombatHealthBarViewer_SetLayoutHorizontal;
                On.RoR2.UI.LoadoutPanelController.UpdateDisplayData -= LoadoutPanelController_UpdateDisplayData;
                On.RoR2.UI.MPButton.Update -= MPButton_Update;
                On.RoR2.UI.MPButton.OnPointerClick -= MPButton_OnPointerClick;
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
        public void BaseMainMenuScreen_OnEnter()
        {

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

            if (_mainMenuLocalPlayerCountController == null)
                _mainMenuLocalPlayerCountController = new XMenuController();

            _mainMenuLocalPlayerCountController?.Initialize();
        }
        private void BaseMainMenuScreen_Awake(On.RoR2.UI.MainMenu.BaseMainMenuScreen.orig_Awake orig, RoR2.UI.MainMenu.BaseMainMenuScreen self)
        {
            orig(self);
        }
        private string NetworkPlayerName_GetResolvedName(On.RoR2.NetworkPlayerName.orig_GetResolvedName orig, ref NetworkPlayerName self)
        {
            if (!string.IsNullOrEmpty(self.nameOverride))
                return (PlatformSystems.lobbyManager as EOSLobbyManager).GetUserDisplayNameFromProductIdString(self.nameOverride);
            if (PlatformSystems.ShouldUseEpicOnlineSystems)
                return (PlatformSystems.lobbyManager as EOSLobbyManager).GetUserDisplayName(new UserID(self.steamId));
            Client instance = Client.Instance;
            if (instance != null)
                return instance.Friends.GetName(self.steamId.steamValue);
            return "???";
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
        private void MPButton_Update(On.RoR2.UI.MPButton.orig_Update orig, RoR2.UI.MPButton self)
        {
            if(!self.eventSystem || self.eventSystem.player == null)
                return;

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

            foreach (RaycastResult raycast in self.m_RaycastResultCache)
            {
                if(self.useCursor)
                {
                    if(raycast.gameObject != null)
                    {
                        if(raycast.gameObject.GetComponent<RoR2.UI.HGButton>())
                        {
                            self.eventSystem.SetSelectedGameObject(raycast.gameObject);
                        }
                    }
                }
            }

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
        }
        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if (arg0.name == "splash")
                RoR2.Console.instance.SubmitCmd(null, "set_scene title", false);
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
                Print($" - {user.inputPlayer.name}");
                foreach (Controller controller in user.inputPlayer.controllers.Controllers)
                {
                    Print($" -- {controller.identifier.controllerType.ToString()}");
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

            if(instance._enabled)
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
                {
                    DisableKeyboard = false;
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

                }
                else
                    Print(MSG_ERROR_INVALID_ARGS);
            }
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

        #region Definitions
        private class XMenuController
        {
            private XMainMenuPanel _xMainMenu;

            protected static RoR2.UI.UISkinData _mainMenuSkinData;
            protected static RoR2.UI.HGTextMeshProUGUI _mainMenuTextMesh;
            protected static UnityEngine.UI.Image _mainMenuUIBackgroundImage;
            protected static UnityEngine.UI.Image _mainMenuUIBaseOutlineImage;
            protected static UnityEngine.UI.Image _mainMenuUIHoverOutlineImage;

            public void Destroy()
            {
                GameObject.Destroy(_xMainMenu?.gameObject);
            }

            public void Initialize()
            {
                if (_xMainMenu != null)
                    return;

                GameObject singlePlayerButton = GameObject.Find("GenericMenuButton (Singleplayer)");

                if (singlePlayerButton == null)
                    Print(MSG_ERROR_MENU_CONTROLLER_CREATION);

                GameObject newMainMenuObject = CreateUIGameObject("MainMenu");
                newMainMenuObject.transform.SetParent(singlePlayerButton.transform.parent);
                newMainMenuObject.transform.localPosition = newMainMenuObject.transform.localPosition + new Vector3(-10, 0, 0);

                _mainMenuUIBackgroundImage = singlePlayerButton.GetComponent<UnityEngine.UI.Image>();
                _mainMenuTextMesh = singlePlayerButton.GetComponentInChildren<RoR2.UI.HGTextMeshProUGUI>();
                _mainMenuSkinData = singlePlayerButton.GetComponent<RoR2.UI.SkinControllers.BaseSkinController>().skinData;
                _mainMenuUIBaseOutlineImage = singlePlayerButton.GetComponentsInChildren<UnityEngine.UI.Image>()[2];
                _mainMenuUIHoverOutlineImage = singlePlayerButton.GetComponent<RoR2.UI.HGButton>().imageOnHover;

                newMainMenuObject.transform.SetSiblingIndex(1);

                _xMainMenu = newMainMenuObject.AddComponent<XMainMenuPanel>();

                // options

                _xMainMenu.Initialize();
            }

            public static GameObject CreateUIGameObject(string name)
            {
                return new GameObject($"[xElement] {name}", typeof(RectTransform), typeof(CanvasRenderer));
            }

            private class XMainMenuPanel : MonoBehaviour
            {
                private GameObject _titleInfo;
                private XInfoBox _playerCount;
                private XInfoBox _addPlayer;
                private XInfoBox _removePlayer;

                public void OnDestroy()
                {
                    OnLocalPlayerCount.RemoveListener(OnLocalPlayerCountChange);
                }

                public void Initialize()
                {
                    RoR2.UI.UILayerKey layerKey = GameObject.Find("TitleMenu").GetComponent<RoR2.UI.UILayerKey>();

                    _titleInfo = CreateUIGameObject("Title");
                    _titleInfo.transform.SetParent(transform);

                    XInfoBox _infoBox = _titleInfo.AddComponent<XInfoBox>();
                    _infoBox.Initialize();
                    _infoBox.text = MSG_UI_MAIN_MENU;
                    _infoBox.SetSize(_infoBox.GetSize() * new Vector2(0.5f, 1));
                    
                    GameObject newObject = CreateUIGameObject("RemovePlayer");
                    newObject.transform.SetParent(transform);

                    _removePlayer = newObject.AddComponent<XInfoBox>();
                    _removePlayer.Initialize();
                    _removePlayer.SetSize(_removePlayer.GetSize() * new Vector2(0.2f, 1));
                    _removePlayer.gameObject.transform.Translate(new Vector3(120, 0, 0));
                    _removePlayer.text = "-";
                    _removePlayer.Button.onClick.AddListener(RemovePlayer);

                    _removePlayer.EnableButton(layerKey);

                    newObject = CreateUIGameObject("PlayerCount");
                    newObject.transform.SetParent(transform);

                    _playerCount = newObject.AddComponent<XInfoBox>();
                    _playerCount.Initialize();
                    _playerCount.SetSize(_playerCount.GetSize() * new Vector2(0.2f, 1));
                    _playerCount.gameObject.transform.Translate(new Vector3(180, 0, 0));

                    OnLocalPlayerCount.AddListener(OnLocalPlayerCountChange);
                    OnLocalPlayerCountChange();
                    
                    newObject = CreateUIGameObject("AddPlayer");
                    newObject.transform.SetParent(transform);
                    
                    _addPlayer = newObject.AddComponent<XInfoBox>();
                    _addPlayer.Initialize();
                    _addPlayer.SetSize(_addPlayer.GetSize() * new Vector2(0.2f, 1));
                    _addPlayer.gameObject.transform.Translate(new Vector3(240, 0, 0));
                    _addPlayer.text = "+";
                    _addPlayer.Button.onClick.AddListener(AddPlayer);

                    _addPlayer.EnableButton(layerKey);
                }

                private void OnLocalPlayerCountChange()
                {
                    Print("Setting local player count");
                    _playerCount.text = LocalPlayerCount.ToString();
                }
            }
            private class XInfoBox : MonoBehaviour
            {
                public RoR2.UI.HGButton Button;

                public string text
                {
                    get 
                    { 
                        return _textMesh ? _textMesh.text : ""; 
                    }
                    set
                    {
                        if (_textMesh)
                            _textMesh.SetText(value);
                    }
                }

                private RoR2.UI.HGTextMeshProUGUI _textMesh;
                private UnityEngine.UI.Image _hoverImage;
                private UnityEngine.UI.Image _baseImage;

                public void EnableButton(RoR2.UI.UILayerKey layerKey)
                {
                    Button.disablePointerClick = false;
                    Button.disableGamepadClick = false;
                    Button.allowAllEventSystems = true;
                    Button.showImageOnHover = true;
                    Button.submitOnPointerUp = true;
                    Button.requiredTopLayer = layerKey;
                }
                public void Initialize()
                {
                    RectTransform rectTransform = GetComponent<RectTransform>();
                    UnityEngine.UI.Image image = gameObject.AddComponent<UnityEngine.UI.Image>();

                    // TODO support multiple languages
                    GameObject textMeshObject = CreateUIGameObject("InfoBox");
                    textMeshObject.transform.SetParent(transform);

                    GameObject hoverOutlineObject = CreateUIGameObject("HoverOutline");
                    hoverOutlineObject.transform.SetParent(transform);
                    _hoverImage = hoverOutlineObject.AddComponent<UnityEngine.UI.Image>();
                    
                    GameObject baseOutlineObject = CreateUIGameObject("BaseOutline");
                    baseOutlineObject.transform.SetParent(transform);
                    _baseImage = baseOutlineObject.AddComponent<UnityEngine.UI.Image>();

                    Canvas hoverCanvas = hoverOutlineObject.AddComponent<Canvas>();
                    RoR2.RefreshCanvasDrawOrder refreshCanvas = hoverOutlineObject.AddComponent<RoR2.RefreshCanvasDrawOrder>();

                    hoverCanvas.overrideSorting = true;
                    hoverCanvas.scaleFactor = 1.3333f;
                    hoverCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                    refreshCanvas.canvas = hoverCanvas;

                    _textMesh = textMeshObject.AddComponent<RoR2.UI.HGTextMeshProUGUI>();

                    gameObject.AddComponent<RoR2.UI.MPEventSystemLocator>();

                    Button = gameObject.AddComponent<RoR2.UI.HGButton>();

                    RoR2.UI.SkinControllers.ButtonSkinController skinController = gameObject.AddComponent<RoR2.UI.SkinControllers.ButtonSkinController>();

                    _textMesh.alignment = _mainMenuTextMesh.alignment;
                    _textMesh.enableAutoSizing = _mainMenuTextMesh.enableAutoSizing;
                    _textMesh.enableWordWrapping = _mainMenuTextMesh.enableWordWrapping;
                    _textMesh.rectTransform.anchorMin = _mainMenuTextMesh.rectTransform.anchorMin;
                    _textMesh.rectTransform.anchorMax = _mainMenuTextMesh.rectTransform.anchorMax;
                    _textMesh.rectTransform.offsetMin = _mainMenuTextMesh.rectTransform.offsetMin;
                    _textMesh.rectTransform.offsetMax = _mainMenuTextMesh.rectTransform.offsetMax;

                    skinController.skinData = _mainMenuSkinData;
                    skinController.OnSkinUI();

                    image.sprite = Instantiate(_mainMenuUIBackgroundImage.sprite);
                    image.type = _mainMenuUIBackgroundImage.type;
                    image.color = _mainMenuUIBackgroundImage.color;
                    image.raycastTarget = true;

                    _hoverImage.sprite = Instantiate(_mainMenuUIHoverOutlineImage.sprite);
                    _hoverImage.type = _mainMenuUIHoverOutlineImage.type;
                    _hoverImage.color = new UnityEngine.Color(_mainMenuUIHoverOutlineImage.color.r, _mainMenuUIHoverOutlineImage.color.b, _mainMenuUIHoverOutlineImage.color.g, 0);
                    _hoverImage.raycastTarget = false;

                    _baseImage.sprite = Instantiate(_mainMenuUIBaseOutlineImage.sprite);
                    _baseImage.type = _mainMenuUIBaseOutlineImage.type;
                    _baseImage.color = new UnityEngine.Color(_mainMenuUIBaseOutlineImage.color.r, _mainMenuUIBaseOutlineImage.color.b, _mainMenuUIBaseOutlineImage.color.g, 0);
                    _baseImage.raycastTarget = false;

                    Button.disableGamepadClick = true;
                    Button.disablePointerClick = true;
                    Button.interactable = true;
                    Button.image = image;
                    Button.imageOnHover = _hoverImage;

                    rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _mainMenuUIBackgroundImage.rectTransform.rect.width);
                    rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _mainMenuUIBackgroundImage.rectTransform.rect.height);
                }
                public Vector2 GetSize()
                {
                    RectTransform rectTransform = GetComponent<RectTransform>();
                    return rectTransform.sizeDelta;
                }
                public void SetSize(Vector2 size)
                {
                    RectTransform rectTransform = GetComponent<RectTransform>();

                    rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                    rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
                }
            }
            private class XButton : RoR2.UI.HGButton
            {
                public XButton(GameObject template)
                {
                    previousState = SelectionState.Disabled;

                    //textMeshProUGui = 
                }

                public void Initialize(GameObject template)
                {
                    GameObject buttonTextObject = new GameObject();
                    GameObject baseOutlineObject = new GameObject();
                    GameObject hoverOutlineObject = new GameObject();

                    buttonTextObject.transform.parent = transform;
                    baseOutlineObject.transform.parent = transform;
                    hoverOutlineObject.transform.parent = transform;

                    UnityEngine.UI.Image xButtonImage = gameObject.AddComponent<UnityEngine.UI.Image>();
                    RoR2.UI.MPEventSystemLocator xButtonLocator = gameObject.AddComponent<RoR2.UI.MPEventSystemLocator>();
                    RoR2.UI.SkinControllers.ButtonSkinController xButtonSkinController = gameObject.AddComponent<RoR2.UI.SkinControllers.ButtonSkinController>();

                    RoR2.UI.HGTextMeshProUGUI buttonTextComponent = buttonTextObject.AddComponent<RoR2.UI.HGTextMeshProUGUI>();

                    RoR2.UI.HGTextMeshProUGUI baseOutlineComponent = buttonTextObject.AddComponent<RoR2.UI.HGTextMeshProUGUI>();

                    RoR2.UI.HGTextMeshProUGUI hoverOutlineComponent1 = buttonTextObject.AddComponent<RoR2.UI.HGTextMeshProUGUI>();

                    previousState = SelectionState.Disabled;
                }
            }
        }

        #endregion
    }
}
