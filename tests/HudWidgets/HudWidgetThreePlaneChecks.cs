using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class HudWidgetThreePlaneChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var definitions = HudWidgetCatalog.All;
        var planes = definitions.GroupBy(d => d.DefaultPose.Euler.y).ToArray();
        check(planes.Length == 3, "Unsaved layouts use exactly three planes");
        var projections = new List<(string id, Vector2[] corners)>();
        foreach (var plane in planes)
        {
            var first = plane.First().DefaultPose;
            var normal = Quaternion.Euler(first.Euler) * new Vector3(0, 0, 1);
            foreach (var definition in plane)
            {
                var pose = definition.DefaultPose;
                var offset = pose.Position - first.Position;
                check(Math.Abs(offset.x * normal.x + offset.y * normal.y + offset.z * normal.z) < .00001f,
                    "Each group is coplanar: " + definition.Id);
                check(pose.Euler.x == first.Euler.x && pose.Euler.z == 0,
                    "Panel rows share tilt and do not introduce roll: " + definition.Id);
                check(Math.Sign(pose.Position.x) == Math.Sign(plane.Key), "Each panel belongs to its left/center/right group");
                var half = definition.Size * (HudWidgetCatalog.MetresPerPixel * pose.Scale * .5f);
                var corners = new List<Vector2>();
                foreach (var sign in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                {
                    var point = pose.Position + Quaternion.Euler(pose.Euler) * new Vector3(sign.Item1 * half.x, sign.Item2 * half.y, 0);
                    check(point.y <= -.079f, "The entire panel stays below the seated horizon: " + definition.Id);
                    check(point.z > .1f, "No panel corner crosses behind or into the seated head: " + definition.Id);
                    corners.Add(new Vector2(point.x / point.z, point.y / point.z));
                }
                projections.Add((definition.Id, corners.ToArray()));
            }
        }
        for (var i = 0; i < projections.Count; i++)
            for (var j = i + 1; j < projections.Count; j++)
                check(Separated(projections[i].corners, projections[j].corners),
                    "Default panels do not overlap in the seated view: " + projections[i].id + " / " + projections[j].id);

        var groups = new[]
        {
            new[] { "threats", "ew", "countermeasures" },
            new[] { "objectives", "weapons", "auxiliary", "power" },
            new[] { "feed", "map", "damage" }
        };
        foreach (var group in groups)
            for (var i = 1; i < group.Length; i++)
                check(HudWidgetCatalog.Get(group[i - 1]).DefaultPose.Position.y > HudWidgetCatalog.Get(group[i]).DefaultPose.Position.y,
                    "Related panels stay in a clear top-to-bottom reading order");
    }

    // Perspective-projected rectangles are convex quadrilaterals. An edge
    // normal from either polygon separates every nonoverlapping pair.
    private static bool Separated(Vector2[] a, Vector2[] b)
    {
        foreach (var polygon in new[] { a, b })
            for (var i = 0; i < polygon.Length; i++)
            {
                var v = polygon[i];
                var w = polygon[(i + 1) % polygon.Length];
                var nx = v.y - w.y;
                var ny = w.x - v.x;
                var aa = a.Select(p => p.x * nx + p.y * ny).ToArray();
                var bb = b.Select(p => p.x * nx + p.y * ny).ToArray();
                if (aa.Max() <= bb.Min() || bb.Max() <= aa.Min())
                    return true;
            }
        return false;
    }
}
