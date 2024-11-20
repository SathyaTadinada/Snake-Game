using SnakeGame;
using System.Drawing;
using System.Text.Json.Serialization;

namespace Models;

public class Snake {
    public int snake { get; private set; }
    public string name { get; private set; }
    public List<Vector2D> body { get; set; }
    public Vector2D dir { get; set; }
    public int score { get; set; }
    public bool died { get; set; }
    public bool alive { get; set; }
    public bool dc { get; set; }
    public bool join { get; set; }

    [JsonIgnore]
    public int FramesAfterDeath { get; set; } = 0;

    [JsonIgnore]
    public List<Rectangle> SnakeRects { get; set; }

    [JsonIgnore]
    public Rectangle HeadRect { get; set; }

    [JsonIgnore]
    public int PowerupDelay { get; set; } = 0;

    [JsonIgnore]
    public bool HasCollidedWithPowerup { get; set; } = false;

    [JsonIgnore]
    public int DistanceMoved { get; set; } = 0;

    [JsonIgnore]
    public bool CanTurn { get; set; } = true;


    [JsonConstructor]
    public Snake(int snake, string name, List<Vector2D> body, Vector2D dir, int score, bool died, bool alive, bool dc, bool join) {
        this.snake = snake;
        this.name = name;
        this.body = body;
        this.dir = dir;
        this.score = score;
        this.died = died;
        this.alive = alive;
        this.dc = dc;
        this.join = join;

        SnakeRects = new List<Rectangle>();
        for (int i = body.Count - 2; i >= 0; i--) {
            Vector2D p1 = body[i + 1];
            Vector2D p2 = body[i];
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
                SnakeRects.Add(new Rectangle(xMin, yMin, width, height));
            }

        }

        HeadRect = new Rectangle(-5 + (int)body[^1].X, -5 + (int)body[^1].Y, 10, 10);
    }
}