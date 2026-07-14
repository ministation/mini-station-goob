// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Builds grid silhouettes using the same algorithm as <c>BaseShuttleControl.DrawGrid</c>
/// (shuttle console / mass scanner radar).
/// </summary>
public static class TypanWarMinimapMesh
{
    public static void Build(
        EntityUid gridUid,
        MapGridComponent grid,
        SharedMapSystem maps,
        List<Vector2> vertices,
        out int edgeIndex,
        List<Vector2i> tileList,
        HashSet<Vector2i> tileSet,
        List<(Vector2 Start, Vector2 End)> edges,
        (DirectionFlag Dir, Vector2i Offset)[] neighborDirections)
    {
        vertices.Clear();
        tileList.Clear();
        tileSet.Clear();
        edges.Clear();

        var tileSize = grid.TileSize;
        var gridEnt = (gridUid, grid);
        var rator = maps.GetAllTilesEnumerator(gridUid, grid);

        while (rator.MoveNext(out var tileRef))
        {
            var index = tileRef.Value.GridIndices;
            tileSet.Add(index);
            tileList.Add(index);

            var bl = maps.TileToVector(gridEnt, index);
            var br = bl + new Vector2(tileSize, 0f);
            var tr = bl + new Vector2(tileSize, tileSize);
            var tl = bl + new Vector2(0f, tileSize);

            vertices.Add(bl);
            vertices.Add(br);
            vertices.Add(tl);

            vertices.Add(br);
            vertices.Add(tl);
            vertices.Add(tr);
        }

        edgeIndex = vertices.Count;

        foreach (var index in tileList)
        {
            foreach (var (dir, dirVec) in neighborDirections)
            {
                if (tileSet.Contains(index + dirVec))
                    continue;

                var bl = maps.TileToVector(gridEnt, index);
                var br = bl + new Vector2(tileSize, 0f);
                var tr = bl + new Vector2(tileSize, tileSize);
                var tl = bl + new Vector2(0f, tileSize);

                var (start, end) = dir switch
                {
                    DirectionFlag.South => (bl, br),
                    DirectionFlag.East => (br, tr),
                    DirectionFlag.North => (tr, tl),
                    DirectionFlag.West => (tl, bl),
                    _ => throw new NotImplementedException(),
                };

                edges.Add((start, end));
            }
        }

        // Merge collinear segments — same pass as BaseShuttleControl.
        var decomposed = true;
        while (decomposed)
        {
            decomposed = false;

            for (var i = 0; i < edges.Count; i++)
            {
                var (start, end) = edges[i];
                var neighborFound = false;
                var neighborIndex = 0;
                Vector2 neighborEnd = Vector2.Zero;

                for (var j = i + 1; j < edges.Count; j++)
                {
                    var (neighborStart, candidateEnd) = edges[j];
                    if (!end.Equals(neighborStart))
                        continue;

                    neighborFound = true;
                    neighborIndex = j;
                    neighborEnd = candidateEnd;
                    break;
                }

                if (!neighborFound)
                    continue;

                if (!CollinearSimplifier.IsCollinear(start, end, neighborEnd, 10f * float.Epsilon))
                    continue;

                decomposed = true;
                edges[i] = (start, neighborEnd);
                edges.RemoveAt(neighborIndex);
            }
        }

        vertices.EnsureCapacity(vertices.Count + edges.Count * 2);
        foreach (var edge in edges)
        {
            vertices.Add(edge.Start);
            vertices.Add(edge.End);
        }
    }

    public static (DirectionFlag Dir, Vector2i Offset)[] CreateNeighborDirections()
    {
        var dirs = new (DirectionFlag, Vector2i)[4];
        for (var i = 0; i < 4; i++)
        {
            var dir = (DirectionFlag) Math.Pow(2, i);
            dirs[i] = (dir, dir.AsDir().ToIntVec());
        }

        return dirs;
    }
}
