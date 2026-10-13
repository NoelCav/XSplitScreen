using Rewired;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dodad.XSplitscreen
{
	/// <summary>
	/// Extra reports for bug reports. Everything here is inactive unless Logging > Diagnostics is enabled.
	/// </summary>
	internal static class Diagnostics
	{
		/// <summary>
		/// Controller hardware the game has saved bindings for (RoR2.DefaultControllerMaps)
		/// </summary>
		private static readonly Dictionary<Guid, string> KnownControllers = new Dictionary<Guid, string>
		{
			{ DefaultControllerMaps.gamepadTemplateGuid, "Gamepad template" },
			{ DefaultControllerMaps.xboneControllerGuid, "Xbox One" },
			{ DefaultControllerMaps.xbox360ControllerGuid, "Xbox 360" },
			{ DefaultControllerMaps.switchDualGuid, "Switch Joy-Con pair" },
			{ DefaultControllerMaps.switchHandheldGuid, "Switch handheld" },
			{ DefaultControllerMaps.switchProGuid, "Switch Pro" },
			{ DefaultControllerMaps.PS4Guid, "PS4" },
			{ DefaultControllerMaps.PS5Guid, "PS5" },
		};

		/// <summary>
		/// Gameplay and UI actions checked in binding reports
		/// </summary>
		private static readonly Dictionary<int, string> CoreActions = new Dictionary<int, string>
		{
			{ RewiredConsts.Action.Jump, "Jump" },
			{ RewiredConsts.Action.Interact, "Interact" },
			{ RewiredConsts.Action.Equipment, "Equipment" },
			{ RewiredConsts.Action.PrimarySkill, "Primary" },
			{ RewiredConsts.Action.SecondarySkill, "Secondary" },
			{ RewiredConsts.Action.UtilitySkill, "Utility" },
			{ RewiredConsts.Action.SpecialSkill, "Special" },
			{ RewiredConsts.Action.Sprint, "Sprint" },
			{ RewiredConsts.Action.Ping, "Ping" },
			{ RewiredConsts.Action.Info, "Info" },
			{ RewiredConsts.Action.Pause, "Pause" },
			{ RewiredConsts.Action.UISubmit, "UISubmit" },
			{ RewiredConsts.Action.UICancel, "UICancel" },
			{ RewiredConsts.Action.UILMB, "UILMB" },
			{ RewiredConsts.Action.UICursorX, "UICursorX" },
			{ RewiredConsts.Action.UICursorY, "UICursorY" },
		};

		private static bool subscribedToControllerEvents;

		//-----------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Apply diagnostics hooks. Does nothing unless Diagnostics is enabled.
		/// </summary>
		internal static void Initialize()
		{
			if (!Log.DiagnosticsEnabled)
				return;

			Log.Diag("Diagnostics enabled");

			var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			var self = typeof(Diagnostics);
			var selfFlags = BindingFlags.Static | BindingFlags.NonPublic;

			// R6: which bindings each player ends up with
			Plugin.TryPatch(typeof(RoR2.LocalUser).GetMethod(nameof(RoR2.LocalUser.ApplyUserProfileBindingsToRewiredPlayer), flags),
				postfix: self.GetMethod(nameof(LocalUser_ApplyBindings_Postfix), selfFlags));

			// R5: which event system each character select screen listens to
			Plugin.TryPatch(typeof(CharacterSelectController).GetMethod("OnEventSystemDiscovered", flags),
				postfix: self.GetMethod(nameof(CharacterSelectController_OnEventSystemDiscovered_Postfix), selfFlags));
			Plugin.TryPatch(typeof(CharacterSelectController).GetMethod("OnEventSystemLost", flags),
				postfix: self.GetMethod(nameof(CharacterSelectController_OnEventSystemLost_Postfix), selfFlags));

			// R2: clicks coming from another player's event system or from a hidden cursor
			Plugin.TryPatch(typeof(MPButton).GetMethod(nameof(MPButton.OnPointerClick), flags),
				prefix: self.GetMethod(nameof(MPButton_OnPointerClick_Prefix), selfFlags));

			// R2: every path that closes a scrapper / printer / crafting panel
			Plugin.TryPatch(typeof(PickupPickerPanel).GetMethod("DestroyIt", flags),
				prefix: self.GetMethod(nameof(PickupPickerPanel_DestroyIt_Prefix), selfFlags));
			Plugin.TryPatch(typeof(CraftingPanel).GetMethod("DestroyIt", flags),
				prefix: self.GetMethod(nameof(CraftingPanel_DestroyIt_Prefix), selfFlags));
			Plugin.TryPatch(typeof(PickupPickerController).GetMethod("OnDisplayEnd", flags),
				prefix: self.GetMethod(nameof(PickupPickerController_OnDisplayEnd_Prefix), selfFlags));
			Plugin.TryPatch(typeof(NetworkUIPromptController).GetMethod(nameof(NetworkUIPromptController.SetParticipantMaster), flags),
				prefix: self.GetMethod(nameof(NetworkUIPromptController_SetParticipantMaster_Prefix), selfFlags));
		}

		/// <summary>
		/// Subscribe to controller connect events (Rewired must be ready)
		/// </summary>
		internal static void SubscribeToControllerEvents()
		{
			if (!Log.DiagnosticsEnabled || subscribedToControllerEvents)
				return;

			subscribedToControllerEvents = true;

			ReInput.ControllerConnectedEvent += args => ReportControllers($"controller connected: '{args.name}'");
			ReInput.ControllerDisconnectedEvent += args => ReportControllers($"controller disconnected: '{args.name}'");
		}

		//-----------------------------------------------------------------------------------------------------------

		#region Reports

		/// <summary>
		/// Log every controller and which Rewired players it is assigned to
		/// </summary>
		internal static void ReportControllers(string reason)
		{
			if (!Log.DiagnosticsEnabled || !ReInput.isReady)
				return;

			var sb = new StringBuilder();
			sb.AppendLine($"Controllers ({reason}):");

			foreach (var controller in ReInput.controllers.Controllers)
			{
				var owners = ReInput.players.Players
					.Where(p => p.controllers.ContainsController(controller.type, controller.id))
					.Select(p => p.name)
					.ToList();

				sb.Append($"  - [{controller.type} {controller.id}] '{controller.name}' (hardware '{controller.hardwareName}', identifier '{controller.hardwareIdentifier}')");

				if (controller.type == ControllerType.Joystick)
					sb.Append($", guid {controller.hardwareTypeGuid} ({DescribeGuid(controller.hardwareTypeGuid)})");

				sb.Append($", players: {(owners.Count == 0 ? "none" : string.Join(", ", owners))}");

				if (owners.Count > 1)
					sb.Append("  <-- assigned to more than one player");

				sb.AppendLine();
			}

			Log.Diag(sb.ToString().TrimEnd());
		}

		/// <summary>
		/// Log every local user, its input player, event system and controllers
		/// </summary>
		internal static void ReportLocalUsers(string reason)
		{
			if (!Log.DiagnosticsEnabled)
				return;

			var sb = new StringBuilder();
			var localUsers = LocalUserManager.readOnlyLocalUsersList;

			sb.AppendLine($"Local users ({reason}): {localUsers.Count}, isInLocalMultiPlayer = {RoR2Application.isInLocalMultiPlayer}");

			foreach (var localUser in localUsers)
			{
				var player = localUser.inputPlayer;
				var eventSystem = localUser.eventSystem;
				var controllers = player == null ? "none" : string.Join(", ", player.controllers.Controllers.Select(c => $"{c.type} {c.id} '{c.name}'"));

				sb.AppendLine($"  - LocalUser {localUser.id}: profile '{localUser.userProfile?.name}', input player '{player?.name}' (id {player?.id}), " +
					$"event system '{(eventSystem ? eventSystem.name : "null")}' (enabled {(eventSystem ? eventSystem.enabled.ToString() : "-")}), controllers: {controllers}");
			}

			Log.Diag(sb.ToString().TrimEnd());

			ReportControllers(reason);
		}

		private static string DescribeGuid(Guid guid)
		{
			return KnownControllers.TryGetValue(guid, out var name) ? $"known: {name}" : "UNKNOWN to RoR2, gets the Xbox One fallback layout";
		}

		/// <summary>
		/// Short stack trace without the diagnostics frames
		/// </summary>
		private static string GetCaller(int maxFrames = 8)
		{
			var lines = Environment.StackTrace.Split('\n')
				.Select(x => x.Trim())
				.Where(x => x.Length > 0 && !x.Contains("Environment") && !x.Contains(nameof(Diagnostics)))
				.Take(maxFrames);

			return "\n      " + string.Join("\n      ", lines);
		}

		/// <summary>
		/// Which players pressed the buttons that can act as a UI click this frame
		/// </summary>
		private static string GetPressedButtons()
		{
			var pressed = new List<string>();

			foreach (var eventSystem in MPEventSystem.readOnlyInstancesList)
			{
				if (!eventSystem || !eventSystem.enabled || eventSystem.player == null)
					continue;

				var player = eventSystem.player;
				var buttons = CoreActions.Where(a => player.GetButtonDown(a.Key)).Select(a => a.Value).ToList();

				if (buttons.Count > 0)
					pressed.Add($"{player.name}: {string.Join("/", buttons)}");
			}

			return pressed.Count == 0 ? "none" : string.Join("; ", pressed);
		}

		private static string GetPath(Transform transform)
		{
			var names = new List<string>();

			for (var t = transform; t != null && names.Count < 6; t = t.parent)
				names.Add(t.name);

			names.Reverse();

			return string.Join("/", names);
		}

		#endregion

		//-----------------------------------------------------------------------------------------------------------

		#region Hooks

		private static void LocalUser_ApplyBindings_Postfix(RoR2.LocalUser __instance)
		{
			try
			{
				var player = __instance.inputPlayer;
				var profile = __instance.userProfile;

				if (player == null || profile == null)
					return;

				var sb = new StringBuilder();
				sb.AppendLine($"Bindings applied for LocalUser {__instance.id} ('{profile.name}', input player '{player.name}'):");

				foreach (var controller in player.controllers.Controllers)
				{
					if (controller.type != ControllerType.Joystick)
						continue;

					var guid = controller.hardwareTypeGuid;
					bool savedInProfile = profile.HardwareJoystickMaps2 != null && profile.HardwareJoystickMaps2.ContainsKey(guid);

					sb.AppendLine($"  - Joystick {controller.id} '{controller.name}', guid {guid} ({DescribeGuid(guid)}), saved in profile: {savedInProfile}");

					var mappedActions = new HashSet<int>();

					foreach (var map in player.controllers.maps.GetMaps(controller.type, controller.id))
					{
						sb.AppendLine($"      map category {map.categoryId}, layout {map.layoutId}, enabled {map.enabled}, map guid {map.hardwareGuid}, {map.AllMaps.Count} bindings");

						if (map.enabled)
							foreach (var element in map.AllMaps)
								mappedActions.Add(element.actionId);
					}

					var missing = CoreActions.Where(a => !mappedActions.Contains(a.Key)).Select(a => a.Value).ToList();
					sb.AppendLine($"      unbound actions: {(missing.Count == 0 ? "none" : string.Join(", ", missing))}");
				}

				Log.Diag(sb.ToString().TrimEnd());
			}
			catch (Exception e)
			{
				Log.Diag($"Binding report failed: {e.Message}");
			}
		}

		private static void CharacterSelectController_OnEventSystemDiscovered_Postfix(CharacterSelectController __instance, MPEventSystem __0)
		{
			var provider = __instance.GetComponentInParent<MPEventSystemProvider>();
			string providerInfo = provider ? $"explicit '{(provider.eventSystem ? provider.eventSystem.name : "none")}', fallBackToMain {provider.fallBackToMainEventSystem}, P2 {provider.fallBackToPlayer2EventSystem}, P3 {provider.fallBackToPlayer3EventSystem}, P4 {provider.fallBackToPlayer4EventSystem}" : "no provider";

			Log.Diag($"Character select '{__instance.name}' now listens to '{(__0 ? __0.name : "null")}' (player '{__0?.player?.name}', profile '{__0?.localUser?.userProfile?.name}'). Provider: {providerInfo}");
		}

		private static void CharacterSelectController_OnEventSystemLost_Postfix(CharacterSelectController __instance, MPEventSystem __0)
		{
			Log.Diag($"Character select '{__instance.name}' lost event system '{(__0 ? __0.name : "null")}'");
		}

		private static void MPButton_OnPointerClick_Prefix(MPButton __instance, PointerEventData __0)
		{
			var module = __0?.currentInputModule;
			var clickingSystem = module ? module.GetComponent<MPEventSystem>() : null;
			var buttonSystem = __instance.eventSystem;

			bool fromOtherPlayer = clickingSystem && buttonSystem && clickingSystem != buttonSystem;
			bool hiddenCursor = clickingSystem && !clickingSystem.isCursorVisible;

			Log.Diag($"Click on '{GetPath(__instance.transform)}' from '{(clickingSystem ? clickingSystem.name : "null")}' (player '{clickingSystem?.player?.name}', cursor visible {(clickingSystem ? clickingSystem.isCursorVisible.ToString() : "-")}). " +
				$"Button belongs to '{(buttonSystem ? buttonSystem.name : "null")}', allowAllEventSystems {__instance.allowAllEventSystems}" +
				(fromOtherPlayer ? "  <-- click from another player" : "") +
				(hiddenCursor ? "  <-- click from a hidden cursor" : ""));
		}

		private static void PickupPickerPanel_DestroyIt_Prefix(PickupPickerPanel __instance)
		{
			Log.Diag($"Picker panel '{__instance.name}' closing because a scoreboard opened. Buttons pressed this frame: {GetPressedButtons()}");
		}

		private static void CraftingPanel_DestroyIt_Prefix(CraftingPanel __instance)
		{
			Log.Diag($"Crafting panel '{__instance.name}' closing because a scoreboard opened. Buttons pressed this frame: {GetPressedButtons()}");
		}

		private static void PickupPickerController_OnDisplayEnd_Prefix(PickupPickerController __instance, RoR2.LocalUser __1)
		{
			Log.Diag($"Picker '{__instance.name}' display ended for LocalUser {__1?.id} ('{__1?.userProfile?.name}'). Buttons pressed this frame: {GetPressedButtons()}{GetCaller()}");
		}

		private static void NetworkUIPromptController_SetParticipantMaster_Prefix(NetworkUIPromptController __instance, CharacterMaster __0)
		{
			if (__0 != null)
				return;

			Log.Diag($"Prompt '{__instance.name}' participant cleared (server). Buttons pressed this frame: {GetPressedButtons()}{GetCaller()}");
		}

		#endregion
	}
}
