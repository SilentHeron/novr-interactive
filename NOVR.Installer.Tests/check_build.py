#!/usr/bin/env python3
"""Optional SDK 10 build-safety checks; all outputs stay in a temporary fixture."""
from pathlib import Path
import tempfile,subprocess,hashlib,zipfile,sys
from xml.sax.saxutils import escape
repo=Path(__file__).resolve().parent.parent
dotnet=sys.argv[1] if len(sys.argv) > 1 else 'dotnet'
with tempfile.TemporaryDirectory(prefix='novr-build-check-') as d:
 root=Path(d);src=root/'source';src.mkdir();(src/'NOVR.dll').write_text('fixture')
 project=root/'check.proj'
 xml_repo=escape(str(repo));xml_root=escape(str(root));xml_src=escape(str(src))
 project.write_text(f'''<Project><PropertyGroup><NuclearOptionGameDirResolved>{xml_root}/game</NuclearOptionGameDirResolved><GameLayoutOutputDir>{xml_root}/layout</GameLayoutOutputDir><TargetDir>{xml_src}/</TargetDir><GameDeployPath>BepInEx/plugins/NOVR</GameDeployPath></PropertyGroup><Import Project="{xml_repo}/NOVR.Build/NOVR.Build.props"/><Import Project="{xml_repo}/NOVR.Build/NOVR.Build.targets"/></Project>''')
 for option in ('', '-p:DeployToGame=typo', '-p:DeployToGame=true'):
  args=[str(dotnet),'msbuild',str(project),'-t:StageGameLayout','-v:q']+([option] if option else [])
  subprocess.run(args,check=True)
  deployed=(root/'game/BepInEx/plugins/NOVR/NOVR.dll').exists()
  assert deployed == (option.endswith('=true')), (option,deployed)
  print('PASS build deployment',option or 'unset',deployed)
 packageSource=root/'package-source';(packageSource/'plugins/NOVR').mkdir(parents=True);(packageSource/'plugins/NOVR/NOVR.dll').write_text('fixture')
 args=[str(dotnet),'msbuild',str(repo/'NOVR.Build/NOVR.Build.csproj'),'-t:CreateReleaseZip','-p:Configuration=Release',f'-p:ReleasePackageSourceDir={packageSource}',f'-p:ReleasePackageDistDir={root}/dist',f'-p:ReleasePackagePath={root}/dist/NOVR.zip','-p:NovrReleaseVersion=0.4.7.3','-v:q']
 subprocess.run(args,check=True)
 package=root/'dist/NOVR.zip'; checksum=(root/'dist/NOVR.zip.sha256').read_text().strip().split()[0]
 assert checksum.lower()==hashlib.sha256(package.read_bytes()).hexdigest()
 with zipfile.ZipFile(package) as z: assert z.read('plugins/NOVR/version.txt').decode().strip()=='0.4.7.3'
 print('PASS release ZIP checksum and explicit version metadata')
