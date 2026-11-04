using NOVR.MotionControls;
using UnityEngine;
using System.Text.Json;

internal static class CollectiveChecks
{
    internal static void Run(string[] args)
    {
        var checks = 0;
        void Check(bool okay, string message) { if (!okay) throw new InvalidOperationException(message); checks++; }
        void Near(Vector3 a, Vector3 b, string message, float tolerance = .000015f) => Check((a-b).magnitude < tolerance, message);
        Vector3 Mirror(Vector3 v) => new(-v.x,v.y,v.z);
        ArmPoseMath.Vector V(Vector3 v) => new(v.x,v.y,v.z);
        Vector3 U(ArmPoseMath.Vector v) => new(v.X,v.Y,v.Z);
        float[] Q(Quaternion q) => [q.x,q.y,q.z,q.w];
        float[] P(Vector3 v) => [v.x,v.y,v.z];
        var exportIndex=Array.IndexOf(args,"--export-collective");
        var export=exportIndex>=0 && exportIndex+1<args.Length ? args[exportIndex+1] : null;
        if(export!=null) Directory.CreateDirectory(export);
        Time.unscaledDeltaTime=1f/90; Time.unscaledTime=0;
        MotionControlVisuals.HasFittedCollective=true;
        var tracking=new GameObject("collective checks").transform;
        using var left=new RiggedGlove(true,tracking,new Material(),default);
        using var right=new RiggedGlove(false,tracking,new Material(),default);
        var grip=new Pose(Vector3.zero,Quaternion.identity);
        var neutralCuff=CollectiveHandPoses.Rotation(true)*new Vector3(0,0,-1);
        Check(neutralCuff.y>.97f && neutralCuff.x>0 && neutralCuff.x<.25f,"Collective cuff must point upward, with a modest inboard lean");
        Check(CollectiveHandPoses.Wrist(true).x<-.03f,"Left supporting palm moved to the wrong side of the collective");
        Quaternion[]? rest=null;
        foreach(var mode in new[]{"rest","thumb-touch","thumb-press","trigger","both"})
        {
            var sample=new ControllerSample { Valid=true,Rotation=Quaternion.identity,
                ThumbstickTouched=mode=="thumb-touch",Primary=mode is "thumb-press" or "both",
                Trigger=mode is "trigger" or "both" ? 1 : 0 };
            var rawTip=left.FreeTouchPoint(sample,true);
            for(var i=0;i<90;i++)
            { Time.unscaledTime+=Time.unscaledDeltaTime;left.Pose(sample,grip,false,true);right.Pose(sample,grip,false,true); }
            Near(left.FreeTouchPoint(sample,true),rawTip,"Collective visual posing changed raw controller touchpoint");
            Near(left.Root.position,Mirror(right.Root.position),"Collective wrists are not mirrored");
            for(var i=0;i<left.Renderer.bones.Length;i++)
            {
                var l=left.Renderer.bones[i];var r=right.Renderer.bones[i];
                Near(l.position,Mirror(r.position),"Collective finger position is not an exact mirrored fit");
                Near(l.rotation*Vector3.up,Mirror(r.rotation*Vector3.up),"Collective joint orientation differs between hands",.0001f);
                if(rest!=null && i!=4 && (i<7||i>9))
                    Check(Quaternion.Angle(rest[i],l.localRotation)<.08f,"Collective button motion displaced a supporting palm/finger joint");
            }
            if(mode=="rest")rest=left.Renderer.bones.Select(b=>b.localRotation).ToArray();
            else if(mode=="thumb-press") Check(Quaternion.Angle(rest![4],left.Renderer.bones[4].localRotation)>.3f,"Thumb press did not articulate the upper joint");
            else if(mode=="trigger")for(var i=7;i<=9;i++)Check(Quaternion.Angle(rest![i],left.Renderer.bones[i].localRotation)>.1f,"Collective trigger did not articulate the index finger");
            if(export!=null)
                File.WriteAllText(Path.Combine(export,"runtime-collective-"+mode+".json"),JsonSerializer.Serialize(new {
                    side="left",rotation=new float[]{0,0,0},translation=P(left.Root.position),rootQuaternion=Q(left.Root.rotation),
                    rotations=left.Renderer.bones.Select(b=>Q(b.localRotation)).ToArray(),curl=new Dictionary<string,float>()
                },new JsonSerializerOptions{WriteIndented=true}));
        }
        // The new model has a real hinge. Moving the handle must preserve that
        // pivot while retaining the user's configured exact vertical travel.
        foreach(var travel in new[]{.05f,.12f,.25f,.6f})
        {
            var neutral=new Vector3(0,FlightControlMath.CollectiveGripHeight,FlightControlMath.CollectiveNeutralForward(travel));
            var solverLeft=new ArmPoseMath();var solverRight=new ArmPoseMath();
            Vector3 previousBend=default;
            for(var step=0;step<=100;step++)
            {
                var command=step/100f;
                var (height,forward)=FlightControlMath.ThrottleOffset(true,command,travel);
                var offset=new Vector3(0,height,forward);
                var pitch=-(MathF.Atan2(neutral.y+height,neutral.z+forward)-MathF.Atan2(neutral.y,neutral.z))*180/MathF.PI;
                var turn=Quaternion.AngleAxis(pitch,Vector3.right);
                Near(offset-turn*neutral,neutral*-1,"Collective model hinge drifts during lift");
                Check(MathF.Abs(height-(command-.5f)*travel)<.00001f,"Collective model refit changed configured input travel");
                var wrist=offset+turn*CollectiveHandPoses.Wrist(true);
                var cuff=turn*CollectiveHandPoses.Rotation(true)*new Vector3(0,0,-1);
                Check(cuff.y>.70f,"Collective cuff turns downward within configured travel");
                if(travel==.12f)Check(cuff.y>.94f,"Normal collective travel lost the upright wrist");
                // Representative seated shoulder: independent of authored pose
                // constants, mirrored before the solver and checked afterward.
                var shoulder=new Vector3(.14f,.37f,-.18f);
                var direction=(wrist-shoulder).normalized;
                var bend=solverLeft.Bend(true,V(direction),V(cuff),true);
                var reflected=solverRight.Bend(false,V(Mirror(direction)),V(Mirror(cuff)),true);
                Check(bend.Finite && MathF.Abs(bend.Length-1)<.0001f,"Collective elbow has an invalid bend");
                Near(U(reflected),Mirror(U(bend)),"Collective mirrored arm pole diverges",.0001f);
                if(step>0)Check(Vector3.Dot(previousBend,U(bend))>.99f,"Collective elbow flips across adjacent lift samples");
                previousBend=U(bend);
            }
        }
        Console.WriteLine($"{checks} shared collective pose/mirror/hinge/input invariants passed; native mesh fitting is validated separately by private renders.");
    }
}
