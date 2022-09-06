using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DoDad.UI.Components
{
    class ImageFollower : MonoBehaviour
    {
        public ControllerDraggable Target;

        public bool Excess = false;
        public int ChildIndex = -1;

        private RectTransform _rectTransform;
        private Image _image;

        private Color _hiddenColor = Vector4.zero;
        private Color _inactiveColor = new Color(1, 1, 1, 0.25f);
        private Color _activeColor = new Color(1, 1, 1, 1);

        private Vector3 _velocity;

        public void Awake()
        {
            _rectTransform = gameObject.GetComponent<RectTransform>();
            _image = gameObject.GetComponent<Image>();
        }
        public void Update()
        {
            if (Target == null)
            {
                if (Excess)
                    Destroy(gameObject);

                _image.color = _hiddenColor;
                return;
            }

            if (Target.Controller.GetAnyButton())
            {
                _image.color = _activeColor;
            }
            else
            {
                _image.color = _inactiveColor;
            }

            _rectTransform.position = Vector3.SmoothDamp(_rectTransform.position, Target.transform.position, ref _velocity, 0.1f);
        }
        public void SetTarget(ControllerDraggable target)
        {
            if (target == null)
                return;

            Target = target;
            Target.HasFollower = true;

            RectTransform rectTransform;

            if (_rectTransform)
                rectTransform = _rectTransform;
            else
                rectTransform = gameObject.GetComponent<RectTransform>();

            rectTransform.position = Target.transform.position;
        }
        public void FindTarget()
        {
            Transform target = ControllerAssignmentManager.Instance.controllerDraggables.GetChild(ChildIndex);

            if (target != null)
            {
                ControllerDraggable draggable = target.GetComponent<ControllerDraggable>();

                if (!draggable.HasFollower)
                {
                    SetTarget(draggable);
                }
            }

            Destroy(gameObject);
            //foreach (ControllerDraggable target in ControllerAssignmentManager.instance.ControllerDraggables.GetComponentsInChildren<ControllerDraggable>())
            //{

            //}
        }
    }
}
