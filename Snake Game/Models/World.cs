using System.Drawing;

namespace Models;

public class World {
    public int Size { get; private set; }
    public Dictionary<int, Snake> Players;
    public Dictionary<int, Powerup> Powerups;
    public Dictionary<int, Wall> Walls;

    public World(int size) {
        Size = size;
        Players = new Dictionary<int, Snake>();
        Powerups = new Dictionary<int, Powerup>();
        Walls = new Dictionary<int, Wall>();
    }
}
