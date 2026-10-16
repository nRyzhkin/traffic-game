namespace TrafficGame.Views.Rendering
{
    /// <summary>A map view drawn in theme colors; it redraws what it shows when the palette changes.</summary>
    public interface IMapPaletteView
    {
        void ApplyPalette(MapPalette palette);
    }
}
