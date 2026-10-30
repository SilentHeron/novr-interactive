using System.IO;

internal static class PayloadFile
{
    // Unity/BepInEx can map managed assemblies before patcher initialization.
    // Do not attempt to overwrite a mapped DLL when the payload is already installed.
    internal static bool CopyIfChanged(string source, string destination)
    {
        if (File.Exists(destination) && SameContents(source, destination)) return false;
        File.Copy(source, destination, true);
        return true;
    }

    private static bool SameContents(string first, string second)
    {
        using var a = File.OpenRead(first);
        using var b = File.OpenRead(second);
        if (a.Length != b.Length) return false;
        var bufferA = new byte[8192];
        var bufferB = new byte[8192];
        int count;
        while ((count = a.Read(bufferA, 0, bufferA.Length)) != 0)
        {
            var read = 0;
            while (read < count)
            {
                var next = b.Read(bufferB, read, count - read);
                if (next == 0) return false;
                read += next;
            }
            for (var i = 0; i < count; i++)
                if (bufferA[i] != bufferB[i]) return false;
        }
        return b.ReadByte() == -1;
    }
}
