using Models;
using NetworkUtil;
using System.Text.Json;
using System.Text.RegularExpressions;

public class GameController {
    private string playerName = "";
    public World World { get; private set; } = new World(-1);
    public int PlayerID { get; private set; }
    private SocketState? socketState;

    public delegate void ErrorHandler(string err);
    public event ErrorHandler? Error;

    public delegate void UpdateArrivedHandler();
    public event UpdateArrivedHandler? UpdateArrived;

    public delegate void WorldUpdateHandler();
    public event WorldUpdateHandler? WorldInitialized;

    public delegate void MainPlayerHandler();
    public event MainPlayerHandler? MainPlayerInitialized;

    /// <summary>
    /// The method that is called to initiate the handshake with the server.
    /// </summary>
    /// <param name="serverName"></param>
    /// <param name="playerName"></param>
    public void ConnectToServer(string serverName, string playerName) {
        this.playerName = playerName;

        //Establish a socket connection to the server on port 11000.
        Networking.ConnectToServer(OnSocketConnect, serverName, 11000);
    }

    /// <summary>
    /// A callback invoked when a connection is established with the server.
    /// </summary>
    /// <param name="s"></param>
    private void OnSocketConnect(SocketState state) {
        socketState = state;
        // if any error occurs, invoke the OnNetworkAction event
        if (socketState.ErrorOccurred) {
            Error?.Invoke(socketState.ErrorMessage!);
            return;
        }

        //Upon connection, send a single '\n' terminated string representing the player's name.
        //The name should be no longer than 16 characters (not including the newline).
        bool result = Networking.Send(socketState.TheSocket, playerName + "\n");

        // if any error occurs, invoke the OnNetworkAction event
        if (!result) {
            Error?.Invoke(socketState.ErrorMessage!);
            return;
        }

        // start an event loop that continuously receives data from the server
        socketState.OnNetworkAction = UpdateFromServer;
        Networking.GetData(socketState);
    }

    /// <summary>
    /// This is a continuous event loop that constantly updates the locations of the players and powerups.
    /// </summary>
    /// <param name="state"></param>
    private void UpdateFromServer(SocketState state) {
        socketState = state;

        // if any error occurs, invoke the OnNetworkAction event
        if (socketState.ErrorOccurred) {
            Error?.Invoke(socketState.ErrorMessage!);
            return;
        }

        // process the JSON messages received
        ProcessMessages(socketState);

        // notifies the view that it needs to redraw itself
        UpdateArrived?.Invoke();

        // Continues the event loop. `state.OnNetworkAction` has not been changed,
        // so it will continue to call this method when more data arrives
        Networking.GetData(socketState);
    }

    /// <summary>
    /// Processes buffered messages from the server, then removes them from the buffer.
    /// </summary>
    /// <param name="socketState"></param>
    public void ProcessMessages(SocketState state) {
        socketState = state;

        string totalData = socketState.GetData();
        string[] serverMsg = Regex.Split(totalData, @"(?<=[\n])");

        int processedDataLength = 0;

        try {
            lock (World) {
                // loop until we have processed all messages
                for (int i = 0; i < serverMsg.Length; i++) {
                    string s = serverMsg[i];
                    // Ignore empty strings added by the regex splitter
                    if (s.Length == 0)
                        continue;

                    // The regex splitter will include the last string even if it doesn't end with a '\n',
                    // so we need to ignore it if this happens. 
                    if (s[s.Length - 1] != '\n')
                        break;

                    // If the first message in the string array is a number, then we know that it is the player ID
                    // (and the second message is the world size)
                    if (int.TryParse(serverMsg[0], out int result)) {
                        PlayerID = result;
                        processedDataLength += serverMsg[0].Length;
                        serverMsg[0] = "";
                        World = new World(int.Parse(serverMsg[1]));
                        WorldInitialized?.Invoke();

                        processedDataLength += serverMsg[1].Length;
                        serverMsg[1] = "";
                        continue;
                    }


                    // parse the JSON into a JsonDocument object
                    JsonDocument doc = JsonDocument.Parse(s);

                    // if the json object is a wall
                    if (doc.RootElement.TryGetProperty("wall", out _)) {
                        Wall? wall = JsonSerializer.Deserialize<Wall>(s);
                        if (wall == null) {
                            continue;
                        }

                        World.Walls.Add(wall.wall, wall);
                        processedDataLength += s.Length;

                    }

                    // if the json object is a snake
                    else if (doc.RootElement.TryGetProperty("snake", out _)) {
                        Snake? snake = JsonSerializer.Deserialize<Snake>(s);
                        if (snake == null) {
                            continue;
                        }

                        // if the dictionary does not store this snake...
                        if (!World.Players.ContainsKey(snake.snake)) {
                            //World.Players.Add(snake.snake, snake);
                            if (!snake.dc) { // if the snake is connected...
                                World.Players.Add(snake.snake, snake);
                            }
                        } else {
                            //World.Players[snake.snake] = snake;
                            if (!snake.dc) { // if the snake is connected...
                                World.Players[snake.snake] = snake;
                            } else { // if it's not, remove it from the dictionary
                                World.Players.Remove(snake.snake);
                            }

                        }

                        // checks if main player is initialized
                        if (World.Players.ContainsKey(PlayerID)) {
                            MainPlayerInitialized?.Invoke();
                        }

                        processedDataLength += s.Length;
                    }

                    // if the json object is a powerup
                    else if (doc.RootElement.TryGetProperty("power", out _)) {
                        Powerup? powerup = JsonSerializer.Deserialize<Powerup>(s);
                        if (powerup == null) {
                            continue;
                        }

                        if (!World.Powerups.ContainsKey(powerup.power)) {
                            if (!powerup.died) {
                                World.Powerups.Add(powerup.power, powerup);
                            }
                        } else {
                            if (!powerup.died) {
                                World.Powerups[powerup.power] = powerup;
                            } else {
                                World.Powerups.Remove(powerup.power);
                            }
                        }
                        processedDataLength += s.Length;
                    }
                }

                // clears existing data in the socket state
                socketState.RemoveData(0, processedDataLength);
            }
        } catch (Exception) {
            // do nothing
        }
    }

    public void MoveUp() {
        bool success = Networking.Send(socketState!.TheSocket, "{\"moving\":\"up\"}\n");
        if (!success) {
            Error?.Invoke(socketState.ErrorMessage!);
            return;
        }
    }

    public void MoveLeft() {
        bool success = Networking.Send(socketState!.TheSocket, "{\"moving\":\"left\"}\n");
        if (!success) {
            Error?.Invoke(socketState.ErrorMessage!);
            return;
        }
    }

    public void MoveDown() {
        bool success = Networking.Send(socketState!.TheSocket, "{\"moving\":\"down\"}\n");
        if (!success) {
            Error?.Invoke(socketState.ErrorMessage!);
            return;
        }
    }

    public void MoveRight() {
        bool success = Networking.Send(socketState!.TheSocket, "{\"moving\":\"right\"}\n");
        if (!success) {
            Error?.Invoke(socketState.ErrorMessage!);
            return;
        }
    }
}