using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.Gravity;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Client.Gravity;

public sealed partial class GravitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCameraRecoilSystem _sharedCameraRecoil = default!;

    private void InitializeShake()
    {
        SubscribeLocalEvent<GravityShakeComponent, ComponentInit>(OnShakeInit);
    }

    private void OnShakeInit(EntityUid uid, GravityShakeComponent component, ComponentInit args)
    {
        var localPlayer = _playerManager.LocalPlayer?.ControlledEntity;

        if (!TryComp<TransformComponent>(localPlayer, out var xform) ||
            xform.GridUid != uid && xform.MapUid != uid)
        {
            return;
        }

        if (Timing.IsFirstTimePredicted && TryComp<GravityComponent>(uid, out var gravity))
        {
            _audio.PlayGlobal(gravity.GravityShakeSound, Filter.Local(), true, AudioParams.Default.WithVolume(-2f));
        }
    }

    protected override void ShakeGrid(EntityUid uid, GravityComponent? gravity = null)
    {
        base.ShakeGrid(uid, gravity);

        if (!Resolve(uid, ref gravity) || !Timing.IsFirstTimePredicted)
            return;

        var localPlayer = _playerManager.LocalPlayer?.ControlledEntity;

        if (!TryComp<TransformComponent>(localPlayer, out var xform))
            return;

        if (xform.GridUid != uid ||
            xform.GridUid == null && xform.MapUid != uid)
        {
            return;
        }

        var kick = new Vector2(_random.NextFloat(), _random.NextFloat()) * GravityKick;
        _sharedCameraRecoil.KickCamera(localPlayer.Value, kick);
    }
}
