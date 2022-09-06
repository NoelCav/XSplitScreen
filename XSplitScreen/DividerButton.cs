using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.EventSystems;

namespace DoDad.UI.Components
{
    class DividerButton : RoR2.UI.MPButton
    {
        public bool isEnabled = true;

        private static DividerButton[] dividers;

        private int _id;
        private int _neighbor;

        private UnityEngine.Color _hiddenColor = new UnityEngine.Color(1, 1, 1, 0);
        private UnityEngine.Color _visibleColor = new UnityEngine.Color(1, 1, 1, 1);
        private UnityEngine.UI.Image _image;

        public override void Awake()
        {
            base.Awake();

            _image = GetComponent<UnityEngine.UI.Image>();
        }

        public void SetStatus(bool active)
        {
            isEnabled = active;
            SetVisibility(active);
        }
        public void AssignId(int id)
        {
            if (dividers == null)
                dividers = new DividerButton[4];

            dividers[id] = this;

            _id = id;

            switch(_id) // lazy
            {
                case 0:
                    _neighbor = 2;
                    break;
                case 1:
                    _neighbor = 3;
                    break;
                case 2:
                    _neighbor = 0;
                    break;
                case 3:
                    _neighbor = 1;
                    break;
            }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if(!isEnabled)
            {
                SetVisibility(true);
            }
        }
        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if(!isEnabled)
            {
                SetVisibility(false);
            }
        }

        private void SetVisibility(bool status)
        {
            if (status)
            {
                _image.color = _visibleColor;


            }
            else
            {
                _image.color = _hiddenColor;
            }
        }
    }
}
