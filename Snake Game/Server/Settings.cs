using Models;
using SnakeGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Server {
    public class Settings {
        public bool Mode { get; set; }
        public int MSPerFrame { get; set; }
        public int RespawnRate { get; set; }
        public int WorldSize { get; set; }
        public int SnakeSpeed { get; set; }
        public int SnakeGrowth { get; set; }
        public int MaxPowerups { get; set; }
        public int MaxPowerupDelay { get; set; }
        public List<Wall> Walls { get; set; }

        // in case the settings.xml does not have values for some of these
        public Settings() {
            Mode = false;
            MSPerFrame = 34;
            RespawnRate = 300;
            WorldSize = 2000;
            SnakeSpeed = 6;
            SnakeGrowth = 24;
            MaxPowerups = 20;
            MaxPowerupDelay = 75;
            Walls = new List<Wall>();
        }

        /// <summary>
        /// Reads an XML file and stores relevant settings values
        /// </summary>
        /// <param name="filename"></param>
        public void ReadFromXML(string filename) {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(filename);

            try { 
                string str = xmlDoc.SelectSingleNode("/GameSettings/Mode")!.InnerText;
                if (str.Equals("Standard")) {
                    Mode = false;
                } else if (str.Equals("Special")) {
                    Mode = true;
                }
            } catch (Exception) { };
            try { MSPerFrame = int.Parse(xmlDoc.SelectSingleNode("/GameSettings/MSPerFrame")!.InnerText); } catch (Exception) { };
            try { RespawnRate = int.Parse(xmlDoc.SelectSingleNode("/GameSettings/RespawnRate")!.InnerText); } catch (Exception) { };
            try { WorldSize = int.Parse(xmlDoc.SelectSingleNode("/GameSettings/UniverseSize")!.InnerText); } catch (Exception) { };
            try { SnakeSpeed = int.Parse(xmlDoc.SelectSingleNode("/GameSettings/SnakeSpeed")!.InnerText); } catch (Exception) { };
            try { SnakeGrowth = int.Parse(xmlDoc.SelectSingleNode("/GameSettings/SnakeGrowth")!.InnerText); } catch (Exception) { };
            try { MaxPowerups = int.Parse(xmlDoc.SelectSingleNode("/GameSettings/MaxPowerups")!.InnerText); } catch (Exception) { };
            try { MaxPowerupDelay = int.Parse(xmlDoc.SelectSingleNode("/GameSettings/MaxPowerupDelay")!.InnerText); } catch (Exception) { };

            try {
                XmlNodeList wallNodes = xmlDoc.SelectNodes("/GameSettings/Walls/Wall")!;
                foreach (XmlNode wallNode in wallNodes!) {
                    int ID = int.Parse(wallNode.SelectSingleNode("ID")!.InnerText);
                    double p1X = double.Parse(wallNode.SelectSingleNode("p1/x")!.InnerText);
                    double p1Y = double.Parse(wallNode.SelectSingleNode("p1/y")!.InnerText);

                    double p2X = double.Parse(wallNode.SelectSingleNode("p2/x")!.InnerText);
                    double p2Y = double.Parse(wallNode.SelectSingleNode("p2/y")!.InnerText);

                    Wall wall = new Wall(ID, new Vector2D(p1X, p1Y), new Vector2D(p2X, p2Y));

                    Walls.Add(wall);
                }
            } catch (Exception) { };
        }
    }
}
