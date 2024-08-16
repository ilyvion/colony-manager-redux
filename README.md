[![RimWorld 1.5](https://img.shields.io/badge/RimWorld-1.5-brightgreen.svg)](http://rimworldgame.com/) [![Build](https://github.com/ilyvion/colony-manager-redux/actions/workflows/ci.yml/badge.svg)](https://github.com/ilyvion/colony-manager-redux/actions/workflows/ci.yml)

**Colony Manager Redux** is ilyvion's new and improved take on Fluffy's Colony Manager. Why did I make my own custom version? This version may never have seen the light of day if Fluffy had kept updating the original (though it's understandable why it's been hard), but after a long time without updates (nearly two years at the time of this writing) it felt like the space was primed for a replacement/new contender. I've put enough effort and care into this project at this point that I consider it an entirely separate project from the original, and will not be removing it or deprecating it should the original Colony Manager make a comeback.

## Features

The purpose of the mod is to let you assign certain tedious managerial tasks to your colonists instead of you having to do them manually. The main principle of the mod is that you configure the resource you want, and how many of that resource you want to maintain, and then the mod, along with a colonist with the manager work type, makes sure that these targets are met.

Out of the box, the mod has the following manager jobs:

**Hunting**: Set how much meat you want, and which kinds of animals you want to hunt, and watch your hunters take care of it.  
**Forestry**: Set how much wood you want, and which kinds of trees you want to chop, and the plant cutters take care of the rest.  
**Forestry (clearing)**: Mark an area for clearing, like the immediate outside of your colony, and watch your enemies having nothing to take cover behind the next time you're raided!  
**Livestock**: Takes care of taming, culling (butchering excess), training and corralling your animals according to your specifications.  
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
