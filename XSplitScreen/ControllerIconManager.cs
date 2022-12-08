using DoDad.Library.UI;
using Rewired;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DoDad.XSplitScreen;

namespace DoDad.XSplitScreen.Components
{
    // TODO
    // On the first launch of the plugin the controller icons should pop out of a chest?

    public class ControllerIconManager : MonoBehaviour
    {
        #region Variables
        public static ControllerIconManager instance { get; private set; }

        public static bool isAssigning;

        public List<Icon> icons { get; private set; }
        public RectTransform followerContainer { get; private set; }

        // TODO move references to XSplitScreenMenu or ConfigurationManager
        public Sprite sprite_Dinput { get; private set; }
        public Sprite sprite_Xinput { get; private set; }
        public Sprite sprite_Keyboard { get; private set; }
        public Sprite sprite_Unknown { get; private set; }
        public Sprite sprite_Xmark { get; private set; }
        public Sprite sprite_Lock { get; private set; }
        public Sprite sprite_Gear { get; private set; }
        public Sprite sprite_Monitor { get; private set; }
        public Sprite sprite_Dot { get; private set; }
        public Sprite sprite_Warning { get; private set; }
        public Sprite sprite_Reset { get; private set; }

        public IconEvent onStartDragIcon { get; private set; }
        public IconEvent onStopDragIcon { get; private set; }
        public IconEvent onIconAdded { get; private set; }
        public IconEvent onIconRemoved { get; private set; }

        private Texture2D texture_Dinput;
        private Texture2D texture_Xinput;
        private Texture2D texture_Keyboard;
        private Texture2D texture_Unknown;
        private Texture2D texture_Xmark;
        private Texture2D texture_Lock;
        private Texture2D texture_Gear;
        private Texture2D texture_Monitor;
        private Texture2D texture_Dot;
        private Texture2D texture_Warning;
        private Texture2D texture_Reset;

        private RectTransform iconPrefab;
        private RectTransform iconContainer;

        private XSplitScreen.Configuration configuration => XSplitScreen.configuration;
        #endregion

        #region Unity Methods
        public void Awake()
        {
            if (instance)
                Destroy(gameObject);

            Initialize();
        }
        public void OnDestroy()
        {
            instance = null;
            ToggleListeners(false);
        }
        #endregion

        #region Initialization & Exit
        private void Initialize()
        {
            InitializeReferences();
            InitializePrefab();
            InitializeIcons();
            ToggleListeners(true);
        }
        private void InitializeReferences()
        {
            instance = this;

            onStartDragIcon = new IconEvent();
            onStopDragIcon = new IconEvent();
            onIconAdded = new IconEvent();
            onIconRemoved = new IconEvent();

            texture_Dinput = XSplitScreen.assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/dinput.png");
            texture_Xinput = XSplitScreen.assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/xinput.png");
            texture_Keyboard = XSplitScreen.assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/keyboardmouse.png");
            texture_Unknown = XSplitScreen.assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/unknown.png");
            texture_Xmark = XSplitScreen.assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/xmark.png");
            texture_Lock = XSplitScreen.assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/lock.png");
            texture_Gear = XSplitScreen.assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/gear.png");
            texture_Monitor = XSplitScreen.assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/monitor.png");
            texture_Dot = XSplitScreen.assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/dot.png");
            texture_Warning = XSplitScreen.assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/warning.png");
            texture_Reset = XSplitScreen.assets.LoadAsset<Texture2D>("Assets/DoDad/Textures/reset.png");

            sprite_Dinput = Sprite.Create(texture_Dinput, new Rect(Vector2.zero, new Vector2(texture_Dinput.width, texture_Dinput.height)), Vector2.zero);
            sprite_Xinput = Sprite.Create(texture_Xinput, new Rect(Vector2.zero, new Vector2(texture_Xinput.width, texture_Xinput.height)), Vector2.zero);
            sprite_Keyboard = Sprite.Create(texture_Keyboard, new Rect(Vector2.zero, new Vector2(texture_Keyboard.width, texture_Keyboard.height)), Vector2.zero);
            sprite_Unknown = Sprite.Create(texture_Unknown, new Rect(Vector2.zero, new Vector2(texture_Unknown.width, texture_Unknown.height)), Vector2.zero);
            sprite_Xmark = Sprite.Create(texture_Xmark, new Rect(Vector2.zero, new Vector2(texture_Xmark.width, texture_Xmark.height)), Vector2.zero);
            sprite_Lock = Sprite.Create(texture_Lock, new Rect(Vector2.zero, new Vector2(texture_Lock.width, texture_Lock.height)), Vector2.zero);
            sprite_Gear = Sprite.Create(texture_Gear, new Rect(Vector2.zero, new Vector2(texture_Gear.width, texture_Gear.height)), Vector2.zero);
            sprite_Monitor = Sprite.Create(texture_Monitor, new Rect(Vector2.zero, new Vector2(texture_Monitor.width, texture_Monitor.height)), Vector2.zero);
            sprite_Dot = Sprite.Create(texture_Dot, new Rect(Vector2.zero, new Vector2(texture_Dot.width, texture_Dot.height)), Vector2.zero);
            sprite_Warning = Sprite.Create(texture_Warning, new Rect(Vector2.zero, new Vector2(texture_Warning.width, texture_Warning.height)), Vector2.zero);
            sprite_Reset = Sprite.Create(texture_Reset, new Rect(Vector2.zero, new Vector2(texture_Reset.width, texture_Reset.height)), Vector2.zero);

            icons = new List<Icon>();

            iconContainer = new GameObject("Icon Container", typeof(RectTransform)).GetComponent<RectTransform>(); 
            
            var parent = (PageState)ConfigurationManager.instance.stateMachine.GetState(DoDad.Library.AI.State.State1);

            var state = parent as ControllerAssignmentState;

            followerContainer = state.followerContainer;

            iconContainer.SetParent(parent.page);
            iconContainer.SetSiblingIndex(3);
            iconContainer.localScale = Vector3.one;

            LayoutElement element = iconContainer.gameObject.AddComponent<LayoutElement>();

            element.preferredHeight = 64;

            iconContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        private void InitializePrefab()
        {
            iconPrefab = new GameObject("Icon Prefab", typeof(RectTransform)).GetComponent<RectTransform>();

            var parent = (PageState)ConfigurationManager.instance.stateMachine.GetState(DoDad.Library.AI.State.State1);

            iconPrefab.SetParent(parent.page);

            Icon icon = iconPrefab.gameObject.AddComponent<Icon>();

            iconPrefab.gameObject.SetActive(false);
        }
        private void InitializeIcons()
        {
            foreach(Controller controller in configuration.controllers)
            {
                CreateIcon(controller);
            }
        }
        private void ToggleListeners(bool status)
        {
            if (configuration is null)
                return;

            if(status)
            {
                configuration.onControllerConnected += OnControllerConnected;
                configuration.onControllerDisconnected += OnControllerDisconnected;
                configuration.onConfigurationUpdated.AddListener(OnConfigurationUpdated);
            }
            else
            {
                configuration.onControllerConnected -= OnControllerConnected;
                configuration.onControllerDisconnected -= OnControllerDisconnected;
                configuration.onConfigurationUpdated.RemoveListener(OnConfigurationUpdated);
            }
        }
        #endregion

        #region Icons
        public void OnConfigurationUpdated()
        {

        }
        public void OnControllerConnected(ControllerStatusChangedEventArgs args)
        {
            foreach (Icon icon in icons)
                if (icon.controller.Equals(args.controller))
                    return;

            CreateIcon(args.controller);
        }
        public void OnControllerDisconnected(ControllerStatusChangedEventArgs args)
        {
            int index = -1;

            for (int e = 0; e < icons.Count; e++)
            {
                if (!icons[e].controller.isConnected)
                {
                    index = e;
                    break;
                }
            }

            if (index > -1)
            {
                Destroy(icons[index].gameObject);
                icons.RemoveAt(index);
            }
        }
        public Sprite GetDeviceSprite(Controller controller)
        {
            Sprite sprite = sprite_Xinput;

            if (controller.name.ToLower().Contains("sony") || controller.name.ToLower().Contains("hyper"))
                sprite = sprite_Dinput;
            else if (controller.name.ToLower().Contains("key"))
                sprite = sprite_Keyboard;

            return sprite;
        }
        public Icon GetIcon(Controller controller)
        {
            if (controller is null)
                return null;

            foreach(Icon icon in icons)
            {
                if (icon.controller.Equals(controller))
                    return icon;
            }

            return null;
        }
        private void CreateIcon(Controller controller)
        {
            Icon icon = Instantiate(iconPrefab).GetComponent<Icon>();
            icon.name = $"(Icon) {controller.name}";
            icon.transform.SetParent(iconContainer);
            icon.transform.position = new Vector3(Screen.width, icons.Count > 0 ? icons[icons.Count - 1].transform.position.y : Screen.height, 0);
            icon.Initialize(controller, followerContainer);

            icon.onStartDragIcon.AddListener(OnStartDragIcon);
            icon.onStopDragIcon.AddListener(OnStopDragIcon);

            icon.gameObject.SetActive(true);
            icons.Add(icon);
        }
        #endregion

        #region Events
        public void OnStartDragIcon(Icon icon)
        {
            onStartDragIcon.Invoke(icon);
        }
        public void OnStopDragIcon(Icon icon)
        {
            onStopDragIcon.Invoke(icon);
        }
        #endregion

        #region Definitions
        #endregion
    }
}