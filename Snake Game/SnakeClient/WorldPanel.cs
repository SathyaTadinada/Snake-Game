using IImage = Microsoft.Maui.Graphics.IImage;
#if MACCATALYST
using Microsoft.Maui.Graphics.Platform;
#else
using Microsoft.Maui.Graphics.Win2D;
#endif
using System.Reflection;
using Models;

namespace SnakeGame;
public class WorldPanel : IDrawable {
    // A delegate for DrawObjectWithTransform
    // Methods matching this delegate can draw whatever they want onto the canvas
    public delegate void ObjectDrawer(object o, ICanvas canvas);

    private int viewSize = 900;

    private World world;
    private int playerID;
    private Snake mainPlayerSnake;

    private bool initializedForDrawing = false;
    private bool worldInitialized = false;
    private bool mainPlayerInitialized = false;

    private IImage wall;
    private IImage background;
    private IImage explosion;

    /// <summary>
    /// Loads an image using either Mac or Windows image loading API
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private IImage LoadImage(string name) {
        Assembly assembly = GetType().GetTypeInfo().Assembly;
        string path = "SnakeClient.Resources.Images";
        using (Stream stream = assembly.GetManifestResourceStream($"{path}.{name}")) {
#if MACCATALYST
                    return PlatformImage.FromStream(stream);
#else
            return new W2DImageLoadingService().FromStream(stream);
#endif
        }
    }

    /// <summary>
    ///  Sets the player ID
    /// </summary>
    /// <param name="playerID"></param>
    public void SetPlayerID(int playerID) {
        this.playerID = playerID;
    }

    /// <summary>
    /// Initializes the world, and also initializes the player snake
    /// </summary>
    /// <param name="w"></param>
    public void SetWorld(World w) {
        world = w;
        worldInitialized = true;
    }

    /// <summary>
    /// Initializes the wall and background images
    /// </summary>
    private void InitializeDrawing() {
        wall = LoadImage("wallsprite.png");
        background = LoadImage("background.png");
        explosion = LoadImage("explosion.png");
        initializedForDrawing = true;
    }

    public void SetMainPlayer(Snake s) {
        mainPlayerSnake = s;
        mainPlayerInitialized = true;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect) {
        if (!initializedForDrawing)
            InitializeDrawing();

        if (!mainPlayerInitialized || !worldInitialized) {
            return;
        }

        // undo previous transformations from last frame
        canvas.ResetState();

        if (world != null) {
            lock (world) {

                // center the view to the player
                float playerX = (float)mainPlayerSnake.body[^1].GetX();
                float playerY = (float)mainPlayerSnake.body[^1].GetY();
                canvas.Translate(-playerX + (viewSize / 2), -playerY + (viewSize / 2));

                // draws the background
                canvas.DrawImage(background,
                -world.Size / 2, -world.Size / 2, world.Size, world.Size);

                // draws the walls
                foreach (var wall in world.Walls.Values) {
                    if (wall.p1.X == wall.p2.X) {
                        int numOfWalls = Math.Abs(((int)wall.p2.Y - (int)wall.p1.Y) / 50);

                        if (wall.p1.Y < wall.p2.Y) {
                            double wallY = wall.p1.Y;
                            for (int i = 0; i <= numOfWalls; i++) {
                                DrawObjectWithTransform(canvas, wall,
                                    wall.p1.X, wallY, 0, WallDrawer);
                                wallY += 50;
                            }
                        } else {
                            double wallY = wall.p2.Y;
                            for (int i = 0; i <= numOfWalls; i++) {
                                DrawObjectWithTransform(canvas, wall,
                                    wall.p1.X, wallY, 0, WallDrawer);
                                wallY += 50;
                            }
                        }
                    } else {
                        int numOfWalls = Math.Abs(((int)wall.p2.X - (int)wall.p1.X)) / 50;

                        if (wall.p1.X < wall.p2.X) {
                            double wallX = wall.p1.X;
                            for (int i = 0; i <= numOfWalls; i++) {
                                DrawObjectWithTransform(canvas, wall,
                                    wallX, wall.p1.Y, 0, WallDrawer);
                                wallX += 50;
                            }
                        } else {
                            double wallX = wall.p2.X;
                            for (int i = 0; i <= numOfWalls; i++) {
                                DrawObjectWithTransform(canvas, wall,
                                    wallX, wall.p1.Y, 0, WallDrawer);
                                wallX += 50;
                            }
                        }
                    }
                }

                // draws the snakes
                foreach (var snake in world.Players.Values) {
                    Vector2D head = snake.body[^1];
                    if (snake.died || !snake.alive) { // if the snake is dead, don't draw it
                        if (snake.died) {
                            DrawObjectWithTransform(canvas, snake,
                            head.X, head.Y, 0, ExplosionDrawer);
                        }
                        continue;
                    }

                    DrawObjectWithTransform(canvas, snake, 0, 0, 0, SnakeDrawer);

                    // changes the text color depending on the player ID
                    canvas.FontColor = (playerID % 8) switch {
                        0 or 4 or 5 or 6 => Colors.White,
                        _ => Colors.Black,
                    };
                    canvas.DrawString(snake.name + ": " + snake.score,
                    (float)head.X, (float)head.Y + 19, HorizontalAlignment.Center);
                }

                // draws the powerups
                foreach (var powerup in world.Powerups.Values) {
                    if (powerup.died) {
                        continue;
                    }
                    DrawObjectWithTransform(canvas, powerup,
                      powerup.loc.GetX(), powerup.loc.GetY(),
                      0, PowerupDrawer);
                }
            }
        }
    }

    /// <summary>
    /// This method performs a translation and rotation to draw an object.
    /// </summary>
    /// <param name="canvas">The canvas object for drawing onto</param>
    /// <param name="o">The object to draw</param>
    /// <param name="worldX">The X component of the object's position in world space</param>
    /// <param name="worldY">The Y component of the object's position in world space</param>
    /// <param name="angle">The orientation of the object, measured in degrees clockwise from "up"</param>
    /// <param name="drawer">The drawer delegate. After the transformation is applied, the delegate is invoked to draw whatever it wants</param>
    private void DrawObjectWithTransform(ICanvas canvas, object o, double worldX, double worldY, double angle, ObjectDrawer drawer) {
        // "push" the current transform
        canvas.SaveState();

        canvas.Translate((float)worldX, (float)worldY);
        canvas.Rotate((float)angle);
        drawer(o, canvas);

        // "pop" the transform
        canvas.RestoreState();
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer delegate
    /// </summary>
    /// <param name="o">The powerup to draw</param>
    /// <param name="canvas"></param>
    private void WallDrawer(object o, ICanvas canvas) {
        Wall p = o as Wall;

        float width = wall.Width * 0.9f;
        float height = wall.Height * 0.9f;

        canvas.DrawImage(wall, -(width / 2), -(height / 2), width, height);
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer delegate
    /// </summary>
    /// <param name="o">The player to draw</param>
    /// <param name="canvas"></param>
    private void SnakeDrawer(object o, ICanvas canvas) {
        Snake snake = o as Snake;

        const double spotDistance = 20;
        double lengthTraversed = 0;
        for (int i = snake.body.Count - 2; i >= 0; i--) {
            Vector2D p1 = snake.body[i + 1];
            Vector2D p2 = snake.body[i];
            float xMin = (float)Math.Min(p1.X, p2.X);
            float yMin = (float)Math.Min(p1.Y, p2.Y);
            float width = (float)Math.Abs(p1.X - p2.X);
            float height = (float)Math.Abs(p1.Y - p2.Y);

            // offset the rectangle to allow for the width of the snake
            xMin -= 5;
            yMin -= 5;
            width += 10;
            height += 10;

            // pick snake color based on the player ID
            canvas.FillColor = (snake.snake % 8) switch {
                0 => Colors.Red,
                1 => Colors.Orange,
                2 => Colors.Yellow,
                3 => Colors.Green,
                4 => Colors.Blue,
                5 => Colors.Purple,
                6 => Colors.Black,
                _ => Colors.White
            };
            canvas.FillRoundedRectangle(xMin, yMin, width, height, 5);

            double segLength = width + height;
            canvas.FillColor = (snake.snake % 8) switch {
                0 => Colors.DarkRed,
                1 => Colors.DarkOrange,
                2 => Colors.YellowGreen,
                3 => Colors.DarkGreen,
                4 => Colors.DarkBlue,
                5 => Colors.MediumPurple,
                6 => Colors.DarkGray,
                _ => Colors.LightGray
            };
            Vector2D direction = (p1 - p2);
            direction.Normalize();
            // j starts at 20 (distance between spots) minus the total length traversed so far.
            // as long as j is less than the difference between a segment length and 20, add a spot, 
            // then increment by the spot distance
            for (double j = spotDistance - lengthTraversed; j < segLength - spotDistance; j += spotDistance) {
                Vector2D spotPoint = p1 - (direction * j);
                canvas.FillCircle((float)spotPoint.X, (float)spotPoint.Y, 5);
            }
            lengthTraversed += segLength;
            lengthTraversed %= spotDistance;
        }
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer delegate
    /// </summary>
    /// <param name="o">The powerup to draw</param>
    /// <param name="canvas"></param>
    private void PowerupDrawer(object o, ICanvas canvas) {
        Powerup p = o as Powerup;
        int width = 16;

        // picks color and shape of powerup based on the powerup's ID
        switch (p.power % 3) {
            case 0:
                canvas.FillColor = Colors.Blue;

                // Ellipses are drawn starting from the top-left corner.
                // So if we want the circle centered on the powerup's location, we have to offset it
                // by half its size to the left (-width/2) and up (-height/2)
                canvas.FillEllipse(-width / 2, -width / 2, width, width);
                break;
            case 1:
                canvas.FillColor = Colors.Red;
                canvas.FillRectangle(-width / 2, -width / 2, width, width);
                break;
            case 2:
            default:
                canvas.FillColor = Colors.Purple;
                canvas.FillRoundedRectangle(-width / 2, -width / 2, width, width, 4);
                break;
        }
    }

    private void ExplosionDrawer(object o, ICanvas canvas) {
        Snake s = o as Snake;

        float width = (float)(explosion.Width * 0.6);
        float height = (float)(explosion.Height * 0.6);

        canvas.DrawImage(explosion, -width / 2, -height / 2, width, height);
    }
}
