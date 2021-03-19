# Click To Move
**--This very much WIP!--**

**Click To Move** is a [Stardew Valley](https://stardewvalley.net/) mod which tries to bring to the 
PC version of the game the behaviour you have in the mobile version when using the Tap-to-move mode.

## Contents
* [Install](#install)
* [Use](#use)
* [Compatibility](#compatibility)
* [Troubleshooting](#troubleshooting)
  * [In-game overlay](#in-game-overlay)
  * [Console command](#console-command)
* [FAQs](#faqs)
* [See also](#see-also)

## Install
1. Install the latest version of [SMAPI](https://smapi.io/).
3. Install [this mod from Nexus mods](https://www.nexusmods.com/stardewvalley/mods/).
4. Run the game using SMAPI.

## Use
Click anywhere on screen to make the farmer walk to where you clicked.
If the path to the space is blocked, it will be highlighted in red. If the path becomes blocked due to 
moving NPCs, pets, or farm animals, the player character may suddenly stop and try a different path.
Keeping the mouse clicked will cause the character to follow the mouse. This feature can be used to 
push NPCs, pets, and farm animals out of the way but it is very literal, moving directly towards 
the cursor without routing around blocking objects.

Click on items to action them. Clicking on stones, wood, stumps, boulders, rocks, etc. will auto-select 
the correct tool for the job.
The one exception with tools is the scythe, which must be selected to use.

Clicking on an NPC, farm animal, or pet will move you next to them and interact with them. If they move, 
the player character will follow them, within limits. If a giftable item is currently selected and you 
click on an NPC, then they will get that item as a gift when you reach them.

**Auto-attack** - Whenever a weapon is selected (e.g. sword, dagger, club) the mod will check for monsters 
near the farmer and they will automatically face and attack any enemies that come within range. You can 
click elsewhere to walk away, however you cannot move while the weapon is in mid-swing.

# Compatibility
Since I had to rewrire large parts of the game code, I expect there will be compatibility issues with other mods.

During development I used a few mods and didn't detect serious problems but the truth is I only played the game 
while debugging for short periods of time.

If you are using the [Expanded Storage](https://www.nexusmods.com/stardewvalley/mods/7431), then disable the 
Controller option, either in the config.json file or in-game, if you have 
[Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) installed. In the latter case,
you can find the option in the Tweaks page.
