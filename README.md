# Deasterisk

FFXIV CN client-side profanity filter disabler.

Due to the government's mandatory requirements for the game industry, FFXIV CN client cannot disable the profanity filter via in-game settings. This project allows the player to manually turn off this filter via technical means.

## Overview

When the chat data arrives at the client (receiver), the chat content is filtered on the client and whether to display the filtered version is determined by related switch in settings.

Since the Chinese version of the client prohibits the adjustment of related configuration items and makes the relevant filter always on (ignoring the status of "Enable profanity filter" checkbox and the stored value in configuration file). Attempting to close this filter by modify local configure file is not possible. Therefore, you need to disable the filter by altering something more fundamental, which is the filtering-process itself.

This project implements a way to do this programmatically. Which makes it possible for average players to disable this filter on their own will.

## Why

Since the Chinese version of FFXIV is operated jointly by Square Enix and Shengqu Games, there is a lot of uncertainty and lack of transparency in words which may trigger the profanity filter. 

At the time of the initial development of this project, words such as BGM, NPC, missile, stack, Alamigo and Alexander (in Chinese) were also considered inappropriate. Some quests requiring players to say/shout out specific sentences also cannot progressed since related words are filtered out.

Currently, some of the above mentioned words are still in the block list.

This has a big impact on the player's gaming experience.

## Scope of effect

Anything displayed in Chat Box, Moogle Mails and recruit criterias in the Party Finder. 

Client-side only. No effect on other players.

## Ban Disclaimer

If you use this library, you are using it at your own risk.

- Can I get banned for using this?

Potentially yes. It is a third-party tool.

- What is the likely-hood of this happening?

Low. No user has reported that they have been banned so far.
