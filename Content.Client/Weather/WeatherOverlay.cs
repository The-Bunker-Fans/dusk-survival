using System.Linq;
using Content.Client.Parallax;
using Content.Shared.Weather;
using OpenToolkit.Graphics.ES11;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Weather;

public sealed class WeatherOverlay : Overlay
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    private readonly SpriteSystem _sprite;
    private readonly WeatherSystem _weather;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private IRenderTexture? _blep;

    public WeatherOverlay(SpriteSystem sprite, WeatherSystem weather)
    {
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        _weather = weather;
        _sprite = sprite;
        IoCManager.InjectDependencies(this);
    }

    // TODO: WeatherComponent on the map.
    // TODO: Fade-in
    // TODO: Scrolling(?) like parallax
    // TODO: Need affected tiles and effects to apply.

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return false;

        if (!_entManager.TryGetComponent<WeatherComponent>(_mapManager.GetMapEntityId(args.MapId), out var weather) ||
            weather.Weather == null)
        {
            return false;
        }

        return base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var mapUid = _mapManager.GetMapEntityId(args.MapId);

        if (!_entManager.TryGetComponent<WeatherComponent>(mapUid, out var weather) ||
            weather.Weather == null ||
            !_protoManager.TryIndex<WeatherPrototype>(weather.Weather, out var weatherProto))
        {
            return;
        }

        var alpha = _weather.GetPercent(weather, mapUid, weatherProto);
        DrawWorld(args, weatherProto, alpha);
    }

    private void DrawWorld(in OverlayDrawArgs args, WeatherPrototype weatherProto, float alpha)
    {
        var worldHandle = args.WorldHandle;
        var mapId = args.MapId;
        var worldAABB = args.WorldAABB;
        var worldBounds = args.WorldBounds;
        var invMatrix = args.Viewport.GetWorldToLocalMatrix();
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var position = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;

        if (_blep?.Texture.Size != args.Viewport.Size)
        {
            _blep?.Dispose();
            _blep = _clyde.CreateRenderTarget(args.Viewport.Size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "weather-stencil");
        }

        // Cut out the irrelevant bits via stencil
        // This is why we don't just use parallax; we might want specific tiles to get drawn over
        // particularly for planet maps or stations.
        worldHandle.RenderInRenderTarget(_blep, () =>
        {
            var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

            foreach (var grid in _mapManager.FindGridsIntersecting(mapId, worldAABB))
            {
                var matrix = xformQuery.GetComponent(grid.GridEntityId).WorldMatrix;
                Matrix3.Multiply(in matrix, in invMatrix, out var matty);
                worldHandle.SetTransform(matty);

                foreach (var tile in grid.GetTilesIntersecting(worldAABB))
                {
                    var tileDef = _tileDefManager[tile.Tile.TypeId];

                    // Ignored tiles for stencil
                    if (weatherProto.Tiles.Contains(tileDef.ID))
                    {
                        var anchoredEnts = grid.GetAnchoredEntitiesEnumerator(tile.GridIndices);

                        if (!anchoredEnts.MoveNext(out _))
                        {
                            continue;
                        }
                    }

                    var gridTile = new Box2(tile.GridIndices * grid.TileSize,
                        (tile.GridIndices + Vector2i.One) * grid.TileSize);

                    worldHandle.DrawRect(gridTile, Color.White);
                }
            }

        }, Color.Transparent);

        worldHandle.SetTransform(Matrix3.Identity);
        worldHandle.UseShader(_protoManager.Index<ShaderPrototype>("StencilMask").Instance());
        worldHandle.DrawTextureRect(_blep.Texture, worldBounds);
        Texture? sprite = null;
        var curTime = _timing.RealTime;
        // TODO: Cache this shit.

        switch (weatherProto.Sprite)
        {
            case SpriteSpecifier.Rsi rsi:
                var rsiActual = IoCManager.Resolve<IResourceCache>().GetResource<RSIResource>(rsi.RsiPath).RSI;
                rsiActual.TryGetState(rsi.RsiState, out var state);
                var frames = state!.GetFrames(RSI.State.Direction.South);
                var delays = state.GetDelays();
                var totalDelay = delays.Sum();
                var time = curTime.TotalSeconds % totalDelay;
                var delaySum = 0f;

                for (var i = 0; i < delays.Length; i++)
                {
                    var delay = delays[i];
                    delaySum += delay;

                    if (time > delaySum)
                        continue;

                    sprite = frames[i];
                    break;
                }

                sprite ??= _sprite.Frame0(weatherProto.Sprite);
                break;
            case SpriteSpecifier.Texture texture:
                sprite = texture.GetTexture(IoCManager.Resolve<IResourceCache>());
                break;
            default:
                throw new NotImplementedException();
        }

        // Draw the rain
        worldHandle.UseShader(_protoManager.Index<ShaderPrototype>("StencilDraw").Instance());

        // TODO: This is very similar to parallax but we need stencil support but we can probably combine these somehow
        // and not make it spaghetti, while getting the advantages of not-duped code?

        // Get position offset but rotated around
        var offset = new Vector2(position.X % 1, position.Y % 1);
        offset = rotation.RotateVec(offset);

        var scale = 1.0f;
        var size = sprite.Size / (float) EyeManager.PixelsPerMeter * scale;

        var mat = Matrix3.CreateTransform(position, -rotation);
        worldHandle.SetTransform(mat);
        var viewBox = args.WorldBounds.Box;

        // Slight overdraw because I'm done but uhh don't worry about it.
        for (var x = -viewBox.Width / 2f - 1f; x <= viewBox.Width / 2f + 1f; x += size.X * scale)
        {
            for (var y = -viewBox.Height - 1f; y <= viewBox.Height / 2f + 1f; y += size.Y * scale)
            {
                var boxPosition = new Vector2(x - offset.X, y - offset.Y);

                // Yes I spent a while making sure no texture holes when the eye is rotating.
                var box = Box2.FromDimensions(boxPosition, size * scale);
                worldHandle.DrawTextureRect(sprite, box, (weatherProto.Color ?? Color.White).WithAlpha(alpha));
                // Deadcode but very useful for debugging to check there's no overlap or dead spots.
                // worldHandle.DrawRect(box, Color.Red.WithAlpha(alpha));
            }
        }

        worldHandle.SetTransform(Matrix3.Identity);
        worldHandle.UseShader(null);
    }
}
