using Rewired;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoDad.UI.Components
{
    // TODO ensure only the MainPlayer can interact with items
    [RequireComponent(typeof(RectTransform))]
    class ControllerDraggable : MPButton
    {
        #region Variables
        public Controller Controller;
        public int ScreenIndex = -1;

        public int ChildId { 
            get { 
                return _childId; 
            } 
            set { 
                _childId = value; 
                ToggleDrag(false); 
            } 
        }

        private int _childId;

        public bool IsDragging = false;
        public bool HasFollower = false;

        private RectTransform rectTransform;
        private Transform parent;

        // OnClick is called twice per frame for gamepads
        private bool _frameHadInput = false;
        #endregion

        #region Unity Methods
        public override void Awake()
        {
            base.Awake();

            image = GetComponent<Image>();

            rectTransform = gameObject.GetComponent<RectTransform>();
        }

        public override void Start()
        {
            base.Start();

            this.onClick.AddListener(OnClick);

            parent = rectTransform.parent;
            eventSystemLocator.Awake();
        }

        public override void OnPointerDown(PointerEventData data)
        {
            base.OnPointerDown(data);

            ToggleDrag(true);
        }
        new public void Update()
        {
            base.Update();

            if (Controller == null)
                Destroy(gameObject);
            else
            {
                if (!Controller.isConnected)
                    Destroy(gameObject);
            }

            if (IsDragging)
            {
                if (eventSystem == null)
                    return;

                rectTransform.position = eventSystem.currentInputModule.input.mousePosition;
                //rectTransform.position = Vector3.SmoothDamp(rectTransform.position, eventSystem.currentInputModule.input.mousePosition, ref _velocity, 0.1f);
            }

            _frameHadInput = false;
        }

        #endregion

        #region Logic
        public void AssignToScreen(DisplayScreen newParent)
        {

        }
        /// <summary>
        /// Determine if the current controller is a gamepad. If so, toggle drag mode. Otherwise disable it.
        /// </summary>
        private void OnClick()
        {
            if (eventSystem.currentInputSource == MPEventSystem.InputSource.MouseAndKeyboard)
            {
                ToggleDrag(false);
                return;
            }

            if (_frameHadInput)
                return;

            _frameHadInput = true;
            
            ToggleDrag(IsDragging ? false : true);
        }

        private void ToggleDrag(bool status)
        {
            IsDragging = status;

            switch(status)
            {
                case true:
                    transform.SetParent(ControllerAssignmentManager.Instance.transform);
                    break;
                case false:
                    DisplayScreen newParent;

                    if (DisplayManager.EvaluateDraggable(this, out ScreenIndex, out newParent))
                    {
                        AssignToScreen(newParent);
                        break;
                    }

                    if (parent == null)
                        break;

                    transform.SetParent(parent);
                    transform.SetSiblingIndex(ChildId);
                    eventSystem.SetSelectedGameObject(null);
                    break;
            }

        }
        #endregion
    }
}
