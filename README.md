# Description

A GUI-based splitscreen mod

# Instructions

1. Install the plugin and click the 'Enable' button in the XSplitScreen menu

If there aren't enough profiles the first profile will be copied

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

## Limitations

- v1.1.0 Gamepads *should* work - need testers
- v1.0.0 As of right now the mod only officially supports a single keyboard and mouse player and a single gamepad player

## Known Bugs

- Sometimes the ability to type in the console is broken. Try disabling and enabling the mod again or enabling it with the console open
- Occasionally loadouts aren't saved to the correct character
- Gamepad players are unable to use the buttons in the right-hand pane on the character select screen - use the mouse for now

## Special thanks

- iDeathHD for creating FixedSplitScreen

## Changelog

**1.1.0**

- Added menu
- Enable splitscreen for connected devices with one click
- Temporarily removed the add and remove player buttons
- Cursors should work for all gamepads now
- Full gamepad support is being tested
- Bugfixes

**1.0.0**

* First release.
