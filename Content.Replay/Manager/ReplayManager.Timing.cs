using System.Linq;
using System.Threading.Tasks;
using Content.Client.UserInterface.Systems.Chat;
using Content.Replay.Observer;
using Robust.Client.GameObjects;
using Robust.Client.GameStates;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Replay.Manager;

// This partial class contains codes for modifying the current game tick/time.
public sealed partial class ReplayManager
{
    /// <summary>
    ///     Set the current replay index (aka, jump to a specific point in time).
    /// </summary>
    private void SetIndex(int value, bool stopPlaying)
    {
        if (CurrentReplay == null)
            return;

        Playing &= !stopPlaying;
        value = Math.Clamp(value, 0, CurrentReplay.States.Count - 1);
        if (value == CurrentReplay.CurrentIndex)
            return;

        // When skipping forward or backward in time, we want to avoid changing the player's current position.
        var sys = _entMan.EntitySysManager.GetEntitySystem<ReplayObserverSystem>();
        var observer = sys.GetObserverPosition();

        bool skipEffectEvents = value > CurrentReplay.CurrentIndex + _visualEventThreshold;
        if (value < CurrentReplay.CurrentIndex)
        {
            skipEffectEvents = true;
            ResetToNearestCheckpoint(value, false);
        }
        else if (value > CurrentReplay.CurrentIndex + _checkpointInterval)
        {
            // If we are skipping many ticks into the future, we try to skip directly to a checkpoint instead of
            // applying every tick.
            var nextCheckpoint = GetNextCheckpoint(CurrentReplay, CurrentReplay.CurrentIndex);
            if (nextCheckpoint.Index < value)
                ResetToNearestCheckpoint(value, false);
        }

        _entMan.EntitySysManager.GetEntitySystem<ClientDirtySystem>().Reset();

        while (CurrentReplay.CurrentIndex < value)
        {
            CurrentReplay.CurrentIndex++;
            var state = CurrentReplay.CurState;

            _timing.LastRealTick = _timing.LastProcessedTick = _timing.CurTick = CurrentReplay.CurTick;
            _gameState.UpdateFullRep(state, cloneDelta: true);
            _gameState.ApplyGameState(state, CurrentReplay.NextState);
            ProcessMessages(CurrentReplay.CurMessages, skipEffectEvents);

            // TODO REPLAYS block audio
            // Find a way to just block audio/midi from starting, instead of stopping it after every state application.
            StopAudio();

            DebugTools.Assert(CurrentReplay.LastApplied + 1 == state.ToSequence);
            CurrentReplay.LastApplied = state.ToSequence;
        }

        sys.SetObserverPosition(observer);
    }

    /// <summary>
    ///     This function resets the game state to some checkpoint state. This is effectively what enables rewinding time.
    /// </summary>
    /// <param name="index">The target tick/index. The actual checkpoint will have an index less than or equal to this.</param>
    /// <param name="flushEntities">Whether to delete all entities</param>
    public void ResetToNearestCheckpoint(int index, bool flushEntities)
    {
        // TODO REPLAYS unload prototypes & resources

        if (CurrentReplay == null)
            return;

        if (flushEntities)
            _entMan.FlushEntities();

        var checkpoint = GetLastCheckpoint(CurrentReplay, index);
        var state = checkpoint.State;

        _sawmill.Info($"Resetting to checkpoint. From {CurrentReplay.CurrentIndex} to {checkpoint.Index}");
        var st = new Stopwatch();
        st.Start();

        CurrentReplay.CurrentIndex = checkpoint.Index;
        DebugTools.Assert(state.ToSequence == new GameTick(CurrentReplay.TickOffset.Value + (uint) CurrentReplay.CurrentIndex));

        foreach (var (name, value) in checkpoint.Cvars)
        {
            _netConf.SetCVar(name, value, force: true);
        }

        _timing.TimeBase = checkpoint.TimeBase;
        _timing.CurTick = _timing.LastRealTick = _timing.LastProcessedTick = new GameTick(CurrentReplay.TickOffset.Value + (uint) CurrentReplay.CurrentIndex);
        CurrentReplay.LastApplied = state.ToSequence;

        _gameState.PartialStateReset(state, false, false);
        _entMan.EntitySysManager.GetEntitySystem<ClientDirtySystem>().Reset();
        _entMan.EntitySysManager.GetEntitySystem<TransformSystem>().Reset();

        // TODO REPLAYS custom chat control
        // Maybe one that allows players to skip directly to players via their names?
        // I don't like having to just manipulate ChatUiController like this.
        _uiMan.GetUIController<ChatUIController>().History.RemoveAll(x => x.Item1 > _timing.CurTick);
        _uiMan.GetUIController<ChatUIController>().Repopulate();
        _gameState.UpdateFullRep(state, cloneDelta: true);
        _gameState.ApplyGameState(state, CurrentReplay.NextState);

        // TODO REPLAYS add asserts
        // foreach entity
        //  if networked
        //    check last applied/modified tick
        //    foreach component
        //      check creation, modified ticks

        _timing.CurTick += 1;
        StopAudio();

        _sawmill.Info($"Resetting to checkpoint took {st.Elapsed}");
    }

    /// <summary>
    ///     This is effectively an async variant of <see cref="ResetToNearestCheckpoint"/> that sets up the 0th tick.
    /// </summary>
    /// <param name="replayData"></param>
    /// <param name="callback"></param>
    /// <param name="yield"></param>
    public async Task StartReplayAsync(ReplayData data,
        Func<float, float, LoadReplayJob.LoadingState, bool, Task> callback)
    {
        if (data.Checkpoints.Length == 0)
            return;

        var checkpoint = data.Checkpoints[0];
        data.CurrentIndex = checkpoint.Index;
        var state = checkpoint.State;

        foreach (var (name, value) in checkpoint.Cvars)
        {
            _netConf.SetCVar(name, value, force: true);
        }

        var tick = new GameTick(data.TickOffset.Value + (uint) data.CurrentIndex);
        _timing.CurTick = _timing.LastRealTick = _timing.LastProcessedTick = tick;

        _gameState.UpdateFullRep(state, cloneDelta: true);

        var i = 0;
        var total = state.EntityStates.Value.Count;
        List<EntityUid> entities = new(state.EntityStates.Value.Count);

        await callback(i, total, LoadReplayJob.LoadingState.Spawning, true);
        foreach (var ent in state.EntityStates.Value)
        {
            var metaState = (MetaDataComponentState?)ent.ComponentChanges.Value?
                .FirstOrDefault(c => c.NetID == _metaCompNetId).State;
            if (metaState == null)
                throw new MissingMetadataException(ent.Uid);

            _entMan.CreateEntityUninitialized(metaState.PrototypeId, ent.Uid);
            entities.Add(ent.Uid);

            if (i++ % 50 == 0)
            {
                await callback(i, total, LoadReplayJob.LoadingState.Spawning, false);
                _timing.CurTick = tick;
            }
        }

        await callback(0, total, LoadReplayJob.LoadingState.Initializing, true);
        // TODO add async variant?
        _gameState.ApplyGameState(state, data.NextState);

        i = 0;
        var query = _entMan.GetEntityQuery<MetaDataComponent>();
        foreach (var uid in entities)
        {
            _entMan.InitializeEntity(uid, query.GetComponent(uid));
            if (i++ % 50 == 0)
            {
                await callback(i, total, LoadReplayJob.LoadingState.Initializing, false);
                _timing.CurTick = tick;
            }
        }

        i = 0;
        await callback(0, total, LoadReplayJob.LoadingState.Starting, true);
        foreach (var uid in entities)
        {
            _entMan.StartEntity(uid);
            if (i++ % 50 == 0)
            {
                await callback(i, total, LoadReplayJob.LoadingState.Starting, false);
                _timing.CurTick = tick;
            }
        }

        _timing.TimeBase = checkpoint.TimeBase;
        data.LastApplied = state.ToSequence;
        DebugTools.Assert(_timing.LastRealTick == tick);
        DebugTools.Assert(_timing.LastProcessedTick == tick);
        _timing.CurTick = tick + 1;
    }

    public void StopAudio()
    {
        _clydeAudio.StopAllAudio();

        foreach (var renderer in _midi.Renderers)
        {
            renderer.ClearAllEvents();
            renderer.StopAllNotes();
        }
    }
}
