* Pathing: more efficient data structs
* General: Add logging
* General: code comments, for the love of God!
* General: exceptions and data validation
* AI: create different behavior types: rush, march, berserk.
* Pathing: add hurt-map to allow attackers to avoid especially dangerous places.
* AI: add unit-specific pathfinding params for crowing, hurt-avoidance, etc.
* Editor: edit spawn points
* Editor: edit goal points
* Editor: edit attack spawn list
* Editor: edit defender placement
* Units: new or improved types
    * Pikemen - range 2 melee for attacking over barriers
    * Knight - high hp/dmg melee.  (Slow?)
    * Berserker - melee that seeks out combat over following path
    * Scout - high damage avoidance, high speed
    * Archer - on attack, stops to shoot whenever possible
* Terrain types (test/implement)
    * Open - regular open ground
    * Wall
    * Water - blocks movement but not vision/shooting
    * Fog - blocks vision but not movement.
