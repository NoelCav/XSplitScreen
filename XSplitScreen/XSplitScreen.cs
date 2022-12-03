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
using System.Globalization;

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
            ToggleConditionalHooks(false);
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
        // TODO remove this once we're certain it's not needed
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
                foreach (LocalUser user in LocalUserManager.readOnlyLocalUsersList)
                {
                    user.ApplyUserProfileBindingsToRewiredPlayer();
                }
                //ToggleUIListeners(false); // as below
                //ScreenOnExit(); // Disabled 11/27/22 - Scene switch bug
            }
        }
        private void ScreenOnEnter()
        {
            ToggleConditionalHooks();

            //if (WaitForMenu != null) // Disabled 11/27/22 - Scene switch bug
            //    StopCoroutine(WaitForMenuRoutine); // Disabled 11/27/22 - Scene switch bug

            //WaitForMenuRoutine = StartCoroutine(WaitForMenu()); // Disabled 11/27/22 - Scene switch bug
        }
        private void ScreenOnExit()
        {
            ToggleConditionalHooks();
        }
        #endregion

        #region Splitscreen
        private void ToggleSplitScreenHooks(bool status)
        {
            input.UpdateCurrentEventSystem(LocalUserManager.GetFirstLocalUser().eventSystem);
            input.UpdateCurrentEventSystem(LocalUserManager.GetFirstLocalUser().eventSystem, true);

            if (status)
            {
                On.RoR2.UI.CursorOpener.Awake += CursorOpener_Awake;

                On.RoR2.UI.MPButton.Update += MPButton_Update;
                On.RoR2.UI.MPButton.OnPointerClick += MPButton_OnPointerClick;
                On.RoR2.UI.MPButton.InputModuleIsAllowed += MPButton_InputModuleIsAllowed;
                On.RoR2.UI.MPButton.Awake += MPButton_Awake;
                On.RoR2.UI.MPButton.CanBeSelected += MPButton_CanBeSelected;

                On.RoR2.UI.MPInput.CenterCursor += MPInput_CenterCursor;
                On.RoR2.UI.MPInput.Update += MPInput_Update;

                //On.RoR2.UI.MPInputModule.GetMousePointerEventData += MPInputModule_GetMousePointerEventData; // Moved to conditional

                //On.RoR2.UI.MPEventSystem.ValidateCurrentSelectedGameobject += MPEventSystem_ValidateCurrentSelectedGameobject; // Moved to conditional

                On.RoR2.UI.CharacterSelectController.Update += CharacterSelectController_Update;

                On.RoR2.CharacterSelectBarController.PickIcon += CharacterSelectBarController_PickIcon;

                On.RoR2.UI.SurvivorIconController.Update += SurvivorIconController_Update;
                On.RoR2.UI.SurvivorIconController.UpdateAvailability += SurvivorIconController_UpdateAvailability;

                // TODO fix vote counting
                On.RoR2.UI.RuleChoiceController.FindNetworkUser += RuleChoiceController_FindNetworkUser;

                On.RoR2.UI.LoadoutPanelController.UpdateDisplayData += LoadoutPanelController_UpdateDisplayData;

                On.RoR2.RunCameraManager.Update += RunCameraManager_Update;

                On.RoR2.LocalCameraEffect.OnUICameraPreCull += LocalCameraEffect_OnUICameraPreCull;

                On.RoR2.UI.CombatHealthBarViewer.SetLayoutHorizontal += CombatHealthBarViewer_SetLayoutHorizontal;

                On.RoR2.NetworkUser.UpdateUserName += NetworkUser_UpdateUserName;
                On.RoR2.NetworkUser.GetNetworkPlayerName += NetworkUser_GetNetworkPlayerName;

                On.RoR2.PlayerCharacterMasterController.GetDisplayName += PlayerCharacterMasterController_GetDisplayName;

                On.RoR2.UI.Nameplate.LateUpdate += Nameplate_LateUpdate;

                On.RoR2.InputBindingDisplayController.Refresh += InputBindingDisplayController_Refresh; // HERE

                On.RoR2.ColorCatalog.GetMultiplayerColor += ColorCatalog_GetMultiplayerColor;

                On.RoR2.UI.BaseSettingsControl.GetCurrentUserProfile += BaseSettingsControl_GetCurrentUserProfile;

                On.RoR2.UI.ProfileNameLabel.LateUpdate += ProfileNameLabel_LateUpdate;

                //On.RoR2.SubjectChatMessage.ConstructChatString += SubjectChatMessage_ConstructChatString;

                /* // Controller navigation requires layer keys
                On.RoR2.SubjectChatMessage.GetSubjectName += SubjectChatMessage_GetSubjectName;

                On.RoR2.UI.InputSourceFilter.Refresh += InputSourceFilter_Refresh;

                On.RoR2.UI.HGGamepadInputEvent.Update += HGGamepadInputEvent_Update;
                */

                // UILayerKey.topLayerRepresentations and queries should probably be handled by this plugin. 
                // MPButton_CanBeSelected is a quick hack to get things working but it makes layer keys useless
                //On.RoR2.UI.UILayerKey.RefreshTopLayerForEventSystem += UILayerKey_RefreshTopLayerForEventSystem; 
            }
            else
            {
                On.RoR2.UI.CursorOpener.Awake -= CursorOpener_Awake;

                On.RoR2.UI.MPButton.Update -= MPButton_Update;
                On.RoR2.UI.MPButton.OnPointerClick -= MPButton_OnPointerClick;
                On.RoR2.UI.MPButton.InputModuleIsAllowed -= MPButton_InputModuleIsAllowed;
                On.RoR2.UI.MPButton.Awake -= MPButton_Awake;
                On.RoR2.UI.MPButton.CanBeSelected -= MPButton_CanBeSelected;

                On.RoR2.UI.MPInput.CenterCursor -= MPInput_CenterCursor;
                On.RoR2.UI.MPInput.Update -= MPInput_Update;

                On.RoR2.UI.MPInputModule.GetMousePointerEventData -= MPInputModule_GetMousePointerEventData; // Moved to conditional

                //On.RoR2.UI.MPEventSystem.ValidateCurrentSelectedGameobject -= MPEventSystem_ValidateCurrentSelectedGameobject; // Moved to conditional

                On.RoR2.UI.CharacterSelectController.Update -= CharacterSelectController_Update;

                On.RoR2.CharacterSelectBarController.PickIcon -= CharacterSelectBarController_PickIcon;

                On.RoR2.UI.SurvivorIconController.Update -= SurvivorIconController_Update;
                On.RoR2.UI.SurvivorIconController.UpdateAvailability -= SurvivorIconController_UpdateAvailability;

                On.RoR2.UI.RuleChoiceController.FindNetworkUser -= RuleChoiceController_FindNetworkUser;

                On.RoR2.UI.LoadoutPanelController.UpdateDisplayData -= LoadoutPanelController_UpdateDisplayData;

                On.RoR2.RunCameraManager.Update -= RunCameraManager_Update;

                On.RoR2.LocalCameraEffect.OnUICameraPreCull -= LocalCameraEffect_OnUICameraPreCull;

                On.RoR2.UI.CombatHealthBarViewer.SetLayoutHorizontal -= CombatHealthBarViewer_SetLayoutHorizontal;

                On.RoR2.NetworkUser.UpdateUserName -= NetworkUser_UpdateUserName;
                On.RoR2.NetworkUser.GetNetworkPlayerName -= NetworkUser_GetNetworkPlayerName;

                On.RoR2.PlayerCharacterMasterController.GetDisplayName -= PlayerCharacterMasterController_GetDisplayName;

                On.RoR2.UI.Nameplate.LateUpdate -= Nameplate_LateUpdate;

                //On.RoR2.SubjectChatMessage.ConstructChatString -= SubjectChatMessage_ConstructChatString;

                On.RoR2.SubjectChatMessage.GetSubjectName -= SubjectChatMessage_GetSubjectName;

                //On.RoR2.UI.InputSourceFilter.Refresh -= InputSourceFilter_Refresh; // LAST

                On.RoR2.ColorCatalog.GetMultiplayerColor -= ColorCatalog_GetMultiplayerColor;
                
                On.RoR2.InputBindingDisplayController.Refresh -= InputBindingDisplayController_Refresh;

                On.RoR2.UI.BaseSettingsControl.GetCurrentUserProfile -= BaseSettingsControl_GetCurrentUserProfile;

                On.RoR2.UI.ProfileNameLabel.LateUpdate += ProfileNameLabel_LateUpdate;

                /*
                On.RoR2.UI.HGGamepadInputEvent.Update -= HGGamepadInputEvent_Update;
                */

                //On.RoR2.UI.GameEndReportPanelController.SetPlayerInfo -= 
                //On.RoR2.UI.UILayerKey.RefreshTopLayerForEventSystem -= UILayerKey_RefreshTopLayerForEventSystem;
            }

            ToggleConditionalHooks();
        }
        private void ToggleConditionalHooks(bool exit = false)
        {
            bool status = false;

            if(configuration != null && XSplitScreenMenu.instance != null)
                status = configuration.enabled || MainMenuController.instance.currentMenuScreen == XSplitScreenMenu.instance;

            if (exit)
                status = false;

            if (status)
            {
                On.RoR2.UI.MPInputModule.GetMousePointerEventData += MPInputModule_GetMousePointerEventData;

                On.RoR2.UI.MPControlHelper.InputModuleIsAllowed += MPControlHelper_InputModuleIsAllowed;
                On.RoR2.UI.MPControlHelper.OnPointerClick += MPControlHelper_OnPointerClick;

                On.RoR2.UI.MPEventSystem.ValidateCurrentSelectedGameobject += MPEventSystem_ValidateCurrentSelectedGameobject;
            }
            else
            {
                On.RoR2.UI.MPInputModule.GetMousePointerEventData -= MPInputModule_GetMousePointerEventData;

                On.RoR2.UI.MPControlHelper.InputModuleIsAllowed -= MPControlHelper_InputModuleIsAllowed;
                On.RoR2.UI.MPControlHelper.OnPointerClick -= MPControlHelper_OnPointerClick;

                On.RoR2.UI.MPEventSystem.ValidateCurrentSelectedGameobject -= MPEventSystem_ValidateCurrentSelectedGameobject;
            }
        }

        #region UI Hooks
        // TODO Replace hooks with IL hooks where appropriate
        private void CursorOpener_Awake(On.RoR2.UI.CursorOpener.orig_Awake orig, CursorOpener self)
        {
            // Force the use of cursors for all gamepads

            orig(self);
            self._forceCursorForGamepad = true;
        }
        private bool MPControlHelper_InputModuleIsAllowed(On.RoR2.UI.MPControlHelper.orig_InputModuleIsAllowed orig, ref MPControlHelper self, BaseInputModule inputModule)
        {
            return true;
        }
        private void MPControlHelper_OnPointerClick(On.RoR2.UI.MPControlHelper.orig_OnPointerClick orig, ref MPControlHelper self, PointerEventData eventData, Action<PointerEventData> baseMethod)
        {
            // On click
            Log.LogDebug($"MPControlHelper_OnPointerClick: '{eventData.currentInputModule.name}'");
            input.UpdateCurrentEventSystem(eventData.currentInputModule.eventSystem);

            orig(ref self, eventData, baseMethod);
        }
        private void MPButton_OnSubmit()
        {
            // TODO - Unused -
            Log.LogDebug($"MPButton_OnSubmit");
        }
        private void MPButton_Awake(On.RoR2.UI.MPButton.orig_Awake orig, MPButton self)
        {
            // TODO - Unused -
            //self.onClick.AddListener(MPButton_OnSubmit);
            self.disableGamepadClick = false;
            self.disablePointerClick = false;
            orig(self);
        }
        private void MPButton_Update(On.RoR2.UI.MPButton.orig_Update orig, RoR2.UI.MPButton self)
        {
            // Remove the check for 'disableGamepadClick'
            // Remove fallback button setting

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
            // On click

            input.UpdateCurrentEventSystem(eventData.currentInputModule.eventSystem);
            orig(self, eventData);
        }
        private bool MPButton_InputModuleIsAllowed(On.RoR2.UI.MPButton.orig_InputModuleIsAllowed orig, RoR2.UI.MPButton self, BaseInputModule inputModule)
        {
            // Allow any input module
            return true;
        }
        private bool MPButton_CanBeSelected(On.RoR2.UI.MPButton.orig_CanBeSelected orig, MPButton self)
        {
            // Remove top layer requirement

            if (!self.gameObject.activeInHierarchy)
                return false;

            return true;
        }
        private void MPInput_CenterCursor(On.RoR2.UI.MPInput.orig_CenterCursor orig, MPInput self)
        {
            // Center each cursor on the assigned screen

            Assignment? assignment = configuration.GetAssignmentByPlayerId(self.playerId - 1);

            if (assignment.HasValue)
            {
                Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;

                float halfWidth = center.x * 0.5f;
                float halfHeight = center.y * 0.5f;

                if (assignment.Value.position.x > 1)
                    center.y -= halfHeight;
                else if (assignment.Value.position.x < 1)
                    center.y += halfHeight;

                if (assignment.Value.position.y > 1)
                    center.x += halfWidth;
                else if (assignment.Value.position.y < 1)
                    center.x -= halfWidth;

                self.internalMousePosition = center;
            }
        }
        private void MPInput_Update(On.RoR2.UI.MPInput.orig_Update orig, MPInput self)
        {
            if (!self.eventSystem.isCursorVisible)
                return;

            float width = Screen.width;
            float height = Screen.height;

            self.internalScreenPositionDelta = Vector2.zero;

            if (self.eventSystem.currentInputSource == MPEventSystem.InputSource.MouseAndKeyboard)
            {
                if (Application.isFocused)
                {
                    if (Vector3.SqrMagnitude(UnityEngine.Input.mousePosition - (Vector3)self.internalMousePosition) > 0.1f)
                    {
                        input.UpdateCurrentEventSystem(self.eventSystem, true);
                    }

                    self.internalMousePosition = UnityEngine.Input.mousePosition;
                }
            }
            else
            {
                float num = Mathf.Min(width / 1920f, height / 1080f);

                Vector2 vector2 = new Vector2(self.player.GetAxis(23), self.player.GetAxis(24));

                float magnitude = vector2.magnitude;

                self.stickMagnitude = Mathf.Min(Mathf.MoveTowards(self.stickMagnitude, magnitude, self.cursorAcceleration * Time.unscaledDeltaTime), magnitude);

                float stickMagnitude = self.stickMagnitude;

                if (self.eventSystem.isHovering)
                    stickMagnitude *= self.cursorStickyModifier;

                self.internalScreenPositionDelta = (magnitude == 0.0 ? Vector2.zero : vector2 * (stickMagnitude / magnitude)) * Time.unscaledDeltaTime * (1920f * self.cursorScreenSpeed * num);
                
                Vector3 delta = self.internalMousePosition + self.internalScreenPositionDelta;

                if (Vector3.SqrMagnitude(delta - (Vector3)self.internalMousePosition) > 0.1f)
                {
                    input.UpdateCurrentEventSystem(self.eventSystem, true);
                }

                self.internalMousePosition = delta;
            }

            self.internalMousePosition.x = Mathf.Clamp(self.internalMousePosition.x, 0.0f, width);
            self.internalMousePosition.y = Mathf.Clamp(self.internalMousePosition.y, 0.0f, height);
            self._scrollDelta = new Vector2(0.0f, self.player.GetAxis(26));
        }
        private object MPInputModule_GetMousePointerEventData(On.RoR2.UI.MPInputModule.orig_GetMousePointerEventData orig, RoR2.UI.MPInputModule self, int playerId, int mouseIndex)
        {
            // Cycle through raycasts to allow input field to be selected
            // Enable MPToggle click

            IMouseInputSource mouseInputSource = self.GetMouseInputSource(playerId, mouseIndex);

            if (mouseInputSource == null)
            {
                //if (playerId > 0)
                //    Log.LogDebug($"MPInputModule_GetMousePointerEventData: '{playerId}' mouseInputSource is null");
                return null;
            }

            PlayerPointerEventData data1;

            int num = self.GetPointerData(playerId, mouseIndex, -1, out data1, true, PointerEventType.Mouse) ? 1 : 0;

            data1.Reset();

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
                        TMPro.TMP_InputField input = raycast.gameObject.GetComponent<TMP_InputField>();
                        MPButton mpButton = raycast.gameObject.GetComponent<MPButton>();
                        HGButton hgButton = raycast.gameObject.transform?.parent.gameObject.GetComponent<HGButton>();
                        MPToggle mpToggle = raycast.gameObject.transform?.parent.gameObject.GetComponent<MPToggle>();

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
                                    Debug.Log($"MPInputModule_GetMousePointerEventData: '{playerId}' selecting '{raycast.gameObject.transform.parent.gameObject}' p2");

                                focusObject = raycast.gameObject.transform.parent.gameObject;
                                priority = 2;
                            }
                        }
                        if (mpButton != null)
                        {
                            if (priority < 1)
                            {
                                if (logOutput)
                                    Debug.Log($"MPInputModule_GetMousePointerEventData: '{playerId}' selecting '{raycast.gameObject}' p1");

                                focusObject = raycast.gameObject;
                                priority = 1;
                            }
                        }
                        if (mpToggle != null)
                        {
                            if (priority < 1)
                            {
                                if (logOutput)
                                    Debug.Log($"MPInputModule_GetMousePointerEventData: '{playerId}' selecting '{raycast.gameObject.transform.parent.gameObject}' p1");

                                focusObject = raycast.gameObject.transform.parent.gameObject;
                                priority = 1;
                            }
                        }
                    }
                }
            }

            if (self.eventSystem.currentSelectedGameObject != null && focusObject == null)
                if (self.eventSystem.currentSelectedGameObject.GetComponent<TMPro.TMP_InputField>() != null)
                    focusObject = self.eventSystem.currentSelectedGameObject;

            //if (focusObject is null)
            //    Log.LogDebug($"MPInputModule_GetMousePointerEventData: '{playerId}' focusObject is null");

            MPToggle toggle = focusObject?.GetComponent<MPToggle>();

            if (toggle)
            {
                MPEventSystemLocator locator = toggle.GetComponent<MPEventSystemLocator>();

                if (locator?.eventSystem)
                {
                    if (locator.eventSystem.player.GetButtonDown(4) || locator.eventSystem.player.GetButtonDown(14))
                    {
                        input.UpdateCurrentEventSystem(locator.eventSystem);
                        toggle.Set(!toggle.isOn);
                    }
                }
            }

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
        private void SurvivorIconController_UpdateAvailability(On.RoR2.UI.SurvivorIconController.orig_UpdateAvailability orig, SurvivorIconController self)
        {
            // Combine and enable entitlements for each profile

            self.SetBoolAndMarkDirtyIfChanged(ref self.survivorIsUnlocked, SurvivorCatalog.SurvivorIsUnlockedOnThisClient(self.survivorIndex));
            self.SetBoolAndMarkDirtyIfChanged(ref self.survivorRequiredExpansionEnabled, self.survivorDef.CheckRequiredExpansionEnabled((NetworkUser)null));

            bool hasEntitlement = false;

            foreach (LocalUser user in LocalUserManager.readOnlyLocalUsersList)
            {
                hasEntitlement |= self.survivorDef.CheckUserHasRequiredEntitlement(user);
            }

            self.SetBoolAndMarkDirtyIfChanged(ref self.survivorRequiredEntitlementAvailable, hasEntitlement);
            self.survivorIsAvailable = self.survivorIsUnlocked && self.survivorRequiredExpansionEnabled && self.survivorRequiredEntitlementAvailable;
        }
        private void MPEventSystem_ValidateCurrentSelectedGameobject(On.RoR2.UI.MPEventSystem.orig_ValidateCurrentSelectedGameobject orig, RoR2.UI.MPEventSystem self)
        {
            // Disabled
            return;

            // Remove input source check
            // Remove navigation mode check

            if (!self.currentSelectedGameObject)
                return;

            MPButton component = self.currentSelectedGameObject.GetComponent<MPButton>();

            if (!component || component.CanBeSelected())
                return;

            self.SetSelectedGameObject(null);
        }
        private void CharacterSelectController_Update(On.RoR2.UI.CharacterSelectController.orig_Update orig, CharacterSelectController self)
        {
            // Update the local user to the player who last interacted with the UI

            if (input.currentButtonEventSystem)
                self.localUser = input.currentButtonEventSystem.localUser;

            orig(self);
        }
        private void CharacterSelectBarController_PickIcon(On.RoR2.CharacterSelectBarController.orig_PickIcon orig, CharacterSelectBarController self, RoR2.UI.SurvivorIconController newPickedIcon)
        {
            // Use input.currentEventSystem

            if (self.pickedIcon == newPickedIcon)
                return;

            self.pickedIcon = newPickedIcon;

            CharacterSelectBarController.SurvivorPickInfoUnityEvent onSurvivorPicked = self.onSurvivorPicked;
            if (onSurvivorPicked == null)
                return;

            LocalUser user = input.currentButtonEventSystem?.localUser;

            if (user is null)
                return;

            onSurvivorPicked.Invoke(new CharacterSelectBarController.SurvivorPickInfo()
            {
                localUser = user,
                pickedSurvivor = newPickedIcon.survivorDef
            });
        }
        private void ViewablesCatalog_AddNodeToRoot(On.RoR2.ViewablesCatalog.orig_AddNodeToRoot orig, ViewablesCatalog.Node node)
        {
            // Stop console spam

            node.SetParent(ViewablesCatalog.rootNode);

            foreach (ViewablesCatalog.Node descendant in node.Descendants())
                if (!ViewablesCatalog.fullNameToNodeMap.ContainsKey(descendant.fullName))
                    ViewablesCatalog.fullNameToNodeMap.Add(descendant.fullName, descendant);
        }
        private void UILayerKey_RefreshTopLayerForEventSystem(On.RoR2.UI.UILayerKey.orig_RefreshTopLayerForEventSystem orig, MPEventSystem eventSystem)
        {
            int num = int.MinValue;

            UILayerKey uiLayerKey1 = null;
            UILayerKey layerRepresentation = UILayerKey.topLayerRepresentations[eventSystem];

            List<UILayerKey> instancesList = InstanceTracker.GetInstancesList<UILayerKey>();

            //bool debug = eventSystem.player.id == 2;

            for (int index = 0; index < instancesList.Count; ++index)
            {
                UILayerKey uiLayerKey2 = instancesList[index];

                //if (debug)
                //    Log.LogDebug($"UILayerKey_RefreshTopLayerForEventSystem: Checking '{uiLayerKey2}'");

                if (!(uiLayerKey2.eventSystemLocator.eventSystem != eventSystem) && uiLayerKey2.layer.priority > num)
                {

                    uiLayerKey1 = uiLayerKey2;
                    num = uiLayerKey2.layer.priority;
                }
            }

            if (uiLayerKey1 == layerRepresentation)
                return;

            if (layerRepresentation)
            {
                layerRepresentation.onEndRepresentTopLayer.Invoke();
                layerRepresentation.representsTopLayer = false;
                //Log.LogDebug($"UILayerKey_RefreshTopLayerForEventSystem: '{layerRepresentation}' representation ended for '{eventSystem?.name}'");
            }

            UILayerKey uiLayerKey3 = UILayerKey.topLayerRepresentations[eventSystem] = uiLayerKey1;

            if (!uiLayerKey3)
                return;

            uiLayerKey3.representsTopLayer = true;
            uiLayerKey3.onBeginRepresentTopLayer.Invoke();
            //Log.LogDebug($"UILayerKey_RefreshTopLayerForEventSystem: '{layerRepresentation}' representation began for '{eventSystem?.name}'");
        }
        private NetworkUser RuleChoiceController_FindNetworkUser(On.RoR2.UI.RuleChoiceController.orig_FindNetworkUser orig, RuleChoiceController self)
        {
            // Use input.currentEventSystem

            return input.currentButtonEventSystem?.localUser.currentNetworkUser;
        }
        private void LoadoutPanelController_UpdateDisplayData(On.RoR2.UI.LoadoutPanelController.orig_UpdateDisplayData orig, RoR2.UI.LoadoutPanelController self)
        {
            // Use input.currentEventSystem

            UserProfile userProfile = input.currentButtonEventSystem?.localUser?.userProfile;
            NetworkUser currentNetworkUser = input.currentButtonEventSystem?.localUser?.currentNetworkUser;

            BodyIndex bodyIndex = (currentNetworkUser) ? currentNetworkUser.bodyIndexPreference : BodyIndex.None;

            self.SetDisplayData(new RoR2.UI.LoadoutPanelController.DisplayData()
            {
                userProfile = userProfile,
                bodyIndex = bodyIndex
            });
        }
        private void RunCameraManager_Update(On.RoR2.RunCameraManager.orig_Update orig, RunCameraManager self)
        {
            // Set screens to desired areas

            bool instance = Stage.instance;

            if (instance)
            {
                int index = 0;

                for (int count = CameraRigController.readOnlyInstancesList.Count; index < count; ++index)
                    if (CameraRigController.readOnlyInstancesList[index].suppressPlayerCameras)
                        return;
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
                        cameraRigController = Instantiate<GameObject>(LegacyResourcesAPI.Load<GameObject>("Prefabs/Main Camera")).GetComponent<CameraRigController>();
                        self.cameras[index1] = cameraRigController;
                    }

                    cameraRigController.viewer = networkUser;

                    networkUser.cameraRigController = cameraRigController;

                    GameObject networkUserBodyObject = RunCameraManager.GetNetworkUserBodyObject(networkUser);

                    ForceSpectate forceSpectate = InstanceTracker.FirstOrNull<ForceSpectate>();

                    if (forceSpectate)
                    {
                        cameraRigController.nextTarget = forceSpectate.target;
                        cameraRigController.cameraMode = RoR2.CameraModes.CameraModePlayerBasic.spectator;
                    }
                    else if (networkUserBodyObject)
                    {
                        cameraRigController.nextTarget = networkUserBodyObject;
                        cameraRigController.cameraMode = RoR2.CameraModes.CameraModePlayerBasic.playerBasic;
                    }
                    else if (!cameraRigController.disableSpectating)
                    {
                        cameraRigController.cameraMode = RoR2.CameraModes.CameraModePlayerBasic.spectator;
                        if (!cameraRigController.target)
                            cameraRigController.nextTarget = CameraRigControllerSpectateControls.GetNextSpectateGameObject(networkUser, null);
                    }
                    else
                        cameraRigController.cameraMode = RoR2.CameraModes.CameraModeNone.instance;

                    ++index1;
                }

                int index3 = index1;

                for (int index2 = index1; index2 < self.cameras.Length; ++index2)
                {
                    ref CameraRigController local = ref self.cameras[index1];

                    if (local != null)
                    {
                        if (local)
                            Destroy(self.cameras[index1].gameObject);
                        local = null;
                    }
                }

                for (int index2 = 0; index2 < index3; ++index2)
                    self.cameras[index2].viewport = configuration.GetScreenRect(index2);
            }
            else
            {
                for (int index = 0; index < self.cameras.Length; ++index)
                {
                    if (self.cameras[index])
                        Destroy(self.cameras[index].gameObject);
                }
            }
        }
        private void LocalCameraEffect_OnUICameraPreCull(On.RoR2.LocalCameraEffect.orig_OnUICameraPreCull orig, UICamera uiCamera)
        {
            // Disable death effect for players still alive

            for (int index = 0; index < LocalCameraEffect.instancesList.Count; index++)
            {
                GameObject target = uiCamera?.cameraRigController?.target;
                LocalCameraEffect instance = LocalCameraEffect.instancesList[index];
                HealthComponent component = uiCamera?.cameraRigController?.localUserViewer?.cachedBody?.healthComponent;

                if (!target || !component || !instance.targetCharacter)
                    continue;

                if (instance.targetCharacter == target && component.alive)
                    instance.effectRoot.SetActive(true);
                else
                    instance.effectRoot.SetActive(false);
            }
        }
        private void CombatHealthBarViewer_SetLayoutHorizontal(On.RoR2.UI.CombatHealthBarViewer.orig_SetLayoutHorizontal orig, RoR2.UI.CombatHealthBarViewer self)
        {
            // iDeathHD fix

            UICamera uiCamera = self.uiCamera;

            if (!uiCamera)
                return;

            self.UpdateAllHealthbarPositions(uiCamera.cameraRigController.sceneCam, uiCamera.camera);
        }
        private void NetworkUser_UpdateUserName(On.RoR2.NetworkUser.orig_UpdateUserName orig, RoR2.NetworkUser self)
        {
            if (self.localUser == null)
            {
                self.userName = self.GetNetworkPlayerName().GetResolvedName();
            }
            else
            {
                self.userName = self.localUser.userProfile.name;
            }
        }
        private NetworkPlayerName NetworkUser_GetNetworkPlayerName(On.RoR2.NetworkUser.orig_GetNetworkPlayerName orig, RoR2.NetworkUser self)
        {
            NetworkPlayerName name = new NetworkPlayerName()
            {
                nameOverride = self.id.strValue != null ? self.id.strValue : (string)null,
                steamId = !string.IsNullOrEmpty(self.id.strValue) ? new CSteamID() : new CSteamID(self.id.value)
            }; 

            if (self.localUser != null)
            {
            //    name.nameOverride = self.localUser?.userProfile.name;
            }

            return name;
        }
        private string PlayerCharacterMasterController_GetDisplayName(On.RoR2.PlayerCharacterMasterController.orig_GetDisplayName orig, RoR2.PlayerCharacterMasterController self)
        {
            string name = "";

            if (self.networkUserObject)
            {
                NetworkUser networkUser = self.networkUserObject.GetComponent<NetworkUser>();

                if (networkUser)
                {
                    if (networkUser.localUser == null)
                    {
                        name = networkUser.userName;
                    }
                    else
                    {
                        name = networkUser.localUser.userProfile.name;
                    }
                }
            }

            return name;
        }
        private void Nameplate_LateUpdate(On.RoR2.UI.Nameplate.orig_LateUpdate orig, RoR2.UI.Nameplate self)
        {
            string str = "";

            Color baseColor = self.baseColor;

            bool flag1 = true;
            bool flag2 = false;
            bool flag3 = false;

            int localUserIndex = -1;

            if (self.body)
            {
                str = self.body.GetDisplayName();

                flag1 = self.body.healthComponent.alive;
                flag2 = !self.body.outOfCombat || !self.body.outOfDanger;
                flag3 = self.body.healthComponent.isHealthLow;

                CharacterMaster master = self.body.master;

                if (master)
                {
                    PlayerCharacterMasterController component1 = master.GetComponent<PlayerCharacterMasterController>();

                    if (component1)
                    {
                        GameObject networkUserObject = component1.networkUserObject;

                        if (networkUserObject)
                        {
                            NetworkUser component2 = networkUserObject.GetComponent<NetworkUser>();

                            if (component2)
                            {
                                str = component2.userName;

                                if (component2.localUser != null)
                                {
                                    str = component2.localUser.userProfile.name;
                                    localUserIndex = component2.localUser.id;
                                }
                            }
                        }
                    }
                    else
                        str = RoR2.Language.GetString(self.body.baseNameToken);
                }
            }

            Color color = flag2 ? self.combatColor : localUserIndex > -1 ? ColorCatalog.GetMultiplayerColor(localUserIndex) : self.baseColor;

            self.aliveObject.SetActive(flag1);
            self.deadObject.SetActive(!flag1);

            if (self.criticallyHurtSpriteRenderer)
            {
                self.criticallyHurtSpriteRenderer.enabled = flag3 & flag1;
                self.criticallyHurtSpriteRenderer.color = HealthBar.GetCriticallyHurtColor();
            }

            if (self.label)
            {
                self.label.text = str;
                self.label.color = color;
            }

            foreach (SpriteRenderer coloredSprite in self.coloredSprites)
                coloredSprite.color = color;
        }
        private string SubjectChatMessage_ConstructChatString(On.RoR2.SubjectChatMessage.orig_ConstructChatString orig, RoR2.SubjectChatMessage self)
        {
            if (self.subjectAsNetworkUser)
            {
                if(self.subjectAsNetworkUser.localUser == null)
                    return Util.EscapeRichTextForTextMeshPro(self.subjectAsNetworkUser.userName);
                else
                    return Util.EscapeRichTextForTextMeshPro(self.subjectAsNetworkUser.localUser.userProfile.name);
            }

            if (self.subjectAsCharacterBody)
                return self.subjectAsCharacterBody.GetDisplayName();

            return "???";
        }
        private string SubjectChatMessage_GetSubjectName(On.RoR2.SubjectChatMessage.orig_GetSubjectName orig, SubjectChatMessage self)
        {
            if (self.subjectAsNetworkUser)
            {
                if (self.subjectAsNetworkUser.localUser != null)
                    return Util.EscapeRichTextForTextMeshPro(self.subjectAsNetworkUser.localUser.userProfile.name);

                return Util.EscapeRichTextForTextMeshPro(self.subjectAsNetworkUser.userName);
            }

            if (self.subjectAsCharacterBody)
                return self.subjectAsCharacterBody.GetDisplayName();

            return "???";
        }
        private void InputBindingDisplayController_Refresh(On.RoR2.InputBindingDisplayController.orig_Refresh orig, InputBindingDisplayController self, bool forceRefresh)
        {
            // TODO use IL hook
            MPEventSystem eventSystem = input.currentMouseEventSystem;

            if (!eventSystem || Run.instance)
            {
                eventSystem = self.eventSystemLocator?.eventSystem;

                if (!eventSystem)
                {
                    Debug.LogError("MPEventSystem is invalid.");
                    return;
                }
            }

            if (!forceRefresh && eventSystem == self.lastEventSystem && eventSystem.currentInputSource == self.lastInputSource)
                return;

            //if (eventSystem.currentInputSource == MPEventSystem.InputSource.MouseAndKeyboard) // Removed for settings screen
            //    return;

            if (self.useExplicitInputSource)
            {
                InputBindingDisplayController.sharedStringBuilder.Clear();
                InputBindingDisplayController.sharedStringBuilder.Append(Glyphs.GetGlyphString(eventSystem, self.actionName, self.axisRange, self.explicitInputSource));
            }
            else
            {
                InputBindingDisplayController.sharedStringBuilder.Clear();
                InputBindingDisplayController.sharedStringBuilder.Append(Glyphs.GetGlyphString(eventSystem, self.actionName, AxisRange.Full));
            }

            if (self.guiLabel)
                self.guiLabel.SetText(InputBindingDisplayController.sharedStringBuilder);

            else if (self.label)
                self.label.SetText(InputBindingDisplayController.sharedStringBuilder);

            self.lastEventSystem = eventSystem;
            self.lastInputSource = eventSystem.currentInputSource;
        }
        private void InputSourceFilter_Refresh(On.RoR2.UI.InputSourceFilter.orig_Refresh orig, InputSourceFilter self, bool forceRefresh)
        {
            if (self.eventSystem?.currentInputSource != MPEventSystem.InputSource.Gamepad || Run.instance)
                orig(self, forceRefresh);

            return;

            //
            MPEventSystem.InputSource? currentInputSource = input.currentMouseEventSystem?.currentInputSource;

            if (Run.instance)
                currentInputSource = self.eventSystem?.currentInputSource;

            MPEventSystem.InputSource requiredInputSource = self.requiredInputSource;

            bool flag = currentInputSource.GetValueOrDefault() == requiredInputSource & currentInputSource.HasValue;

            if (flag != self.wasOn | forceRefresh)
            {
                for (int index = 0; index < self.objectsToFilter.Length; ++index)
                    self.objectsToFilter[index].SetActive(flag);
            }

            self.wasOn = flag;
        }
        private void HGGamepadInputEvent_Update(On.RoR2.UI.HGGamepadInputEvent.orig_Update orig, HGGamepadInputEvent self)
        {
            bool flag = self.CanAcceptInput();

            if (self.couldAcceptInput != flag)
            {
                foreach (GameObject gameObject in self.enabledObjectsIfActive)
                    gameObject.SetActive(flag);
            }

            if (self.CanAcceptInput() && self.eventSystem.player.GetButtonDown(self.actionName))
                self.actionEvent.Invoke();

            self.couldAcceptInput = flag;
        }
        private Color ColorCatalog_GetMultiplayerColor(On.RoR2.ColorCatalog.orig_GetMultiplayerColor orig, int playerSlot)
        {
            //Log.LogDebug($"ColorCatalog_GetMultiplayerColor: Returning color for playerSlot ''");

            Assignment? assignment = configuration.GetAssignmentByLocalId(playerSlot);

            if (assignment.HasValue)
                return assignment.Value.color;

            return orig(playerSlot);
        }
        private UserProfile BaseSettingsControl_GetCurrentUserProfile(On.RoR2.UI.BaseSettingsControl.orig_GetCurrentUserProfile orig, BaseSettingsControl self)
        {
            if (input.currentMouseEventSystem is null)
                return orig(self);

            return input.currentMouseEventSystem.localUser.userProfile;
        }
        private void ProfileNameLabel_LateUpdate(On.RoR2.UI.ProfileNameLabel.orig_LateUpdate orig, ProfileNameLabel self)
        {
            string str = input.currentMouseEventSystem?.localUser?.userProfile.name ?? string.Empty;

            if (str == self.currentUserName)
                return;

            self.currentUserName = str;
            self.label.text = RoR2.Language.GetStringFormatted(self.token, self.currentUserName);
        }
        //private void GameEndReportPanelController_SetPlayerInfo(On.RoR2.UI.GameEndReportPanelController.) // End game player name
        #endregion

        #region Conditional Hooks

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
            screen.onEnter.AddListener(ScreenOnEnter);
            screen.onExit.AddListener(ScreenOnExit);

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
                if(!opener.name.Contains("XSplit"))
                    opener.forceCursorForGamePad = status;
            }

            if (!status)
                foreach (MPEventSystem instance in MPEventSystem.instancesList)
                    instance.SetSelectedGameObject(null);
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
                int localId = 1;

                Assignment[] assignments = new Assignment[configuration.assignments.Count];
                configuration.assignments.CopyTo(assignments);
                
                foreach (Assignment assignment in assignments)
                {
                    if (assignment.isAssigned)
                    {
                        localUsers.Add(new LocalUserManager.LocalUserInitializationInfo()
                        {
                            player = ReInput.players.GetPlayer(localId),//assignment.playerId + 1),
                            profile = profiles[assignment.profileId],
                        });

                        configuration.SetLocalId(assignment.playerId, localId - 1);
                        localId++;
                    }
                }
            }

            for (int indexA = 0; indexA < localUsers.Count; indexA++)
            {
                for (int indexB = indexA + 1; indexB < localUsers.Count; indexB++)
                {
                    if (localUsers[indexA].profile is null || localUsers[indexB].profile is null)
                        continue;

                    if (string.Compare(localUsers[indexA].profile.fileName, localUsers[indexB].profile.fileName) == 0)
                        return false;
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
                PrintControllers();
                return;
            }

            // PrintControllers();

            bool keyboardAssigned = false;

            foreach (Assignment assignment in configuration.assignments)
            {
                if (!assignment.isAssigned || assignment.localId < 0)
                    continue;

                int playerIndex = assignment.localId;//assignment.playerId;

                LocalUserManager.readOnlyLocalUsersList[playerIndex].inputPlayer.controllers.ClearAllControllers();

                if (assignment.controller.type == ControllerType.Keyboard)
                {
                    keyboardAssigned = true;

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
                LocalUserManager.readOnlyLocalUsersList[playerIndex].ApplyUserProfileBindingsToRewiredPlayer();

                Log.LogDebug($"XSplitScreen.AssignControllers: Assigning '{assignment.controller.name}' to playerIndex '{playerIndex}'");
            }

            if (!keyboardAssigned)
            {
                foreach (Controller controller in ReInput.controllers.Controllers)
                {
                    if (controller.type == ControllerType.Mouse || controller.type == ControllerType.Keyboard)
                    {
                        Log.LogDebug($"Keyboard not assigned - adding to first player");
                        LocalUserManager.GetFirstLocalUser().inputPlayer.controllers.AddController(controller, false);
                        LocalUserManager.GetFirstLocalUser().ApplyUserProfileBindingsToRewiredPlayer();
                    }
                }
            }

            PrintControllers();
        }
        private void PrintControllers()
        {
            Log.LogDebug($"XSplitScreen.PrintControllers: readOnlyLocalUsersList");
            for (int e = 0; e < LocalUserManager.readOnlyLocalUsersList.Count; e++)
            {
                foreach (Controller controller in LocalUserManager.readOnlyLocalUsersList[e].inputPlayer.controllers.Controllers)
                {
                    Log.LogDebug($"XSplitScreen.PrintControllers: Player '{LocalUserManager.readOnlyLocalUsersList[e].inputPlayer.name}' has controller '{controller}'");
                }
            }

            Log.LogDebug($"XSplitScreen.PrintControllers: ReInput players");
            foreach (Player player in ReInput.players.AllPlayers)
            {
                
                //Print($" - {player.name}");
                foreach (Controller controller in player.controllers.Controllers)
                {
                    Log.LogDebug($"XSplitScreen.PrintControllers: '{player.name}' <- '{controller.name}'");
                }
            }

            Log.LogDebug($"XSplitScreen.PrintControllers: MPInputModules");

            foreach (MPEventSystem eventSystem in MPEventSystem.readOnlyInstancesList)
            {
                Log.LogDebug($"XSplitScreen.PrintControllers: '{eventSystem.name}' currentInputSource = '{eventSystem.currentInputSource}'");
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

            public int assignedPlayerCount
            {
                get
                {
                    int value = 0;

                    foreach (Assignment assignment in assignments)
                    {
                        if (assignment.isAssigned)
                            value++;
                    }

                    return value;
                }
            }
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

                if (onControllerConnected != null)
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

                if(onControllerDisconnected != null)
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
            public void SetLocalId(int playerId, int localId)
            {
                Assignment? assignment = GetAssignmentByPlayerId(playerId);

                if (assignment.HasValue)
                {
                    Assignment newAssignment = assignment.Value;
                    newAssignment.localId = localId;

                    SetAssignment(newAssignment);
                }
            }
            public Rect GetScreenRect(int playerId)
            {
                Assignment? assignment = GetAssignmentByLocalId(playerId);

                Rect screenRect = new Rect(0, 0, 1, 1);

                if (!assignment.HasValue)
                    return screenRect;

                screenRect.y = assignment.Value.position.x > 0 ? 0 : 0.5f;
                screenRect.x = assignment.Value.position.y < 2 ? 0 : 0.5f;

                screenRect.width = assignment.Value.position.y == 1 ? 1 : 0.5f;
                screenRect.height = assignment.Value.position.x == 1 ? 1 : 0.5f;

                return screenRect;
            }
            public Assignment? GetAssignmentByLocalId(int localId)
            {
                foreach (Assignment assignment in assignments)
                {
                    if (assignment.localId == localId)
                        return assignment;
                }

                return null;
            }
            public Assignment? GetAssignmentByPlayerId(int playerId)
            {
                foreach (Assignment assignment in assignments)
                {
                    if (assignment.playerId == playerId)
                        return assignment;
                }

                return null;
            }
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

                List<Assignment> readonlyAssignments = assignments.AsReadOnly().ToList();

                foreach (Assignment other in readonlyAssignments)
                {
                    if(other.position.Equals(assignment.position))
                    {
                        Assignment unassigned = other;
                        unassigned.position = int2.negative;
                        assignments[unassigned.playerId] = unassigned;
                    }
                }

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
                if (PlatformSystems.saveSystem.loadedUserProfiles.Values.Count == 0 || assignedPlayerCount < 2)
                    return false;

                foreach (Assignment assignment in configuration.assignments)
                {
                    if (assignment.isAssigned)
                    {
                        foreach (Assignment other in configuration.assignments)
                        {
                            if (other.playerId == assignment.playerId)
                                continue;

                            if (other.position.Equals(assignment.position) && other.displayId == assignment.displayId)
                                return false;
                        }

                        if (assignment.profileId == -1 || assignment.profileId >= PlatformSystems.saveSystem.loadedUserProfiles.Values.Count
                            || assignment.controller == null || assignment.displayId < 0 ||
                            assignment.displayId >= 1/*Display.displays.Length*/) // Disable multi monitor until ready
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
            public MPEventSystem currentButtonEventSystem { get; private set; }
            public MPEventSystem currentMouseEventSystem { get; private set; }

            public bool clickedThisFrame;

            public void UpdateCurrentEventSystem(EventSystem eventSystem, bool mouse = false)
            {
                if (eventSystem)
                    UpdateCurrentEventSystem(eventSystem as MPEventSystem, mouse);
                else
                {
                    if (mouse)
                        currentMouseEventSystem = null;
                    else
                        currentButtonEventSystem = null;
                }
            }
            public void UpdateCurrentEventSystem(MPEventSystem eventSystem, bool mouse = false)
            {
                if (mouse)
                {
                    if (currentMouseEventSystem)
                        currentMouseEventSystem.SetSelectedGameObject(null);

                    currentMouseEventSystem = eventSystem;
                }
                else
                {
                    if (currentButtonEventSystem)
                        currentButtonEventSystem.SetSelectedGameObject(null);

                    currentButtonEventSystem = eventSystem;
                }
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
                Log.LogDebug($"Preference.Update: preference = {this}");
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
            public int localId; // Group: Assignment

            public Assignment(Controller controller)
            {
                this.controller = controller;

                position = int2.negative;
                context = int2.negative;
                displayId = -1;
                playerId = -1;
                profileId = -1;
                color = Color.white;
                localId = -1;
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