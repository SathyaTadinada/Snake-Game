using Models;
using NetworkUtil;
using SnakeGame;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server;

public class Server {
    private static Dictionary<long, SocketState> clients = new();
    private static World world = new World(2000);
    private readonly Settings settings = new Settings();
    private static int MSPerFrame;
    private static int MaxPowerups;
    private static int MaxPowerupDelay;
    private static int RespawnRate;
    private static int SnakeSpeed;
    private static int SnakeGrowth;
    private static bool Mode;

    private static readonly Random rand = new();

    private static Dictionary<int, Snake> snakesToAdd = new();
    private static Dictionary<int, Powerup> powerupsToAdd = new();

    private static int randomPowerupSpawn;
    private static int worldSize = 2000;
    private static int totalPowerupCounter = 0;

    private int powerupFrames;
    private static readonly Stopwatch updateWatch = new();
    private bool firstClient = true;

    private static readonly Stopwatch sendWatch = new();

    private static long interval = 0;
    private static readonly int snakeWidth = 10;

    public static void Main(string[] args) {
        Server server = new Server();
        server.InitializeComponents();
        Console.WriteLine("Successfully initialized.\n\n--------------------------------\n");

        // starts an event loop for accepting connections
        server.StartServer();

        randomPowerupSpawn = rand.Next(MaxPowerupDelay);

        // starts the frame loop - an infinite loop so main method doesn't close
        updateWatch.Start();
        sendWatch.Start();
        while (true) {
            // wait until the next frame
            while (updateWatch.ElapsedMilliseconds < MSPerFrame) { /* empty loop body */ }
            interval += updateWatch.ElapsedMilliseconds;

            updateWatch.Restart();

            server.Update();

            if (interval >= 1000) {
                Console.WriteLine("FPS: " + interval / MSPerFrame);
                interval = 0;
            }
        }
    }

    /// <summary>
    /// Initializes all the relevant game component settings
    /// </summary>
    private void InitializeComponents() {
        string xmlFileString = "C:\\Users\\sathy\\Source\\Repos\\game-bit_by_bit\\PS8\\Server\\settings.xml";
        settings.ReadFromXML(xmlFileString);
        Console.WriteLine("Loading preferences from settings.xml...\n");

        // transfers over all relevant initial settings from the Settings class into this Server
        worldSize = settings.WorldSize;
        world = new World(worldSize);
        foreach (Wall wall in settings.Walls) {
            world.Walls.Add(wall.wall, wall);
        }

        Mode = settings.Mode;
        MSPerFrame = settings.MSPerFrame;
        MaxPowerups = settings.MaxPowerups;
        MaxPowerupDelay = settings.MaxPowerupDelay;
        RespawnRate = settings.RespawnRate;
        SnakeSpeed = settings.SnakeSpeed;
        SnakeGrowth = settings.SnakeGrowth;
    }

    /// <summary>
    /// Starts the server
    /// </summary>
    public void StartServer() {
        Console.WriteLine("Server is running. Accepting clients.");
        Networking.StartServer(NewClientConnected, 11000);
    }

    /// <summary>
    /// Callback when a new client connects to the server
    /// </summary>
    /// <param name="s">The SocketState of the player</param>
    private void NewClientConnected(SocketState s) {
        // don't need to remove a client since it never connected
        if (s.ErrorOccurred) {
            return;
        }

        Console.WriteLine("Accepted new connection from " + s.TheSocket.RemoteEndPoint);
        // changes the socket state's OnNetworkAction, then gets data
        s.OnNetworkAction = ReceivePlayerName;
        Networking.GetData(s);
    }

    /// <summary>
    /// Callback when the server receives the player name
    /// </summary>
    /// <param name="s">The SocketState of the player</param>
    private void ReceivePlayerName(SocketState s) {
        if (s.ErrorOccurred) {
            RemoveClientAndModifyWorld(s.ID);
            return;
        }

        // gets the player ID and player name (without the \n at the end)
        int ID = (int)s.ID;
        string playerName = s.GetData().ToString().Split("\n")[0];

        Console.WriteLine("Player (" + s.ID + ") \"" + playerName + "\"  joined.");

        InitializeSnake(ID, playerName, true);

        // first message sent to client: 
        //    snake ID \n
        //    world size \n
        string worldInfo = ID + "\n" + world.Size + "\n";
        Networking.Send(s.TheSocket, worldInfo);

        // wall info
        string str = PackageWallInfo();
        Networking.Send(s.TheSocket, str);

        // adds the client to a list
        lock (clients) {
            clients.Add(s.ID, s);
        }

        // if there is only one client in the server, means that this is the first client
        if (firstClient) {
            InitializePowerups();
            firstClient = false;
        }

        // sends all the powerups and snakes to the client
        string message = PackagePowerupsAndSnakes();
        Networking.Send(s.TheSocket, message);

        // updates the SocketState's OnNetworkAction
        s.OnNetworkAction = ReceiveCommands;

        // continues event loop
        Networking.GetData(s);
    }

    /// <summary>
    /// Main event loop that continually processes movement and sends object data to the clients
    /// whenever a client sends a command request.
    /// </summary>
    /// <param name="s">The SocketState of the player</param>
    private void ReceiveCommands(SocketState s) {
        if (s.ErrorOccurred) {
            RemoveClientAndModifyWorld(s.ID);
            return;
        }

        // process commands
        ProcessMessages(s);

        // we wait to send data to clients once a frame
        while (sendWatch.ElapsedMilliseconds < MSPerFrame) { /* empty loop body */ }
        sendWatch.Restart();

        // send all clients every object
        string packagedPowerupsAndSnakes = PackagePowerupsAndSnakes();

        // Broadcast the message to all clients
        // Lock here because we can't have new connections 
        // adding while looping through the clients list.
        // We also need to remove any disconnected clients.
        HashSet<long> disconnectedClients = new HashSet<long>();
        lock (clients) {
            foreach (SocketState state in clients.Values) {
                if (!Networking.Send(state.TheSocket, packagedPowerupsAndSnakes)) {
                    disconnectedClients.Add(state.ID);
                }
            }
        }
        foreach (long ID in disconnectedClients)
            RemoveClientFromDict(ID);

        // Continue the event loop that receives messages from this client
        Networking.GetData(s);
    }

    private static void ProcessMessages(SocketState s) {
        // gets the player ID of the player who sent the command
        int playerID = (int)s.ID;

        string totalData = s.GetData();
        string[] parts = Regex.Split(totalData, @"(?<=[\n])");

        // Loop until we have processed all messages.
        // We may have received more than one.
        foreach (string p in parts) {
            // Ignore empty strings added by the regex splitter
            if (p.Length == 0)
                continue;
            // The regex splitter will include the last string even if it doesn't end with a '\n',
            // So we need to ignore it if this happens. 
            if (p[^1] != '\n')
                break;

            lock (world) {
                Snake snake = world.Players[playerID];
                Vector2D previousDir = new Vector2D(snake.dir.X, snake.dir.Y);

                if (snake.CanTurn == true && !snake.dc && snake.alive && !snake.died) {
                    if (p.Contains("up")) {
                        snake.CanTurn = false;
                        if (snake.dir.Y == 0)
                            snake.dir = new Vector2D(0, -1);
                    } else if (p.Contains("down")) {
                        snake.CanTurn = false;
                        if (snake.dir.Y == 0)
                            snake.dir = new Vector2D(0, 1);
                    } else if (p.Contains("left")) {
                        snake.CanTurn = false;
                        if (snake.dir.X == 0)
                            snake.dir = new Vector2D(-1, 0);
                    } else if (p.Contains("right")) {
                        snake.CanTurn = false;
                        if (snake.dir.X == 0)
                            snake.dir = new Vector2D(1, 0);
                    } else {
                        // ignore every other message

                        //return;
                    }

                    if (!previousDir.Equals(snake.dir)) {
                        snake.body.Add(snake.body[^1]);
                    }
                }

                // Remove it from the SocketState's growable buffer
                s.RemoveData(0, p.Length);
            }
        }

        // clear the socket state buffer
        //s.RemoveData(0, totalData.Length);
    }

    /// <summary>
    /// Closes the socket of the client and also removes it from dictionary
    /// Modifies the world to reflect the client's death
    /// Creates a powerup at the client's location
    /// </summary>
    /// <param name="ID">The ID of the client</param>
    private static void RemoveClientAndModifyWorld(long ID) {
        //s.TheSocket.Close();
        RemoveClientFromDict(ID);

        lock (world) {
            Snake snake = world.Players[(int)ID];
            snake.dc = true;
            snake.alive = false;
            snake.died = true;

            // removes snake hitboxes
            snake.SnakeRects.Clear();
            snake.HeadRect = new Rectangle();

            Vector2D loc = snake.body[^1];

            // if the snake has a score of 2 or more and the mode is set to Special,
            // spawn a powerup at the snake's location
            if (snake.score >= 2 && Mode) {
                powerupsToAdd.Add(totalPowerupCounter,
                        new Powerup(totalPowerupCounter, loc, false, snake.score));
                totalPowerupCounter++;
            }
        }
    }

    private static void RemoveClientFromDict(long ID) {
        lock (clients)
            if (clients.Remove(ID))
                Console.WriteLine("Client " + ID + " disconnected.");

    }

    /// <summary>
    /// Initializes all the powerups in the world
    /// </summary>
    /// <returns></returns>
    private static void InitializePowerups() {
        int halfSize = worldSize / 2;
        for (totalPowerupCounter = 0; totalPowerupCounter < MaxPowerups; totalPowerupCounter++) {
            int x = rand.Next(worldSize - 10) + 5;
            int y = rand.Next(worldSize - 10) + 5;
            Powerup powerup = new Powerup(totalPowerupCounter,
                new Vector2D(-halfSize + x, -halfSize + y), false);
            if (!CollidedWithWall(powerup) && !IsOutOfWorld(powerup)) {
                lock (world) {
                    world.Powerups.Add(powerup.power, powerup);
                }
            } else {
                totalPowerupCounter--;
            }
        }
    }

    /// <summary>
    /// Creates a string of all JSON-serialized wall objects
    /// </summary>
    /// <returns></returns>
    private static string PackageWallInfo() {
        StringBuilder sb = new StringBuilder();
        lock (world) {
            foreach (var wall in world.Walls.Values) {
                string s = JsonSerializer.Serialize(wall);
                sb.Append(s + "\n");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Packages all the objects in the world into a string of JSON-serialized objects
    /// </summary>
    /// <returns></returns>
    private static string PackagePowerupsAndSnakes() {
        StringBuilder sb = new StringBuilder();
        lock (world) {
            // packages all powerups
            foreach (var powerup in world.Powerups.Values) {
                string s = JsonSerializer.Serialize(powerup);
                sb.Append(s + "\n");
            }

            // packages all snakes
            foreach (var snake in world.Players.Values) {
                string s = JsonSerializer.Serialize(snake);
                sb.Append(s + "\n");
                if (snake.join) {
                    snake.join = false;
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// This method is invoked every iteration of the frame loop. 
    /// Updates the world, then sends it to client.
    /// </summary>
    private void Update() {
        lock (world) {
            // adds all the initialized powerups to the world, then removes them from the powerupsToAdd dictionary
            // this is done to prevent a concurrent modification exception
            foreach (var powerup in powerupsToAdd.Values) {
                world.Powerups.Add(powerup.power, powerup);
            }
            powerupsToAdd.Clear();

            // cleanup the deactivated objects
            IEnumerable<int> powerupsToRemove = world.Powerups.Values.Where(x => x.died)
                                                                     .Select(x => x.power);
            IEnumerable<int> playersToRemove = world.Players.Values.Where(x => x.dc)
                                                                    .Select(x => x.snake);

            foreach (int i in powerupsToRemove)
                world.Powerups.Remove(i);

            int halfSize = worldSize / 2;

            // updates the world's powerups
            if (powerupFrames >= randomPowerupSpawn) {
                List<int> removedPowerupIDs = new List<int>();
                for (int i = 0; i < world.Powerups.Count; i++) {
                    if (!world.Powerups.ContainsKey(i)) {
                        removedPowerupIDs.Add(i);
                    }
                }

                // if there are less than the max amount of powerups, add more
                if (world.Powerups.Count < MaxPowerups) {
                    foreach (int i in removedPowerupIDs) {
                        int x = -halfSize + rand.Next(world.Size - 10) + 5;
                        int y = -halfSize + rand.Next(world.Size - 10) + 5;
                        Powerup powerup = new Powerup(i,
                            new Vector2D(x, y), false);
                        if (!CollidedWithWall(powerup)) {
                            world.Powerups.Add(powerup.power, powerup);
                        }
                    }
                }

                powerupFrames = 0;
                randomPowerupSpawn = rand.Next(MaxPowerupDelay);
            } else {
                powerupFrames++;
            }


            // updates the world's players

            foreach (var ID in playersToRemove)
                world.Players.Remove(ID);

            // adds all the initialized snakes to the world, then removes them from the snakesToAdd dictionary
            // this is done to prevent a concurrent modification exception
            foreach (var snake in snakesToAdd.Values) {
                if (!world.Players.ContainsKey(snake.snake)) {
                    world.Players.Add(snake.snake, snake);
                } else {
                    world.Players[snake.snake] = snake;
                }
            }
            snakesToAdd.Clear();

            foreach (var snake in world.Players.Values) {
                // if the snake isn't alive and has been dead for longer than the respawn rate
                if (snake.FramesAfterDeath >= RespawnRate && !snake.alive) {
                    InitializeSnake(snake.snake, snake.name, false);
                    snake.FramesAfterDeath = 0;
                } else if (snake.died || !snake.alive) { // else if the snake is dead or isn't alive (and is connected)
                    snake.FramesAfterDeath++;
                    snake.died = false;
                } else { // if the snake is alive
                    if (CollidedWithSnake(snake)) {
                        Vector2D loc = snake.body[^1];
                        snake.alive = false;
                        snake.died = true;

                        // removes snake hitboxes
                        snake.SnakeRects.Clear();
                        snake.HeadRect = new Rectangle();

                        // if the snake has a score of 2 or more and the mode is set to Special,
                        // spawn a powerup at the snake's location
                        if (snake.score >= 2 && Mode) {
                            world.Powerups.Add(totalPowerupCounter,
                                new Powerup(totalPowerupCounter, loc, false, snake.score));
                            totalPowerupCounter++;
                        }
                    }

                    if (!snake.HasCollidedWithPowerup) {
                        snake.HasCollidedWithPowerup = CollidedWithPowerup(snake);
                    } else {
                        CollidedWithPowerup(snake);
                    }

                    if (IsOutOfWorld(snake)) {
                        // adds a point outside the world (that way, the client does not render that segment)
                        snake.body.Add(snake.body[^1]);

                        // depending on the direction, add two duplicate points at the place where the snake should wrap around
                        if (snake.dir.X == 0) { // snake is going up or down
                            if (snake.dir.Y > 0) { // snake is going down
                                snake.body.Add(new Vector2D(snake.body[^1].X, snake.body[^1].Y - worldSize));
                                snake.body.Add(snake.body[^1]);
                            } else { // snake is going up
                                snake.body.Add(new Vector2D(snake.body[^1].X, snake.body[^1].Y + worldSize));
                                snake.body.Add(snake.body[^1]);
                            }
                        } else { // snake is going left or right
                            if (snake.dir.X > 0) { // snake is going right
                                snake.body.Add(new Vector2D(snake.body[^1].X - worldSize, snake.body[^1].Y));
                                snake.body.Add(snake.body[^1]);
                            } else { // snake is going left}
                                snake.body.Add(new Vector2D(snake.body[^1].X + worldSize, snake.body[^1].Y));
                                snake.body.Add(snake.body[^1]);
                            }
                        }
                    }

                    UpdateSnakePosition(snake);
                }
            }
        }
    }

    /// <summary>
    /// Update the snake's position
    /// </summary>
    /// <param name="snake">The snake to update the position of</param>
    /// <param name="powerupEaten">True if a powerup was consumed, false otherwise</param>
    private static void UpdateSnakePosition(Snake snake) {
        if (snake.body.Count < 2) {
            return;
        }

        if (!CollidedWithWall(snake)) {
            snake.body[^1] = snake.body[^1] + snake.dir * SnakeSpeed;
            snake.HeadRect = new Rectangle(-5 + (int)snake.body[^1].X, -5 + (int)snake.body[^1].Y,
                10, 10);

            // if it's not able to turn...
            if (!snake.CanTurn) {
                // increment the distance moved by the snake speed
                snake.DistanceMoved += SnakeSpeed;

                // if the distance moved is greater than the width of the snake...
                if (snake.DistanceMoved > snakeWidth) {
                    // make it able to turn and set distance moved to 0
                    snake.CanTurn = true;
                    snake.DistanceMoved = 0;
                }
            }

            // if a powerup was consumed, decrement the powerup delay
            if (snake.HasCollidedWithPowerup && snake.PowerupDelay > 0) {
                snake.PowerupDelay--;
            }

            if (snake.PowerupDelay == 0) {
                // calculate the tail vector movement
                double remainder = SnakeSpeed;
                while (remainder > 0) {
                    // calculate the segment between the last two points
                    var lastSegLen =
                    Math.Abs(snake.body[0].X - snake.body[1].X
                    + snake.body[0].Y - snake.body[1].Y);

                    // if the amount to remove is greater than the segment length
                    if (remainder > lastSegLen) {

                        // decrement the length from the remainder
                        remainder -= lastSegLen;

                        // remove the tail vector
                        snake.body.RemoveAt(0);
                    } else {

                        if (lastSegLen >= 2000) {
                            snake.body.RemoveAt(0);
                        }

                        double distanceX = snake.body[0].X - snake.body[1].X;
                        double distanceY = snake.body[0].Y - snake.body[1].Y;
                        Vector2D tailDir;

                        if (distanceX == 0) {
                            if (distanceY < 0) {
                                tailDir = new Vector2D(0, 1);
                            } else {
                                tailDir = new Vector2D(0, -1);
                            }
                        } else {
                            if (distanceX < 0) {
                                tailDir = new Vector2D(1, 0);
                            } else {
                                tailDir = new Vector2D(-1, 0);
                            }
                        }

                        // decrement the proper amount from the last tail segment
                        snake.body[0] = snake.body[0] + tailDir * remainder;

                        // set the remainder to 0 to break out of while loop
                        remainder = 0;
                    }
                }
                snake.HasCollidedWithPowerup = false;
            }

            // update the snake's rectangles
            snake.SnakeRects.Clear();
            for (int i = snake.body.Count - 2; i >= 0; i--) {
                Vector2D p1 = snake.body[i + 1];
                Vector2D p2 = snake.body[i];
                int xMin = (int)Math.Min(p1.X, p2.X);
                int yMin = (int)Math.Min(p1.Y, p2.Y);
                int width = (int)Math.Abs(p1.X - p2.X);
                int height = (int)Math.Abs(p1.Y - p2.Y);

                // offset the rectangle to allow for the width of the snake
                xMin -= 5;
                yMin -= 5;
                width += 10;
                height += 10;

                if (width < 2000 && height < 2000) {
                    snake.SnakeRects.Add(new Rectangle(xMin, yMin, width, height));
                }
            }
        } else { // if the snake is in a wall
            snake.alive = false;
            Vector2D loc = snake.body[^2];
            snake.died = true;
            snake.SnakeRects.Clear();
            snake.HeadRect = new Rectangle();

            // if the snake's score is greater than or equal to 2, and the mode is set to Special
            // spawn a powerup at the snake's location
            if (snake.score >= 2 && Mode) {
                powerupsToAdd.Add(totalPowerupCounter,
                    new Powerup(totalPowerupCounter, loc, false, snake.score));
                totalPowerupCounter++;
            }

        }
    }

    /// <summary>
    /// Initializes a snake at a random location
    /// </summary>
    /// <param name="ID">The ID of the snake</param>
    /// <param name="name">The name of the snake</param>
    /// <param name="join">A boolean indicating if the snake is joining the server for the first time or not</param>
    private static void InitializeSnake(int ID, string name, bool join) {
        for (int i = 0; i < 1; i++) { // loop until a snake is initialized
            int halfSize = world.Size / 2;

            int x = -halfSize + rand.Next(world.Size);
            int y = -halfSize + rand.Next(world.Size);

            int dirX = rand.Next(2) - rand.Next(2);
            int dirY;

            if (dirX != 0) {
                dirY = 0;
            } else {
                var randFlag = rand.Next(2) == 1;
                if (randFlag) {
                    dirY = -1;
                } else {
                    dirY = 1;
                }
            }

            Vector2D dir = new Vector2D(dirX, dirY);
            dir.Normalize();

            // set the head vector to the random point
            Vector2D head = new Vector2D(x, y);

            // calculate where the tail vector should be based on direction and head vector position
            Vector2D tail;

            if (dirX == 0) {
                if (dirY < 0) { // if the snake is moving up
                    tail = new Vector2D(x, y + 120);
                } else { // if the snake is moving down
                    tail = new Vector2D(x, y - 120);
                }
            } else { // dirY = 0
                if (dirX < 0) { // if the snake is moving left
                    tail = new Vector2D(x + 120, y);
                } else { // if the snake is moving right
                    tail = new Vector2D(x - 120, y);
                }
            }

            // head vector has to be the last vector in the list
            List<Vector2D> body = new List<Vector2D>() { tail, head };
            Snake player = new Snake(ID, name,
            body, dir, 0,
                false, true, false, join);

            // if the snake is not in a wall, add it to the list of snakes to spawn
            if (!CollidedWithWall(player)) {
                lock (world) {
                    if (!world.Players.ContainsKey(ID)) {
                        world.Players.Add(ID, player);
                    } else {
                        snakesToAdd.Add(ID, player);
                    }
                    return;
                }
            } else {
                // if the snake is in a wall, try again
                i--;
            }
        }
    }

    /// <summary>
    /// A method that determines if a snake or powerup collides with a wall hitbox
    /// </summary>
    /// <param name="o">A snake object or a powerup object</param>
    /// <returns>True if the snake/powerup collides with a wall, false otherwise</returns>
    private static bool CollidedWithWall(object o) {
        lock (world) {
            if (o is Snake s) {
                // Check for intersection with walls
                foreach (var wall in world.Walls.Values) {
                    if (wall.WallRect.IntersectsWith(s.HeadRect)) {
                        s.HeadRect = new Rectangle();
                        s.SnakeRects.Clear();
                        return true;
                    }
                }

            } else if (o is Powerup p) {
                // Check for intersection with walls
                foreach (var wall in world.Walls.Values) {
                    if (wall.WallRect.IntersectsWith(p.PowerupRect)) {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the powerup has collided with a snake or not
    /// </summary>
    /// <param name="snake">The snake in question</param>
    /// <returns>True if the snake collided with the powerup, false otherwise</returns>
    private static bool CollidedWithPowerup(Snake snake) {
        lock (world) {
            foreach (var powerup in world.Powerups.Values) {
                if (powerup.PowerupRect.IntersectsWith(snake.HeadRect)) {
                    powerup.died = true;
                    if (!Mode) { // if the mode is Standard
                        snake.PowerupDelay += SnakeGrowth;
                        snake.score += 1;
                    } else { // if the mode is Special
                        snake.PowerupDelay += SnakeGrowth * powerup.Value / SnakeSpeed;
                        snake.score += powerup.Value;
                    }

                    return true;
                }
            }
        }

        return false;
    }

    private static bool CollidedWithSnake(Snake snake) {
        lock (world) {
            // Check for intersection with snakes
            foreach (var Snake in world.Players.Values) {
                if (snake.Equals(Snake)) {
                    for (int i = snake.SnakeRects.Count - 1; i > 2; i--) {
                        if (snake.SnakeRects[i].IntersectsWith(snake.HeadRect)) {
                            return true;
                        }
                    }
                } else {
                    foreach (Rectangle r in Snake.SnakeRects) {
                        if (r.IntersectsWith(snake.HeadRect) && r.Location != snake.HeadRect.Location) {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private static bool IsOutOfWorld(object o) {
        if (o is Snake snake) {
            if (worldSize / 2 < Math.Abs(snake.body[^1].X) ||
                worldSize / 2 < Math.Abs(snake.body[^1].Y)) {
                return true;
            } else { // if the snake is within the world
                return false;
            }
        } else if (o is Powerup p) {
            if (worldSize / 2 <= Math.Abs(p.loc.X) ||
                worldSize / 2 <= Math.Abs(p.loc.Y)) {
                return true;
            } else { // if the snake is within the world
                return false;
            }
        } else {
            return false;
        }
    }

}