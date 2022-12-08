using DoDad.Library.Math;
using DoDad.XSplitScreen.Components;
using Rewired;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DoDad.XSplitScreen
{
    public struct Assignment
    {
        public Controller controller; // Group: Assignment

        public Color color; // Group: Assignment

        public int2 position;  // Group: Screen

        public int displayId;  // Group: Display
        public int playerId; // Group: Assignment
        public int profileId; // Group: Assignment
        public int localId; // Group: Assignment

        public Assignment(Controller controller)
        {
            this.controller = controller;

            position = int2.negative;
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
                if (!(controller is null))
                {
                    return controller.type == ControllerType.Keyboard;
                }

                return false;
            }
        }
        public override string ToString()
        {
            return $"Assignment(position = '{position}', playerId = '{playerId}', profileId = '{profileId}', displayId = '{displayId}', localId = '{localId}', controller = '{(controller != null ? controller.name : "null")}')";
        }
        public bool MatchesPlayer(Preference preference)
        {
            return this.playerId == preference.playerId;
        }
        public bool MatchesPlayer(Assignment assignment)
        {
            return this.playerId == assignment.playerId;
        }
        public bool MatchesAssignment(Assignment assignment)
        {
            return this.playerId == assignment.playerId && this.deviceId == assignment.deviceId && this.controller.Equals(assignment.controller) && this.position.Equals(assignment.position);
        }
        public bool HasController(Controller controller)
        {
            if (this.controller is null)
                return false;

            return this.controller.Equals(controller);
        }
        public bool HasController()
        {
            if (this.controller is null)
                return false;

            return true;
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
            displayId = ControllerAssignmentState.currentDisplay;
        }
        public void Load(Controller controller)
        {
            this.controller = controller;
        }
        public void ClearPlayer()
        {
            this.controller = null;
            playerId = -1;
            profileId = -1;
            color = Color.white;
        }
        public void ClearScreen()
        {
            position = int2.negative;
            displayId = -1;
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
}
