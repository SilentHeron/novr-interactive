"""Blender: import pinned Valve slim gloves; retain skin weights, retarget reference poses.
Inputs are downloaded from ValveSoftware/steamvr_unity_plugin, revision in ThirdParty notice.
No Nuclear Option assets are read or written by this tool.
"""
import bpy, json, sys, pathlib, re
from mathutils import Vector, Matrix, Quaternion
src=pathlib.Path(sys.argv[sys.argv.index('--')+1]);out=pathlib.Path(sys.argv[-1]);out.mkdir(parents=True,exist_ok=True)
# FBX world -> Unity: fingers forward, back of hand up, left thumb right.
convert=Matrix(((0,0,1),(1,0,0),(0,-1,0)))
def vec(v):return [round(x,7) for x in v]
def pose(name,side):
 text=(src/('ReferencePose_'+name+'.asset')).read_text().split('  '+side+'Hand:')[1].split('  rightHand:')[0]
 ps=text.split('    bonePositions:')[1].split('    boneRotations:')[0]
 qs=text.split('    boneRotations:')[1]
 def values(t):return [dict((k,float(v)) for k,v in re.findall(r'([xyzw]): ([-+0-9.eE]+)',l)) for l in t.splitlines() if '    - {' in l][:31]
 p=values(ps);q=values(qs)
 return [Vector((v['x'],v['y'],v['z'])) for v in p],[Quaternion((v['w'],v['x'],v['y'],v['z'])) for v in q]
def basis(points):
 z=(points[12]-points[1]).normalized();x=(points[7]-points[17]);x=(x-z*x.dot(z)).normalized();y=z.cross(x).normalized();y=y if (points[15]-points[1]).dot(y)>0 else -y;return Matrix((x,y,z)).transposed()
for side in ['left','right']:
 convert=Matrix(((0,0,1),(1,0,0),(0,-1,0))) if side=='left' else Matrix(((0,0,-1),(-1,0,0),(0,-1,0)))
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(src/('vr_glove_'+side+'_model_slim.fbx')))
 arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');obj=next(o for o in bpy.data.objects if o.type=='MESH');arm.data.pose_position='REST';bpy.context.view_layer.update()
 bones=list(arm.data.bones);names=[b.name for b in bones];parents=[names.index(b.parent.name) if b.parent else -1 for b in bones]
 wrist=arm.matrix_world@bones[1].head_local
 points=[convert@((arm.matrix_world@b.head_local)-wrist) for b in bones];points[0]=Vector((0,0,0))
 bindp,bindq=pose('BindPose',side);refp=[];refr=[]
 for i in range(len(bones)):
  parent=parents[i];refp.append(bindp[i] if parent<0 else refp[parent]+refr[parent]@bindp[i]);refr.append(bindq[i] if parent<0 else refr[parent]@bindq[i])
 k=basis(points)@basis(refp).transposed();kq=k.to_quaternion()
 poses={}
 for label in ['OpenHand','Fist']:
  pp,qq=pose(label,side);local=[]
  for i in range(len(bones)):
   q=Quaternion() if i<2 else (k @ (refr[parents[i]]@qq[i]@refr[i].inverted()).to_matrix() @ k.transposed()).to_quaternion()
   local.append(vec((q.x,q.y,q.z,q.w)))
  poses[label]=local
 mesh=obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh();mesh.calc_loop_triangles()
 vertices=[];weights=[];indices=[];uvs=[];triangles=[];lookup={}
 # Split UV seams while preserving each source vertex's skin weights.
 for triangle in mesh.loop_triangles:
  for loop in reversed(list(triangle.loops)):
   vi=mesh.loops[loop].vertex_index;uv=mesh.uv_layers.active.data[loop].uv;key=(vi,round(uv.x,7),round(uv.y,7))
   if key not in lookup:
    lookup[key]=len(vertices)//3;v=mesh.vertices[vi];vertices.extend(vec(convert@((obj.matrix_world@v.co)-wrist)));uvs.extend(vec(uv))
    w=sorted([(names.index(obj.vertex_groups[g.group].name),g.weight) for g in obj.data.vertices[vi].groups if g.weight>0],key=lambda q:-q[1])[:4]
    total=sum(t[1] for t in w);w=[(i,x/total) for i,x in w]
    while len(w)<4:w.append((0,0))
    weights.extend([round(x,7) for _,x in w]);indices.extend([i for i,_ in w])
   triangles.append(lookup[key])
 result=dict(source='Valve SteamVR slim glove',side=side,vertices=vertices,uv=uvs,triangles=triangles,weights=weights,indices=indices,bones=[dict(name=b.name,parent=parents[i],position=vec(points[i]-(points[parents[i]] if parents[i]>=0 else Vector()))) for i,b in enumerate(bones)],open=poses['OpenHand'],fist=poses['Fist'])
 (out/(side+'.json')).write_text(json.dumps(result,separators=(',',':')))
 print(side,'vertices',len(vertices)//3,'triangles',len(triangles)//3,'bones',len(bones))
 # Save the original rigged model as an editable, attribution-bearing reference.
 bpy.ops.wm.save_as_mainfile(filepath=str(out/(side+'.blend')))
