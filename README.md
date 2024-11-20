# <u> _Snake Game_</u>

## Author Information
- Authors: Sathya Tadinada and Jaron Tsao
- Date: December 9, 2023

## Summary
This README provides essential information for the Snake Game Client. It documents gameplay mechanics, design details, additional features, and other relevant details.

*Note: for best readability of this file, please use a [Markdown Text Viewer](https://stackedit.io/app#).*

## Getting Started

<u>Dependencies</u>
- .NET 7
- .NET MAUI

<u>Installation</u>

Clone the repository to your local machine. Open the solution file (SnakeGame.sln) in Visual Studio. Build the solution.

<u>Running the Server</u>

Simply run the server executable.

<u>Running the Client</u>

1. Ensure that the Snake game server is running.
2. Run the SnakeGame client executable.
3. Enter your player name and the server's IP address or host name.
4. Connect to the server.


## Gameplay
- Control your snake using WASD (for up, left, down, and right, respectively).
- Collect powerups to grow your snake and increase your score.
- Avoid running into walls and other snakes (and yourself!)

## Additional Features
### Client-Side:
- Colorful Snakes: There are eight unique colors for snakes, along with corresponding stripes for enhanced visual appearance.
- Player Names: Each player's name is displayed above their snake, with different corresponding colors to the snake color.
- Connection Errors: The client notifies you of any connection errors and provides the option to retry connecting.
- Performance: The GUI is designed to keep up with the server, aiming for 30 frames per second.
- Explosions: When a player collides with a wall, another player, or themselves, a brief explosion appears at the point of collision.
### Server-Side:
- Teleporting Boundaries: When a player exceeds the border, they are wrapped around the other side rather than dying on impact (essentially "teleporting" the player).
- Special Mode: If the **\<Mode\>** is changed in the `settings.xml` file (detailed below), when a player dies and their score is 2 or above, a powerup with that player's score will spawn near their head. This allows other players to consume that powerup to gain more points quicker. Additionally, for every powerup consumed, snakes grow **\<SnakeGrowth\>** units instead of frames. This results in shorter snake growth overall. This can make the game more challenging, since it will take more time to reach longer lengths.

## Configuring the Game with the `settings.xml` File 
The `settings.xml` file is used to customize/modify the settings and other initial configurations of the Snake game. The following tags must be used in order to change the default settings:


- **\<Mode\>**: The particular gamemode. There are two options: `Standard` and `Special`. Default is **Standard**. 
- **\<MSPerFrame\>**: Milliseconds per frame (i.e. how long is each frame?). Default is **34** milliseconds.
- **\<RespawnRate\>**: The number of frames a player must wait before respawning. Default is **100** frames.
- **\<UniverseSize\>**: The number of units on each side of the square universe. Default is **2000** units.
- **\<SnakeSpeed\>**: The speed of a snake. Default is **6** units per frame.
- **\<SnakeStartingLength\>**: The length of a snake when they respawn. Default is **120** units.
- **\<SnakeGrowth\>**: The length that a snake increases by after consuming a powerup. Default is **24** frames worth of movement.
	- If the **\<Mode\>** is selected to `Special`, the length increase is X *units*.
- **\<MaxPowerups\>**: The maximum number of powerups that are initially spawned. Default is **20** powerups.
- **\<MaxPowerupDelay\>**: The maximum number of frames of delay between spawning new powerups. Default is **75** frames initially. The server picks a new number less than this every time powerups spawn.
- **\<Walls\>**: A list of walls that are located throughout the world. Default is **0** walls in the world.
	- **\<Wall\>**: An individual wall object, which consists of two points (where it starts and ends).
		- **\<ID\>**: The unique ID of a wall.
		- **\<p1\>**:  The starting point of the wall.
			- **\<x\>**:  The x-coordinate of the starting point.
			- **\<y\>**:  The y-coordinate of the starting point.
		- **\<p2\>**:  The ending point of the wall.
			- **\<x\>**:  The x-coordinate of the ending point.
			- **\<y\>**:  The y-coordinate of the ending point.

A sample file is given below.
```xml
<GameSettings>
	<Mode>Standard</Mode>
	<MSPerFrame>34</MSPerFrame>
	<RespawnRate>100</RespawnRate>
	<UniverseSize>2000</UniverseSize>
	<SnakeSpeed>6</SnakeSpeed>
	<SnakeStartingLength>120</SnakeStartingLength>
	<SnakeGrowth>24</SnakeGrowth>
	<MaxPowerups>20</MaxPowerups>
	<MaxPowerupDelay>75</MaxPowerupDelay>
	<Walls>
		<Wall>
			<ID>0</ID>
			<p1>
				<x>-975</x>
				<y>-975</y>
			</p1>
			<p2>
				<x>975</x>
				<y>-975</y>
			</p2>
		</Wall>
		...
	</Walls>
</GameSettings>
```

## Design Decisions
- Separation of Concerns: The solution follows the MVC architecture to separate the model, view, and controller components.
    - Model: There are three separate object classes (`Snake`, `Powerup`, and `Wall`) and a `World` class that stores dictionaries of each of these objects (using their unique IDs as keys). 
    - View: The `WorldPanel.cs` file is responsible for drawing all of the components on the client window. There is basic logic involved to draw the walls, players, and images accurately. It also handles the death animation logic when a player dies.
    - Controller: There are three main controller classes -  `NetworkController`, `Server`, and `GameController`. All of the network components are abstracted into the `NetworkController` class while the `GameController` handles all the main game logic, such as JSON parsing and establishing the "handshake" between the client and the server.  The `Server` class deals with the actual storing and sending of data, as well as maintaining the overall world state.
### Client-Side:
- SnakeDrawer: Spots are generated every 20 pixels along the snake, with their predetermined color associated with the snake color. This was calculated by getting a normalized direction vector from two endpoint vectors, and then looping through each segment, incrementing by the spot distance each time.
### Server-Side:
- Models: Every class (`Snake`, `Powerup`, and `Wall`) have Rectangle objects that act as the "hitboxes" for each of these objects. This allows us to easily check Snake-Snake, Snake-Powerup, and Snake-Wall collisions, along with being able to check Powerup-Wall collisions when randomly spawning in a powerup somewhere in the world. The `IntersectsWith()` method is used for the collision detections.
	- For the snakes, there is a `HeadRect` rectangle that is used to determine collisions with other objects. This is mainly because only a snake head can collide with other objects, so we would not need to calculate collisions with every object, every frame.
	- All powerups have a new property, `Value`. This allows the server to set specific values to each powerup to vary the player's score incrementation (useful in the `Special` gamemode, where a dead player spawns a powerup with their score).

## Known Issues
### Client-Side:
- The explosion only happens in one frame, making it seem too fast at times.
- There is slightly noticeable lag at 15+ players.
### Server-Side:
- When connecting the AI clients to the server (around 4 or 5), there is sometimes a stack overflow exception that occurs (slightly randomly).
- When a snake wraps around the world, there are cases where the snake gets teleported to a location not rendered on the map (the only working solution at this point is to collide with oneself and spawn again within the world).
