using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Iskra.App.Services;

/// <summary>Перечисление приложений AppsFolder через IShellFolder (имя + AppUserModelID). Без прав администратора.</summary>
internal static class StoreApps
{
    public static IEnumerable<(string Name, string Aumid)> Enumerate()
    {
        var list = new List<(string, string)>();
        Guid IID_IShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
        // shell:AppsFolder — known folder {4234d49b-0245-4df3-b780-3893943456e1}
        IntPtr pidl;
        if (SHParseDisplayName(@"shell:AppsFolder", IntPtr.Zero, out pidl, 0, out _) != 0) return list;
        try
        {
            Guid IID_IShellFolder = new("000214E6-0000-0000-C000-000000000046");
            if (SHBindToObject(IntPtr.Zero, pidl, IntPtr.Zero, ref IID_IShellFolder, out var sfPtr) != 0 || sfPtr == IntPtr.Zero)
                return list;
            var sf = (IShellFolder)Marshal.GetObjectForIUnknown(sfPtr);
            if (sf.EnumObjects(IntPtr.Zero, SHCONTF.NONFOLDERS | SHCONTF.FOLDERS, out var enumPtr) != 0 || enumPtr == IntPtr.Zero)
            { Marshal.Release(sfPtr); return list; }
            var en = (IEnumIDList)Marshal.GetObjectForIUnknown(enumPtr);
            var items = new IntPtr[1];
            while (en.Next(1, items, out var fetched) == 0 && fetched == 1)
            {
                string name = GetName(sf, items[0], SHGDN.NORMAL);
                string aumid = GetName(sf, items[0], SHGDN.FORPARSING);
                Marshal.FreeCoTaskMem(items[0]);
                if (!string.IsNullOrEmpty(aumid) && aumid.Contains('!')) list.Add((name, aumid));
                else if (!string.IsNullOrEmpty(aumid) && !aumid.Contains('\\') && aumid.Contains('_')) list.Add((name, aumid));
            }
            Marshal.ReleaseComObject(en); Marshal.Release(enumPtr); Marshal.ReleaseComObject(sf); Marshal.Release(sfPtr);
        }
        finally { Marshal.FreeCoTaskMem(pidl); }
        return list;
    }

    private static string GetName(IShellFolder sf, IntPtr pidl, SHGDN flags)
    {
        if (sf.GetDisplayNameOf(pidl, flags, out var str) != 0) return "";
        return StrRetToString(ref str, pidl);
    }

    private static string StrRetToString(ref STRRET str, IntPtr pidl)
    {
        var sb = new System.Text.StringBuilder(260);
        if (StrRetToBuf(ref str, pidl, sb, sb.Capacity) == 0) return sb.ToString();
        return "";
    }

    [Flags] private enum SHCONTF { FOLDERS = 0x20, NONFOLDERS = 0x40 }
    [Flags] private enum SHGDN { NORMAL = 0, FORPARSING = 0x8000 }
    [StructLayout(LayoutKind.Explicit, Size = 272)] private struct STRRET { [FieldOffset(0)] public uint uType; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, IntPtr bc, out IntPtr pidl, uint attrIn, out uint attrOut);
    [DllImport("shell32.dll")] private static extern int SHBindToObject(IntPtr sf, IntPtr pidl, IntPtr bc, ref Guid riid, out IntPtr ppv);
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)] private static extern int StrRetToBuf(ref STRRET str, IntPtr pidl, System.Text.StringBuilder buf, int cch);

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        int ParseDisplayName(IntPtr h, IntPtr bc, string name, out uint eaten, out IntPtr pidl, ref uint attr);
        int EnumObjects(IntPtr h, SHCONTF flags, out IntPtr enumIdList);
        int BindToObject(IntPtr pidl, IntPtr bc, ref Guid riid, out IntPtr ppv);
        int BindToStorage(IntPtr pidl, IntPtr bc, ref Guid riid, out IntPtr ppv);
        int CompareIDs(IntPtr l, IntPtr pidl1, IntPtr pidl2);
        int CreateViewObject(IntPtr owner, ref Guid riid, out IntPtr ppv);
        int GetAttributesOf(uint cidl, IntPtr apidl, ref uint inOut);
        int GetUIObjectOf(IntPtr owner, uint cidl, IntPtr apidl, ref Guid riid, IntPtr rgf, out IntPtr ppv);
        int GetDisplayNameOf(IntPtr pidl, SHGDN flags, out STRRET name);
        int SetNameOf(IntPtr h, IntPtr pidl, string name, SHGDN flags, out IntPtr pidlOut);
    }

    [ComImport, Guid("000214F2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumIDList
    {
        int Next(uint celt, IntPtr[] rgelt, out uint fetched);
        int Skip(uint celt);
        int Reset();
        int Clone(out IEnumIDList ppenum);
    }
}
