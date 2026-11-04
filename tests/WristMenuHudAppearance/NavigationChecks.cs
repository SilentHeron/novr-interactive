using NOVR.MotionControls;
using NOVR.VrUi;

internal static class NavigationChecks
{
    internal static void Run(Action<bool,string> check)
    {
        const string home="View";
        var history=new MenuHistory<string>(home);
        var calls=new List<ViewMenuDestination>();
        var actions=Enum.GetValues<ViewMenuDestination>().ToDictionary(destination=>destination,
            destination=>(Action)(()=>{ calls.Add(destination); if(destination is not ViewMenuDestination.EditLayout and not ViewMenuDestination.Recenter)history.Push(destination.ToString()); }));
        var buttons=new List<(WristMenuNavigation.Item item,Action click)>();
        // This is the same builder and route table consumed by the runtime menu.
        WristMenuNavigation.BuildHome((item,click)=>buttons.Add((item,click)),actions);
        check(calls.Count==0&&history.Current==home,"Constructing View does not execute commands or navigate");
        check(buttons.Count==Enum.GetValues<ViewMenuDestination>().Length,"Every current home destination has exactly one control");
        check(buttons.Select(b=>b.item.Destination).Distinct().Count()==buttons.Count,"No duplicated home destination replaces another setting");
        check(buttons.Select(b=>(b.item.Column,b.item.Row)).Distinct().Count()==buttons.Count,"Home controls have unique non-overlapping grid positions");
        foreach(var button in buttons)
        {
            history.Home(); calls.Clear(); button.click();
            check(calls.SequenceEqual(new[]{button.item.Destination}),"Home button invokes only its assigned production route: "+button.item.Destination);
            if(button.item.OpensPage)
            {
                check(history.Current==button.item.Destination.ToString()&&history.Trail.Count==2,"Common settings open from View in one click without a settings hub: "+button.item.Destination);
                history.Back(); check(history.Current==home&&!history.CanBack,"Back from a direct setting returns to View");
            }
            else check(history.Current==home,"Layout and recenter commands retain their callbacks instead of becoming empty pages");
        }
        var cue=buttons.Single(b=>b.item.Destination==ViewMenuDestination.LookCue);
        check(cue.item.Row==0,"Look-direction cue is visible in the first row");
        history.Home(); calls.Clear(); cue.click(); cue.click();
        check(history.Trail.Count==2&&history.Current==ViewMenuDestination.LookCue.ToString(),"Repeated direct cue navigation cannot accumulate identical intermediary pages");
        history.Home();
        buttons.Single(b=>b.item.Destination==ViewMenuDestination.Hud).click(); history.Push("Symbols & labels"); cue.click();
        check(history.Current==ViewMenuDestination.LookCue.ToString(),"Cue remains accessible as a shared destination");
        history.Back(); check(history.Current=="Symbols & labels","Back from shared cue controls returns to the caller");
        history.Home(); check(history.Current==home&&history.Trail.Count==1,"Home clears nested navigation in one action");
        history.Push("Flight actions"); history.Push("Eject confirmation"); history.Home();
        check(history.Current==home&&!history.CanBack,"Reopening/home cannot leave an eject confirmation in history");

        foreach(var button in buttons)
        {
            var bounds=WristMenuNavigation.HomeBounds(button.item);
            check(bounds.Width>=300&&bounds.Height>=42,"Flattening navigation preserves existing readable button size");
            check(Math.Abs(bounds.X)+bounds.Width*.5f<WristMenuNavigation.PanelWidth*.5f&&
                Math.Abs(bounds.Y)+bounds.Height*.5f<WristMenuNavigation.PanelHeight*.5f,"Every home control fits inside the existing panel without scrolling");
            check(bounds.Y+bounds.Height*.5f<199,"Home buttons remain below the section headings and status area");
            for(var footer=0;footer<3;footer++)
                check(!Overlaps(bounds,WristMenuNavigation.ButtonBounds(footer,8,3)),"Home controls never cover persistent Back/Home/Close");
            foreach(var other in buttons.Where(b=>b.item.Destination!=button.item.Destination))
                check(!Overlaps(bounds,WristMenuNavigation.HomeBounds(other.item)),"Direct routes have distinct physical click areas");
        }
        var recenter=buttons.Single(b=>b.item.Destination==ViewMenuDestination.Recenter);
        check(recenter.item.Row==7&&!buttons.Any(b=>b.item.Column==0&&b.item.Row==6),"Recenter action has a visual separation from navigation routes");
        foreach(var destination in Enum.GetValues<ViewMenuDestination>())
        {
            var missing=new Dictionary<ViewMenuDestination,Action>(actions); missing.Remove(destination);
            var rejected=false;
            try { WristMenuNavigation.BuildHome((_,_)=>{},missing); } catch(ArgumentException) { rejected=true; }
            check(rejected,"A missing destination cannot silently create a dead View button");
        }
    }
    private static bool Overlaps(WristMenuNavigation.ButtonRect a,WristMenuNavigation.ButtonRect b)=>
        Math.Abs(a.X-b.X)<(a.Width+b.Width)*.5f&&Math.Abs(a.Y-b.Y)<(a.Height+b.Height)*.5f;
}
