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
using DoDad.UI.Components;

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

        public static int DefaultLayer { get; internal set; }

        public static bool Ready 
        { 
            get 
            { 
                return (ButtonPrefab != null && PopupPrefab != null);
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Ensure you call this when you're done or else.
        /// </summary>
        public static void CleanupReferences()
        {
            if (ButtonPrefab)
                GameObject.Destroy(ButtonPrefab);
            if(PopupPrefab)
                GameObject.Destroy(PopupPrefab);

            ClearScreens();
        }
        public static void CreateReferences()
        {
            if (ButtonPrefab == null)
                CreateButtonPrefab();

            if(PopupPrefab == null)
                CreatePopupPrefab();

            //Print($"ModManager: {ButtonPrefab == null} / {PopupPrefab == null}");
            if (Ready)
                DefaultLayer = ButtonPrefab.layer;
        }
        /// <summary>
        /// Adds a new screen to the main menu and disables it by default.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static ModScreen AddScreen(string name, UILayer layer)
        {
            if(MainMenuController.instance == null)
            {
                Print(MSG_ERROR_NO_MENU, Log.LogLevel.Warning);
                return null;
            }
            
            if (ActiveScreens.ContainsKey(name))
            {
                Print(string.Format(MSG_ERROR_SCREEN_EXISTS, name), Log.LogLevel.Warning);

                if(ActiveScreens[name])
                    if(ActiveScreens[name].transform.parent)
                        GameObject.Destroy(ActiveScreens[name].transform.parent.gameObject);

                ActiveScreens.Remove(name);
            }

            GameObject screenParent = UIResources.CreateUIGameObject(string.Format(TAG_SCREEN, name));
            screenParent.transform.SetParent(MainMenuController.instance.transform);
            GameObject.Destroy(screenParent.GetComponent<CanvasRenderer>());

            GameObject screenObject = UIResources.CreateUIGameObject(name);
            screenObject.transform.SetParent(screenParent.transform);

            ModScreen newScreen = screenObject.AddComponent<ModScreen>();

            SetupScreen(screenObject, layer);

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
            if(ButtonPrefab == null)
                CreateReferences();

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
            if (PopupPrefab == null)
                CreateReferences();

            GameObject newPopup = GameObject.Instantiate(PopupPrefab);
            newPopup.name = string.Format(TAG_POPUP, name);

            if (screenParent)
            {
                newPopup.transform.SetParent(screenParent.transform);

                RectTransform menuRect = newPopup.GetComponent<RectTransform>();
                RectTransform templateRect = PopupPrefab.GetComponent<RectTransform>();
                menuRect.anchorMin = templateRect.anchorMin;
                menuRect.anchorMax = templateRect.anchorMax;
                menuRect.offsetMin = templateRect.offsetMin;
                menuRect.offsetMax = templateRect.offsetMax;

                menuRect.GetChild(0).GetComponent<RectTransform>().anchorMin = templateRect.GetChild(0).GetComponent<RectTransform>().anchorMin;
                menuRect.GetChild(0).GetComponent<RectTransform>().anchorMax = templateRect.GetChild(0).GetComponent<RectTransform>().anchorMax;
                menuRect.GetChild(0).GetComponent<RectTransform>().offsetMin = templateRect.GetChild(0).GetComponent<RectTransform>().offsetMin;
                menuRect.GetChild(0).GetComponent<RectTransform>().offsetMax = templateRect.GetChild(0).GetComponent<RectTransform>().offsetMax;

                UILayerKey layerKey = screenParent.GetComponent<UILayerKey>();

                foreach(HGButton button in screenParent.GetComponentsInChildren<HGButton>(true))
                {
                    button.requiredTopLayer = layerKey;
                }
            }

            ResetBindingControllers(newPopup);

            newPopup.SetActive(true);

            return newPopup;
        }
        public static GameObject CreateDraggable(string name)
        {
            GameObject draggable = new GameObject(name, typeof(RectTransform));

            Image image = draggable.AddComponent<UnityEngine.UI.Image>();

            image.preserveAspect = true;

            draggable.AddComponent<ControllerDraggable>().enabled = false;

            return draggable;
        }
        public static GameObject CreateImage(string name)
        {
            GameObject image = new GameObject(name, typeof(RectTransform), typeof(Image));

            return image;
        }
        #endregion

        #region Private Methods
        private static void ClearScreens()
        {
            foreach (KeyValuePair<string, ModScreen> keyPair in ActiveScreens)
            {
                if(keyPair.Value)
                    GameObject.Destroy(keyPair.Value?.gameObject.transform.parent.gameObject);
            }

            ActiveScreens.Clear();
        }
        private static void SetupScreen(GameObject screen, UILayer layer)
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

            layerKey.layer = layer;
            layerKey.onBeginRepresentTopLayer = new UnityEvent();
            layerKey.onEndRepresentTopLayer = new UnityEvent();

            screen.AddComponent<MPEventSystemProvider>();
            //screen.AddComponent<MPEventSystemLocator>();

            GameObject duplicateMenu = GameObject.Instantiate(template.transform.GetChild(0).gameObject);
            duplicateMenu.name = string.Format(TAG_SCREEN, "Main Panel");
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

            screen.GetComponentInChildren<ModScreen>().myMainMenuController = MainMenuController.instance;

            foreach (HGButton button in screen.GetComponentsInChildren<HGButton>())
                button.requiredTopLayer = layerKey;

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
            if (PopupPrefab != null)
                return;

            GameObject template = GameObject.Find("MENU: Profile").transform.GetChild(0).GetChild(0).gameObject;

            if (template == null)
                return;

            PopupPrefab = GameObject.Instantiate(template);
            PopupPrefab.name = string.Format(TAG_POPUP, "PopupPrefab");
            GameObject.Destroy(PopupPrefab.GetComponent<HGGamepadInputEvent>());

            //PopupPrefab.transform.GetChild(1).gameObject.SetActive(false);

            GameObject.Destroy(PopupPrefab.transform.GetChild(0).gameObject);

            UIJuice juice = PopupPrefab.transform.GetChild(1).GetComponent<UIJuice>();
            CanvasGroup canvasGroup = PopupPrefab.transform.GetChild(1).GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1;

            GameObject.Destroy(PopupPrefab.transform.GetChild(1).GetComponent<OnEnableEvent>());

            juice.gameObject.SetActive(false);
            //OnEnableEvent onEnableEvent = PopupPrefab.transform.GetChild(1).GetComponent<OnEnableEvent>();
            //onEnableEvent.action.RemoveAllListeners();
            //onEnableEvent.action.AddListener(juice.TransitionPanFromTop);
            //onEnableEvent.action.AddListener(juice.TransitionAlphaFadeIn);

            foreach (HGButton button in PopupPrefab.transform.gameObject.GetComponentsInChildren<HGButton>(true))
            {
                if (!button.gameObject.activeSelf)
                    continue;

                GameObject.Destroy(button.gameObject);
            }

            foreach (UserProfileListController controller in PopupPrefab.transform.GetComponentsInChildren<UserProfileListController>(true))
                GameObject.Destroy(controller);

            foreach (UserProfileListElementController controller in PopupPrefab.transform.GetComponentsInChildren<UserProfileListElementController>(true))
                GameObject.Destroy(controller);

            foreach (LanguageTextMeshController languageController in PopupPrefab.GetComponentsInChildren<LanguageTextMeshController>(true))
                languageController.token = CONFIG_TOKEN_DEFAULT;

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
            msg = level != Log.LogLevel.UnityDebug ? msg : string.Format(MSG_TAG, msg);

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
            newUI.layer = ModMenuManager.DefaultLayer;
            //GameObject currentLayer =
            //newUI.layer = GameObject.Find("GenericMenuButton (Singleplayer)").layer;
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
        private List<UIJuice> _onEnableJuice;
        private List<GameObject> _onEnableObject;

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

            _onEnableJuice = new List<UIJuice>();
            _onEnableObject = new List<GameObject>();

            onEnter = new UnityEvent();
            onExit = new UnityEvent();

            XSplitScreen.OnLocalPlayerCount.AddListener(OnEnableUpdate);
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
            HGButton button = newObject.GetComponent<HGButton>();
            button.hoverLanguageTextMeshController = _buttonPanel.transform.GetChild(0).gameObject.GetComponentInChildren<LanguageTextMeshController>();
            button.requiredTopLayer = GetComponent<UILayerKey>();
        }
        public void AddJuiceOnEnable(UIJuice element)
        {
            _onEnableJuice.Add(element);
        }
        public void AddObjectOnEnable(GameObject obj)
        {
            _onEnableObject.Add(obj);
        }
        private void OnEnableUpdate()
        {
            foreach (UIJuice child in _onEnableJuice)
            {
                if(XSplitScreen.Enabled)
                {
                    child.gameObject.SetActive(true);
                    child.TransitionAlphaFadeIn();
                    child.TransitionPanFromTop();
                }
                else
                {
                    child.TransitionAlphaFadeOut();
                    child.TransitionPanToBottom();
                }
            }

            foreach(GameObject obj in _onEnableObject)
            {
                obj.SetActive(XSplitScreen.Enabled);
            }

            //GetComponent<CursorOpener>().forceCursorForGamePad = XSplitScreen.XSplitScreen.Enabled;

            
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
            XSplitScreen.OnLocalPlayerCount.AddListener(UpdateToken);
        }
        private void OnDisable()
        {
            XSplitScreen.OnLocalPlayerCount.RemoveListener(UpdateToken);
        }
        public void UpdateToken()
        {
            if (!_controller)
                Initialize();

            _controller.token = XSplitScreen.Enabled ? OnEnabledToken : OnDisabledToken;
            _button.hoverToken = XSplitScreen.Enabled ? OnEnabledHoverToken : OnDisabledHoverToken;
        }
    }
}