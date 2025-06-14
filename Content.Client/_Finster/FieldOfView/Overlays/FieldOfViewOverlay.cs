using System.Numerics;
using Content.Client.Examine;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.DamageOverlays.Overlays;
using Content.Client.Viewport;
using Content.KayMisaZlevels.Client;
using Content.Shared._Finster.FieldOfView;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.Utility;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._Finster.FieldOfView;

public sealed class FieldOfViewOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    //private readonly ClientFieldOfViewSystem _fovSystem;

    private EntityQuery<FieldOfViewComponent> _fovQuery;
    private EntityQuery<EyeComponent> _eyeQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public FieldOfViewOverlay()
    {
        IoCManager.InjectDependencies(this);

        //_fovSystem = _entManager.System<ClientFieldOfViewSystem>();

        _fovQuery = _entManager.GetEntityQuery<FieldOfViewComponent>();
        _eyeQuery = _entManager.GetEntityQuery<EyeComponent>();
        _xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        ZIndex = DamageOverlay.DrawingDepth - 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not null &&
            _fovQuery.TryComp(_player.LocalEntity.Value, out var fovComp))
            return fovComp.Enabled;

        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is null)
            return;

        var playerEntity = _player.LocalEntity.Value;

        if (!_eyeQuery.TryComp(playerEntity, out var eyeComp))
            return;

        if (args.Viewport.Eye != eyeComp.Eye)
            return;

        if (!_entManager.TryGetComponent<TransformComponent>(playerEntity, out var playerTransform) ||
            !_entManager.TryGetComponent<FieldOfViewComponent>(playerEntity, out var fovComp))
            return;

        var viewport = args.WorldBounds;

        DrawDarkenedArea(args.WorldHandle, viewport, playerTransform, fovComp, eyeComp);
    }

    private void DrawDarkenedArea(
        DrawingHandleWorld handle,
        Box2Rotated viewport,
        TransformComponent playerTransform,
        FieldOfViewComponent fieldOfView,
        EyeComponent eyeComp)
    {
        var playerRotation = playerTransform.WorldRotation;
        playerRotation = playerRotation.Reduced().FlipPositive();

        if (fieldOfView.Simple4DirMode)
        {
            // Получаем поворот грида, на котором стоит игрок
            if (playerTransform.GridUid is { } gridUid &&
                _xformQuery.TryComp(gridUid, out var gridTransform))
            {
                var gridRotation = gridTransform.WorldRotation;

                // Переводим угол в локальные координаты грида
                var localPlayerRotation = (playerRotation - gridRotation).Reduced().FlipPositive();

                // Определяем 4 направления относительно грида
                var rsiDirection = SpriteComponent.Layer.GetDirection(Robust.Shared.Graphics.RSI.RsiDirectionType.Dir4, localPlayerRotation);
                var targetDirection = rsiDirection.Convert();

                // Возвращаем обратно в мировые координаты
                playerRotation = (targetDirection.ToAngle() + gridRotation).Reduced();
            }
            /*
            else
            {
                var rsiDirection = SpriteComponent.Layer.GetDirection(Robust.Shared.Graphics.RSI.RsiDirectionType.Dir4, playerRotation);
                var targetDirection = rsiDirection.Convert();
                playerRotation = targetDirection.ToAngle();
            }
            */
        }

        var blockedViewDir = fieldOfView.GetRotation(fieldOfView.Direction);

        var startAngle = playerRotation + MathHelper.DegreesToRadians(blockedViewDir - fieldOfView.Angle / 2);
        var endAngle = playerRotation + MathHelper.DegreesToRadians(blockedViewDir + fieldOfView.Angle / 2);

        //DrawSector(handle, playerTransform.WorldPosition, startAngle, endAngle, fieldOfView.Distance, Color.Black.WithAlpha(0.5f));
        DrawNonVisibleSectors(
            handle,
            playerTransform.WorldPosition,
            startAngle,
            endAngle,
            fieldOfView.MinDistance,
            fieldOfView.MaxDistance,
            Color.Black.WithAlpha(0.25f));
    }

    /// GENERATED BY DEEPSEEK. Also idk how this shit is works, but looks cool :)
    private void DrawNonVisibleSectors(
        DrawingHandleWorld handle,
        Vector2 center,
        Angle visibleStartAngle,
        Angle visibleEndAngle,
        float minRadius,
        float maxRadius,
        Color color)
    {
        visibleStartAngle = NormalizeAngle(visibleStartAngle);
        visibleEndAngle = NormalizeAngle(visibleEndAngle);

        if (Math.Abs(visibleEndAngle - visibleStartAngle) >= 2 * Math.PI)
            return;

        if (visibleEndAngle > visibleStartAngle)
        {
            DrawSector(handle, center, visibleEndAngle, visibleStartAngle + 2 * Math.PI, minRadius, maxRadius, color);
        }
        else
        {
            DrawSector(handle, center, visibleEndAngle, visibleStartAngle, minRadius, maxRadius, color);
        }
    }

    /// GENERATED BY DEEPSEEK. Also idk how this shit is works, but looks cool :)
    private void DrawSector(
        DrawingHandleWorld handle,
        Vector2 center,
        Angle startAngle,
        Angle endAngle,
        float minRadius,
        float maxRadius,
        Color color)
    {
        // Количество сегментов для аппроксимации сектора
        const int segments = 32;

        // Создаём вершины для внешнего и внутреннего радиусов
        var outerVertices = new Vector2[segments + 1];
        var innerVertices = new Vector2[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            var angle = startAngle + (endAngle - startAngle) * i / segments;
            outerVertices[i] = center + new Vector2(MathF.Cos((float) angle), MathF.Sin((float) angle)) * maxRadius;
            innerVertices[i] = center + new Vector2(MathF.Cos((float) angle), MathF.Sin((float) angle)) * minRadius;
        }

        // Создаём массив вершин для отрисовки
        var vertices = new Vector2[(segments + 1) * 2];
        for (int i = 0; i <= segments; i++)
        {
            vertices[i * 2] = outerVertices[i];
            vertices[i * 2 + 1] = innerVertices[i];
        }

        // Отрисовываем сектор как треугольную полосу (TriangleStrip)
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, vertices, color);
    }

    private double NormalizeAngle(double angle)
    {
        angle = angle % (2 * Math.PI);
        if (angle < 0)
            angle += 2 * Math.PI;
        return angle;
    }
}
