using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace TauTick.UI;

public sealed class Theme : Styles
{
    public Theme() => AvaloniaXamlLoader.Load(this);
}
