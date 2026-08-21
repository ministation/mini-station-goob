using System.Linq;
using Content.Server.Medical.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared._Trauma.Genetics.Console;

namespace Content.Server._Trauma.Genetics.Console;

public sealed class GeneticsScannerLinkSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneticsScannerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GeneticsScannerComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<GeneticsScannerComponent, PortDisconnectedEvent>(OnPortDisconnected);
    }

    private void OnMapInit(Entity<GeneticsScannerComponent> ent, ref MapInitEvent args)
    {
        TryBindScanner(ent);
    }

    private void OnNewLink(Entity<GeneticsScannerComponent> ent, ref NewLinkEvent args)
    {
        if (!TryComp<MedicalScannerComponent>(args.Sink, out var scanner))
            return;

        scanner.ConnectedConsole = ent;
        RaiseConnected(ent, args.Sink, scanner);
    }

    private void OnPortDisconnected(Entity<GeneticsScannerComponent> ent, ref PortDisconnectedEvent args)
    {
        var ev = new ScannerDisconnectedEvent(ent.Comp.Scanner ?? ent);
        RaiseLocalEvent(ent, ref ev);
    }

    private void TryBindScanner(Entity<GeneticsScannerComponent> ent)
    {
        if (ent.Comp.Scanner != null && HasComp<MedicalScannerComponent>(ent.Comp.Scanner))
        {
            RaiseConnected(ent, ent.Comp.Scanner.Value, Comp<MedicalScannerComponent>(ent.Comp.Scanner.Value));
            return;
        }

        if (TryComp<DeviceLinkSourceComponent>(ent, out var source))
        {
            foreach (var port in source.Outputs.Values.SelectMany(ports => ports))
            {
                if (!TryComp<MedicalScannerComponent>(port, out var linked))
                    continue;

                linked.ConnectedConsole = ent;
                RaiseConnected(ent, port, linked);
                return;
            }
        }

        var consoleXform = Transform(ent);
        EntityUid? nearest = null;
        var nearestDist = 4f;
        var query = EntityQueryEnumerator<MedicalScannerComponent, TransformComponent>();
        while (query.MoveNext(out var scannerUid, out var scanner, out var scannerXform))
        {
            if (!consoleXform.Coordinates.TryDistance(EntityManager, scannerXform.Coordinates, out var dist))
                continue;

            if (dist > nearestDist)
                continue;

            nearestDist = dist;
            nearest = scannerUid;
        }

        if (nearest == null || !TryComp<MedicalScannerComponent>(nearest, out var nearestScanner))
            return;

        nearestScanner.ConnectedConsole = ent;
        RaiseConnected(ent, nearest.Value, nearestScanner);
    }

    private void RaiseConnected(EntityUid console, EntityUid scanner, MedicalScannerComponent scannerComp)
    {
        var connected = new ScannerConnectedEvent(scanner);
        RaiseLocalEvent(console, ref connected);

        if (scannerComp.BodyContainer.ContainedEntity is { } body)
        {
            var inserted = new ScannerInsertedEvent(scanner, body);
            RaiseLocalEvent(console, ref inserted);
        }
    }
}
