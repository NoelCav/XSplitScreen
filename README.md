# Description

A GUI-based splitscreen mod in development. My son really wanted to play Risk of Rain 2 together but all the splitscreen mods were broken by the latest updates. If you appreciate the work please join the Discord and try to play a game with us!

https://discord.gg/maHhJSv62G

Please see the Limitations section below for important information

Looking for testers. Anyone with multiple gamepads or monitors is welcome to send me a DM to test new features.

# Instructions

1. Install the plugin and click the 'Enable' button in the XSplitScreen menu

If there aren't enough profiles the first profile will be copied. Console commands are also available.

# Keybinds

`F2`

- Switch between vertical and horizontal splitscreen while ingame

# Console commands

Commands that affect local users can only be used in the main menu

`xsplitset [1-4] [kb]`

 - Set the number of local players and optionally request a keyboard player

`xsplitswap [1-4] [1-4]`

 - Swap the profiles of two signed in users

`xdevice_status`

 - View who owns what device

## Planned

- Profile selection window
- Configure controller assignment
- Multi monitor support
- Keyboard mode button
- Risk of Options support
- Config customization for default settings

## Limitations

- v1.1.0 Gamepads *should* work - need testers
- v1.0.0 As of right now the mod only officially supports a single keyboard and mouse player and a single gamepad player

## Known Bugs

- v1.2.0 may break other mods that manipulate the camera. This is being worked on. If you experience problems fall back to v1.1.2
- Opening rule panels do not work for gamepads but selecting the rules themselves do. Try using the mouse for now
- Occasionally loadouts aren't saved to the correct character
- Multiple gamepads with no keyboard is untested or broken

## Special thanks

- iDeathHD for creating FixedSplitScreen

## Changelog

**1.2.1**

- Fixed character selection commando invasion bug
- Temporary fix for non-existant controllers being recognized. If you have problems with some controllers now NOT being recognized, you'll have to wait until the profile selection window has been completed. That should happen in the next month or so

**1.2.0**

- Added vertical splitscreen. If mods that manipulate the camera break fall back to 1.1.2

**1.1.2**

- Fixed chat and console
- Most clickable UI buttons should work for the gamepad cursor now
- Selecting items via the Command Artifact should be fixed
- Removed screen blur when viewing scoreboard
- Added Discord link and logging for better troubleshooting

**1.1.1**

- Added dependency string for R2API. This should fix the mod not loading if you didn't have R2API previously

**1.1.0**

- Added menu
- Enable splitscreen for connected devices with one click
- Temporarily removed the add and remove player buttons
- Cursors should work for all gamepads now
- Full gamepad support is being tested
- Bugfixes

**1.0.0**

* First release
