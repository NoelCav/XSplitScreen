using DoDad.Library.Math;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DoDad.XSplitScreen
{
    public struct Preference
    {
        public Color color;

        public int2 position;

        public int displayId;
        public int playerId;
        public int profileId;

        public Preference(int playerId)
        {
            position = int2.negative;
            displayId = -1;
            profileId = -1;
            this.playerId = playerId;
            color = Color.white;
        }
        public override string ToString()
        {
            string newFormat = "Preference(position = '{0}', displayId = '{1}', playerId = '{2}', profileId = '{3}', color = '{4}')";

            return string.Format(newFormat, position, displayId, playerId, profileId, color);
        }
        public bool Matches(Preference preference)
        {
            return preference.playerId == this.playerId;
        }
        public void Update(Assignment assignment)
        {
            position = assignment.position;
            displayId = assignment.displayId;
            playerId = assignment.playerId;
            profileId = assignment.profileId;
            color = assignment.color;
        }
    }
}
