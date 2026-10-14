using AVAPirates.Rules;
using AVAPirates.Sea;
using AVAPirates.World;

namespace AVAPirates.Game;

public enum Mode { Title, Sea, Port, Battle, Duel, Retired }

/// <summary>
/// The whole game, apart from drawing it: the title and a new career, sailing the isles, the ports,
/// sea fights and sword fights, and the long business of growing rich and old. Keys arrive by name
/// ("Up", "Enter", "Space"): presses for menus and orders, and the keys held down for the helm and
/// the sword, so the tests and the attract demo play exactly as a person does. Sailing and fighting
/// run on a fixed sixty-steps-a-second clock.
/// </summary>
public sealed partial class GameEngine
{
    public const double Step = 1 / 60.0;
    public const double DemoAfter = 30;
    public const double DemoLength = 75;

    public Mode Mode { get; private set; } = Mode.Title;
    public WorldState World { get; private set; } = new();
    public Rng Rng { get; private set; }
    public SaveStore Saves { get; }
    public List<Panel> Panels { get; } = [];
    public Panel? Top => Panels.Count > 0 ? Panels[^1] : null;
    public List<(string Text, uint Colour)> Log { get; } = [];
    public double Clock { get; private set; }
    public double Idle { get; private set; }
    public DemoPilot? Demo { get; private set; }
    public bool InDemo => Demo is not null;
    public event Action<Sfx>? Sound;
    public event Action? QuitRequested;
    /// <summary>The keys held down right now, by name.</summary>
    public HashSet<string> Held { get; } = [];
    /// <summary>Keys pressed since the last step of the clock, for the real-time parts.</summary>
    private readonly HashSet<string> _pressed = [];
    private double _accumulator;

    public static SeaMap Map => SeaMap.Instance;
    public Traffic Traffic { get; private set; }

    public GameEngine(int seed, SaveStore? saves = null)
    {
        Rng = new Rng((ulong)seed * 2654435761UL + 1);
        Saves = saves ?? new SaveStore();
        Traffic = new Traffic(Map, Rng);
        ShowTitle();
    }

    public void Play(Sfx s) => Sound?.Invoke(s);

    public void Say(string text, uint colour = 0xFFE8E0D0)
    {
        Log.Add((text, colour));
        if (Log.Count > 40) Log.RemoveAt(0);
    }

    // ───────────────────────────── the clock ─────────────────────────────

    public void Update(double dt)
    {
        Clock += dt;
        if (Demo is not null)
        {
            Demo.Update(dt);
            if (Demo is not null && (Demo.Elapsed > DemoLength || Mode is Mode.Title or Mode.Retired)) EndDemo();
        }
        else if (Mode == Mode.Title && Panels.Count == 1)
        {
            Idle += dt;
            if (Idle > DemoAfter) StartDemo();
        }
        _accumulator = Math.Min(_accumulator + dt, 0.25);
        while (_accumulator >= Step)
        {
            _accumulator -= Step;
            Tick(Step);
        }
    }

    /// <summary>One step of the real-time clock.</summary>
    private void Tick(double dt)
    {
        if (Top is null)
        {
            switch (Mode)
            {
                case Mode.Sea: SeaTick(dt); break;
                case Mode.Battle: BattleTick(dt); break;
                case Mode.Duel: DuelTick(dt); break;
            }
        }
        _pressed.Clear();
    }

    /// <summary>Whether a key was pressed since the last step (for firing and lunging).</summary>
    private bool Pressed(params string[] keys) => keys.Any(_pressed.Contains);

    // ───────────────────────────── panels ─────────────────────────────

    public Panel Push(Panel p)
    {
        Panels.Add(p);
        return p;
    }

    public Panel Show(Func<Panel> make)
    {
        var p = make();
        p.Make = make;
        return Push(p);
    }

    public void Pop()
    {
        if (Panels.Count > 0) Panels.RemoveAt(Panels.Count - 1);
        Refresh();
    }

    public void CloseAll() => Panels.Clear();

    public void Refresh()
    {
        if (Top is not { Make: { } make } old) return;
        var p = make();
        p.Make = make;
        p.Selected = Math.Clamp(old.Selected, 0, Math.Max(0, p.Options.Count - 1));
        Panels[^1] = p;
    }

    /// <summary>A message with a Continue button.</summary>
    public void Tell(string title, string text, Action? then = null, PanelKind kind = PanelKind.Plain)
    {
        var p = new Panel { Title = title, Kind = kind }.Say(text);
        p.Add("Continue", () => { Pop(); then?.Invoke(); });
        p.Back = () => { Pop(); then?.Invoke(); };
        Push(p);
    }

    // ───────────────────────────── keys ─────────────────────────────

    /// <summary>A key goes down.</summary>
    public void Press(string key)
    {
        if (Demo is not null)
        {
            EndDemo();
            return;
        }
        Input(key);
    }

    /// <summary>A key comes up.</summary>
    public void Release(string key) => Held.Remove(key);

    internal void Input(string key)
    {
        Idle = 0;
        Held.Add(key);
        if (Top is { } p)
        {
            PanelKey(p, key);
            return;
        }
        _pressed.Add(key);
        switch (Mode)
        {
            case Mode.Sea: SeaKey(key); break;
            case Mode.Battle: if (key == "Esc") Held.Clear(); break;
        }
    }

    /// <summary>Typed text, for the captain's name.</summary>
    public void Type(string text)
    {
        if (Top is not { Entry: not null } p) return;
        foreach (char ch in text)
            if ((char.IsLetterOrDigit(ch) || ch is ' ' or '\'' or '-' or '.') && p.Entry!.Length < 18)
                p.Entry += ch;
    }

    private void PanelKey(Panel p, string key)
    {
        if (p.Entry is not null)
        {
            switch (key)
            {
                case "Enter":
                    p.OnEntry?.Invoke(p.Entry.Trim());
                    return;
                case "Back":
                    if (p.Entry.Length > 0) p.Entry = p.Entry[..^1];
                    return;
                case "Esc":
                    p.Back?.Invoke();
                    return;
            }
            return;
        }
        var opts = p.Options;
        switch (key)
        {
            case "Up":
                if (opts.Count > 0) { p.Selected = (p.Selected + opts.Count - 1) % opts.Count; Play(Sfx.Menu); }
                return;
            case "Down":
                if (opts.Count > 0) { p.Selected = (p.Selected + 1) % opts.Count; Play(Sfx.Menu); }
                return;
            case "Left" or "Right":
            {
                int d = key == "Left" ? -1 : 1;
                if (opts.Count > 0 && opts[p.Selected].Adjust is { } adj) { adj(d); Play(Sfx.Menu); Refresh(); }
                else if (p.Turn is { } turn) { turn(d); Play(Sfx.Menu); }
                return;
            }
            case "Enter" or "Space":
                Choose(p, p.Selected);
                return;
            case "Esc" or "Back":
                if (p.Back is { } back) { back(); Play(Sfx.Menu); }
                return;
        }
        if (key.Length == 1 && char.IsDigit(key[0]) && key[0] != '0')
        {
            int n = key[0] - '1';
            if (n < opts.Count) { p.Selected = n; Choose(p, n); }
        }
    }

    private void Choose(Panel p, int index)
    {
        if (index < 0 || index >= p.Options.Count) return;
        var o = p.Options[index];
        if (!o.Enabled || o.Act is null) return;
        Play(Sfx.Pick);
        o.Act();
        if (Top == p) Refresh();
    }
}
