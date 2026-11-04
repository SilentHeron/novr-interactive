using NOVR.MotionControls;
using NOVR.VrUi;

internal static class ActivityChecks
{
    internal static void Run(Action<bool,string> check)
    {
        foreach (var frame in new[] { true, false }) foreach(var left in new[] { true, false })
        foreach (var button in new ControllerButton?[] { null, ControllerButton.Primary, ControllerButton.Secondary,
            ControllerButton.Top, ControllerButton.Outside, ControllerButton.Shoulder, ControllerButton.Menu,
            ControllerButton.Trigger, ControllerButton.StickClick })
        {
            var supported = frame || button != ControllerButton.Top && button != ControllerButton.Outside && button != ControllerButton.Shoulder && (left || button != ControllerButton.Menu);
            var press = new ControllerSample { Valid=true, NativeFrame=frame };
            switch(button)
            {
                case null: press.Grip=.8f;break; case ControllerButton.Primary:press.Primary=true;break;
                case ControllerButton.Secondary:press.Secondary=true;break;case ControllerButton.Top:press.Top=true;break;
                case ControllerButton.Outside:press.Outside=true;break;case ControllerButton.Shoulder:press.Shoulder=true;break;
                case ControllerButton.Menu:press.Menu=true;break;case ControllerButton.Trigger:press.Trigger=.8f;break;
                case ControllerButton.StickClick:press.StickClick=true;break;
            }
            check(ControllerDiagramActivity.Read(press,button,left,frame)==(supported?ControllerDiagramInput.Press:ControllerDiagramInput.None),
                "Physical press mapping, including unavailable Touch/system controls");
            press.Valid=false;
            check(ControllerDiagramActivity.Read(press,button,left,frame)==ControllerDiagramInput.None,"Tracking loss clears stale pressed state");
            check(ControllerDiagramActivity.Read(new ControllerSample {Valid=true},button,left,frame)==ControllerDiagramInput.None,"Release clears physical activity");
            var touch=new ControllerSample {Valid=true,NativeFrame=frame};
            switch(button)
            {
                case null:touch.GripTouched=true;break;case ControllerButton.Primary:touch.PrimaryTouched=true;break;
                case ControllerButton.Secondary:touch.SecondaryTouched=true;break;case ControllerButton.Top:touch.TopTouched=true;break;
                case ControllerButton.Outside:touch.OutsideTouched=true;break;case ControllerButton.Trigger:touch.TriggerTouched=true;break;
                case ControllerButton.StickClick:touch.ThumbstickTouched=true;break;
            }
            var touchSupported=supported && button!=ControllerButton.Menu && button!=ControllerButton.Shoulder;
            check(ControllerDiagramActivity.Read(touch,button,left,frame)==(touchSupported?ControllerDiagramInput.Touch:ControllerDiagramInput.None),"Exact supported capacitive control maps to touch");
            if(button!=ControllerButton.Primary)
                check(ControllerDiagramActivity.Read(touch,ControllerButton.Primary,left,frame)==ControllerDiagramInput.None,"Touching another physical control never lights the primary button");
        }
        var both=new ControllerSample {Valid=true,Primary=true,PrimaryTouched=true};
        check(ControllerDiagramActivity.Read(both,ControllerButton.Primary,false,false)==ControllerDiagramInput.Press,"Press takes precedence over touch on the same control");
        check(ControllerDiagramActivity.Read(new ControllerSample {Valid=true,Trigger=.001f,Grip=.001f},null,true,true)==ControllerDiagramInput.None,"Tiny analog idle noise does not light grip");
        check(ControllerDiagramActivity.Read(new ControllerSample {Valid=true,Trigger=.001f},ControllerButton.Trigger,true,true)==ControllerDiagramInput.None,"Tiny analog idle noise does not light trigger");
    }
}
