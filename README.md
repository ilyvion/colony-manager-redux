[![RimWorld 1.5](https://img.shields.io/badge/RimWorld-1.5-brightgreen.svg)](http://rimworldgame.com/) [![Build](https://github.com/ilyvion/colony-manager-redux/actions/workflows/ci.yml/badge.svg)](https://github.com/ilyvion/colony-manager-redux/actions/workflows/ci.yml)

> [!IMPORTANT]  
> You might be used to downloading people's GitHub mods by using the **Code -> Download ZIP** method, but this won't work on my repos[^badpractice]; I make use of proper releases and you can always find the latest version of the mod for download on the [Releases page](https://github.com/ilyvion/colony-manager-redux/releases/latest).

> [!IMPORTANT]  
> This mod depends on [ilyvion's Laboratory](https://github.com/ilyvion/ilyvion-laboratory) to work. If you're installing this mod manually (i.e. not from the Steam Workshop), make sure you install it too. Also, whenever this mod requires an update to ilyvion's Laboratory to function properly, I will make sure to announce that in the change notes, so when you update this mod, either also always update ilyvion's Laboratory, to be safe, or track which version you have so you know when to update.

**Colony Manager Redux** is ilyvion's new and improved take on Fluffy's Colony Manager. Why did I make my own custom version? This version may never have seen the light of day if Fluffy had kept updating the original (though it's understandable why it's been hard), but after a long time without updates (nearly two years at the time of this writing) it felt like the space was primed for a replacement/new contender. I've put enough effort and care into this project at this point that I consider it an entirely separate project from the original, and will not be removing it or deprecating it should the original Colony Manager make a comeback.

## Features

The purpose of the mod is to let you assign certain tedious managerial tasks to your colonists instead of you having to do them manually. The main principle of the mod is that you configure the resource you want, and how many of that resource you want to maintain, and then the mod, along with a colonist with the manager work type, makes sure that these targets are met.

Out of the box, the mod has the following manager jobs:

**Hunting**: Set how much meat you want, and which kinds of animals you want to hunt, and watch your hunters take care of it.  
**Forestry**: Set how much wood you want, and which kinds of trees you want to chop, and the plant cutters take care of the rest.  
**Forestry (clearing)**: Mark an area for clearing, like the immediate outside of your colony, and watch your enemies having nothing to take cover behind the next time you're raided!  
**Livestock**: Takes care of taming, culling (butchering or releasing excess), training and corralling your animals according to your specifications.  
**Foraging**: Set how much you want of berries/herbal medicine/mushrooms, and watch your colonists go out and collect it.  
**Resource gathering**: Set how much steel/silver/gold/jade/stone/etc. you want, and watch your colonists haul chunks for processing and mine the resources you're after automatically.

## Background

One of the biggest issues I had with the original was that its tabs and manager jobs were hard-coded, so initially I was going to leave it mostly as it was, but with the ability for third party modders to add their own manager tabs/jobs. But, as is often the case when I get really into a project, I got completely absorbed into the project, came up with a ton of new ideas, and spent a whole month implementing everything I could think of. The result is this mod.

I also intend to keep adding features (and feature requests) and fixing bugs going forward, so make sure you report any bugs you encounter and request any new features you want!

To see how much has changed since the original, you don't have to look any further than the [change log](CHANGELOG.md). Any change I made that had an impact on the behavior of the mod has been dilligently documented there.

It would be way too much to list it all here, but here are some highlights:

-   As mentioned in the introduction, adding a new manager job/tab by third parties is now directly supported by making them Def-based. More on this farther down.
-   Added comprehensive mod settings to let players configure many more aspects of the mod
-   Importing and exporting jobs is back
-   You can disable the recording of historical data, which is a bit of a performance drain (it is on by default)
-   Jobs log their activities so you can go back and look at what the mod's been doing (the 100 last logs are kept by default)
-   The manager work now has a much higher priority (placed between Warden and Handle) instead of being relegated to less important than Research, as managing the colony is important work.
-   Guest animals (such as those from Royalty quests) are now properly handled by the relevant parts of the livestock job (such as limiting them to the set area) without involving them fully (they don't get counted against the target population and won't be automatically slaughtered, e.g.)
-   _A lot_ of small and not so small changes have been made to the UI; again, check the change log for all the details.
-   _A lot_ of small and not so small bugs have been fixed.

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

-   _None yet_

Want your translation in this list? Release a translation mod (i.e. a mod with only a Languages folder, [i]not a copy of this entire mod + the translation[/i]) for this mod, and notify me of its existence, and I'll add it to the list.

The original Colony Manager had some translations to various languages, but when I updated the mod, I added, changed and removed enough translations that it doesn't feel worthwhile to include these anymore. Plus, as mentioned above, I'd prefer translations to be separate mods to reduce my own maintenance load. You can find the old translation files from the original under the [OldLanguages](OldLanguages) directory.

## License

The software and documentation is licensed under the MIT license ([LICENSE](LICENSE) or http://opensource.org/licenses/MIT) and any original content is licensed under the Creative Commons Attribution-ShareAlike 4.0 International Public License ([LICENSE](LICENSE) or https://creativecommons.org/licenses/by-sa/4.0/)

### Contribution

Unless you explicitly state otherwise, any contribution intentionally submitted for inclusion in the work by you shall be licensed as above, without any additional terms or conditions.

### Attribution

Parts of this mod were created by, or derived from works created by;

-   Smashicons: top hat icon used in mod preview and mod icon graphics ([BY-NC](https://www.flaticon.com/authors/smashicons))
-   [jeonggihun](https://steamcommunity.com/id/jeonggihun): new textures for the work benches and AI manager based on the [[JGH] Colony Manager retexture](https://steamcommunity.com/sharedfiles/filedetails/?id=2603340242) mod. Used with permission; license unknown.

[^badpractice]: I think this is really bad practice, but I won't fault less experienced developers for not setting up a whole build and release workflow since it's a rather advanced DevOps topic. Still, you won't find me doing it because, again, I think it's really bad practice for a whole host of reasons.
