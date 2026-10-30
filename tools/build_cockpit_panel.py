"""Author the shared NOVR console; export the same meshes to Unity JSON, GLB and a preview.
Run with Blender --background --python tools/build_cockpit_panel.py -- OUTPUT_DIR.
No game assets or third-party models are used.
"""
import bpy, json, math, sys
from pathlib import Path
from mathutils import Vector
out = Path(sys.argv[sys.argv.index('--') + 1]); out.mkdir(parents=True, exist_ok=True)
repo = Path(__file__).resolve().parents[1]
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
colors = {'case':(.035,.045,.052,1),'face':(.085,.10,.105,1),'edge':(.15,.18,.19,1),
          'black':(.008,.012,.014,1),'silver':(.42,.46,.47,1),'white':(.78,.86,.80,1),
          'green':(.30,.90,.62,1),'amber':(1,.64,.06,1),'red':(.60,.025,.018,1)}
mats={}
for name,color in colors.items():
    m=bpy.data.materials.new(name); m.diffuse_color=color; m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=color
    bs.inputs['Roughness'].default_value=.53; bs.inputs['Metallic'].default_value=.25 if name in ('silver','edge') else .05
    mats[name]=m
# Blender Z is screen-up; -Y faces the viewer. Export Unity (x,z,y).
def v(p): return (p[0],p[2],p[1])
objects=[]
def finish(obj,name,mat,group='static'):
    obj.name=name; obj.data.materials.append(mats[mat]); obj['panel_group']=group; objects.append(obj)
    return obj
def box(name,pos,size,mat,bevel=.002,group='static'):
    bpy.ops.mesh.primitive_cube_add(size=1,location=v(pos)); obj=bpy.context.object
    obj.dimensions=v(size); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=obj.modifiers.new('machined edges','BEVEL');mod.width=bevel;mod.segments=2
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj,name,mat,group)
def cylinder(name,pos,radius,depth,mat,axis='z',group='static'):
    bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=radius,depth=depth,location=v(pos))
    obj=bpy.context.object
    obj.rotation_euler=(math.pi/2,0,0) if axis=='z' else ((0,math.pi/2,0) if axis=='x' else (0,0,0))
    return finish(obj,name,mat,group)
def label(text,pos,size=.009,mat='white',group='static'):
    bpy.ops.object.text_add(location=v(pos),rotation=(math.pi/2,0,0));obj=bpy.context.object
    obj.data.body=text;obj.data.align_x='CENTER';obj.data.align_y='CENTER';obj.data.size=size;obj.data.extrude=.00012
    bpy.ops.object.convert(target='MESH');return finish(obj,'Label '+text,mat,group)
box('Chassis',(0,0,.012),(.77,.36,.055),'case',.014)
# A recessed gasket, machined perimeter rails and inset service sections give
# the same lightweight instrument a coherent fabricated construction.
box('Face gasket',(0,0,-.015),(.762,.352,.008),'black',.011)
box('Faceplate' ,(0,0,-.019),(.756,.346,.012),'face',.009)
for x in (-.355,.355):
    for y in (-.15,.15):
        cylinder('Captive screw',(x,y,-.029),.005,.003,'silver')
        box('Screw slot',(x,y,-.031),(.006,.001,.001),'black',0)
for x in (-.376,.376):
    box('Side rail',(x,0,-.012),(.012,.313,.026),'edge',.005)
for y in (-.171,.171):
    box('Edge rail',(0,y,-.013),(.715,.008,.023),'edge',.003)
for x in (-.315,.315):
    box('Control cassette seam',(x,-.005,-.0255),(.102,.286,.003),'black',.006)
    box('Control cassette',(x,-.005,-.0275),(.098,.282,.003),'face',.005)
box('Switch bank seam',(-.065,-.132,-.026),(.395,.074,.003),'black',.004)
box('Switch bank plate',(-.065,-.132,-.028),(.390,.070,.003),'face',.003)
for x in (-.270,.270):
    for y in (-.152,.150):
        cylinder('Bezel fastener',(x,y,-.032),.0032,.002,'silver')
        box('Fastener slot',(x,y,-.0332),(.0038,.0006,.0006),'black',0)
label('FLIGHT SYSTEMS' ,(0,.154,-.028),.010)
for x,name in ((-.13,'LEFT MFD'),(.13,'RIGHT MFD')):
    box(name+' gasket',(x,.025,-.028),(.254,.244,.010),'edge',.009)
    box(name+' bezel' ,(x,.025,-.032),(.248,.238,.018),'black',.008)
    box(name+' screen',(x,.025,-.043),(.226,.211,.002),'black',.001)
    label(name,(x,.139,-.045),.008,'green')
for x,side in ((-.13,'left'),(.13,'right')):
    for dx,title,direction in ((-.055,'<','prev'),(.055,'>','next')):
        group='mfd_'+side+'_'+direction
        box(group,(x+dx,-.089,-.046),(.030,.014,.008),'edge',.002,group)
        label(title,(x+dx,-.089,-.051),.011,'white',group)
for x,side in ((-.13,'left'),(.13,'right')):
    box('Left stick slave key',(x,-.089,-.046),(.041,.014,.008),'edge',.002,'mfd_'+side+'_slave')
# Labeled, functional bezel keys; no decorative dead buttons.
for x,y,title,group in ((-.249,.017,'CM','cm'),(-.249,-.060,'HOV','hover'),(.249,.066,'+','zoom_in'),(.249,-.014,'-','zoom_out')):
    box(title+' bezel key',(x,y,-.046),(.018,.025,.008),'edge',.002,group)
    label(title,(x,y,-.051),.0065 if group=='hover' else .008 if group=='cm' else .015,'white',group)
cylinder('Hover indicator',(-.249,-.037,-.048),.0035,.003,'green',group='hover_lamp')
label('GEAR',(-.315,.123,-.031),.012)
box('Gear gate',(-.315,.015,-.034),(.040,.14,.009),'black',.005)
label('UP',(-.315,.096,-.041),.008)
label('DN',(-.315,-.068,-.041),.008)
for x in (-.337,-.293):
    box('Gear gate edge',(x,.015,-.039),(.004,.133,.008),'silver',.001)
cylinder('Gear pivot' ,(-.315,.015,-.042),.014,.008,'silver')
# Dynamic groups have pivots below; coordinates here remain in panel space.
cylinder('Gear shaft',(-.315,.015,-.072),.005,.055,'silver',group='gear')
cylinder('Wheel grip',(-.315,.015,-.101),.018,.027,'black',axis='x',group='gear')
cylinder('Wheel hub',(-.330,.015,-.101),.008,.002,'silver',axis='x',group='gear')
for i in range(12):
    theta=i*math.tau/12
    ridge=box('Wheel grip rib',(-.315,.015+math.sin(theta)*.0175,-.101+math.cos(theta)*.0175),(.028,.003,.004),'edge',.001,'gear')
    ridge.rotation_euler.x=-theta
for x in (-.333,-.315,-.297): cylinder('Gear indicator',(x,-.103,-.035),.004,.003,'green',group='gear_lamps')
label('WHEELS',(-.315,-.124,-.035),.006)
for x,title,group in ((-.19,'RADAR','radar'),(-.065,'ENGINE','engine'),(.065,'ASSIST','assist')):
    label(title,(x,-.105,-.033),.009)
    cylinder(title+' boot',(x,-.139,-.034),.015,.007,'black')
    cylinder(title+' switch mount' ,(x,-.139,-.035),.012,.006,'silver')
    cylinder(title+' toggle',(x,-.139,-.054),.004,.035,'silver',group=group)
    box(title+' tip',(x,-.139,-.074),(.014,.018,.012),'white',.003,group)
box('Next weapon key',(.185,-.137,-.039),(.078,.037,.016),'edge',.004,'weapon')
label('NEXT WPN',(.185,-.137,-.049),.007,group='weapon')
label('ESCAPE',(.315,.123,-.032),.011,'amber')
box('Escape recess',(.315,.025,-.036),(.083,.13,.010),'black',.005)
# A yellow-black U pull handle, protected by a hinged red cover.
for x in (.291,.339):
    box('Pull handle side',(x,.022,-.059),(.009,.061,.009),'amber',.004,'eject')
box('Pull handle bar',(.315,-.009,-.059),(.057,.010,.010),'amber',.004,'eject')
for x in (.299,.318,.336): box('Handle stripe',(x,-.009,-.065),(.007,.010,.002),'black',0,'eject')
# Raised rim, hinge knuckles and a tactile lip on the hinged safety cover.
for x in (.273,.357):
    box('Guard side rail',(x,.027,-.075),(.006,.108,.021),'edge',.002)
box('Guard',(.315,.027,-.080),(.082,.104,.013),'red',.006,'guard')
box('Guard lift lip',(.315,-.023,-.087),(.058,.009,.013),'red',.003,'guard')
for x in (.286,.301,.329,.344):
    cylinder('Hinge knuckle',(x,.083,-.071),.009,.012,'edge',axis='x')
label('LIFT' ,(.315,.045,-.088),.010,'white','guard')
label('EJECT',(.315,.015,-.088),.010,'white','guard')
cylinder('Guard hinge',(.315,.083,-.071),.007,.086,'silver',axis='x')
label('PULL',(.315,-.058,-.041),.009,'amber')
label('GUARDED',(.315,-.085,-.036),.006,'amber')
box('Map key',(.315,-.137,-.039),(.078,.037,.016),'edge',.004,'map')
label('MAP',(.315,-.137,-.049),.008,group='map')
pivots={'gear_lamps':(0,0,0),'static':(0,0,0),'gear':(-.315,.015,-.044),'radar':(-.19,-.139,-.038),
'engine':(-.065,-.139,-.038),'assist':(.065,-.139,-.038),'weapon':(.185,-.137,-.039),
'eject':(.315,.022,-.059),'guard':(.315,.083,-.071),'map':(.315,-.137,-.039),
'cm':(-.249,.017,-.046),'hover':(-.249,-.060,-.046),'hover_lamp':(0,0,0),'zoom_in':(.249,.066,-.046),'zoom_out':(.249,-.014,-.046)}
for x,side in ((-.13,'left'),(.13,'right')):
    for dx,direction in ((-.055,'prev'),(.055,'next')): pivots['mfd_'+side+'_'+direction]=(x+dx,-.089,-.046)
for x,side in ((-.13,'left'),(.13,'right')): pivots['mfd_'+side+'_slave']=(x,-.089,-.046)
# Apply transforms, triangulate and export individual meshes grouped by animated pivot.
parts=[]
for obj in objects:
    obj.data.calc_loop_triangles();p=pivots[obj['panel_group']];verts=[]
    for vert in obj.data.vertices:
        w=obj.matrix_world@vert.co;verts.extend([round(w.x-p[0],6),round(w.z-p[1],6),round(w.y-p[2],6)])
    # Axis swap reverses winding.
    tris=[i for t in obj.data.loop_triangles for i in (t.vertices[0],t.vertices[2],t.vertices[1])]
    parts.append({'name':obj.name,'group':obj['panel_group'],'material':list(colors).index(obj.data.materials[0].name),'vertices':verts,'triangles':tris})
data={'materials':[{'name':n,'color':list(c)} for n,c in colors.items()],
      'groups':[{'name':n,'position':list(p)} for n,p in pivots.items()], 'parts':parts}
(repo/'NOVR/Assets/CockpitPanel.json').write_text(json.dumps(data,separators=(',',':')))
# Parent the export to actual pivot objects, preserving model transforms.
for name,p in pivots.items():
    empty=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(empty);empty.location=v(p); bpy.context.view_layer.update()
    for obj in objects:
        if obj['panel_group']==name:
            world=obj.matrix_world.copy();obj.parent=empty;obj.matrix_world=world
bpy.ops.object.select_all(action='DESELECT')
for obj in objects:obj.select_set(True)
for obj in bpy.context.scene.objects:
    if obj.type=='EMPTY':obj.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(out/'NOVR-universal-cockpit-panel.glb'),export_format='GLB',use_selection=True)
# Studio preview of the shipped model; displays intentionally blank, live in the game.
bpy.ops.object.camera_add(location=(.58,-1.18,.56));camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=.99
bpy.context.scene.camera=camera
for pos,energy,size in [((-.45,-.8,1),65,1.1),((.5,-.2,.4),22,.5)]:
    bpy.ops.object.light_add(type='AREA',location=pos);light=bpy.context.object;light.data.energy=energy;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=48
scene.world.color=(.08,.09,.11);scene.render.resolution_x=1600;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(out/'NOVR-universal-cockpit-panel.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'NOVR-universal-cockpit-panel.blend'))
bpy.ops.render.render(write_still=True)
print('Exported panel:',len(parts),'meshes,',sum(len(p['triangles'])//3 for p in parts),'triangles')
