using System.Diagnostics;

namespace SnakeGame;

public partial class MainPage : ContentPage {
    private GameController ctrl;

    public MainPage() {
        InitializeComponent();
        graphicsView.Invalidate();
        ctrl = new GameController();

        ctrl.Error += NetworkErrorHandler;
        ctrl.UpdateArrived += OnFrame;
        ctrl.WorldInitialized += UpdateWorldAndPlayerID;
        ctrl.MainPlayerInitialized += SetMainPlayer;
    }

    void OnTapped(object sender, EventArgs args) {
        keyboardHack.Focus();
    }

    void OnTextChanged(object sender, TextChangedEventArgs args) {
        Entry entry = (Entry)sender;
        string text = entry.Text.ToLower();
        if (text == "w") {
            ctrl.MoveUp();
        } else if (text == "a") {
            ctrl.MoveLeft();
        } else if (text == "s") {
            ctrl.MoveDown();
        } else if (text == "d") {
            ctrl.MoveRight();
        }

        entry.Text = "";
        keyboardHack.Focus();
    }

    /// <summary>
    /// If there is any network error, this method will be called.
    /// </summary>
    private void NetworkErrorHandler(string err) {
        Dispatcher.Dispatch(() => {
            DisplayAlert("Error", err, "OK");
            connectButton.IsEnabled = true;
            serverText.IsEnabled = true;
            nameText.IsEnabled = true;
        });
    }

    private void SetMainPlayer() {
        worldPanel.SetMainPlayer(ctrl.World.Players[ctrl.PlayerID]);
    }

    /// <summary>
    /// Event handler for the connect button
    /// We will put the connection attempt interface here in the view.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void ConnectClick(object sender, EventArgs args) {
        if (serverText.Text == "") {
            DisplayAlert("No Server Address", "Please enter a server address", "OK");
            return;
        }
        if (nameText.Text == "") {
            DisplayAlert("No Name", "Please enter a name", "OK");
            return;
        }
        if (nameText.Text.Length > 16) {
            DisplayAlert("Name Is Too Long", "Name must be less than 16 characters", "OK");
            return;
        }

        // Disable the controls and try to connect
        connectButton.IsEnabled = false;
        serverText.IsEnabled = false;
        nameText.IsEnabled = false;

        // begins the handshake process through the GameController
        ctrl.ConnectToServer(serverText.Text, nameText.Text);
    }

    /// <summary>
    /// Refreshes the graphics view every frame when the controller has updated the world
    /// </summary>
    public void OnFrame() {
        Dispatcher.Dispatch(() => graphicsView.Invalidate());
    }

    /// <summary>
    /// Handler that updates the world and the player ID in WorldPanel
    /// </summary>
    public void UpdateWorldAndPlayerID() {
        worldPanel.SetPlayerID(ctrl.PlayerID);
        worldPanel.SetWorld(ctrl.World);
    }

    private void ControlsButton_Clicked(object sender, EventArgs e) {
        DisplayAlert("Controls",
                     "W:\t\t Move up\n" +
                     "A:\t\t Move left\n" +
                     "S:\t\t Move down\n" +
                     "D:\t\t Move right\n",
                     "OK");
    }

    private void AboutButton_Clicked(object sender, EventArgs e) {
        DisplayAlert("About",
      "SnakeGame solution\nArtwork by Jolie Uk and Alex Smith\nGame design by Daniel Kopta and Travis Martin\n" +
      "Implementation by Sathya Tadinada and Jaron Tsao\n" +
        "CS 3500 Fall 2023, University of Utah", "OK");
    }

    private void ContentPage_Focused(object sender, FocusEventArgs e) {
        if (!connectButton.IsEnabled) {
            keyboardHack.Focus();
        }
    }
}