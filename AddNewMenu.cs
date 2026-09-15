using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace RailPlanner;

/// <summary>
/// Central ADD NEW chooser. It deliberately sits outside MainGame so the
/// existing modal editors remain the single source of truth for object data.
/// </summary>
public sealed class AddNewMenu : DrawableGameComponent
{
    readonly MainGame game;
    SpriteBatch batch = null!;
    SpriteFont font = null!;
    Texture2D pixel = null!;
    KeyboardState oldKeys;
    MouseState oldMouse;
    bool open;
    int selected;

    static readonly string[] Items = { "STATION", "LINE", "SECTION", "TRAIN" };

    public AddNewMenu(MainGame game) : base(game)
    {
        this.game = game;
        DrawOrder = 10000;
        UpdateOrder = 10000;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = Game.Content.Load<SpriteFont>("DefaultFont");
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        base.LoadContent();
    }

    public override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        var mouse = Mouse.GetState();
        bool Pressed(Keys key) => keys.IsKeyDown(key) && !oldKeys.IsKeyDown(key);

        if (!open)
        {
            if (Pressed(Keys.Insert) || Pressed(Keys.F12))
            {
                open = true;
                selected = 0;
            }
        }
        else
        {
            if (Pressed(Keys.Escape))
            {
                open = false;
            }
            else if (Pressed(Keys.Up))
            {
                selected = (selected + Items.Length - 1) % Items.Length;
            }
            else if (Pressed(Keys.Down))
            {
                selected = (selected + 1) % Items.Length;
            }
            else if (Pressed(Keys.Enter))
            {
                Activate(selected);
            }
            else if (mouse.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released)
            {
                var rect = MenuRect();
                for (var i = 0; i < Items.Length; i++)
                {
                    var item = new Rectangle(rect.X + 28, rect.Y + 78 + i * 58, rect.Width - 56, 48);
                    if (item.Contains(mouse.Position))
                    {
                        Activate(i);
                        break;
                    }
                }
            }
        }

        oldKeys = keys;
        oldMouse = mouse;
        base.Update(gameTime);
    }

    void Activate(int index)
    {
        selected = Math.Clamp(index, 0, Items.Length - 1);
        open = false;

        switch (selected)
        {
            case 0:
                Invoke("BeginAddStation");
                break;
            case 1:
                Invoke("OpenLineEditor", null);
                break;
            case 2:
                Invoke("OpenSectionEditor", null, true);
                break;
            case 3:
                Invoke("OpenTrainDialog");
                break;
        }
    }

    void Invoke(string method, params object?[] args)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var candidates = typeof(MainGame).GetMethods(flags)
            .Where(x => x.Name == method)
            .ToArray();

        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            if (parameters.Length != args.Length) continue;
            try
            {
                candidate.Invoke(game, args);
                return;
            }
            catch (ArgumentException)
            {
            }
        }
    }

    Rectangle MenuRect()
    {
        const int width = 560;
        const int height = 390;
        return new Rectangle(
            (GraphicsDevice.Viewport.Width - width) / 2,
            (GraphicsDevice.Viewport.Height - height) / 2,
            width,
            height);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (!open) return;

        var r = MenuRect();
        batch.Begin(samplerState: SamplerState.PointClamp);

        Rect(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 120));
        Rect(r, new Color(18, 22, 28));
        Border(r, new Color(90, 100, 115));

        Text("ADD NEW", new Vector2(r.X + 28, r.Y + 22), Color.White, 1.15f);
        Text("SELECT OBJECT TYPE", new Vector2(r.X + 28, r.Y + 50), Color.LightGray, .65f);

        for (var i = 0; i < Items.Length; i++)
        {
            var item = new Rectangle(r.X + 28, r.Y + 78 + i * 58, r.Width - 56, 48);
            var active = i == selected;
            Rect(item, active ? new Color(48, 56, 68) : new Color(28, 33, 40));
            Border(item, active ? new Color(160, 175, 195) : new Color(65, 72, 82));
            Text(Items[i], new Vector2(item.X + 16, item.Y + 14), active ? Color.White : Color.LightGray, .8f);
        }

        Text("UP / DOWN  SELECT    ENTER  OPEN    ESC  CANCEL", new Vector2(r.X + 28, r.Bottom - 27), Color.Gray, .55f);
        batch.End();
    }

    void Rect(Rectangle rectangle, Color color) => batch.Draw(pixel, rectangle, color);

    void Border(Rectangle r, Color color)
    {
        Rect(new Rectangle(r.X, r.Y, r.Width, 2), color);
        Rect(new Rectangle(r.X, r.Bottom - 2, r.Width, 2), color);
        Rect(new Rectangle(r.X, r.Y, 2, r.Height), color);
        Rect(new Rectangle(r.Right - 2, r.Y, 2, r.Height), color);
    }

    void Text(string value, Vector2 position, Color color, float scale)
        => batch.DrawString(font, value, position, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
}
