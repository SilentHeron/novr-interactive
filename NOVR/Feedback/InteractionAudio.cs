using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.XR;
namespace NOVR.MotionControls;

internal sealed class InteractionAudio : MonoBehaviour
{
    private static InteractionAudio? _instance;
    private readonly AudioSource[] _voices=new AudioSource[6];
    private readonly AudioClip[] _clips=new AudioClip[Enum.GetValues(typeof(InteractionCue)).Length];
    private readonly float[,] _next=new float[2,Enum.GetValues(typeof(InteractionCue)).Length];
    private Aircraft? _aircraft;
    private AudioMixerGroup? _mixer;
    private bool _ready;
    private void Awake()=>_instance=this;
    internal static void Play(InputDevice device, InteractionCue cue)
    {
        var instance=_instance;var volume=ModConfiguration.Instance.MotionControls.InteractionVolume.Value;
        if(instance==null||volume<=0||AudioListener.pause||!TrackedControllers.Available||APIBus.MainCamera==null)return;
        var left=(device.characteristics&InputDeviceCharacteristics.Left)!=0;var side=left?0:1;var index=(int)cue;
        if(Time.unscaledTime<instance._next[side,index])return;
        instance._next[side,index]=Time.unscaledTime+.045f;
        if(!instance._ready)instance.Prepare();
        GameManager.GetLocalAircraft(out var aircraft);
        if(instance._aircraft!=aircraft)
        {
            instance._aircraft=aircraft;instance._mixer=null;
            if(aircraft!=null)foreach(var source in aircraft.GetComponentsInChildren<AudioSource>(true))
                if(source.outputAudioMixerGroup!=null){instance._mixer=source.outputAudioMixerGroup;break;}
            foreach(var voice in instance._voices)voice.outputAudioMixerGroup=instance._mixer;
        }
        AudioSource? selected=null;foreach(var voice in instance._voices)if(!voice.isPlaying){selected=voice;break;}
        if(selected==null)return;
        var hand=TrackedControllers.Read(left);var tracking=APIBus.MainCamera.transform.parent;
        selected.transform.position=hand.Valid&&tracking!=null?tracking.TransformPoint(hand.Position):APIBus.MainCamera.transform.position;
        selected.volume=volume;selected.clip=instance._clips[index];selected.Play();
    }
    private void Prepare()
    {
        for(var i=0;i<_clips.Length;i++){var data=InteractionSoundWave.Create((InteractionCue)i);var clip=AudioClip.Create("NOVR "+(InteractionCue)i,data.Length,1,24000,false);clip.SetData(data,0);_clips[i]=clip;}
        for(var i=0;i<_voices.Length;i++)
        {
            var go=new GameObject("NOVR interaction voice");go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;
            source.spatialBlend=.85f;source.dopplerLevel=0;source.minDistance=.3f;source.maxDistance=4;source.rolloffMode=AudioRolloffMode.Logarithmic;source.priority=160;_voices[i]=source;
        }
        _ready=true;
    }
    private void OnDisable(){if(_ready)foreach(var voice in _voices)voice.Stop();}
    private void OnDestroy(){if(_instance==this)_instance=null;foreach(var clip in _clips)if(clip!=null)Destroy(clip);foreach(var voice in _voices)if(voice!=null)Destroy(voice.gameObject);}
}
