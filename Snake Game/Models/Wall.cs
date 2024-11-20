using SnakeGame;
using System.Drawing;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Models {
    public class Wall {
        [XmlElement("ID")]
        public int wall { get; private set; }

        public Vector2D p1 { get; private set; }
        public Vector2D p2 { get; private set; }

        [JsonIgnore]
        public Rectangle WallRect { get; set; }

        [JsonConstructor]
        public Wall(int wall, Vector2D p1, Vector2D p2) {
            this.wall = wall;
            this.p1 = p1;
            this.p2 = p2;

            int xMin = (int)Math.Min(p1.X, p2.X);
            int yMin = (int)Math.Min(p1.Y, p2.Y);
            int width = (int)Math.Abs(p1.X - p2.X);
            int height = (int)Math.Abs(p1.Y - p2.Y);

            // offset the rectangle to allow for the width of the snake
            xMin -= 25;
            yMin -= 25;
            width += 50;
            height += 50;

            // Create WallRect using adjusted coordinates and dimensions
            WallRect = new Rectangle(xMin, yMin, width, height);
        }
    }
}
