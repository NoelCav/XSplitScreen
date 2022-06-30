using System;
using System.Collections.Generic;
using System.Text;
using RoR2.UI;
using RoR2.UI.MainMenu;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using RoR2.UI.SkinControllers;
using RoR2;

namespace DoDad.UI
{
    /// <summary>
    /// Should only work in the title scene!
    /// TODO: Maybe just add a Clone method
    /// TODO: Set parent by string
    /// </summary>
    public static class ModMenuManager
    {
        #region Variables
        private static readonly string CONFIG_TOKEN_DEFAULT = "PLAYER_NAME_UNAVAILABLE";

        private static readonly string TAG_SCREEN = "[ModMenu] {0}";
        private static readonly string TAG_BUTTON = "[ModButton] {0}";
        private static readonly string TAG_POPUP = "[ModPopup] {0}";
        private static readonly string MSG_TAG = "[DoDad.UI.MainMenuManager] {0}";
        private static readonly string MSG_ERROR_SCREEN_EXISTS = "Screen '{0}' already exists";
        private static readonly string MSG_ERROR_NO_MENU = "MainMenuController does not exist. Try calling this function from the title screen.";

        public static Dictionary<string, ModScreen> ActiveScreens = new Dictionary<string, ModScreen>();

        private static GameObject ButtonPrefab;
        private static GameObject PopupPrefab;
        #endregion

        #region Public Methods
        /// <summary>
        /// Ensure you call this when you're done or else.
        /// </summary>
        public static void CleanupReferences()
        {
            GameObject.Destroy(ButtonPrefab);
            GameObject.Destroy(PopupPrefab);
            Print("PopupPrefab is null: " + (PopupPrefab == null).ToString());
            ClearScreens();
        }
        public static void CreateReferences()
        {
            CreateButtonPrefab();
            CreatePopupPrefab();
        }
        /// <summary>
        /// Adds a new screen to the main menu and disables it by default.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static ModScreen AddScreen(string name)
        {
            if(MainMenuController.instance == null)
            {
                Print(MSG_ERROR_NO_MENU, Log.LogLevel.Warning);
                return null;
            }
            
            if (ActiveScreens.ContainsKey(name))
            {
                Print(string.Format(MSG_ERROR_SCREEN_EXISTS, name), Log.LogLevel.Warning);
                return null;
            }

            GameObject screenParent = UIResources.CreateUIGameObject(string.Format(TAG_SCREEN, name));
            screenParent.transform.SetParent(MainMenuController.instance.transform);
            GameObject.Destroy(screenParent.GetComponent<CanvasRenderer>());

            GameObject screenObject = UIResources.CreateUIGameObject(name);
            screenObject.transform.SetParent(screenParent.transform);

            ModScreen newScreen = screenObject.AddComponent<ModScreen>();

            SetupScreen(screenObject);

            ActiveScreens.Add(name, newScreen);

            return ActiveScreens[name];
        }
        public static bool RemoveScreen(string name)
        {
            if(ActiveScreens.ContainsKey(name))
            {
                GameObject.Destroy(ActiveScreens[name]?.gameObject);
                ActiveScreens.Remove(name);
                return true;
            }

            return false;
        }
        /// <summary>
        /// Create a new button and optionally attach it to an existing screen.
        /// </summary>
        /// <param name="name">Button name</param>
        /// <param name="screen">Custom screen name</param>
        /// <param name="menuTemplate">Supported existing screens</param>
        /// <returns></returns>
        public static HGButton CreateHGButton(string name, string token, Menu menuParent = Menu.None, ModScreen screenParent = null)
        {
            //if (ButtonPrefab == null)
            //    CreateButtonPrefab();

            HGButton newButton = GameObject.Instantiate(ButtonPrefab).GetComponent<HGButton>();
            GameObject newObject = newButton.gameObject;
            newObject.name = string.Format(TAG_BUTTON, name);

            if (menuParent == Menu.Title)
            {
                GameObject parent = GameObject.Find("GenericMenuButton (Singleplayer)");
                newObject.transform.SetParent(parent.transform.parent);
                newObject.transform.SetSiblingIndex(1);
                newObject.transform.localScale = Vector3.one;
                newButton.hoverLanguageTextMeshController = newObject.transform.parent.GetChild(0).GetComponentInChildren<LanguageTextMeshController>();
                newButton.requiredTopLayer = GameObject.Find("TitleMenu").GetComponent<RoR2.UI.UILayerKey>();
            }
            else if(screenParent)
            {
                screenParent.AddButton(newObject);
            }

            newObject.GetComponent<MPEventSystemLocator>().eventSystemProvider = newObject.GetComponentInParent<MPEventSystemProvider>();
            newObject.SetActive(true);

            if (token != null)
                newObject.GetComponent<LanguageTextMeshController>().token = token;
            else
                newObject.GetComponent<LanguageTextMeshController>().token = CONFIG_TOKEN_DEFAULT;

            return newButton;
        }
        public static GameObject CreatePopupPanel(string name, ModScreen screenParent = null)
        {
            //if (!PopupPrefab)
                //CreatePopupPrefab();

            GameObject newPopup = GameObject.Instantiate(PopupPrefab);
            newPopup.name = string.Format(TAG_BUTTON, name);
            Print("First: " + newPopup.transform.GetChild(0).name);

            if (screenParent)
            {
                Print("Setting parent to modScreen");
                newPopup.transform.SetParent(screenParent.transform);
            }

            ResetBindingControllers(newPopup);
            //newPopup.SetActive(true);

            return newPopup;
        }
        #endregion

        #region Private Methods
        private static void ClearScreens()
        {
            foreach (KeyValuePair<string, ModScreen> keyPair in ActiveScreens)
            {
                GameObject.Destroy(keyPair.Value?.gameObject.transform.parent.gameObject);
            }

            ActiveScreens.Clear();
        }
        private static void SetupScreen(GameObject screen)
        {
            GameObject.Destroy(screen.GetComponent<CanvasRenderer>());
            GameObject template = MainMenuController.instance.extraGameModeMenuScreen.gameObject;

            // TODO fix reflection

            Canvas tCanvas = template.GetComponent<Canvas>();
            CanvasScaler tCanvasScaler = template.GetComponent<CanvasScaler>();
            GraphicRaycaster tRaycaster = template.GetComponent<GraphicRaycaster>();
            CanvasGroup tCanvasGroup = template.GetComponent<CanvasGroup>();
            UILayerKey tLayerKey = template.GetComponent<UILayerKey>();

            Canvas canvas = screen.AddComponent<Canvas>();
            CanvasScaler canvasScaler = screen.AddComponent<CanvasScaler>();
            GraphicRaycaster raycaster = screen.AddComponent<GraphicRaycaster>();
            CanvasGroup canvasGroup = screen.AddComponent<CanvasGroup>();
            UILayerKey layerKey = screen.AddComponent<UILayerKey>();

            CursorOpener cursorOpener = screen.AddComponent<CursorOpener>();
            //InputSourceFilter gamepadFilter = screen.AddComponent<InputSourceFilter>();
            //InputSourceFilter keyboardFilter = screen.AddComponent<InputSourceFilter>();

            canvas.additionalShaderChannels = tCanvas.additionalShaderChannels;
            canvas.renderMode = tCanvas.renderMode;
            canvasScaler.screenMatchMode = tCanvasScaler.screenMatchMode;
            canvasScaler.uiScaleMode = tCanvasScaler.uiScaleMode;
            canvasScaler.matchWidthOrHeight = tCanvasScaler.matchWidthOrHeight;
            canvasScaler.referenceResolution = tCanvasScaler.referenceResolution;
            canvasGroup.blocksRaycasts = tCanvasGroup.blocksRaycasts;
            canvas.scaleFactor = 1.3333f;
            layerKey.layer = tLayerKey.layer;
            layerKey.onBeginRepresentTopLayer = new UnityEvent();
            layerKey.onEndRepresentTopLayer = new UnityEvent();

            screen.AddComponent<MPEventSystemProvider>();
            //screen.AddComponent<MPEventSystemLocator>();

            cursorOpener.forceCursorForGamePad = true;

            GameObject duplicateMenu = GameObject.Instantiate(template.transform.GetChild(0).gameObject);
            duplicateMenu.transform.SetParent(screen.transform);

            OnEnableEvent onEnableGenericMenu = duplicateMenu.transform.GetChild(0).GetComponent<OnEnableEvent>();
            onEnableGenericMenu.action.AddListener(duplicateMenu.transform.GetChild(0).GetComponent<UIJuice>().TransitionPanFromLeft);
            onEnableGenericMenu.action.AddListener(duplicateMenu.transform.GetChild(0).GetComponent<UIJuice>().TransitionAlphaFadeIn);

            OnEnableEvent onEnableSubmenu = duplicateMenu.transform.GetChild(2).GetComponent<OnEnableEvent>();
            onEnableSubmenu.action.AddListener(duplicateMenu.transform.GetChild(2).GetComponent<UIJuice>().TransitionPanFromBottom);
            onEnableSubmenu.action.AddListener(duplicateMenu.transform.GetChild(2).GetComponent<UIJuice>().TransitionAlphaFadeIn);
            // assign all of the invocations to fix everything
            
            //screen.GetComponent<ModScreen>().onEnter
            GameObject.Destroy(onEnableGenericMenu.gameObject.transform.GetChild(0).GetChild(1).gameObject);
            GameObject.Destroy(onEnableGenericMenu.gameObject.transform.GetChild(0).GetChild(2).gameObject);
            GameObject.Destroy(onEnableGenericMenu.gameObject.transform.GetChild(0).GetChild(3).gameObject);

            ResetBindingControllers(screen);

            RectTransform menuRect = duplicateMenu.GetComponent<RectTransform>();
            RectTransform templateRect = template.GetComponent<RectTransform>();

            menuRect.anchorMax = new Vector2(0.95f, 0.95f);// templateRect.anchorMax;
            menuRect.anchorMin = new Vector2(0.05f, 0.05f);//;
            menuRect.offsetMax = Vector2.zero;
            menuRect.offsetMin = Vector2.zero;
            menuRect.anchoredPosition = Vector2.zero;
            menuRect.localScale = Vector3.one;


            screen.SetActive(false);
            //menuRect.localScale = templateRect.localScale;
            //menuRect.localPosition = templateRect.localPosition;

            //HGGamepadEvent and HGGamepadHistory should be explored if functionality isn't there

            //gamepadFilter.requiredInputSource = MPEventSystem.InputSource.Gamepad;
            //keyboardFilter.requiredInputSource = MPEventSystem.InputSource.MouseAndKeyboard;
        }
        private static void CreateButtonPrefab()
        {
            if (ButtonPrefab != null)
                return;

            GameObject template = GameObject.Find("GenericMenuButton (Singleplayer)");

            if (template == null)
                return;

            ButtonPrefab = GameObject.Instantiate(template);
            ButtonPrefab.name = string.Format(TAG_BUTTON, "ButtonPrefab");
            HGButton button = ResetHGButton(ButtonPrefab.GetComponent<HGButton>());

            foreach (ViewableTag tag in ButtonPrefab.GetComponentsInChildren<ViewableTag>())
                GameObject.Destroy(tag);

            ButtonPrefab.transform.SetParent(null);
            ButtonPrefab.SetActive(false);
        }
        private static void CreatePopupPrefab()
        {
            Print("PopupPrefab is null: " + (PopupPrefab == null).ToString());
            if (PopupPrefab != null)
                return;

            GameObject template = GameObject.Find("MENU: Profile").transform.GetChild(0).GetChild(0).gameObject;

            if (template == null)
                return;

            PopupPrefab = GameObject.Instantiate(template);
            PopupPrefab.name = string.Format(TAG_POPUP, "PopupPrefab");
            GameObject.Destroy(PopupPrefab.GetComponent<HGGamepadInputEvent>());

            PopupPrefab.transform.GetChild(0).gameObject.SetActive(true);
            GameObject.Destroy(PopupPrefab.transform.GetChild(0).gameObject);

            foreach (OnEnableEvent onEnableEvent in PopupPrefab.GetComponentsInChildren<OnEnableEvent>(true))
            {
                //Print("Clearing enable event");
                onEnableEvent.action.RemoveAllListeners();
            }
            foreach(HGButton button in PopupPrefab.transform.GetComponentInChildren<ContentSizeFitter>().gameObject.GetComponentsInChildren<HGButton>(true))
            {
                //Print("Destroying button");
                GameObject.Destroy(button.gameObject);
            }
            foreach (UserProfileListController controller in PopupPrefab.transform.GetComponentsInChildren<UserProfileListController>(true))
            {
                //Print("Destroying controller " + controller.name);
                GameObject.Destroy(controller);
            }
            foreach (UserProfileListElementController controller in PopupPrefab.transform.GetComponentsInChildren<UserProfileListElementController>(true))
            {
                //Print("Destroying element controller " + controller.name);
                GameObject.Destroy(controller);
            }
            foreach (LanguageTextMeshController languageController in PopupPrefab.GetComponentsInChildren<LanguageTextMeshController>(true))
            {
                //Print("Setting language token");
                languageController.token = CONFIG_TOKEN_DEFAULT;
            }

            Print("Children: " + PopupPrefab.GetComponentsInChildren<Transform>(true).Length.ToString());
            foreach (Transform child in PopupPrefab.GetComponentsInChildren<Transform>(true))
            {
               // Print("Stored child: " + child.name);
            }


            PopupPrefab.SetActive(false);
        }
        // TODO combine
        private static HGButton ResetHGButton(HGButton button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.m_PersistentCalls.Clear();
            button.onDeselect.RemoveAllListeners();
            button.onSelect.RemoveAllListeners();
            button.onFindSelectableLeft.RemoveAllListeners();
            button.onFindSelectableRight.RemoveAllListeners();
            button.previousState = Selectable.SelectionState.Normal;

            return button;
        }
        private static void ResetBindingControllers(GameObject subMenu)
        {
            foreach (InputBindingDisplayController child in subMenu.GetComponentsInChildren<InputBindingDisplayController>(true))
            {
                if (child)
                {
                    child.GetComponent<MPEventSystemLocator>().Awake();
                    child.Awake();
                }
            }
            foreach (HGButton child in subMenu.GetComponentsInChildren<HGButton>(true))
            {
                child.disablePointerClick = false;
                child.disableGamepadClick = false;
                child.GetComponent<MPEventSystemLocator>().Awake();
            }
        }
        #endregion

        #region Helpers
        private static void Print(object msg, Log.LogLevel level = Log.LogLevel.UnityDebug)
        {
            msg = string.Format(MSG_TAG, msg);

            switch(level)
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
        #endregion
    }

    #region Additional Definitions
    internal class UIResources
    {
        // TODO listen for title exit and clear
        private static Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        public static UISkinData GetSkinData(string name)
        {
            foreach(UISkinData skinData in Resources.FindObjectsOfTypeAll<UISkinData>())
            {
                if (string.Compare(name, skinData.name) == 0)
                    return skinData;
            }

            return null;
        }
        public static Sprite GetSprite(string name)
        {
            if (_sprites.Count == 0)
                Initialize();

            if (_sprites.ContainsKey(name))
                return _sprites[name]; //GameObject.Instantiate(_sprites[name]);

            return null;
        }
        public static GameObject CreateUIGameObject(string name)
        {
            GameObject newUI = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            newUI.layer = GameObject.Find("GenericMenuButton (Singleplayer)").layer;
            return newUI;
        }
        private static void Initialize()
        {
            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();

            foreach (Sprite sprite in sprites)
            {
                if (sprite.texture.name.Length == 0 || sprite.texture.name == null)
                    continue;

                if (_sprites.ContainsKey(sprite.texture.name))
                    if (_sprites[sprite.texture.name] != null)
                        continue;

                _sprites.Add(sprite.texture.name, sprite);
            }
            return;
        }
    }
    public class ModScreen : BaseMainMenuScreen
    {
        private GameObject _worldPosition;
        private Transform _buttonPanel;

        public void SetCameraPosition(Vector3 position, Quaternion forward)
        {
            _worldPosition.transform.position = position;
            _worldPosition.transform.rotation = forward;
        }

        public ModScreen()
        {
            _worldPosition = new GameObject("CameraPositionMarker");
            _worldPosition.transform.SetParent(transform.parent);

            desiredCameraTransform = _worldPosition.transform;

            onEnter = new UnityEvent();
            onExit = new UnityEvent();
        }
        public void AddButton(GameObject newObject)
        {
            if(!_buttonPanel)
            {
                _buttonPanel = transform.gameObject.GetComponentInChildren<VerticalLayoutGroup>().transform;
            }

            newObject.transform.SetParent(_buttonPanel);
            newObject.transform.SetSiblingIndex(1);
            newObject.transform.localScale = Vector3.one;
            newObject.GetComponent<HGButton>().hoverLanguageTextMeshController = _buttonPanel.transform.GetChild(0).gameObject.GetComponentInChildren<LanguageTextMeshController>();
        }
    }
    public class ModLanguageTextMesh : LanguageTextMeshController
    {
        private static readonly string DefaultToken = "PLAYER_NAME_UNAVAILABLE";

        public ModLanguageTextMesh()
        {
            _token = DefaultToken;
        }

        public void SetToken(string newToken)
        {
            previousToken = newToken;
            _token = newToken;
        }
    }
    public enum Menu
    {
        None,
        Title
    }
    #endregion
}

namespace DoDad.UI.Components
{
    [RequireComponent(typeof(HGButton))]
    public class SplitscreenTextMeshController : MonoBehaviour
    {
        public string OnEnabledToken = "PLAYER_NAME_UNAVAILABLE";
        public string OnDisabledToken = "PLAYER_NAME_UNAVAILABLE";
        public string OnEnabledHoverToken = "PLAYER_NAME_UNAVAILABLE";
        public string OnDisabledHoverToken = "PLAYER_NAME_UNAVAILABLE";

        private LanguageTextMeshController _controller;
        private HGButton _button;

        public void Initialize()
        {
            _controller = GetComponent<LanguageTextMeshController>();
            _button = GetComponent<HGButton>();
        }
        private void OnEnable()
        {
            XSplitScreen.XSplitScreen.OnLocalPlayerCount.AddListener(UpdateToken);
        }
        private void OnDisable()
        {
            XSplitScreen.XSplitScreen.OnLocalPlayerCount.RemoveListener(UpdateToken);
        }
        public void UpdateToken()
        {
            if (!_controller)
                Initialize();
            Debug.Log("Updating Token");
            _controller.token = XSplitScreen.XSplitScreen.Enabled ? OnEnabledToken : OnDisabledToken;
            _button.hoverToken = XSplitScreen.XSplitScreen.Enabled ? OnEnabledHoverToken : OnDisabledHoverToken;
        }
    }
}