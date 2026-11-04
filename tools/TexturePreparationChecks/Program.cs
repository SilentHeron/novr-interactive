var passed = 0;
TexturePreparationChecks.Run((name, action) =>
{
    action();
    Console.WriteLine("PASS " + name);
    passed++;
}, condition => { if (!condition) throw new InvalidOperationException("Texture check failed"); });
Console.WriteLine($"{passed} texture preparation checks passed.");

BindingChecks.Run();
ResourceChecks.Run();
