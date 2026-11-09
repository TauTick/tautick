namespace TauTick.UI.Gallery;

public sealed record GalleryOptions(string Scene = "controls", bool Compact = false)
{
    public static readonly string[] SceneNames = ["controls", "toolbar", "settings"];
    public static GalleryOptions Parse(string[] args)
    {
        var scene = "controls"; var compact = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--compact") compact = true;
            else if (args[i] == "--scene" && ++i < args.Length && SceneNames.Contains(args[i])) scene = args[i];
            else throw new ArgumentException("Usage: TauTick.UI.Gallery [--scene controls|toolbar|settings] [--compact]");
        }
        return new(scene, compact);
    }
}
