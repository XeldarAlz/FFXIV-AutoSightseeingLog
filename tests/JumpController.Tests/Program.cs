using AutoSightseeingLog.Core.Tasks;
using AutoSightseeingLog.Core.Vistas;
using System.Numerics;

var tests = new List<(string Name, Action Run)>
{
    ("Kugane leg 44: the lower lip is not the previous takeoff without a supported path", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var lowerLip = new Vector3(-54.31f, 54.29f, -64.25f); // 13:06:06, after the failed startup.
        Equal(42, JumpPosition.NearestLeg(route, lowerLip, false, 43)); // Proximity alone is misleading.
        Assert(!JumpLanding.CanCorrect(lowerLip, route[42].Point, true, JumpFloorPath.Unavailable));
        Assert(!JumpLanding.RetainedFoothold(route, lowerLip, 43));
        Assert(JumpLanding.CanCorrect(lowerLip, route[42].Point, true, JumpFloorPath.Supported));
        Assert(!JumpLanding.CanCorrect(new(-54.28f, 53.66f, -64.28f), route[42].Point, true, JumpFloorPath.Unavailable));
    }),
    ("Kugane leg 44: a jump flag without ascent cannot supply a usable flight arc", () =>
    {
        var launch = new Vector3(-54.21f, 54.51f, -64.35f);
        var flight = new JumpFlight(launch, new(-55.5f, 55.13f, -62.6f), 0);
        JumpSample[] samples =
        [
            new(new(-54.23f, 54.51f, -64.33f), true, 0.017),
            new(new(-54.25f, 54.51f, -64.31f), true, 0.032),
            new(new(-54.27f, 54.51f, -64.29f), true, 0.048),
            new(new(-54.29f, 54.51f, -64.27f), true, 0.062),
            new(new(-54.31f, 54.29f, -64.25f), true, 0.077),
        ];
        foreach (var sample in samples.SkipLast(1))
            Equal(FlightDecision.Flying, flight.Check(sample));
        Equal(FlightDecision.Missed, flight.Check(samples[^1]));
        Assert(flight.Takeoff.HasValue && !flight.RoseFromLaunch);
        var trajectory = new JumpTrajectory();
        Assert(!trajectory.Observe(samples, new(launch, false, 0), 0.031f, out _));
        Equal(0, trajectory.Count);
    }),
    ("The ornament takeoff aligns the observed 6cm offset before a short preserved run-up", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var contact = new Vector3(-54.19f, 54.51f, -64.37f);
        var step = route[42];
        Assert(JumpPosition.OnPoint(contact, step.Point, JumpPosition.AlignMeters));
        Assert(JumpPosition.NeedsLandingAlignment(contact, step.Point, true, route[43], step.LandingTolerance));
        Assert(JumpLanding.CanCorrect(contact, step.Point, true, JumpFloorPath.Unavailable));
        Assert(!JumpPosition.NeedsLandingAlignment(step.Point, step.Point, true, route[43], step.LandingTolerance));
        Near(0.027f, JumpPhysics.LaunchDistance(route[43].RunUpSeconds), 0.001f);
        Assert(route[43].PreserveRunUp);
    }),
    ("A reset verifies the aetheryte arrival even when the entrance is on a different street level", () =>
    {
        var aetheryte = new Vector3(50f, 4f, 50f);
        var arrival = aetheryte + new Vector3(4f, 0f, 4f);
        Assert(JumpLanding.IsAtTeleportDestination(arrival, aetheryte, true));
        Assert(!JumpLanding.IsAtRouteGroundLevel(arrival, new(-42.05f, 14.03f, -36.19f)));
        Assert(!JumpLanding.IsAtTeleportDestination(new(-54.28f, 53.66f, -64.28f), aetheryte, true));
        Assert(!JumpLanding.IsAtTeleportDestination(arrival + Vector3.UnitY * 15f, aetheryte, true));
        Assert(!JumpLanding.IsAtTeleportDestination(arrival + Vector3.UnitX * 30f, aetheryte, true));
    }),
    ("A reset to a city aetheryte without a recorded height checks only the ground distance", () =>
    {
        var marker = new Vector3(-24f, 0f, 2f);
        var arrival = new Vector3(-20f, 37f, 6f);
        Assert(JumpLanding.IsAtTeleportDestination(arrival, marker, false));
        Assert(!JumpLanding.IsAtTeleportDestination(arrival, marker, true));
        Assert(!JumpLanding.IsAtTeleportDestination(arrival + Vector3.UnitX * 30f, marker, false));
    }),
    ("The upper-roof route follows seven successful recorded landings and excludes the final fall", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var flights = KuganeBalconyRecording.Flights(KuganeBalconyRecording.Read("kugane-upper-roof-20261018.csv"));
        Equal(9, flights.Count);
        var legs = new[] { 60, 61, 62, 64, 65, 68, 69 };
        for (var index = 0; index < legs.Length; index++)
        {
            var flight = flights[index + 1];
            var step = route[legs[index]];
            Assert(step.Jumps);
            Assert(Vector3.Distance(step.Point, flight[^1].Position) < 0.001f);
            var observer = new JumpFlight(flight[0].Position, step.Point, flight[0].Seconds,
                allowDescendingStartup: step.RunUpSlope < 0f);
            foreach (var sample in flight.Skip(1).SkipLast(1))
                Equal(FlightDecision.Flying, observer.Check(sample));
            Equal(FlightDecision.Touchdown, observer.Check(flight[^1]));
        }
        Assert(flights[^1][^1].Position.Y < flights[^1][0].Position.Y - 9f);
        Assert(route.Steps.All(step => Vector3.Distance(step.Point, flights[^1][^1].Position) > 0.1f));
        Equal(JumpStep.Walk(-44.24f, 77.25f, -65.65f), route[70]);
        Equal(JumpStep.Jump(-42.81f, 79.05f, -64.55f, 0.07f), route[71]);
        Assert(route[65].RunUpSeconds > 0f); // The 75.11m post needs a moving jump.
    }),
    ("The recorded downhill roof run-up can launch while still detecting real falls", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var frames = KuganeBalconyRecording.Read("kugane-upper-roof-20261018.csv");
        var launch = KuganeBalconyRecording.Flights(frames)[1][0];
        var from = route[59].Point;
        var step = route[60];
        var distance = JumpPhysics.LaunchDistance(step.RunUpSeconds);
        var runUp = new JumpRunUp(from, step.Point, distance, launch.Seconds - 0.12, step.RunUpSlope);
        Assert(step.PreserveRunUp && step.RunUpSlope < 0f);
        Equal(RunUpDecision.Fell, new JumpRunUp(from, step.Point, distance, launch.Seconds - 0.12).Check(launch));
        Equal(RunUpDecision.Jump, runUp.Check(launch));
        Equal(RunUpDecision.Fell, runUp.Check(launch with { Airborne = true }));
        Equal(RunUpDecision.Fell, runUp.Check(launch with { Position = launch.Position - Vector3.UnitY * 0.2f }));
    }),
    ("The upper-roof turn-back has its actual run-up start instead of net displacement", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var frames = KuganeBalconyRecording.Read("kugane-upper-roof-20261018.csv");
        var reversal = frames.Where(sample => sample.Seconds is >= 6.80 and <= 7.03).ToArray();
        Assert(reversal.All(sample => !sample.Airborne && Vector3.Distance(sample.Position, route[63].Point) < 0.001f));
        var takeoff = KuganeBalconyRecording.Flights(frames)[4][0];
        var runUp = Vector2.Distance(new(route[63].Point.X, route[63].Point.Z), new(takeoff.Position.X, takeoff.Position.Z));
        Near(runUp, JumpPhysics.LaunchDistance(route[64].RunUpSeconds), 0.002f);
        Assert(runUp > 0.5f); // The recorder's net displacement was only 0.214m.
    }),
    ("Downhill jump startup cannot hide a fall or a later descending miss", () =>
    {
        var flight = new JumpFlight(Vector3.Zero, new(3f, 1.46f, 0f), 0, allowDescendingStartup: true);
        Equal(FlightDecision.Flying, flight.Check(Sample(0.3f, -0.2f, 0.07, true)));
        Equal(FlightDecision.Missed, flight.Check(Sample(0.8f, -0.2f, 0.26, true)));
        var fall = new JumpFlight(Vector3.Zero, new(3f, 1.46f, 0f), 0, allowDescendingStartup: true);
        Equal(FlightDecision.Missed, fall.Check(Sample(0.5f, -0.5f, 0.1, true)));
        var arc = new JumpFlight(Vector3.Zero, new(3f, 1.46f, 0f), 0, allowDescendingStartup: true);
        Equal(FlightDecision.Flying, arc.Check(Sample(1f, 1.6f, 0.4, true)));
        Equal(FlightDecision.Missed, arc.Check(Sample(2f, 0.8f, 0.7, true)));
        var ordinary = new JumpFlight(Vector3.Zero, new(3f, 1.46f, 0f), 0);
        Equal(FlightDecision.Missed, ordinary.Check(Sample(0.3f, -0.2f, 0.07, true)));
    }),
    ("Kugane leg 54 uses the recorded two-hop roof entrance with reachable landing heights", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var frames = KuganeBalconyRecording.Read("kugane-roof-entrance-20261018.csv");
        var flights = KuganeBalconyRecording.Flights(frames);
        Equal(2, flights.Count);
        var measured = new JumpArc(10.35f, 29.75f, 2.1f, 0.016f); // Preceding jump, 12:52:44.
        Assert(!measured.TryFlightSeconds(66.48f - frames[0].Position.Y, out _));
        for (var index = 0; index < flights.Count; index++)
        {
            var step = route[53 + index];
            var flight = flights[index];
            Assert(step.Jumps && step.PreserveRunUp && step.RunUpSeconds == 0f);
            Assert(Vector3.Distance(step.Point, flight[^1].Position) < 0.001f);
            Assert(measured.TryFlightSeconds(step.Point.Y - flight[0].Position.Y, out _));
            var observer = new JumpFlight(flight[0].Position, step.Point, flight[0].Seconds);
            foreach (var sample in flight.Skip(1).SkipLast(1))
                Equal(FlightDecision.Flying, observer.Check(sample));
            Equal(FlightDecision.Touchdown, observer.Check(flight[^1]));
            Assert(JumpPosition.OnPoint(flight[^1].Position, step.Point, JumpPosition.AlignMeters));
        }
        Equal(JumpStep.Walk(-54.75f, 67.27f, -63.7f), route[55]); // Rejoins the existing roof walk.
    }),
    ("Kugane leg 40 settles on the following recorded stair foothold", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var contact = new Vector3(-53.34f, 53.65f, -65.49f); // 12:48:25 live run.
        Assert(!JumpPosition.OnPoint(contact, route[39].Point, 1f));
        Equal(41, JumpPosition.FollowingWalkLeg(route, 39, contact, false));
        Assert(JumpLanding.CanCorrect(contact, route[41].Point, true, JumpFloorPath.Unavailable));
        Equal(-1, JumpPosition.FollowingWalkLeg(route, 39, contact, true));
        Equal(-1, JumpPosition.FollowingWalkLeg(route, 39, contact - Vector3.UnitY, false));
        Equal(-1, JumpPosition.FollowingWalkLeg(route, 39, route[44].Point, false)); // Beyond another jump.
        Equal(-1, JumpPosition.FollowingWalkLeg(route, 38, contact, false)); // Failed walk, not a hop.
    }),
    ("Roof recovery follows a pitched surface, including a ridge above both endpoints", () =>
    {
        Vector3? Floor(Vector3 point)
        {
            var height = point.X <= 5f ? point.X * 1.1f : 5.5f - (point.X - 5f) * 0.7f;
            return MathF.Abs(point.Y - height) <= 0.4f ? new(point.X, height, point.Z) : null;
        }
        Assert(JumpRecoveryPath.TryCreate(Vector3.Zero, new(10f, 2f, 0f), Floor, (_, _) => true,
            out var path, out var failure));
        Equal(JumpRecoveryFailure.None, failure);
        Assert(path!.Points.Max(point => point.Y) > 5f);
        Assert(path.Contains(new(5f, 5.5f, 0f)));
        Assert(!path.Contains(new(5f, 4.5f, 0f))); // Falling below the roof stops recovery.
        Assert(!path.Contains(new(5f, 5.5f, 0.5f))); // So does leaving its supported corridor.
    }),
    ("Roof recovery rejects missing floor, gaps, obstacles and a different roof under the target", () =>
    {
        Vector3? Floor(Vector3 point) => new(point.X, 0f, point.Z);
        Assert(!JumpRecoveryPath.TryCreate(Vector3.Zero, new(10f, 0f, 0f), _ => null,
            (_, _) => true, out _, out var missing));
        Equal(JumpRecoveryFailure.FloorUnavailable, missing);
        Assert(!JumpRecoveryPath.TryCreate(Vector3.Zero, new(10f, 0f, 0f),
            point => point.X is > 4f and < 6f ? null : Floor(point), (_, _) => true, out _, out var gap));
        Equal(JumpRecoveryFailure.GapOrSlope, gap);
        Assert(!JumpRecoveryPath.TryCreate(Vector3.Zero, new(10f, 0f, 0f), Floor,
            (from, to) => !(from.X < 5f && to.X >= 5f), out _, out var wall));
        Equal(JumpRecoveryFailure.Obstacle, wall);
        Assert(!JumpRecoveryPath.TryCreate(Vector3.Zero, new(10f, 3f, 0f), Floor,
            (_, _) => true, out _, out var level));
        Equal(JumpRecoveryFailure.WrongLevel, level);
    }),
    ("Roof recovery requires floor beside the centre line and rejects cliffs", () =>
    {
        Assert(!JumpRecoveryPath.TryCreate(Vector3.Zero, new(10f, 0f, 0f),
            point => MathF.Abs(point.Z) > 0.05f ? null : new(point.X, 0f, point.Z),
            (_, _) => true, out _, out var narrow));
        Equal(JumpRecoveryFailure.GapOrSlope, narrow);
        Assert(!JumpRecoveryPath.TryCreate(Vector3.Zero, new(10f, 3f, 0f),
            point => new(point.X, point.X < 5f ? 0f : 3f, point.Z),
            (_, _) => true, out _, out var cliff));
        Equal(JumpRecoveryFailure.GapOrSlope, cliff);
    }),
    ("Kugane roof recovery selects the first roof takeoff without skipping to unvisited jumps", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var from = new Vector3(-56.83f, 38.70f, -70.07f);
        var target = route[25].Point;
        // A hypothetical connected roof tests the selection logic, not the game's collision mesh.
        Vector3? Floor(Vector3 point)
        {
            var height = float.Lerp(from.Y, target.Y, (point.X - from.X) / (target.X - from.X));
            return MathF.Abs(point.Y - height) <= 0.4f ? new(point.X, height, point.Z) : null;
        }
        var candidates = JumpRecoveryPath.Checkpoints(route, 29).ToArray();
        Assert(candidates.All(leg => leg <= 29 && !route[leg].Jumps && !route[leg].Drops));
        var selected = candidates.First(leg => JumpRecoveryPath.TryCreate(from, route[leg].Point,
            Floor, (_, _) => true, out _, out _));
        Equal(25, selected);
        Assert(!JumpRecoveryPath.Checkpoints(route, 24).Contains(25));
    }),
    ("Kugane roof miss: capped-speed calibration must not underestimate the intermediate jump", () =>
    {
        var model = new JumpLaunchModel(KuganeRoofRegression.Prior);
        Assert(model.TryPredict(0.360f, out var speed, out var startup));
        Assert(JumpArc.TryFit(KuganeRoofRegression.Miss, KuganeRoofRegression.Launch.Y, out var actual));
        Assert(actual.TryFlightSeconds(1.5f, out var seconds));
        var actualStartup = Vector2.Distance(new(KuganeRoofRegression.Launch.X, KuganeRoofRegression.Launch.Z), actual.TakeoffPosition);
        var range = speed * seconds + startup;
        var actualRange = actual.HorizontalSpeed * seconds + actualStartup;
        Near(actualRange, range, 0.10f);
        Assert(range > 3.412f + 0.15f); // The old prediction missed the ledge on this attempt.
    }),
    ("A speed ceiling bounds long run-ups without flattening acceleration below it", () =>
    {
        var model = new JumpLaunchModel(KuganeRoofRegression.Prior);
        Assert(model.TryPredict(0.7f, out var capped, out _));
        Near(7.04f, capped, 0.01f);
        Assert(model.TryPredict(0.36f, out var accelerating, out _));
        Assert(accelerating > 6.4f && accelerating < capped);
        Assert(!model.TryPredict(1f, out _, out _));
    }),
    ("Manual balcony recording contains five jumps and one walk-off drop", () =>
    {
        var flights = KuganeBalconyRecording.Flights(KuganeBalconyRecording.Read());
        Equal(6, flights.Count);
        for (var index = 0; index < flights.Count; index++)
        {
            var flight = flights[index];
            Equal(index == 2, RecordedAirMotion.IsDrop(flight[0].Position.Y,
                flight.Max(sample => sample.Position.Y), flight[^1].Position.Y));
        }
    }),
    ("A jump to a lower platform still records a jump when it rises first", () =>
    {
        Assert(!RecordedAirMotion.IsDrop(28.1f, 29.57f, 26.6f));
        Assert(RecordedAirMotion.IsDrop(27.76f, 27.76f, 26.6f));
        Assert(!RecordedAirMotion.IsDrop(27.76f, 27.76f, 27.76f));
    }),
    ("The balcony route uses the successful recording's landings and drop action", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var recording = KuganeBalconyRecording.Read();
        var flights = KuganeBalconyRecording.Flights(recording);
        var legs = new[] { 15, 16, 17, 19, 20, 21 };
        Assert(Vector3.Distance(route[14].Point, recording[0].Position) < 0.001f);
        for (var index = 0; index < legs.Length; index++)
        {
            var step = route[legs[index]];
            Assert(Vector3.Distance(step.Point, flights[index][^1].Position) < 0.001f);
            Equal(index == 2, step.Drops);
            Equal(index != 2, step.Jumps);
            Equal(index != 2, step.PreserveRunUp);
        }

        Near(1.934f, JumpPhysics.LaunchDistance(route[15].RunUpSeconds), 0.001f);
        Near(0.047f, JumpPhysics.LaunchDistance(route[20].RunUpSeconds), 0.001f);
        Assert(route[24].Point.Y > route[23].Point.Y); // Rejoins the existing roof ascent.
    }),
    ("The recorded drop is followed through contact without an upward arc", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var samples = KuganeBalconyRecording.Flights(KuganeBalconyRecording.Read())[2];
        var flight = new JumpFlight(samples[0].Position, route[17].Point, samples[0].Seconds);
        foreach (var sample in samples.Skip(1).SkipLast(1))
            Equal(FlightDecision.Flying, flight.Check(sample));
        Equal(FlightDecision.Touchdown, flight.Check(samples[^1]));
        Assert(JumpPosition.OnPoint(samples[^1].Position, route[17].Point, 0.01f));
    }),
    ("A following drop does not bypass alignment at its starting ledge", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        Assert(JumpPosition.NeedsLandingAlignment(route[16].Point + Vector3.UnitX * 0.25f,
            route[16].Point, true, route[17]));
    }),
    ("Kugane balcony miss does not enter ground-navigation recovery", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var actualContact = new Vector3(-45.14f, 26.60f, -79.51f);
        Assert(!JumpLanding.IsAtRouteGroundLevel(actualContact, route[0].Point));
        Assert(JumpLanding.IsAtRouteGroundLevel(new(-45.2f, 14.4f, -34.7f), route[0].Point));
        Assert(!JumpLanding.IsAtRouteGroundLevel(new(-45.2f, 5f, -34.7f), route[0].Point));
    }),
    ("Kugane second jump: stationary landing-animation frames do not poison the arc", () =>
    {
        Assert(JumpArc.TryFit(KuganeLaunchRegression.SecondJump, 15.48f, out var arc));
        Near(10.35f, arc.VerticalSpeed, 0.2f);
        Near(30f, arc.Gravity, 1f);
        Near(4.6f, arc.HorizontalSpeed, 0.15f);
        Assert(arc.TakeoffSeconds is > 0.06 and < 0.10);
        Assert(arc.Error < 0.04f);
    }),
    ("Kugane high landing: the end of the animation is not part of the flight", () =>
    {
        Assert(JumpArc.TryFit(KuganeLaunchRegression.HighLanding, 19.21f, out var arc));
        Near(7.2f, arc.HorizontalSpeed, 0.15f);
        Near(30f, arc.Gravity, 1f);
    }),
    ("A grounded animation tail is excluded even when the character keeps walking", () =>
    {
        var flight = Arc(60);
        var contact = flight[^1];
        var tail = Enumerable.Range(1, 6).Select(index => new JumpSample(
            contact.Position + Vector3.UnitX * index * 0.05f, true, contact.Seconds + index / 60d));
        Assert(JumpArc.TryFit(flight.Concat(tail).ToArray(), 0f, out var arc));
        Near(10f, arc.VerticalSpeed, 0.01f);
        Near(20f, arc.Gravity, 0.01f);
        Near(4f, arc.HorizontalSpeed, 0.01f);
    }),
    ("Kugane overshoot: earlier arcs predict the excessive range including startup", () =>
    {
        var trajectory = KuganeTrajectory();
        Assert(trajectory.TryRange(0.159f, 0.2f, out var predicted));
        Assert(JumpArc.TryFit(KuganeLaunchRegression.Overshoot, 17.21f, out var measured));
        Assert(measured.TryFlightSeconds(0.2f, out var duration));
        var startup = Vector2.Distance(new(-39.36f, -37.82f), measured.TakeoffPosition);
        Assert(startup is > 0.25f and < 0.4f);
        var actualRange = startup + measured.HorizontalSpeed * duration;
        Near(actualRange, predicted, 0.15f);
        var remaining = Vector2.Distance(new(-39.36f, -37.82f), new(-36.19f, -38.90f));
        Assert(predicted > remaining + 0.3f);
    }),
    ("An apex landing tolerates recorded height rounding", () =>
    {
        Assert(new JumpArc(10.38f, 30f, 4f, 0f).TryFlightSeconds(1.8f, out _));
        Assert(!new JumpArc(10.38f, 30f, 4f, 0f).TryFlightSeconds(1.9f, out _));
    }),
    ("The launch model does not extrapolate far outside measured run-ups", () =>
    {
        Assert(!KuganeTrajectory().TryRange(1f, 0.2f, out _));
    }),
    ("Kugane leg 4: actual chained landings align despite an unavailable floor ray", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        foreach (var actual in new[] { new Vector3(-39.47f, 17.21f, -37.82f), new(-39.45f, 17.21f, -37.85f), new(-39.46f, 17.21f, -37.83f) })
        {
            Assert(JumpPosition.NeedsLandingAlignment(actual, route[3].Point, true, route[4]));
            Assert(JumpLanding.CanCorrect(actual, route[3].Point, true, JumpFloorPath.Unavailable));
            Assert(JumpLanding.RetainedFoothold(route, actual, 3));
            Equal(3, JumpPosition.NearestLeg(route, actual, false, 3));
        }
    }),
    ("Kugane leg 8: a wide supported landing gets a guarded correction", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var actual = new Vector3(-29.97f, 20.91f, -38.81f);
        Assert(JumpPosition.NeedsLandingAlignment(actual, route[7].Point, true, route[8]));
        Assert(JumpLanding.CanCorrect(actual, route[7].Point, true, JumpFloorPath.Unavailable));
        Assert(JumpLanding.RetainedFoothold(route, actual, 7));
    }),
    ("A visible gap still vetoes landing correction", () =>
    {
        Assert(!JumpLanding.CanCorrect(new(0.2f, 0f, 0f), Vector3.Zero, true, JumpFloorPath.Blocked));
        Assert(!JumpLanding.CanCorrect(new(0.2f, 0f, 0f), Vector3.Zero, false, JumpFloorPath.Unavailable));
        Assert(!JumpLanding.CanCorrect(new(1.2f, 0f, 0f), Vector3.Zero, true, JumpFloorPath.Unavailable));
        Assert(!JumpLanding.CanCorrect(new(0.2f, -1f, 0f), Vector3.Zero, true, JumpFloorPath.Unavailable));
    }),
    ("Guarded correction stops at the first falling frame", () =>
    {
        var guard = new JumpCorrectionGuard(Vector3.Zero, new(0.3f, 0.02f, 0f));
        Assert(!guard.LostFooting(Sample(0.1f, 0.01f, 0.1)));
        Assert(guard.LostFooting(Sample(0.11f, 0f, 0.11, true)));
        Assert(guard.LostFooting(Sample(0.11f, -0.09f, 0.11)));
    }),
    ("A lower roof is a real fall, not a retained foothold", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        Assert(!JumpLanding.RetainedFoothold(route, route[3].Point - Vector3.UnitY * 3f, 3));
        Assert(JumpLanding.RetainedFoothold(route, route[3].Point, 4));
    }),
    ("Kugane log: all three railing landings continue to the recorded walk", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        foreach (var landing in new[] { new Vector3(-40.75f, 15.50f, -36.17f), new(-40.77f, 15.50f, -36.18f), new(-40.75f, 15.49f, -36.18f) })
        {
            Assert(JumpPosition.OnPoint(landing, route[1].Point, 1f));
            Assert(!JumpPosition.NeedsLandingAlignment(landing, route[1].Point, true, route[2]));
        }
    }),
    ("Kugane log: flight and stable grounding accept the first railing", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        var flight = new JumpFlight(new(-42.13f, 14.03f, -36.19f), route[1].Point, 0d);
        foreach (var sample in KuganeRegression.FirstJump)
            Equal(FlightDecision.Flying, flight.Check(sample));
        var contact = new JumpSample(new(-40.75f, 15.49f, -36.18f), false, 0.7);
        Equal(FlightDecision.Touchdown, flight.Check(contact));
        var grounding = new JumpGrounding();
        Assert(!grounding.Check(contact));
        Assert(grounding.Check(contact with { Seconds = 0.9 }));
        Assert(!JumpPosition.NeedsLandingAlignment(contact.Position, route[1].Point, true, route[2]));
    }),
    ("Kugane log: the real arc fits after excluding grounded animation startup", () =>
    {
        Assert(JumpArc.TryFit(KuganeRegression.FirstJump, 14.03f, out var arc));
        Assert(arc.VerticalSpeed is > 8f and < 12f);
        Assert(arc.Gravity is > 20f and < 40f);
        Assert(arc.HorizontalSpeed is > 1.9f and < 2.3f);
        Assert(arc.Error < 0.05f);
    }),
    ("Chained jumps still require alignment to their recorded launch point", () =>
    {
        var target = new Vector3(-40.42f, 15.43f, -36.18f);
        var actual = new Vector3(-40.75f, 15.50f, -36.18f);
        Assert(JumpPosition.NeedsLandingAlignment(actual, target, true, JumpStep.Jump(-39f, 17f, -36f, 0.08f)));
        Assert(JumpPosition.NeedsLandingAlignment(actual, target, true, null));
    }),
    ("A walk after a jump does not excuse a wide or wrong-height landing", () =>
    {
        var next = JumpStep.Walk(1f, 0f, 0f);
        Assert(JumpPosition.NeedsLandingAlignment(new(0.8f, 0f, 0f), Vector3.Zero, true, next));
        Assert(JumpPosition.NeedsLandingAlignment(new(0.1f, -1f, 0f), Vector3.Zero, true, next));
    }),
    ("Primitive collision shapes can provide a floor without triangle vertices", () =>
    {
        Assert(JumpSurface.IsWalkable(Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.UnitY));
        Assert(!JumpSurface.IsWalkable(Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.UnitX));
        Assert(!JumpSurface.IsWalkable(Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero));
        Assert(JumpSurface.IsWalkable(Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, Vector3.Zero));
    }),
    ("Diagnostic sampling always includes takeoff and the final airborne frame", () =>
    {
        foreach (var count in new[] { 0, 1, 24, 26, 49, 80, 100, 144 })
        {
            var samples = Enumerable.Range(0, count).Select(index => Sample(index, 0f, index)).ToArray();
            var summary = JumpTrace.Summarize(samples).ToArray();
            Equal(Math.Min(25, count), summary.Length);
            if (count > 0)
            {
                Equal(samples[0], summary[0]);
                Equal(samples[^1], summary[^1]);
            }
        }
    }),
    ("Positive short run-ups remain moving jumps", () =>
    {
        foreach (var seconds in new[] { 0.01f, 0.02f, 0.05f, 0.08f })
            Assert(JumpPhysics.LaunchDistance(seconds) > 0f);
        Equal(0f, JumpPhysics.LaunchDistance(0f));
    }),
    ("No movement never satisfies the run-up watchdog", () =>
        Equal(RunUpDecision.TimedOut, RunUp().Check(Sample(0f, 0f, 2)))),
    ("Upward or lateral displacement does not count as forward run-up", () =>
    {
        Equal(RunUpDecision.Continue, RunUp().Check(Sample(0f, 0.1f, 0.1)));
        Equal(RunUpDecision.OffLine, RunUp().Check(new(new(0f, 0f, 0.2f), false, 0.1)));
    }),
    ("Excessive run-up never issues a jump", () =>
        Equal(RunUpDecision.Overshot, RunUp().Check(Sample(0.27f, 0f, 0.09)))),
    ("Walking off the source ledge aborts takeoff", () =>
    {
        Equal(RunUpDecision.Fell, RunUp().Check(Sample(0.15f, 0f, 0.1, true)));
        Equal(RunUpDecision.Fell, RunUp().Check(Sample(0.15f, -0.2f, 0.1)));
    }),
    ("Safe run-up room bounds even an uncalibrated attempt", () =>
        Equal(RunUpDecision.Overshot, RunUp().Check(Sample(0.15f, 0f, 0.1), safeMeters: 0.1f))),
    ("Observed run-up reaches its trigger", () =>
        Equal(RunUpDecision.Jump, RunUp().Check(Sample(0.15f, 0f, 0.1)))),
    ("Uncalibrated run-up chooses a closer earlier frame", () =>
        Equal(RunUpDecision.Jump, RunUp().Check(Sample(0.1f, 0f, 0.1), nextTravel: 0.2f))),
    ("A stopped short run-up does not launch early", () =>
        Equal(RunUpDecision.Continue, RunUp().Check(Sample(0.1f, 0f, 0.1)))),
    ("Cannot accelerate beyond available floor to reach a target", () =>
        Equal(RunUpDecision.NoLaunchWindow, RunUp().Check(Sample(0.15f, 0f, 0.1), 1f, 1.1f, 0.15f))),
    ("Jumping animation cannot hold the controller forever", () =>
    {
        var flight = Flight();
        flight.Check(Sample(0f, 0.2f, 0.1, true));
        Equal(FlightDecision.TimedOut, flight.Check(Sample(1f, 2f, 9, true)));
    }),
    ("Action acceptance cannot substitute for observed takeoff", () =>
    {
        var flight = Flight();
        Equal(FlightDecision.WaitingForTakeoff, flight.Check(Sample(3.5f, 1f, 0.1)));
        Equal(FlightDecision.NeverTookOff, flight.Check(Sample(3.5f, 1f, 0.8)));
    }),
    ("Passing above target remains an airborne jump", () =>
    {
        var flight = Flight();
        Equal(FlightDecision.Flying, flight.Check(Sample(0.1f, 0.2f, 0.1, true)));
        Equal(FlightDecision.Flying, flight.Check(Sample(3.5f, 2f, 0.3, true)));
        Assert(flight.Touchdown is null);
    }),
    ("First contact stops steering even off target", () =>
    {
        var flight = Flight();
        flight.Check(Sample(1f, 2f, 0.2, true));
        Equal(FlightDecision.Touchdown, flight.Check(Sample(3f, 1f, 0.7)));
        Equal(new Vector3(3f, 1f, 0f), flight.Touchdown!.Value);
    }),
    ("A descending miss stops before reaching the lower roof", () =>
    {
        var flight = Flight();
        flight.Check(Sample(1f, 2f, 0.2, true));
        Equal(FlightDecision.Missed, flight.Check(Sample(4f, 0.3f, 0.8, true)));
    }),
    ("Grounding rejects brief flag gaps and continued sliding", () =>
    {
        var grounding = new JumpGrounding();
        Assert(!grounding.Check(Sample(0f, 0f, 0)));
        Assert(!grounding.Check(Sample(0f, 0f, 0.1)));
        Assert(!grounding.Check(Sample(0f, 0f, 0.12, true)));
        Assert(!grounding.Check(Sample(0f, 0f, 0.2)));
        Assert(!grounding.Check(Sample(0.1f, 0f, 0.3)));
        Assert(grounding.Check(Sample(0.1f, 0f, 0.5)));
    }),
    ("Higher platforms and lower awnings are not successful landings", () =>
    {
        Assert(!JumpPosition.OnPoint(new(0f, 4f, 0f), Vector3.Zero, 1f));
        Assert(!JumpPosition.OnPoint(new(0f, -5.5f, 0f), Vector3.Zero, 1f));
        Assert(JumpPosition.OnPoint(new(0.1f, 0.05f, 0f), Vector3.Zero, 0.12f));
    }),
    ("Recovery cannot skip to an unvisited point", () =>
    {
        var route = new JumpRoute([JumpStep.Walk(0f, 0f, 0f), JumpStep.Jump(0.25f, 0f, 0f, 0f)], true);
        Equal(0, JumpPosition.NearestLeg(route, new(0.25f, 0f, 0f), false, 0));
        Equal(-1, JumpPosition.NearestLeg(route, Vector3.Zero, true, 1));
        Equal(-1, JumpPosition.NearestLeg(route, new(1.5f, 0f, 0f), false, 1));
    }),
    ("Fits reject insufficient samples and wall collisions", () =>
    {
        Assert(!JumpArc.TryFit(Arc(60).Take(3).ToArray(), 0f, out _));
        var collision = Arc(60).Select(sample => sample with { Position = sample.Position with { X = Math.Min(sample.Position.X, 0.3f) } }).ToArray();
        Assert(!JumpArc.TryFit(collision, 0f, out _));
    }),
    ("Impossible height has no flight time", () =>
        Assert(!new JumpArc(10f, 20f, 4f, 0f).TryFlightSeconds(3f, out _))),
    ("No unobserved physics are assumed", () =>
        Assert(!new JumpTrajectory().TryRange(4f, 1f, out _))),
    ("One observation cannot determine the running launch model", () =>
    {
        var trajectory = new JumpTrajectory();
        Assert(trajectory.Observe(Arc(60), Sample(0f, 0f, 0d), 0f, out _));
        Equal(1, trajectory.Count);
        Assert(!trajectory.TryRange(0.1f, 1f, out _));
    }),
    ("Calibration resists a single outlier", () =>
    {
        var trajectory = new JumpTrajectory();
        Assert(trajectory.Observe(Arc(60, 2f), Sample(0f, 0f, 0d), 0.1f, out _));
        for (var index = 0; index < 4; index++)
            Assert(trajectory.Observe(Arc(60), Sample(0f, 0f, 0d), 0.4f, out _));
        var faster = Arc(60).Select(sample => sample with { Position = sample.Position with { X = sample.Position.X * 2f } }).ToArray();
        Assert(trajectory.Observe(faster, Sample(0f, 0f, 0d), 0.4f, out _));
        Assert(trajectory.TryRange(0.4f, 0f, out var range));
        Near(4f, range, 0.01f);
    }),
    ("Tower route still reaches the summit", () =>
    {
        Assert(JumpRoutes.TryGet(168, out var route));
        Equal(108, route.Length);
        Assert(route[^1].Point.Y > 107f);
    }),
};

foreach (var fps in new[] { 20, 30, 60, 144 })
{
    tests.Add(($"Ballistic fit at {fps} FPS tolerates delayed takeoff notification", () =>
    {
        var delayed = Arc(fps).Where(sample => sample.Seconds >= 0.08).ToArray();
        Assert(JumpArc.TryFit(delayed, 0f, out var arc));
        Near(10f, arc.VerticalSpeed, 0.01f);
        Near(20f, arc.Gravity, 0.01f);
        Near(4f, arc.HorizontalSpeed, 0.01f);
        Assert(arc.TryFlightSeconds(1.8f, out var time));
        Near(1.8f, 10f * time - 10f * time * time, 0.001f);
    }));
    tests.Add(($"Adaptive launch reaches a rising 3.5m target at {fps} FPS", () =>
    {
        var trajectory = new JumpTrajectory();
        Assert(trajectory.Observe(Arc(fps, 2f), Sample(0f, 0f, 0d), 0.1f, out _));
        Assert(trajectory.Observe(Arc(fps, 5f), Sample(0f, 0f, 0d), 0.625f, out _));
        var start = Sample(0f, 0f, 0);
        var velocity = new JumpVelocity(start);
        var runUp = new JumpRunUp(Vector3.Zero, new(3.5f, 1.8f, 0f), 0.148f, 0d);
        for (var frame = 1; frame < fps; frame++)
        {
            var t = frame / (double)fps;
            var position = (float)(10d * t * t); // 20 m/s2 acceleration on the source platform
            var sample = Sample(position, 0f, t);
            velocity.Add(sample);
            var nextTravel = (velocity.Speed + velocity.NextSpeed) * 0.5f / fps;
            Assert(trajectory.TryRange(position, 1.8f, out var range));
            Assert(trajectory.TryRange(position + nextTravel, 1.8f, out var next));
            var decision = runUp.Check(sample, range, next + nextTravel, 0.75f);
            if (decision == RunUpDecision.Continue)
                continue;
            Equal(RunUpDecision.Jump, decision);
            Near(3.5f, position + range, 0.25f);
            return;
        }
        throw new Exception("Never selected a launch frame");
    }));
}

foreach (var fps in new[] { 60, 100, 144 })
{
    tests.Add(($"First roof overshoot selects a shorter run-up at {fps} FPS", () =>
    {
        var model = new JumpLaunchModel(KuganeRoofRegression.Prior);
        var from = new Vector3(-49.24f, 43.81f, -70.37f);
        var target = KuganeRoofRegression.Target;
        var direction = Vector3.Normalize(new Vector3(target.X - from.X, 0f, target.Z - from.Z));
        var runUp = new JumpRunUp(from, target, 0.370f, 0d);
        Assert(JumpArc.TryFit(KuganeRoofRegression.Miss, from.Y, out var heldOut));
        Assert(heldOut.TryFlightSeconds(target.Y - from.Y, out var seconds));
        for (var frame = 1; frame < fps; frame++)
        {
            var t = frame / (double)fps;
            var distance = (float)(14d * t * t);
            var nextDistance = (float)(14d * (t + 1d / fps) * (t + 1d / fps));
            Assert(model.TryPredict(distance, out var speed, out var startup));
            Assert(model.TryPredict(nextDistance, out var nextSpeed, out var nextStartup));
            var range = startup + speed * seconds;
            var decision = runUp.Check(new(from + direction * distance, false, t), range,
                nextStartup + nextSpeed * seconds + nextDistance - distance);
            if (decision == RunUpDecision.Continue)
                continue;
            Equal(RunUpDecision.Jump, decision);
            Assert(distance < 0.360f);
            Near(Vector2.Distance(new(from.X, from.Z), new(target.X, target.Z)), distance + range, 0.15f);
            return;
        }
        throw new Exception("Never selected a launch frame");
    }));
    tests.Add(($"Kugane overshoot: choose an earlier launch from real prior arcs at {fps} FPS", () =>
    {
        var trajectory = KuganeTrajectory();
        var from = new Vector3(-39.51f, 17.21f, -37.77f);
        var target = new Vector3(-36.19f, 17.41f, -38.90f);
        var direction = Vector3.Normalize(new Vector3(target.X - from.X, 0f, target.Z - from.Z));
        var runUp = new JumpRunUp(from, target, 0.148f, 0d);
        for (var frame = 1; frame < fps; frame++)
        {
            var t = frame / (double)fps;
            var distance = (float)(14d * t * t);
            var nextDistance = (float)(14d * (t + 1d / fps) * (t + 1d / fps));
            Assert(trajectory.TryRange(distance, target.Y - from.Y, out var range));
            Assert(trajectory.TryRange(nextDistance, target.Y - from.Y, out var next));
            var decision = runUp.Check(new(from + direction * distance, false, t), range,
                next + nextDistance - distance);
            if (decision == RunUpDecision.Continue)
                continue;
            Equal(RunUpDecision.Jump, decision);
            Assert(distance < 0.148f);
            Near(Vector2.Distance(new(from.X, from.Z), new(target.X, target.Z)), distance + range, 0.15f);
            return;
        }
        throw new Exception("Never selected a launch frame");
    }));
}

var failures = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL {name}: {exception.Message}"); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} tests passed");
return failures == 0 ? 0 : 1;

static JumpSample Sample(float x, float y, double seconds, bool airborne = false) => new(new(x, y, 0f), airborne, seconds);
static JumpTrajectory KuganeTrajectory()
{
    var trajectory = new JumpTrajectory();
    Assert(trajectory.Observe(KuganeRegression.FirstJump, new(new(-42.13f, 14.03f, -36.19f), false, 0), 0f, out _));
    Assert(trajectory.Observe(KuganeLaunchRegression.SecondJump, new(new(-40.68f, 15.48f, -36.05f), false, 0), 0.111f, out _));
    return trajectory;
}
static JumpRunUp RunUp() => new(Vector3.Zero, new(3.5f, 1f, 0f), 0.148f, 0d);
static JumpFlight Flight() => new(Vector3.Zero, new(3.5f, 1f, 0f), 0d);
static JumpSample[] Arc(int fps, float airSpeed = 4f) => Enumerable.Range(1, (int)(0.7 * fps)).Select(index =>
{
    var t = index / (double)fps;
    return Sample((float)(airSpeed * t), (float)(10d * t - 10d * t * t), t, true);
}).ToArray();
static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
static void Near(float expected, float actual, float tolerance) { if (MathF.Abs(expected - actual) > tolerance) throw new Exception($"Expected {expected} ± {tolerance}, got {actual}"); }
