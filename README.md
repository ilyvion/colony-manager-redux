[![RimWorld 1.5](https://img.shields.io/badge/RimWorld-1.5-brightgreen.svg)](http://rimworldgame.com/) [![RimWorld 1.6](https://img.shields.io/badge/RimWorld-1.6-brightgreen.svg)](http://rimworldgame.com/) [![Build](https://github.com/ilyvion/colony-manager-redux/actions/workflows/ci.yml/badge.svg)](https://github.com/ilyvion/colony-manager-redux/actions/workflows/ci.yml)

> [!IMPORTANT]  
> You might be used to downloading people's GitHub mods by using the **Code -> Download ZIP** method, but this won't work on my repos[^badpractice]; I make use of proper releases and you can always find the latest version of the mod for download on the [Releases page](https://github.com/ilyvion/colony-manager-redux/releases/latest).

> [!IMPORTANT]  
> This mod depends on [ilyvion's Laboratory](https://github.com/ilyvion/ilyvion-laboratory) to work. If you're installing this mod manually (i.e. not from the Steam Workshop), make sure you install it too. Also, whenever this mod requires an update to ilyvion's Laboratory to function properly, I will make sure to announce that in the change notes, so when you update this mod, either also always update ilyvion's Laboratory, to be safe, or track which version you have so you know when to update.

**Colony Manager Redux** lets you assign tedious managerial tasks to your colonists instead of doing them by hand. You configure the resource you want and how much of it to maintain, and the mod, along with a colonist with the manager work type, takes care of the rest. It's a spiritual successor to Fluffy's Colony Manager, built as a separate, actively maintained project.

## Features

Out of the box, the mod has the following manager jobs:

**Hunting**: Set how much meat you want, and which kinds of animals you want to hunt, and watch your hunters take care of it.  
**Forestry**: Set how much wood you want, and which kinds of trees you want to chop, and the plant cutters take care of the rest.  
**Forestry (clearing)**: Mark an area for clearing, like the immediate outside of your colony, and watch your enemies having nothing to take cover behind the next time you're raided!  
**Livestock**: Takes care of taming, culling (butchering, releasing, or sterilizing excess), training and corralling your animals according to your specifications.  
**Foraging**: Set how much you want of berries/herbal medicine/mushrooms, and watch your colonists go out and collect it.  
**Resource gathering**: Set how much steel/silver/gold/jade/stone/etc. you want, and watch your colonists haul chunks for processing, mine, and run deep drills to get the resources you're after automatically.  
**Power**: Keeps an eye on your colony's power production, consumption and battery storage, and warns you if things are looking unbalanced.  
**Production**: Pick a recipe and the manager keeps a work table running it for you, either maintaining a stock of what it produces or consuming a surplus of some other resource; production jobs can even be chained together so raw materials flow automatically down a production chain.

Manager jobs and tabs are Def-based, so third party mods can add their own; see [For modders](#for-modders) below.

Beyond the jobs themselves, the mod includes:

-   A dedicated **Job Defaults** settings tab to configure the defaults new jobs start with, plus extensive per-job settings and performance settings to control how much work the mod does per tick.
-   **Import/export** for individual jobs, plus reusable **templates** you can save and automatically apply to future colonies.
-   **Activity logging** and **history graphs** for each job, so you can see what the mod's been doing and how your stockpiles have trended over time.
-   Manager work is a high-priority work type (placed between Warden and Handle), since managing the colony is important work.
-   If a gravship has a manager database facility built on it, its manager jobs travel with it when it launches and lands, and you're asked which set of jobs to keep if the map it lands on already has its own.
-   Guest animals (such as those from Royalty quests) are handled by the livestock job without being fully managed — they're not counted against targets and won't be automatically slaughtered.
-   Optional integrations add extra functionality when [AAAA](https://steamcommunity.com/sharedfiles/filedetails/?id=3264193512) (area evacuation on danger) or [Animal Genetics](https://steamcommunity.com/sharedfiles/filedetails/?id=2830943477) are also loaded.

I intend to keep adding features (and feature requests) and fixing bugs going forward, so make sure you report any bugs you encounter and request any new features you want! See the [change log](CHANGELOG.md) for a full history of changes.

The mod can be added to a game at any time. Removing the mod should be fine as well, at least it has been in my own testing. There will be a fairly large list of once-off errors when first loading such a save, however, due to the way Rimworld's save system works.

## For modders

So, you want to add your own manager job/tab to the mod? Awesome! I've written a small set of articles on how to do this on the [Wiki](https://github.com/ilyvion/colony-manager-redux/wiki/Adding-a-custom-manager-feature), and you're also free to come ask me any questions you may have on my [Discord server](https://discord.gg/J9Q78avHgM) and I also hang out in the RimWorld discord.

## Troubleshooting

If you get an error that looks like this:

```
ReflectionTypeLoadException getting types in assembly ColonyManagerRedux: System.Reflection.ReflectionTypeLoadException: Exception of type 'System.Reflection.ReflectionTypeLoadException' was thrown.
```

It most likely means that you've updated this mod but not ilyvion's Laboratory. I try my best to remember to announce when a new release requires an update to ilyvion's Laboratory, so I apologize in advance if I ever forget.

## Translations

-   [Chinese](https://steamcommunity.com/sharedfiles/filedetails/?id=3371946696) by [Akiu](https://steamcommunity.com/profiles/76561198986560305)

Want your translation in this list? Release a translation mod (i.e. a mod with only a Languages folder, [i]not a copy of this entire mod + the translation[/i]) for this mod, and notify me of its existence, and I'll add it to the list.

Translations from the original Colony Manager are no longer included, since too much has changed for them to stay accurate.

## License

The software and documentation is licensed under the MIT license ([LICENSE](LICENSE) or http://opensource.org/licenses/MIT) and any original content is licensed under the Creative Commons Attribution-ShareAlike 4.0 International Public License ([LICENSE](LICENSE) or https://creativecommons.org/licenses/by-sa/4.0/)

### Contribution

Unless you explicitly state otherwise, any contribution intentionally submitted for inclusion in the work by you shall be licensed as above, without any additional terms or conditions.

### Attribution

Parts of this mod were created by, or derived from works created by;

-   Smashicons: top hat icon used in mod preview and mod icon graphics ([BY-NC](https://www.flaticon.com/authors/smashicons))
-   [jeonggihun](https://steamcommunity.com/id/jeonggihun): new textures for the work benches and AI manager based on the [[JGH] Colony Manager retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2603340242) mod. Used with permission; license unknown.

[^badpractice]: I think this is really bad practice, but I won't fault less experienced developers for not setting up a whole build and release workflow since it's a rather advanced DevOps topic. Still, you won't find me doing it because, again, I think it's really bad practice for a whole host of reasons.
