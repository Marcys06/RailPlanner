using RailPlanner;
using var game = new MainGame();
game.Components.Add(new AddNewMenu(game));
game.Run();
