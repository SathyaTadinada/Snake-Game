using SnakeGame;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Models;

public class Powerup {
    public int power { get; private set; }

    public Vector2D loc { get; private set; }
    public bool died { get; set; }

    [JsonIgnore]
    public Rectangle PowerupRect { get; set; }

    [JsonIgnore]
    public int Value { get; set; }

    [JsonConstructor]
    public Powerup(int power, Vector2D loc, bool died) {
        this.power = power;
        this.loc = loc;
        this.died = died;
        Value = 1;

        PowerupRect = new Rectangle(-8 + (int)loc.X, -8 + (int)loc.Y, 16, 16);
    }

    public Powerup(int power, Vector2D loc, bool died, int value) {
        this.power = power;
        this.loc = loc;
        this.died = died;
        Value = value;

        PowerupRect = new Rectangle(-8 + (int)loc.X, -8 + (int)loc.Y, 16, 16);
    }
}
