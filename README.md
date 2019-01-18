# BattlePlan

A crude tower-defense style game.

## Prerequisits

This app is written in .NET Core, an open-source cross-platform runtime.  You can download the SDK or runtime here:

https://dotnet.microsoft.com/download

## Running

Right now I'm assuming that anyone trying to run this is a dev, and so they have the SDK installed.  As such, these
instructions assume you're working from source code rather than a published app.

### Play

To "play" an already setup scenario, type:

    dotnet run play scenarios/test1.json

You can set up scenarios by editing the JSON files, or using the editor (see below).


### Edit

To interactively edit a scenario (map, attack placement, defender placement, etc.), type:

    dotnet run edit

or

    dotnet run edit scenarios/somefile.json

Not all features are supported yet.  But it looks nice!
