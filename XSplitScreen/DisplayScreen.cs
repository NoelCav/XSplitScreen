using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoDad.UI.Components
{
    class DisplayScreen : Button
    {
        public ControllerDraggable AssignedController;

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            UnityEngine.Debug.Log(gameObject.name + " OnPointerEnter");
        }

        public void OnChangedDisplay()
        {

        }
    }
}
