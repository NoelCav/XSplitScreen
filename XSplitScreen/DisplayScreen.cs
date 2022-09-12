using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DoDad.UI.Components
{
    public class DisplayScreen : Button
    {
        public ControllerIcon AssignedController;

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
