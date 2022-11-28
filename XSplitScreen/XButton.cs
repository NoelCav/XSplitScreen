using DoDad.Library.Events;
using RoR2.UI;
using RoR2.UI.SkinControllers;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.UI.Button;

namespace XSplitScreen
{ // TODO move to Components
    
    // This was supposed to replace ingame buttons but that wasn't a good idea.
    public class XButton : HGButton
    {
        #region Variables
        public MonoEvent onHoverStart;
        public MonoEvent onHoverStop;
        public MonoEvent onPointerDown;
        public MonoEvent onPointerUp;
        public MonoEvent onClickMono;

        public ButtonClickedEvent migratedOnClick;

        public bool allowOutsiderOnPointerUp = false;

        private bool receivedClickThisFrame = false; // Gamepads click twice
        #endregion

        #region Unity Methods
        public override void Awake()
        {
            base.Awake();

            onPointerDown = new MonoEvent();
            onPointerUp = new MonoEvent();
            onClickMono = new MonoEvent();
            onHoverStart = new MonoEvent();
            onHoverStop = new MonoEvent();

            onSelect = new UnityEngine.Events.UnityEvent();
            onDeselect = new UnityEngine.Events.UnityEvent();

            onClick.AddListener(OnClick);
        }
        public override void OnEnable()
        {
            base.OnEnable();

            if (eventSystem == null)
                eventSystemLocator.Awake();
        }
        public new void Update()
        {
            base.Update();

            if (allowOutsiderOnPointerUp)
                CheckForOutsiderPointerUp();
        }
        public new void LateUpdate()
        {
            base.LateUpdate();

            receivedClickThisFrame = false;

            if (eventSystem == null)
                eventSystemLocator.Awake();
        }
        #endregion

        #region Interaction
        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            onPointerDown.Invoke(this);
        }
        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);

            onPointerUp.Invoke(this);
        }
        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            onHoverStart.Invoke(this);
        }
        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            onHoverStop.Invoke(this);
            eventSystem.SetSelectedGameObject(null);
        }
        public void OnClick()
        {
            if (receivedClickThisFrame)
                return;

            onClickMono.Invoke(this);
            migratedOnClick?.Invoke();
            receivedClickThisFrame = true;
        }
        private void CheckForOutsiderPointerUp() // Maybe just have the icon monitor the assignment for MouseButtonUp?
        {
            if (eventSystem is null)
                return;

            if (!InputModuleIsAllowed(eventSystem.currentInputModule))
                return;

            bool? mouseUp = (eventSystem?.currentInputModule?.input.GetMouseButtonUp(0));

            if (mouseUp != null)
                if ((bool)mouseUp)
                    onPointerUp.Invoke(this);
        }
        #endregion
    }
    public class XButtonConverter : MonoBehaviour
    {
        public bool migrateOnClick = false;

        // Button
        public ButtonClickedEvent migratedOnClick;

        // XButton
        public MonoEvent onPointerDown;
        public MonoEvent onPointerUp;
        public MonoEvent onClickMono;

        public bool allowOutsiderOnPointerUp = false;

        // MPButton
        public bool allowAllEventSystems;
        public bool submitOnPointerUp;
        public bool disablePointerClick;
        public bool disableGamepadClick;
        public UILayerKey requiredTopLayer;
        public bool defaultFallbackButton;

        // HGButton
        public TextMeshProUGUI textMeshProUGui;
        public Color originalColor;
        public bool showImageOnHover;
        public Image imageOnHover;
        public Image imageOnInteractable;
        public bool updateTextOnHover;
        public LanguageTextMeshController hoverLanguageTextMeshController;
        public string hoverToken;
        public string uiClickSoundOverride;
        public float buttonScaleVelocity;
        public float imageOnHoverAlphaVelocity;
        public float imageOnHoverScaleVelocity;

        // ButtonSkinController
        public UISkinData skinData;
        public bool useRecommendedButtonWidth = true;
        public bool useRecommendedButtonHeight = true;
        public bool useRecommendedImage = true;
        public bool useRecommendedMaterial = true;
        public bool useRecommendedAlignment = true;
        public bool useRecommendedLabel = true;

        // LanguageTextMeshController
        public string token;

        // Destroying components will take effect next frame. 
        private int frameCount = 0;
        private int frameToDestroy = 1;
        private bool initialized = false;

        public void Awake()
        {
            if(!initialized)
            {
                Initialize();
            }
        }
        public void Initialize(int frameToDestroy = 1)
        {
            if (gameObject.GetComponent<XButton>() != null)
            {
                Destroy(gameObject);
                return;
            }

            this.frameToDestroy = frameToDestroy;

            gameObject.SetActive(false);
            StoreReferences();
            DestroyComponents();
            gameObject.SetActive(true);
        }
        public void Update()
        {
            frameCount++;

            if(frameCount == frameToDestroy)
            {
                CreateXButton();
                Destroy(this);
            }
        }
        private void StoreReferences()
        {
            onPointerDown = new MonoEvent();
            onClickMono = new MonoEvent();
            onPointerUp = new MonoEvent();

            HGButton button = GetComponent<HGButton>();

            if (button is null)
            {
                Destroy(this);
                return;
            }

            if(migrateOnClick)
                migratedOnClick = button.onClick;

            allowAllEventSystems = button.allowAllEventSystems;
            submitOnPointerUp = button.submitOnPointerUp;
            disablePointerClick = button.disablePointerClick;
            disableGamepadClick = button.disableGamepadClick;
            requiredTopLayer = button.requiredTopLayer;
            defaultFallbackButton = button.defaultFallbackButton;

            textMeshProUGui = button.textMeshProUGui;
            originalColor = button.originalColor;
            showImageOnHover = button.showImageOnHover;
            imageOnHover = button.imageOnHover;
            imageOnInteractable = button.imageOnInteractable;
            updateTextOnHover = button.updateTextOnHover;
            hoverLanguageTextMeshController = button.hoverLanguageTextMeshController;
            hoverToken = button.hoverToken;
            uiClickSoundOverride = button.uiClickSoundOverride;
            buttonScaleVelocity = button.buttonScaleVelocity;
            imageOnHoverAlphaVelocity = button.imageOnHoverAlphaVelocity;
            imageOnHoverScaleVelocity = button.imageOnHoverScaleVelocity;

            ButtonSkinController controller = GetComponent<ButtonSkinController>();

            if(controller != null)
            {
                skinData = controller.skinData;
                useRecommendedButtonWidth = controller.useRecommendedButtonWidth;
                useRecommendedButtonHeight = controller.useRecommendedButtonHeight;
                useRecommendedImage = controller.useRecommendedImage;
                useRecommendedMaterial = controller.useRecommendedMaterial;
                useRecommendedAlignment = controller.useRecommendedAlignment;
                useRecommendedLabel = controller.useRecommendedLabel;
            }

            LanguageTextMeshController langController = GetComponent<LanguageTextMeshController>();

            if(langController != null)
                token = langController.token;
            // TODO ensure there are no broken references between button and text / languagecontroller
        }
        private void CreateXButton()
        {
            XButton button = GetComponent<XButton>();

            if (button != null)
            {
                Destroy(this);
                return;
            }

            button = gameObject.AddComponent<XButton>();

            button.migratedOnClick = migratedOnClick;

            button.allowAllEventSystems = allowAllEventSystems;
            button.submitOnPointerUp = submitOnPointerUp;
            button.disablePointerClick = disablePointerClick;
            button.disableGamepadClick = disableGamepadClick;
            button.requiredTopLayer = requiredTopLayer;
            button.defaultFallbackButton = defaultFallbackButton;

            button.textMeshProUGui = textMeshProUGui;
            button.originalColor = originalColor;
            button.showImageOnHover = showImageOnHover;
            button.imageOnHover = imageOnHover;
            button.imageOnInteractable = imageOnInteractable;
            button.updateTextOnHover = updateTextOnHover;
            button.hoverLanguageTextMeshController = hoverLanguageTextMeshController;
            button.hoverToken = hoverToken;
            button.uiClickSoundOverride = uiClickSoundOverride;
            button.buttonScaleVelocity = buttonScaleVelocity;
            button.imageOnHoverAlphaVelocity = imageOnHoverAlphaVelocity;
            button.imageOnHoverScaleVelocity = imageOnHoverScaleVelocity;

            button.onClickMono = onClickMono;
            button.onPointerUp = onPointerUp;
            button.onPointerDown = onPointerDown;

            if(skinData != null)
            {
                ButtonSkinController controller = gameObject.AddComponent<ButtonSkinController>();

                controller.skinData = skinData;
                controller.useRecommendedButtonWidth = useRecommendedButtonWidth;
                controller.useRecommendedButtonHeight = useRecommendedButtonHeight;
                controller.useRecommendedImage = useRecommendedImage;
                controller.useRecommendedMaterial = useRecommendedMaterial;
                controller.useRecommendedAlignment = useRecommendedAlignment;
                controller.useRecommendedLabel = useRecommendedLabel;

                controller.OnSkinUI();
            }

            LanguageTextMeshController langController = GetComponent<LanguageTextMeshController>();

            if(langController != null)
                langController.token = token;
        }
        private void DestroyComponents()
        {
            foreach (ViewableTag tag in gameObject.GetComponentsInChildren<ViewableTag>())
                Destroy(tag);

            Destroy(gameObject.GetComponent<ButtonSkinController>());
            Destroy(gameObject.GetComponent<UserProfileListElementController>());
            Destroy(gameObject.GetComponent<HGButton>());
        }
    }
}