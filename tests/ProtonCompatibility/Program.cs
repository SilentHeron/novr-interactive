using System.Reflection;
using HarmonyLib;
using NOVR;
using NOVR.Compatibility;
using Rewired;
using Rewired.Platforms;
using UnityEngine;

var checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
void Set(string field,object value)=>typeof(ProtonControllerCompatibility).GetField(field,BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,value);
bool Enabled()=>(bool)typeof(ProtonControllerCompatibility).GetField("_enabled",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
void Reset(bool wineEnabled=false)
{
    Set("_registered",false);Set("_enabled",wineEnabled);
    ModConfiguration.Instance.DisableProtonXInput.Value=true;ReInput.isReady=false;
    ReInput.RuntimeConfigurationReads=ReInput.EventRegistrations=ReInput.ResetCalls=0;
    Debug.Logs.Clear();Debug.Warnings.Clear();
}
void Prefix(Type patch,InputManager_Base manager)
{
    var hook=patch.GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static)!;
    Check(hook.ReturnType==typeof(void)&&hook.IsDefined(typeof(HarmonyPrefix)),"Prefix cannot skip native initialization");
    hook.Invoke(null,new object[]{manager});
}
Reset();ModConfiguration.Instance.DisableProtonXInput.Value=false;ProtonControllerCompatibility.Initialize();
Check(!Enabled(),"Opt-out never activates the compatibility hooks");
Check(ReInput.EventRegistrations==0&&ReInput.RuntimeConfigurationReads==0,"Initialization never subscribes to or mutates an initialized callback");
// On Windows without Wine, or on this portable Linux runner, the library or
// entry point is absent. The production detection must fail safely.
Reset();ProtonControllerCompatibility.Initialize();
Check(!Enabled(),"A native platform without wine_get_version does not enable the Proton workaround");
var native=new InputManager_Base();Prefix(typeof(ProtonInputManagerAwakePatch),native);
Check(native.FirstPollUsesXInput(),"Native-platform prefixes leave XInput configuration unchanged");

foreach(var hook in new[]{typeof(ProtonInputManagerAwakePatch),typeof(ProtonInputManagerEnablePatch)})
{
    var attr=hook.GetCustomAttribute<HarmonyPatch>()!;
    Check(attr.Type==typeof(InputManager_Base)&&attr.Method==(hook==typeof(ProtonInputManagerAwakePatch)?"Awake":"OnEnable"),"Each prefix targets the intended pre-initialization entry point");
    foreach(var source in Enum.GetValues<WindowsStandalonePrimaryInputSource>())
    {
        Reset(wineEnabled:true);var manager=new InputManager_Base();var data=manager.userData!;var config=data.ConfigVars!;var bindings=data.SavedBindings;
        config.windowsStandalonePrimaryInputSource=source;
        Prefix(hook,manager);
        Check(!manager.FirstPollUsesXInput(),"Proton backend starts with XInput disabled before its first poll");
        Check(config.windowsStandalonePrimaryInputSource==(source==WindowsStandalonePrimaryInputSource.XInput?WindowsStandalonePrimaryInputSource.RawInput:source),"Only XInput-primary falls back to RawInput; all other backend choices survive");
        Check(ReferenceEquals(manager.userData,data)&&ReferenceEquals(data.ConfigVars,config)&&ReferenceEquals(data.SavedBindings,bindings),"Configuration and existing bindings stay in place");
        Check(config.useNativeMouse&&config.useEnhancedDeviceSupport&&config.updateLoop==7,"Unrelated input features remain unchanged");
        Prefix(hook,manager);
        Check(Debug.Logs.Count==1,"Repeated Awake/OnEnable is idempotent without duplicate diagnostics");
        Check(ReInput.RuntimeConfigurationReads==0&&ReInput.EventRegistrations==0&&ReInput.ResetCalls==0,"No live-reset API or initialization callback is used");
    }
}
Reset(wineEnabled:true);var enabledManager=new InputManager_Base();ReInput.isReady=true;
ProtonControllerCompatibility.BeforeInitialization(enabledManager);
Check(enabledManager.FirstPollUsesXInput()&&Debug.Logs.Count==0,"An already-live Rewired instance is left untouched rather than reset from an unknown call stack");
ReInput.isReady=false;ProtonControllerCompatibility.BeforeInitialization(enabledManager);
Check(!enabledManager.FirstPollUsesXInput(),"A later real reinitialization still receives the compatibility setting before polling");
Reset(wineEnabled:true);ModConfiguration.Instance.DisableProtonXInput.Value=false;var optedOut=new InputManager_Base();ProtonControllerCompatibility.BeforeInitialization(optedOut);
Check(optedOut.FirstPollUsesXInput(),"Opt-out also guards the actual pre-initialization hooks");
Reset(wineEnabled:true);var preconfigured=new InputManager_Base();preconfigured.userData!.ConfigVars!.useXInput=false;
ProtonControllerCompatibility.BeforeInitialization(preconfigured);
Check(Debug.Logs.Count==0,"Already-safe configurations need no mutation or diagnostic");
preconfigured.userData.ConfigVars.windowsStandalonePrimaryInputSource=WindowsStandalonePrimaryInputSource.XInput;
ProtonControllerCompatibility.BeforeInitialization(preconfigured);
Check(!preconfigured.FirstPollUsesXInput(),"XInput-primary is corrected even if the auxiliary XInput flag was already false");
ProtonControllerCompatibility.BeforeInitialization(null!);
ProtonControllerCompatibility.BeforeInitialization(new InputManager_Base{userData=null});
ProtonControllerCompatibility.BeforeInitialization(new InputManager_Base{userData=new(){ConfigVars=null}});
Check(Debug.Warnings.Count==0,"Absent startup data is handled without throwing or replacing native data");
Console.WriteLine($"{checks} source-linked Proton initialization checks passed.");
