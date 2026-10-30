using UnityEngine;

namespace NOVR.MotionControls;

// Geometry locations measured against the installed FS-20 cockpit, in its own
// local coordinates. Contains no game meshes or textures.
internal static class Fs20ControlMap
{
    internal enum Motion { Button, Toggle, Lever, Knob }
    internal sealed class Control
    {
        internal readonly string Id, Label, Action;
        internal readonly Vector3 Point;
        internal readonly Bounds Region;
        internal readonly Motion Animation;
        internal bool Contains(Vector3 a, Vector3 b, Vector3 c) =>
            Region.Contains(a) && Region.Contains(b) && Region.Contains(c) ||
            Id == "gear" && GearStem.Contains(a) && GearStem.Contains(b) && GearStem.Contains(c);
        internal Control(string id, string label, string action, Vector3 point, Vector3 min, Vector3 max, Motion animation)
        { Id = id; Label = label; Action = action; Point = point; Region = new Bounds((min + max) * .5f, max - min + Vector3.one * .00004f); Animation = animation; }
    }
    // The white stem is a second disconnected island; move it with the black
    // wheel so the lever remains assembled throughout its travel.
    private static readonly Bounds GearStem = new(new Vector3(-.2300342f,.2644597f,.5233003f),new Vector3(.0250161f,.0490085f,.0560129f));
    internal static bool Matches(string? definitionKey) => definitionKey == "SmallFighter1";
    internal static readonly Control[] Controls =
    {
        new("gear", "GEAR", "Gear", new(-0.2207417f, 0.2816734f, 0.4874827f), new(-0.2393576f, 0.2587425f, 0.4657988f), new(-0.2207277f, 0.3065163f, 0.5135652f), Motion.Lever),
        new("weapon", "WEAPON", "Next Weapon", new(-0.2898183f, 0.3801307f, 0.5279783f), new(-0.318136f, 0.3516785f, 0.5231382f), new(-0.2696738f, 0.40009f, 0.5563865f), Motion.Knob),
        new("engine", "ENGINE / APU", "Toggle Engine", new(0.311419f, 0.1954618f, 0.4981515f), new(0.3065716f, 0.1807032f, 0.4861199f), new(0.3207698f, 0.207458f, 0.5203862f), Motion.Toggle),
        new("assist", "FLIGHT ASSIST", "Flight Assist", new(0.0276556f, 0.1054862f, 0.4819685f), new(0.0222204f, 0.0867895f, 0.4748238f), new(0.0364185f, 0.1206817f, 0.5024294f), Motion.Toggle),
        new("radar", "RADAR", "Radar", new(0.2629063f, 0.1948572f, 0.501945f), new(0.2598724f, 0.1806573f, 0.4861103f), new(0.2740706f, 0.2074121f, 0.5203766f), Motion.Toggle),
        new("hover", "HOVER ASSIST", "Auto Hover", new(0.0265452f, 0.0060604f, 0.469229f), new(0.0222201f, -0.0119385f, 0.4498006f), new(0.0364183f, 0.0148164f, 0.4840669f), Motion.Toggle),
        new("brake", "WHEEL BRAKE · HOLD", "", new(-0.3144226f, 0.1966423f, 0.4995006f), new(-0.3207698f, 0.1807032f, 0.4861198f), new(-0.3065716f, 0.207458f, 0.5203862f), Motion.Toggle),
        new("range_out", "TACTICAL RANGE +", "", new(-0.1768752f, 0.362999f, 0.5462908f), new(-0.1869849f, 0.3552357f, 0.545238f), new(-0.170828f, 0.3688063f, 0.5530308f), Motion.Button),
        new("range_in", "TACTICAL RANGE −", "", new(-0.1565346f, 0.3629989f, 0.5462911f), new(-0.1664955f, 0.3552345f, 0.545238f), new(-0.1503386f, 0.3688051f, 0.5530308f), Motion.Button),
        new("map", "OPEN MAP", "", new(-0.0937439f, 0.3629988f, 0.5462913f), new(-0.101687f, 0.3552307f, 0.5452375f), new(-0.0855301f, 0.3688013f, 0.5530304f), Motion.Button),
        new("lights", "NAV LIGHTS", "Nav Lights", new(0.0734033f, 0.3629987f, 0.5462915f), new(0.0649329f, 0.3552294f, 0.5452375f), new(0.0810897f, 0.3688f, 0.5530304f), Motion.Button),
        new("aots", "EOTS HUD", "", new(0.157419f, 0.3629988f, 0.5462911f), new(0.1503386f, 0.3552345f, 0.545238f), new(0.1664955f, 0.3688051f, 0.5530308f), Motion.Button),
        new("head", "EOTS HEAD AIM", "", new(0.1777596f, 0.362999f, 0.5462908f), new(0.170828f, 0.3552357f, 0.545238f), new(0.1869849f, 0.3688063f, 0.5530309f), Motion.Button),
        new("prev_weapon", "PREVIOUS WEAPON", "Previous Weapon", new(-0.1734217f, 0.2020698f, 0.5161858f), new(-0.184823f, 0.1943382f, 0.5151395f), new(-0.1620011f, 0.2079088f, 0.5229324f), Motion.Button),
        new("next_weapon", "NEXT WEAPON", "Next Weapon", new(-0.1477296f, 0.20207f, 0.5161855f), new(-0.1604684f, 0.1943368f, 0.515139f), new(-0.1376465f, 0.2079074f, 0.5229319f), Motion.Button),
        new("cm", "COUNTERMEASURE TYPE", "Next Countermeasure", new(-0.0770763f, 0.2020697f, 0.516186f), new(-0.0879117f, 0.1943325f, 0.5151386f), new(-0.0650898f, 0.2079031f, 0.5229315f), Motion.Button),
        new("link", "LINK GUNS", "Link Guns", new(-0.0532194f, 0.2020698f, 0.5161859f), new(-0.063557f, 0.1943311f, 0.5151381f), new(-0.0407352f, 0.2079017f, 0.522931f), Motion.Button),
        new("unlock", "UNLOCK LAST TARGET", "", new(0.0541369f, 0.2020699f, 0.5161858f), new(0.0407352f, 0.1943311f, 0.5151381f), new(0.063557f, 0.2079017f, 0.5229309f), Motion.Button),
        new("clear", "CLEAR TARGETS", "", new(0.0779939f, 0.2020698f, 0.5161859f), new(0.0650898f, 0.1943325f, 0.5151385f), new(0.0879117f, 0.2079031f, 0.5229314f), Motion.Button),
    };
}
