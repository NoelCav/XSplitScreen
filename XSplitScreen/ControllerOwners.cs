using Rewired;
using System;
using System.Collections.Generic;

namespace Dodad.XSplitscreen
{
	// (specter) Used to record which player owns which controller so that when a controller disconnects and then reconnects, we can reassign it to the correct player.
	internal static class ControllerOwners
	{
		private static readonly Dictionary<Guid, string> owners = new Dictionary<Guid, string>();

		internal static void Record(Controller controller, Player player)
		{
			if (controller is Joystick joystick && joystick.deviceInstanceGuid != Guid.Empty)
				owners[joystick.deviceInstanceGuid] = player.name;
		}

		internal static void Record(Player player)
		{
			foreach (var joystick in player.controllers.Joysticks)
				Record(joystick, player);
		}

		internal static string GetOwner(Controller controller)
		{
			return controller is Joystick joystick && owners.TryGetValue(joystick.deviceInstanceGuid, out var name) ? name : null;
		}
	}
}
