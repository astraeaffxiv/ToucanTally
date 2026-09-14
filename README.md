# Toucan Tally

Toucan Tally replaces the numbers that float over characters in Final Fantasy XIV. It hides the game's own
flying text and pop-up text, and shows the same hits, heals and buffs in boxes that you place on your screen
yourself. It is based on the World of Warcraft addon Parrot.

## What you see

Out of the box there are three boxes:

- **Outgoing**, on the right: your damage and your heals on other players.
- **Incoming**, on the left: damage and heals on you.
- **Notifications**, at the top: buffs and debuffs on you, the abilities you use, and your killing blows.

Every text has the icon of the skill or status that caused it. Some examples:

| What happened | What the text looks like |
|---|---|
| You hit an enemy | `12,345 Heavy Swing` |
| Your damage-over-time ticks | `1,571 Caustic Bite` |
| An AoE hits several enemies | `18,210 Overpower (5 hits)` |
| You heal a party member | `+5,412 Clemency` |
| An enemy hits you | `-12,338 attack`, with the enemy's name underneath |
| Someone heals you | `+21,093 Eukrasian Diagnosis` |
| You gain or lose a buff | `+ Sprint`, `- Sprint` |
| You use an ability | `Bloodwhetting` |
| You kill something | `Killing blow`, with the enemy's name underneath |

Crits, crit direct hits and killing blows are sticky. They zoom in big in the middle of their box, stay there
for a moment over the scrolling text, and then fade out.

## Commands

- `/toucan` opens the settings.
- `/toucan move` shows the boxes so you can drag and resize them.
- `/toucan debug` opens the debug window. You only need it when something looks wrong.

## Settings

- **General.** Turn hiding the game's own text on or off. Move the boxes, or show sample text to see your
  layout without fighting. Pick the font, size and outline that every box uses unless you change it for one
  box. Choose full numbers (12,345) or short ones (12.3k). Colour names by role. Show or hide the healer's
  name on heals, and hide heals that were all overheal. Reset everything to the factory settings.
- **Areas.** One entry per box. Set its size, text alignment and icons. Each box has two text styles: normal
  text and sticky text. For each you pick a font, an animation (Straight, Parabola, Angled, Sprinkler or
  Static), the direction it moves in, and how long it stays on screen. You can add as many boxes as you like.
- **Events.** One row per kind of text, such as "Your damage" or "Buff gained". Turn it on or off, choose
  which box it goes to, its colour, whether it is sticky, how long to wait to merge repeated hits, and the
  smallest number worth showing.
- **Modifiers.** Crit, direct hit, blocked, parried, resisted and overheal. Each one can add a word after the
  skill name, colour the number, and make the text sticky.
- **Filters.** Hide one skill or status you never want to see, or hide only its small numbers.
- **About.** Who made it, and links to GitHub and Patreon.

## How it works

For people who want to read the code. The plugin listens to four places in the game:

| Where | What the plugin does there |
|---|---|
| `BattleLog.AddToScreenLogWithScreenLogKind` | The game calls this for every hit, heal, miss and buff it wants to show. It tells who did it, to whom, with which skill, and the number. The plugin builds its own text from it, and stops the call so the game's own number never appears. |
| `BattleLog.AddToScreenLogWithLogMessageId` | Most attacks pass through this one first, and it then calls the one above. The plugin only logs it and always lets it through. |
| `ScreenLog.AddScreenLogEntry` | The very last step before the game puts text on screen. Crafting numbers only pass through here. Logged only. |
| `ActionEffectHandler.Receive` | The server's message that says what a skill did to each target. It arrives a moment before the numbers. The plugin uses it to name damage-over-time ticks, work out overheal, announce your abilities and spot killing blows. |

All four addresses come from the FFXIVClientStructs library, so the plugin has no custom signatures to update. When Dalamud updates during patch cycles, we can just piggy-back off the hard work of the reverse engineers.

## Building it yourself

```powershell
dotnet build ToucanTally -c Debug
```

In Dalamud, go to Settings, Experimental, Dev Plugin Locations, and add
`ToucanTally\bin\x64\Debug\ToucanTally.dll`. Then turn on Toucan Tally in `/xlplugins`.

## AI disclosure

See [AI_DISCLOSURE.md](AI_DISCLOSURE.md).

## License

AGPL-3.0-or-later.
