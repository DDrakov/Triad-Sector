using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Shuttles.Save;
using Content.Tests;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Maths;
using Robust.Shared.IoC;
using Robust.Shared.Network; // Needed for NetUserId

namespace Content.IntegrationTests.Tests.Shuttle;

/// <summary>
/// Regression test: ensure the refactored ShipSerializationSystem actually serializes entities
/// (previously only tiles were saved due to incorrect YAML parsing).
/// </summary>
public sealed class ShipSerializationTest : ContentUnitTest
{
    [Test]
    public async Task RefactoredSerializer_SerializesEntities()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var map = await pair.CreateTestMap();

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var shipSer = entManager.System<ShipSerializationSystem>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var mapSys = entManager.System<SharedMapSystem>();

        // --- Setup ---
        cfg.SetCVar(CCVars.ShipyardUseLegacySerializer, false);

        entManager.DeleteEntity(map.Grid);
        var gridEnt = mapManager.CreateGridEntity(map.MapId);
        var gridUid = gridEnt.Owner;
        var gridComp = gridEnt.Comp;

        mapSys.SetTile(gridUid, gridComp, Vector2i.Zero, new Tile(1));

        var ent1 = entManager.SpawnEntity("AirlockShuttle",
            new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
        var ent2 = entManager.SpawnEntity("ChairOffice",
            new EntityCoordinates(gridUid, new Vector2(1.5f, 0.5f)));

        await server.WaitIdleAsync();

        // --- Assertions (single pass) ---
        Assert.That(entManager.EntityExists(ent1));
        Assert.That(entManager.EntityExists(ent2));

        var data = shipSer.SerializeShip(gridUid, new NetUserId(Guid.NewGuid()), "TestShip");

        Assert.That(data.Grids.Count, Is.EqualTo(1));
        var g = data.Grids[0];

        Assert.That(g.Tiles.Count, Is.EqualTo(1));
        Assert.That(g.Entities.Count >= 2);

        var foundAirlock = false;
        var foundChair = false;

        foreach (var e in g.Entities)
        {
            if (e.Prototype == "AirlockShuttle") foundAirlock = true;
            if (e.Prototype == "ChairOffice") foundChair = true;
        }

        Assert.That(foundAirlock, "Missing AirlockShuttle");
        Assert.That(foundChair, "Missing ChairOffice");

        await pair.CleanReturnAsync();
    }
}
