# BattlePlan

A crude tower-defense style game.

## Prerequisits

This app is written in .NET Core, an open-source cross-platform runtime.  You can download the SDK or runtime here:

https://dotnet.microsoft.com/download

## Running

If you're working from source code and the .NET Core SDK, you run the app like this:

    dotnet run <some_args>

If you're running from a published DLL, it's like this:

    dotnet battleplan.dll <some_args>

The rest of these instructions assume the earlier situation, but hopefully it's obvious how to adapt.

### Play

To "play" an already setup scenario, type:

    dotnet run play scenarios/test1.json

You can set up scenarios by editing the JSON files, or using the editor (see below).


### Edit

To interactively edit a scenario (map, attack placement, defender placement, etc.), type:

    dotnet run edit

or

    dotnet run edit scenarios/somefile.json

The right sidebar lists commands available in the current mode.  Hit Enter to change modes.  In some modes you can hit T to change which team you're editing for.  In Attacks mode, you must also pick a spawn point and time delay to add attackers at.

In most other modes, you use the cursor keys to move around the map and then various keys to place terrain, spawns/goals, or defenders.

