using System.Reflection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class DemocritusArrangementRuntimeChecks
{
    public static Task Run()
    {
        ModelDb.Inject(typeof(DemocritusArrangement));
        DemocritusArrangement canonical = ModelDb.Event<DemocritusArrangement>();
        EventRoom initial = new(canonical);
        SerializableRoom saved = initial.ToSerializable();
        Check(saved.RoomType == RoomType.Event && Equals(saved.EventId, canonical.Id)
            && !saved.IsPreFinished, "ENC110: the initial native checkpoint must contain the exact open Democritus event.");

        EventRoom restored = (EventRoom)AbstractRoom.FromSerializable(saved, null!)!;
        SerializableRoom roundTrip = restored.ToSerializable();
        Check(ReferenceEquals(restored.CanonicalEvent, canonical)
            && roundTrip.RoomType == RoomType.Event && Equals(roundTrip.EventId, saved.EventId)
            && !roundTrip.IsPreFinished, "ENC110: native room restore preserves the canonical event and open flag without recreating combat.");

        MethodInfo matches = typeof(DemocritusArrangement).GetMethod(
            "MatchesSavedRoom", BindingFlags.Static | BindingFlags.NonPublic)!;
        bool Matches(SerializableRun candidate) => (bool)matches.Invoke(null, [candidate, initial])!;
        SerializableRun snapshot = new() { PreFinishedRoom = roundTrip };
        Check(Matches(snapshot), "ENC110: the event persistence boundary accepts the native round-trip checkpoint.");
        snapshot.PreFinishedRoom = null;
        Check(!Matches(snapshot), "ENC110: a SaveRun(null) checkpoint cannot confirm this event.");
        snapshot.PreFinishedRoom = roundTrip;
        roundTrip.EventId = new ModelId(canonical.Id.Category, "OTHER_EVENT");
        Check(!Matches(snapshot), "ENC110: another native event cannot confirm the Democritus checkpoint.");
        roundTrip.EventId = canonical.Id;
        roundTrip.RoomType = (RoomType)(-1);
        Check(!Matches(snapshot), "ENC110: a mismatched native room type cannot confirm the event checkpoint.");
        roundTrip.RoomType = RoomType.Event;
        roundTrip.IsPreFinished = true;
        Check(!Matches(snapshot), "ENC110: a changed native pre-finished flag cannot confirm an open checkpoint.");

        EventRoom preFinished = (EventRoom)AbstractRoom.FromSerializable(roundTrip, null!)!;
        Check(preFinished.IsPreFinished && preFinished.ToSerializable().IsPreFinished,
            "ENC110: native serialization preserves a pre-finished flag instead of silently reopening it.");
        Console.WriteLine("ENC110 native room checks passed: exact EventId/RoomType/open flag, native serialization and restore, missing/wrong event and flag rejection; no rendered UI or actual run save.");
        return Task.CompletedTask;
    }
}
